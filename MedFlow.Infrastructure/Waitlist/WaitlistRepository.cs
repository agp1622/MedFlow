using MedFlow.Core;
using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using MedFlow.Infrastructure.Data;
using MedFlow.Infrastructure.Reminders;
using MedFlow.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Infrastructure.Waitlist;

public class WaitlistRepository : IWaitlistRepository
{
    private readonly AppDbContext _db;
    private readonly IBookingRepository _booking;
    private readonly IAuditService _audit;

    public WaitlistRepository(AppDbContext db, IBookingRepository booking, IAuditService audit)
    {
        _db = db; _booking = booking; _audit = audit;
    }

    private async Task<(WaitlistAddOutcome Outcome, WaitlistEntryDto? Entry)> CreateAsync(Patient patient)
    {
        if (patient.Status != PatientStatus.Active) return (WaitlistAddOutcome.NotActive, null);
        if (await _db.WaitlistEntries.AnyAsync(w => w.PatientId == patient.Id && w.Status == WaitlistStatus.Waiting))
            return (WaitlistAddOutcome.AlreadyWaiting, null);
        var entry = new WaitlistEntry { PatientId = patient.Id, DoctorId = patient.DoctorId };
        _db.WaitlistEntries.Add(entry);
        await _db.SaveChangesAsync();
        return (WaitlistAddOutcome.Added, new WaitlistEntryDto(entry.Id, patient.Id, patient.FullName, entry.CreatedAt));
    }

