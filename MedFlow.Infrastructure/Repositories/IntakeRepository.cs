using System.Security.Cryptography;
using MedFlow.Core;
using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using MedFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Infrastructure.Repositories;

public class IntakeRepository : IIntakeRepository
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(14);
    private readonly AppDbContext _db;
    public IntakeRepository(AppDbContext db) { _db = db; }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    public async Task<(string Token, DateTime ExpiresAt)> CreateLinkAsync(Patient patient)
    {
        var now = DateTime.UtcNow;
        var pending = await _db.IntakeLinks.Where(i => i.PatientId == patient.Id && i.UsedAt == null).ToListAsync();
        foreach (var p in pending) p.UsedAt = now;

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var link = new IntakeLink
        {
            PatientId = patient.Id,
            Email = patient.Email,
            TokenHash = Hash(token),
            ExpiresAt = now.Add(Lifetime)
        };
        _db.IntakeLinks.Add(link);
        await _db.SaveChangesAsync();
        return (token, link.ExpiresAt);
    }

    public async Task<(IntakeLink Link, Patient Patient)?> FindValidLinkAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 200) return null;
        var hash = Hash(token);
        var link = await _db.IntakeLinks.FirstOrDefaultAsync(i => i.TokenHash == hash);
        if (link == null || link.UsedAt != null || link.ExpiresAt < DateTime.UtcNow) return null;

        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == link.PatientId);
        if (patient == null || patient.Status != PatientStatus.Active) return null;
        if (!string.Equals(patient.Email, link.Email, StringComparison.OrdinalIgnoreCase)) return null;
        return (link, patient);
    }

    public async Task<bool> SubmitAsync(IntakeLink link, Patient patient, IntakeSubmission submission)
    {
        var now = DateTime.UtcNow;
        submission.PatientId = patient.Id;
        submission.DoctorId = patient.DoctorId;
        submission.IntakeLinkId = link.Id;
        submission.Status = IntakeStatus.Pending;
        submission.SubmittedAt = now;
        submission.SignedAt = now;
        submission.ConsentVersion = IntakeConsent.Version;

        // UsedAt is a concurrency token: if another request consumed the link first, this save fails and nothing is stored.
        // The link update and the submission insert happen in the same save.
        var tracked = await _db.IntakeLinks.FirstAsync(i => i.Id == link.Id);
        if (tracked.UsedAt != null) return false;
        tracked.UsedAt = now;
        _db.IntakeSubmissions.Add(submission);
        try
        {
            await _db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public async Task<PagedResult<IntakeSubmissionSummaryDto>> GetPagedAsync(string doctorId, IntakeStatus? status, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = from s in _db.IntakeSubmissions
                    join p in _db.Patients on s.PatientId equals p.Id
                    where s.DoctorId == doctorId && p.DoctorId == doctorId
                    select new { s, p };
        if (status != null) query = query.Where(x => x.s.Status == status);

        var total = await query.CountAsync();
        var rows = await query.OrderByDescending(x => x.s.SubmittedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.s.Id, x.s.PatientId, x.p.FirstName, x.p.LastName, x.s.Status, x.s.SubmittedAt })
            .ToListAsync();
        var items = rows.Select(r => new IntakeSubmissionSummaryDto(
            r.Id, r.PatientId, $"{r.FirstName} {r.LastName}", r.Status.ToString(), r.SubmittedAt));
        return new PagedResult<IntakeSubmissionSummaryDto>(items, total, page, pageSize);
    }

    private async Task<(IntakeSubmission Submission, Patient Patient)?> LoadOwnedAsync(int id, string doctorId)
    {
        var s = await _db.IntakeSubmissions.FirstOrDefaultAsync(x => x.Id == id && x.DoctorId == doctorId);
        if (s == null) return null;
        var p = await _db.Patients.FirstOrDefaultAsync(x => x.Id == s.PatientId && x.DoctorId == doctorId);
        return p == null ? null : (s, p);
    }

    public async Task<IntakeSubmissionDetailDto?> GetDetailAsync(int id, string doctorId)
    {
        var loaded = await LoadOwnedAsync(id, doctorId);
        if (loaded == null) return null;
        var (s, p) = loaded.Value;
        var answers = new IntakeAnswersDto(s.FirstName, s.LastName, s.DateOfBirth, s.Gender.ToString(), s.Phone,
            s.Address, s.City, s.State, s.ZipCode, s.InsuranceProvider, s.InsurancePolicyNumber,
            s.PrimaryCondition, s.Allergies, s.CurrentMedications, s.PastHistory, s.AdditionalNotes);
        var current = new IntakeAnswersDto(p.FirstName, p.LastName, p.DateOfBirth, p.Gender.ToString(), p.Phone,
            p.Address, p.City, p.State, p.ZipCode, p.InsuranceProvider, p.InsurancePolicyNumber,
            p.PrimaryCondition, p.Allergies, null, null, null);
        return new IntakeSubmissionDetailDto(s.Id, s.PatientId, s.Status.ToString(), s.SubmittedAt, answers, current,
            new IntakeConsentDto(s.ConsentVersion, s.ConsentAgreed, s.SignatureName, s.SignedAt),
            s.DecidedAt, s.RejectionReason);
    }

    public async Task<DecisionResult> DecideAsync(int id, string doctorId, bool accept, string? reason)
    {
        var loaded = await LoadOwnedAsync(id, doctorId);
        if (loaded == null) return DecisionResult.NotFound;
        var (s, p) = loaded.Value;
        if (s.Status != IntakeStatus.Pending) return DecisionResult.AlreadyDecided;

        var now = DateTime.UtcNow;
        if (accept)
        {
            p.FirstName = s.FirstName; p.LastName = s.LastName; p.DateOfBirth = s.DateOfBirth;
            p.Gender = s.Gender; p.Phone = s.Phone;
            p.Address = s.Address; p.City = s.City; p.State = s.State; p.ZipCode = s.ZipCode;
            p.InsuranceProvider = s.InsuranceProvider; p.InsurancePolicyNumber = s.InsurancePolicyNumber;
            p.PrimaryCondition = s.PrimaryCondition; p.Allergies = s.Allergies;

            var extra = new List<string>();
            if (!string.IsNullOrWhiteSpace(s.CurrentMedications)) extra.Add($"Current medications: {s.CurrentMedications}");
            if (!string.IsNullOrWhiteSpace(s.PastHistory)) extra.Add($"Past history: {s.PastHistory}");
            if (!string.IsNullOrWhiteSpace(s.AdditionalNotes)) extra.Add($"Additional notes: {s.AdditionalNotes}");
            if (extra.Count > 0)
            {
                var block = $"[Intake form {s.SubmittedAt:yyyy-MM-dd}]\n" + string.Join("\n", extra);
                p.Notes = string.IsNullOrWhiteSpace(p.Notes) ? block : p.Notes + "\n\n" + block;
            }
            s.Status = IntakeStatus.Accepted;
        }
        else
        {
            s.Status = IntakeStatus.Rejected;
            s.RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        }
        s.DecidedByDoctorId = doctorId;
        s.DecidedAt = now;
        await _db.SaveChangesAsync(); // patient and submission change in one save
        return DecisionResult.Done;
    }
}