    public async Task<PagedResult<WaitlistEntryDto>> GetPagedAsync(ClinicScope scope, QueryParams q)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 100);
        var query = from w in _db.WaitlistEntries
                    join p in _db.Patients on w.PatientId equals p.Id
                    where w.ClinicId == scope.ClinicId && w.Status == WaitlistStatus.Waiting && p.ClinicId == scope.ClinicId
                    select new { w.Id, w.PatientId, p.FirstName, p.LastName, w.CreatedAt };
        var total = await query.CountAsync();
        var rows = await query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * size).Take(size).ToListAsync();
        return new PagedResult<WaitlistEntryDto>(
            rows.Select(r => new WaitlistEntryDto(r.Id, r.PatientId, r.FirstName + " " + r.LastName, r.CreatedAt)).ToList(),
            total, page, size);
    }

    public async Task<(WaitlistAddOutcome Outcome, WaitlistEntryDto? Entry)> AddAsync(int patientId, ClinicScope scope)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == patientId && p.ClinicId == scope.ClinicId);
        if (patient == null) return (WaitlistAddOutcome.PatientNotFound, null);
        return await CreateAsync(patient);
    }

    public Task<(WaitlistAddOutcome Outcome, WaitlistEntryDto? Entry)> JoinAsync(Patient patient) => CreateAsync(patient);

    public async Task<int?> RemoveAsync(int entryId, ClinicScope scope)
    {
        var entry = await _db.WaitlistEntries.FirstOrDefaultAsync(w =>
            w.Id == entryId && w.ClinicId == scope.ClinicId && w.Status == WaitlistStatus.Waiting);
        if (entry == null) return null;
        Close(entry, WaitlistStatus.Removed);
        await _db.SaveChangesAsync();
        return entry.PatientId;
    }

    public async Task<PortalWaitlistDto> GetForPatientAsync(int patientId)
    {
        var entry = await _db.WaitlistEntries.AsNoTracking()
            .FirstOrDefaultAsync(w => w.PatientId == patientId && w.Status == WaitlistStatus.Waiting);
        return new PortalWaitlistDto(entry != null, entry?.CreatedAt);
    }

    public async Task<bool> LeaveAsync(int patientId)
    {
        var entry = await _db.WaitlistEntries.FirstOrDefaultAsync(w =>
            w.PatientId == patientId && w.Status == WaitlistStatus.Waiting);
        if (entry == null) return false;
        Close(entry, WaitlistStatus.Removed);
        await _db.SaveChangesAsync();
        return true;
    }

    private static void Close(WaitlistEntry entry, WaitlistStatus status)
    {
        entry.Status = status;
        entry.ClosedAt = DateTime.UtcNow;
        entry.UpdatedAt = entry.ClosedAt.Value;
    }

    // A token is usable only for a sent, unexpired, unclaimed offer of a still-waiting, still-active patient
    private async Task<(WaitlistOffer Offer, WaitlistEntry Entry, Patient Patient)?> FindAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128) return null;
        var hash = ReminderTokens.Hash(token);
        var offer = await _db.WaitlistOffers.FirstOrDefaultAsync(o => o.TokenHash == hash);
        var now = DateTime.UtcNow;
        if (offer == null || offer.SentAt == null || offer.ClaimedAt != null || offer.ExpiresAt <= now
            || offer.SlotStartsAt <= now) return null;
        var entry = await _db.WaitlistEntries.FirstOrDefaultAsync(w => w.Id == offer.EntryId);
        if (entry == null || entry.Status != WaitlistStatus.Waiting) return null;
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == entry.PatientId);
        if (patient == null || patient.Status != PatientStatus.Active || patient.DoctorId != entry.DoctorId) return null;
        return (offer, entry, patient);
    }

    private async Task<string> DoctorNameAsync(string doctorId) =>
        await _db.Doctors.Where(d => d.UserId == doctorId).Select(d => d.FirstName + " " + d.LastName)
            .FirstOrDefaultAsync() ?? "";

    public async Task<WaitlistOfferDto?> LookupOfferAsync(string token)
    {
        var found = await FindAsync(token);
        if (found == null) return null;
        var (offer, entry, _) = found.Value;
        return new WaitlistOfferDto(offer.SlotStartsAt, BookingRepository.SlotMinutes,
            await DoctorNameAsync(entry.DoctorId), offer.ExpiresAt);
    }

    public async Task<(WaitlistClaimOutcome Outcome, WaitlistClaimDto? Claim)> ClaimOfferAsync(string token)
    {
        var found = await FindAsync(token);
        if (found == null) return (WaitlistClaimOutcome.Invalid, null);
        var (offer, entry, patient) = found.Value;

        // The same atomic path as online booking decides the winner; nothing is held for offered patients
        var (outcome, appt) = await _booking.BookAsync(patient, offer.SlotStartsAt, null);
        if (outcome != BookingOutcome.Booked || appt == null) return (WaitlistClaimOutcome.SlotUnavailable, null);

        var now = DateTime.UtcNow;
        offer.ClaimedAt = now;
        offer.UpdatedAt = now;
        Close(entry, WaitlistStatus.Booked);
        await _db.SaveChangesAsync();

        if (!string.IsNullOrEmpty(patient.PortalUserId))
        {
            await _audit.RecordPortalAsync(patient.PortalUserId, patient.Id, AuditAction.Change,
                AuditItemKind.Appointment, appt.Id);
            await _audit.RecordPortalAsync(patient.PortalUserId, patient.Id, AuditAction.Change,
                AuditItemKind.Waitlist, entry.Id);
        }
        return (WaitlistClaimOutcome.Claimed,
            new WaitlistClaimDto(offer.SlotStartsAt, appt.DurationMinutes, await DoctorNameAsync(entry.DoctorId)));
    }

    public async Task<bool> LeaveByTokenAsync(string token)
    {
        var found = await FindAsync(token);
        if (found == null) return false;
        var (_, entry, patient) = found.Value;
        Close(entry, WaitlistStatus.Removed);
        await _db.SaveChangesAsync();
        if (!string.IsNullOrEmpty(patient.PortalUserId))
            await _audit.RecordPortalAsync(patient.PortalUserId, patient.Id, AuditAction.Change,
                AuditItemKind.Waitlist, entry.Id);
        return true;
    }
}
