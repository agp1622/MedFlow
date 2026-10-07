using MedFlow.Core;
using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using MedFlow.Infrastructure.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Infrastructure.Repositories;

// ── Base Repository ───────────────────────────────────────────────────────────
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly AppDbContext _db;
    protected readonly DbSet<T> _set;

    public Repository(AppDbContext db) { _db = db; _set = db.Set<T>(); }

    public async Task<T?> GetByIdAsync(int id) => await _set.FindAsync(id);
    public async Task<T?> GetInClinicAsync(int id, int clinicId) =>
        await _set.FirstOrDefaultAsync(e => e.Id == id && EF.Property<int>(e, "ClinicId") == clinicId);
    public async Task<IEnumerable<T>> GetAllAsync() => await _set.ToListAsync();
    public async Task<T> AddAsync(T entity) { await _set.AddAsync(entity); await _db.SaveChangesAsync(); return entity; }
    public async Task UpdateAsync(T entity) { _set.Update(entity); await _db.SaveChangesAsync(); }
    public async Task DeleteAsync(int id)
    {
        var entity = await _set.FindAsync(id);
        if (entity != null) { entity.IsDeleted = true; await _db.SaveChangesAsync(); }
    }
    public async Task<bool> ExistsAsync(int id) => await _set.AnyAsync(e => e.Id == id);
}

// ── Patient Repository ────────────────────────────────────────────────────────
public class PatientRepository : Repository<Patient>, IPatientRepository
{
    public PatientRepository(AppDbContext db) : base(db) { }

    private static PatientSummaryDto ToSummary(Patient p, bool clinical) => new(
        p.Id, p.FullName, p.Age, p.Gender.ToString(), clinical ? p.BloodType.ToString() : nameof(BloodType.Unknown),
        p.Status.ToString(), p.Email, p.Phone, clinical ? p.PrimaryCondition : null, null, null);

    public async Task<PagedResult<PatientSummaryDto>> GetPagedAsync(ClinicScope scope, QueryParams q)
    {
        var clinical = scope.Has(Permission.PatientClinicalFields);
        var query = _db.Patients.Where(p => p.ClinicId == scope.ClinicId);

        // Staff without clinical access must not be able to infer withheld data by searching it
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = clinical
                ? query.Where(p =>
                    p.FirstName.Contains(q.Search) ||
                    p.LastName.Contains(q.Search) ||
                    p.Email.Contains(q.Search) ||
                    (p.PrimaryCondition != null && p.PrimaryCondition.Contains(q.Search)))
                : query.Where(p =>
                    p.FirstName.Contains(q.Search) ||
                    p.LastName.Contains(q.Search) ||
                    p.Email.Contains(q.Search));

        var total = await query.CountAsync();

        query = q.SortBy switch
        {
            "name" => q.SortDesc ? query.OrderByDescending(p => p.LastName) : query.OrderBy(p => p.LastName),
            "age" => q.SortDesc ? query.OrderByDescending(p => p.DateOfBirth) : query.OrderBy(p => p.DateOfBirth),
            _ => query.OrderByDescending(p => p.UpdatedAt)
        };

        var patients = await query.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToListAsync();

        var dtos = patients.Select(p => ToSummary(p, clinical));

        return new PagedResult<PatientSummaryDto>(dtos, total, q.Page, q.PageSize);
    }

    public async Task<Patient?> GetWithDetailsAsync(int id, ClinicScope scope) =>
        await _db.Patients
            .Include(p => p.Appointments.Where(a => !a.IsDeleted).OrderByDescending(a => a.ScheduledAt).Take(10))
            .Include(p => p.Prescriptions.Where(rx => !rx.IsDeleted))
            .Include(p => p.Invoices.Where(i => !i.IsDeleted))
            .Include(p => p.VitalSigns.Where(v => !v.IsDeleted).OrderByDescending(v => v.RecordedAt).Take(5))
            .Include(p => p.MedicalNotes.Where(n => !n.IsDeleted).OrderByDescending(n => n.NoteDate).Take(10))
            .FirstOrDefaultAsync(p => p.Id == id && p.ClinicId == scope.ClinicId);

    public async Task<DateTime?> GetLastVisitAsync(int patientId) =>
        await _db.Appointments
            .Where(a => a.PatientId == patientId && a.Status == AppointmentStatus.Completed)
            .OrderByDescending(a => a.ScheduledAt)
            .Select(a => (DateTime?)a.ScheduledAt)
            .FirstOrDefaultAsync();

    public async Task<DateTime?> GetNextAppointmentAsync(int patientId) =>
        await _db.Appointments
            .Where(a => a.PatientId == patientId && a.ScheduledAt > DateTime.UtcNow
                && (a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.Pending))
            .OrderBy(a => a.ScheduledAt)
            .Select(a => (DateTime?)a.ScheduledAt)
            .FirstOrDefaultAsync();

    public async Task<IEnumerable<PatientSummaryDto>> GetRecentAsync(ClinicScope scope, int count = 5)
    {
        var clinical = scope.Has(Permission.PatientClinicalFields);
        var rows = await _db.Patients
            .Where(p => p.ClinicId == scope.ClinicId)
            .OrderByDescending(p => p.UpdatedAt)
            .Take(count)
            .ToListAsync();
        return rows.Select(p => ToSummary(p, clinical)).ToList();
    }
}

// ── Appointment Repository ────────────────────────────────────────────────────
public class AppointmentRepository : Repository<Appointment>, IAppointmentRepository
{
    public AppointmentRepository(AppDbContext db) : base(db) { }

    private IQueryable<AppointmentDto> Project() =>
        _db.Appointments.Include(a => a.Patient).Select(a => new AppointmentDto(
            a.Id, a.PatientId, a.Patient != null ? a.Patient.FullName : "",
            a.ScheduledAt, a.DurationMinutes,
            a.Type.ToString(), a.Status.ToString(),
            a.Reason, a.Notes, a.Location, a.CreatedAt));

    public async Task<PagedResult<AppointmentDto>> GetPagedAsync(ClinicScope scope, QueryParams q)
    {
        var query = _db.Appointments
            .Include(a => a.Patient)
            .Where(a => a.ClinicId == scope.ClinicId);

        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(a => a.Patient != null &&
                (a.Patient.FirstName.Contains(q.Search) || a.Patient.LastName.Contains(q.Search)));

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(a => a.ScheduledAt)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(a => new AppointmentDto(
                a.Id, a.PatientId, a.Patient != null ? a.Patient.FullName : "",
                a.ScheduledAt, a.DurationMinutes,
                a.Type.ToString(), a.Status.ToString(),
                a.Reason, a.Notes, a.Location, a.CreatedAt))
            .ToListAsync();

        return new PagedResult<AppointmentDto>(items, total, q.Page, q.PageSize);
    }

    public async Task<IEnumerable<AppointmentDto>> GetTodayAsync(ClinicScope scope)
    {
        var today = DateTime.UtcNow.Date;
        return await _db.Appointments
            .Include(a => a.Patient)
            .Where(a => a.ClinicId == scope.ClinicId && a.ScheduledAt.Date == today)
            .OrderBy(a => a.ScheduledAt)
            .Select(a => new AppointmentDto(
                a.Id, a.PatientId, a.Patient != null ? a.Patient.FullName : "",
                a.ScheduledAt, a.DurationMinutes,
                a.Type.ToString(), a.Status.ToString(),
                a.Reason, a.Notes, a.Location, a.CreatedAt))
            .ToListAsync();
    }

    public async Task<IEnumerable<AppointmentDto>> GetByPatientAsync(int patientId, ClinicScope scope) =>
        await _db.Appointments
            .Include(a => a.Patient)
            .Where(a => a.PatientId == patientId && a.ClinicId == scope.ClinicId)
            .OrderByDescending(a => a.ScheduledAt)
            .Select(a => new AppointmentDto(
                a.Id, a.PatientId, a.Patient != null ? a.Patient.FullName : "",
                a.ScheduledAt, a.DurationMinutes,
                a.Type.ToString(), a.Status.ToString(),
                a.Reason, a.Notes, a.Location, a.CreatedAt))
            .ToListAsync();

    public async Task<IEnumerable<AppointmentDto>> GetUpcomingAsync(ClinicScope scope, int count = 5) =>
        await _db.Appointments
            .Include(a => a.Patient)
            .Where(a => a.ClinicId == scope.ClinicId && a.ScheduledAt > DateTime.UtcNow
                && a.Status != AppointmentStatus.Cancelled)
            .OrderBy(a => a.ScheduledAt)
            .Take(count)
            .Select(a => new AppointmentDto(
                a.Id, a.PatientId, a.Patient != null ? a.Patient.FullName : "",
                a.ScheduledAt, a.DurationMinutes,
                a.Type.ToString(), a.Status.ToString(),
                a.Reason, a.Notes, a.Location, a.CreatedAt))
            .ToListAsync();
}

// ── Prescription Repository ───────────────────────────────────────────────────
public class PrescriptionRepository : Repository<Prescription>, IPrescriptionRepository
{
    public PrescriptionRepository(AppDbContext db) : base(db) { }

    public async Task<PagedResult<PrescriptionDto>> GetPagedAsync(ClinicScope scope, QueryParams q)
    {
        var query = _db.Prescriptions.Include(p => p.Patient).Where(p => p.ClinicId == scope.ClinicId);
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(p => p.DrugName.Contains(q.Search) ||
                (p.Patient != null && p.Patient.FirstName.Contains(q.Search)));
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(p => p.CreatedAt)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(p => new PrescriptionDto(
                p.Id, p.PatientId, p.Patient != null ? p.Patient.FullName : "",
                p.DrugName, p.Dosage, p.Frequency, p.Instructions,
                p.IssuedDate, p.ExpiryDate, p.RefillsRemaining, p.Status.ToString(), p.CreatedAt))
            .ToListAsync();
        return new PagedResult<PrescriptionDto>(items, total, q.Page, q.PageSize);
    }

    public async Task<IEnumerable<PrescriptionDto>> GetByPatientAsync(int patientId, ClinicScope scope) =>
        await _db.Prescriptions.Include(p => p.Patient)
            .Where(p => p.PatientId == patientId && p.ClinicId == scope.ClinicId)
            .OrderByDescending(p => p.IssuedDate)
            .Select(p => new PrescriptionDto(
                p.Id, p.PatientId, p.Patient != null ? p.Patient.FullName : "",
                p.DrugName, p.Dosage, p.Frequency, p.Instructions,
                p.IssuedDate, p.ExpiryDate, p.RefillsRemaining, p.Status.ToString(), p.CreatedAt))
            .ToListAsync();

    public async Task<PrescriptionDocumentData?> GetDocumentDataAsync(int id, ClinicScope scope)
    {
        var rx = await _db.Prescriptions.AsNoTracking()
            .Include(p => p.Patient).Include(p => p.Doctor)
            .FirstOrDefaultAsync(p => p.Id == id && p.ClinicId == scope.ClinicId
                && p.Patient != null && p.Patient.ClinicId == scope.ClinicId);
        if (rx?.Patient == null || rx.Doctor == null) return null;
        var pt = rx.Patient;
        var cityLine = string.Join(" ", new[] { pt.City, pt.State, pt.ZipCode }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var address = string.Join(", ", new[] { pt.Address, cityLine }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return new PrescriptionDocumentData(
            rx.Id, rx.PatientId, rx.Doctor.FullName, rx.Doctor.Specialty, rx.Doctor.LicenseNumber, rx.Doctor.Phone,
            pt.FullName, pt.DateOfBirth, pt.Phone, address.Length > 0 ? address : null,
            rx.DrugName, rx.Dosage, rx.Frequency, rx.Instructions,
            rx.IssuedDate, rx.ExpiryDate, rx.RefillsRemaining, rx.Status);
    }

    public async Task<int> GetExpiringCountAsync(ClinicScope scope, int daysAhead = 30) =>
        await _db.Prescriptions
            .Where(p => p.ClinicId == scope.ClinicId && p.Status == PrescriptionStatus.Active
                && p.ExpiryDate <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysAhead)))
            .CountAsync();

    public async Task UpdateExpiryStatusesAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var soon = today.AddDays(30);
        await _db.Prescriptions
            .Where(p => p.Status == PrescriptionStatus.Active && p.ExpiryDate < today)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PrescriptionStatus.Expired));
        await _db.Prescriptions
            .Where(p => p.Status == PrescriptionStatus.Active && p.ExpiryDate <= soon && p.ExpiryDate >= today)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PrescriptionStatus.ExpiringSoon));
    }
}

// ── Invoice Repository ────────────────────────────────────────────────────────
public class InvoiceRepository : Repository<Invoice>, IInvoiceRepository
{
    public InvoiceRepository(AppDbContext db) : base(db) { }

    public async Task<ClaimSourceData?> GetClaimSourceAsync(int id, ClinicScope scope)
    {
        var inv = await _db.Invoices.AsNoTracking()
            .Include(i => i.Patient).Include(i => i.Doctor).Include(i => i.Appointment)
            .FirstOrDefaultAsync(i => i.Id == id && i.ClinicId == scope.ClinicId
                && i.Patient != null && i.Patient.ClinicId == scope.ClinicId);
        if (inv?.Patient == null || inv.Doctor == null) return null;
        var p = inv.Patient;
        return new ClaimSourceData(
            inv.Id, inv.PatientId, inv.InvoiceNumber, inv.Status.ToString(), inv.ServiceDescription,
            inv.Amount, inv.InvoiceDate, inv.Appointment?.ScheduledAt,
            p.FirstName, p.LastName, p.DateOfBirth, p.Gender, p.Phone,
            p.Address, p.City, p.State, p.ZipCode,
            p.InsuranceProvider, p.InsurancePolicyNumber, p.InsuranceGroupNumber, p.InsurancePayerId,
            p.InsuranceSubscriberName, p.InsuranceSubscriberDateOfBirth, p.InsuranceSubscriberRelationship,
            inv.Doctor.FullName, inv.Doctor.Phone);
    }

    public async Task<PagedResult<InvoiceDto>> GetPagedAsync(ClinicScope scope, QueryParams q)
    {
        var query = _db.Invoices.Include(i => i.Patient).Where(i => i.ClinicId == scope.ClinicId);
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(i => i.InvoiceNumber.Contains(q.Search) ||
                (i.Patient != null && i.Patient.FirstName.Contains(q.Search)));
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(i => i.InvoiceDate)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(i => new InvoiceDto(
                i.Id, i.PatientId, i.Patient != null ? i.Patient.FullName : "",
                i.AppointmentId, i.InvoiceNumber, i.ServiceDescription,
                i.Amount, i.PaidAmount, i.Status.ToString(),
                i.InvoiceDate, i.DueDate, i.PaidDate, i.Notes, i.CreatedAt))
            .ToListAsync();
        return new PagedResult<InvoiceDto>(items, total, q.Page, q.PageSize);
    }

    public async Task<Invoice?> GetWithPatientAsync(int id) =>
        await _db.Invoices.Include(i => i.Patient).FirstOrDefaultAsync(i => i.Id == id);

    public async Task<IEnumerable<InvoiceDto>> GetByPatientAsync(int patientId, ClinicScope scope) =>
        await _db.Invoices.Include(i => i.Patient)
            .Where(i => i.PatientId == patientId && i.ClinicId == scope.ClinicId)
            .OrderByDescending(i => i.InvoiceDate)
            .Select(i => new InvoiceDto(
                i.Id, i.PatientId, i.Patient != null ? i.Patient.FullName : "",
                i.AppointmentId, i.InvoiceNumber, i.ServiceDescription,
                i.Amount, i.PaidAmount, i.Status.ToString(),
                i.InvoiceDate, i.DueDate, i.PaidDate, i.Notes, i.CreatedAt))
            .ToListAsync();

    public async Task<decimal> GetPendingAmountAsync(ClinicScope scope) =>
        await _db.Invoices
            .Where(i => i.ClinicId == scope.ClinicId && (i.Status == InvoiceStatus.Pending || i.Status == InvoiceStatus.Overdue))
            .SumAsync(i => i.Amount);

    public async Task<int> GetOverdueCountAsync(ClinicScope scope) =>
        await _db.Invoices.Where(i => i.ClinicId == scope.ClinicId && i.Status == InvoiceStatus.Overdue).CountAsync();

    public async Task UpdateOverdueStatusesAsync() =>
        await _db.Invoices
            .Where(i => i.Status == InvoiceStatus.Pending && i.DueDate < DateTime.UtcNow)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, InvoiceStatus.Overdue));

    public async Task<string> GenerateInvoiceNumberAsync()
    {
        var count = await _db.Invoices.CountAsync() + 1;
        return $"INV-{count:D4}";
    }
}

// ── VitalSign Repository ──────────────────────────────────────────────────────
public class VitalSignRepository : Repository<VitalSign>, IVitalSignRepository
{
    public VitalSignRepository(AppDbContext db) : base(db) { }

    public async Task<IEnumerable<VitalSignDto>> GetByPatientAsync(int patientId, ClinicScope scope) =>
        await _db.VitalSigns.Where(v => v.PatientId == patientId && v.ClinicId == scope.ClinicId)
            .OrderByDescending(v => v.RecordedAt)
            .Select(v => new VitalSignDto(v.Id, v.PatientId, v.RecordedAt,
                v.BloodPressure, v.HeartRate, v.Weight, v.Height,
                v.Bmi, v.Temperature, v.OxygenSaturation, v.RecordedBy))
            .ToListAsync();

    public async Task<VitalSignDto?> GetLatestByPatientAsync(int patientId, ClinicScope scope) =>
        await _db.VitalSigns.Where(v => v.PatientId == patientId && v.ClinicId == scope.ClinicId)
            .OrderByDescending(v => v.RecordedAt)
            .Select(v => new VitalSignDto(v.Id, v.PatientId, v.RecordedAt,
                v.BloodPressure, v.HeartRate, v.Weight, v.Height,
                v.Bmi, v.Temperature, v.OxygenSaturation, v.RecordedBy))
            .FirstOrDefaultAsync();
}

// ── MedicalNote Repository ────────────────────────────────────────────────────
public class MedicalNoteRepository : Repository<MedicalNote>, IMedicalNoteRepository
{
    public MedicalNoteRepository(AppDbContext db) : base(db) { }

    public async Task<IEnumerable<MedicalNoteDto>> GetByPatientAsync(int patientId, ClinicScope scope)
    {
        // The author may be a doctor, an owner or a nurse: prefer the doctor profile, fall back to the user's name
        var rows = await (from n in _db.MedicalNotes
                          where n.PatientId == patientId && n.ClinicId == scope.ClinicId
                          join d in _db.Doctors on n.DoctorId equals d.UserId into ds
                          from d in ds.DefaultIfEmpty()
                          join u in _db.Users on n.DoctorId equals u.Id into us
                          from u in us.DefaultIfEmpty()
                          orderby n.NoteDate descending
                          select new
                          {
                              n.Id, n.PatientId, n.Content, n.VisitType, n.NoteDate, n.SharedWithPatient,
                              DoctorFirst = d != null ? d.FirstName : null, DoctorLast = d != null ? d.LastName : null,
                              UserFirst = u != null ? u.FirstName : null, UserLast = u != null ? u.LastName : null
                          }).ToListAsync();
        return rows.Select(r => new MedicalNoteDto(r.Id, r.PatientId,
            r.DoctorFirst != null ? $"Dr. {r.DoctorFirst} {r.DoctorLast}"
                : r.UserFirst != null ? $"{r.UserFirst} {r.UserLast}".Trim() : "Unknown",
            r.Content, r.VisitType, r.NoteDate, r.SharedWithPatient)).ToList();
    }

    public async Task<CopyForwardDto?> GetLatestForPatientAsync(int patientId, ClinicScope scope) =>
        await _db.MedicalNotes
            .Where(n => n.PatientId == patientId && n.ClinicId == scope.ClinicId)
            .OrderByDescending(n => n.NoteDate).ThenByDescending(n => n.Id)
            .Select(n => new CopyForwardDto(n.Id, n.Content, n.VisitType, n.NoteDate))
            .FirstOrDefaultAsync();
}

// ── NoteTemplate Repository ───────────────────────────────────────────────────
public class NoteTemplateRepository : Repository<NoteTemplate>, INoteTemplateRepository
{
    public NoteTemplateRepository(AppDbContext db) : base(db) { }

    public async Task<PagedResult<NoteTemplateDto>> GetPagedAsync(string doctorId, QueryParams q)
    {
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 100);
        var query = _db.NoteTemplates.Where(t => t.DoctorId == doctorId);
        var total = await query.CountAsync();
        var items = await query.OrderBy(t => t.Name)
            .Skip((page - 1) * size).Take(size)
            .Select(t => new NoteTemplateDto(t.Id, t.Name, t.Body, false, t.UpdatedAt))
            .ToListAsync();
        return new PagedResult<NoteTemplateDto>(items, total, page, size);
    }

    public async Task<NoteTemplate?> GetWithOwnerCheckAsync(int id, string doctorId) =>
        await _db.NoteTemplates.FirstOrDefaultAsync(t => t.Id == id && t.DoctorId == doctorId);

    public async Task<bool> NameExistsAsync(string doctorId, string name, int? excludeId = null) =>
        await _db.NoteTemplates.AnyAsync(t => t.DoctorId == doctorId
            && t.Id != excludeId && t.Name.ToLower() == name.ToLower());
}

// ── Dashboard Repository ──────────────────────────────────────────────────────
public class DashboardRepository : IDashboardRepository
{
    private readonly AppDbContext _db;
    public DashboardRepository(AppDbContext db) { _db = db; }

    public async Task<DashboardStatsDto> GetStatsAsync(ClinicScope scope)
    {
        var today = DateTime.UtcNow.Date;
        var clinicId = scope.ClinicId;
        // Figures for areas the role cannot open are not computed (zero), so the dashboard never leaks them
        var canRx = scope.Has(Permission.PrescriptionsRead);
        var canInvoices = scope.Has(Permission.InvoicesRead);
        var clinical = scope.Has(Permission.PatientClinicalFields);

        var totalPatients = await _db.Patients.CountAsync(p => p.ClinicId == clinicId);
        var activePatients = await _db.Patients.CountAsync(p => p.ClinicId == clinicId && p.Status == PatientStatus.Active);
        var todayAppts = await _db.Appointments.CountAsync(a => a.ClinicId == clinicId && a.ScheduledAt.Date == today);
        var upcomingAppts = await _db.Appointments.CountAsync(a => a.ClinicId == clinicId && a.ScheduledAt > DateTime.UtcNow && a.Status != AppointmentStatus.Cancelled);
        var activeRx = canRx ? await _db.Prescriptions.CountAsync(p => p.ClinicId == clinicId && p.Status == PrescriptionStatus.Active) : 0;
        var expiringRx = canRx ? await _db.Prescriptions.CountAsync(p => p.ClinicId == clinicId && p.Status == PrescriptionStatus.ExpiringSoon) : 0;
        var pendingAmount = canInvoices
            ? await _db.Invoices.Where(i => i.ClinicId == clinicId && (i.Status == InvoiceStatus.Pending || i.Status == InvoiceStatus.Overdue)).SumAsync(i => i.Amount)
            : 0m;
        var overdueInv = canInvoices ? await _db.Invoices.CountAsync(i => i.ClinicId == clinicId && i.Status == InvoiceStatus.Overdue) : 0;

        var todaySchedule = await _db.Appointments
            .Include(a => a.Patient)
            .Where(a => a.ClinicId == clinicId && a.ScheduledAt.Date == today)
            .OrderBy(a => a.ScheduledAt)
            .Select(a => new AppointmentDto(a.Id, a.PatientId, a.Patient != null ? a.Patient.FullName : "",
                a.ScheduledAt, a.DurationMinutes, a.Type.ToString(), a.Status.ToString(),
                a.Reason, a.Notes, a.Location, a.CreatedAt))
            .ToListAsync();

        var recent = await _db.Patients
            .Where(p => p.ClinicId == clinicId)
            .OrderByDescending(p => p.UpdatedAt)
            .Take(5)
            .ToListAsync();
        var recentPatients = recent.Select(p => new PatientSummaryDto(p.Id, p.FullName, p.Age, p.Gender.ToString(),
            clinical ? p.BloodType.ToString() : nameof(BloodType.Unknown), p.Status.ToString(), p.Email, p.Phone,
            clinical ? p.PrimaryCondition : null, null, null)).ToList();

        return new DashboardStatsDto(
            totalPatients, activePatients, todayAppts, upcomingAppts,
            activeRx, expiringRx, pendingAmount, overdueInv,
            todaySchedule, recentPatients);
    }
}

// ── PatientAttachment Repository ──────────────────────────────────────────────
public class PatientAttachmentRepository : Repository<PatientAttachment>, IPatientAttachmentRepository
{
    public PatientAttachmentRepository(AppDbContext db) : base(db) { }

    public async Task<IEnumerable<PatientAttachmentDto>> GetByPatientAsync(int patientId, ClinicScope scope) =>
        await _db.PatientAttachments
            .Where(a => a.PatientId == patientId && a.ClinicId == scope.ClinicId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new PatientAttachmentDto(
                a.Id, a.PatientId, a.FileName, a.ContentType,
                a.FileSize, a.Category, a.Description, a.CreatedAt, a.SharedWithPatient))
            .ToListAsync();

    public async Task SetSharingAsync(PatientAttachment attachment, bool shared)
    {
        attachment.SharedWithPatient = shared;
        await _db.SaveChangesAsync();
    }
}


// ── Portal Repository (patient-facing, read-only) ─────────────────────────────
public class PortalRepository : IPortalRepository
{
    private readonly AppDbContext _db;
    public PortalRepository(AppDbContext db) { _db = db; }

    public async Task<Patient?> GetPatientByUserIdAsync(string userId) =>
        await _db.Patients.FirstOrDefaultAsync(p => p.PortalUserId == userId);

    public async Task<PortalProfileDto?> GetProfileAsync(Patient patient)
    {
        var doctor = await _db.Doctors.FirstOrDefaultAsync(d => d.UserId == patient.DoctorId);
        return new PortalProfileDto(patient.FirstName, patient.LastName, doctor?.FullName ?? "Your doctor");
    }

    public async Task<IEnumerable<PortalAppointmentDto>> GetAppointmentsAsync(int patientId)
    {
        var now = DateTime.UtcNow;
        return await _db.Appointments
            .Where(a => a.PatientId == patientId && a.ScheduledAt >= now && a.Status != AppointmentStatus.Cancelled)
            .OrderBy(a => a.ScheduledAt)
            .Select(a => new PortalAppointmentDto(a.Id, a.ScheduledAt, a.DurationMinutes,
                a.Type.ToString(), a.Status.ToString(), a.Reason, a.Location))
            .ToListAsync();
    }

    public async Task<IEnumerable<PortalPrescriptionDto>> GetPrescriptionsAsync(int patientId) =>
        await _db.Prescriptions
            .Where(p => p.PatientId == patientId)
            .OrderByDescending(p => p.IssuedDate)
            .Select(p => new PortalPrescriptionDto(p.Id, p.DrugName, p.Dosage, p.Frequency, p.Instructions,
                p.IssuedDate, p.ExpiryDate, p.RefillsRemaining, p.Status.ToString()))
            .ToListAsync();

    public async Task<IEnumerable<PortalInvoiceDto>> GetInvoicesAsync(int patientId) =>
        await _db.Invoices
            .Where(i => i.PatientId == patientId)
            .OrderByDescending(i => i.InvoiceDate)
            .Select(i => new PortalInvoiceDto(i.Id, i.InvoiceNumber, i.ServiceDescription, i.Amount, i.PaidAmount,
                i.Status.ToString(), i.InvoiceDate, i.DueDate, i.PaidDate))
            .ToListAsync();

    public async Task<Invoice?> GetOwnInvoiceAsync(int id, int patientId) =>
        await _db.Invoices.FirstOrDefaultAsync(i => i.Id == id && i.PatientId == patientId);

    public async Task<IEnumerable<PortalAttachmentDto>> GetSharedAttachmentsAsync(int patientId) =>
        await _db.PatientAttachments
            .Where(a => a.PatientId == patientId && a.SharedWithPatient)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new PortalAttachmentDto(a.Id, a.FileName, a.ContentType, a.FileSize,
                a.Category, a.Description, a.CreatedAt))
            .ToListAsync();

    public async Task<PatientAttachment?> GetSharedAttachmentAsync(int id, int patientId) =>
        await _db.PatientAttachments
            .FirstOrDefaultAsync(a => a.Id == id && a.PatientId == patientId && a.SharedWithPatient);

    public async Task<IEnumerable<PortalNoteDto>> GetSharedNotesAsync(int patientId) =>
        await (from n in _db.MedicalNotes
               where n.PatientId == patientId && n.SharedWithPatient
               join d in _db.Doctors on n.DoctorId equals d.UserId into ds
               from d in ds.DefaultIfEmpty()
               orderby n.NoteDate descending
               select new PortalNoteDto(n.Id,
                   d != null ? "Dr. " + d.FirstName + " " + d.LastName : "Your doctor",
                   n.VisitType, n.Content, n.NoteDate))
            .ToListAsync();

    public async Task LogAccessAsync(int patientId, string resourceType, IEnumerable<int> resourceIds, string action)
    {
        foreach (var id in resourceIds)
            _db.PortalAccessLogs.Add(new PortalAccessLog
            {
                PatientId = patientId, ResourceType = resourceType, ResourceId = id,
                Action = action, OccurredAt = DateTime.UtcNow
            });
        await _db.SaveChangesAsync();
    }
}

// ── Portal Invitation Repository ──────────────────────────────────────────────
public class PortalInvitationRepository : IPortalInvitationRepository
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);
    private readonly AppDbContext _db;
    public PortalInvitationRepository(AppDbContext db) { _db = db; }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    public async Task<(string Token, DateTime ExpiresAt)> CreateAsync(Patient patient)
    {
        var now = DateTime.UtcNow;
        // Supersede earlier pending invitations
        var pending = await _db.PortalInvitations
            .Where(i => i.PatientId == patient.Id && i.UsedAt == null).ToListAsync();
        foreach (var p in pending) p.UsedAt = now;

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var invitation = new PortalInvitation
        {
            PatientId = patient.Id,
            Email = patient.Email,
            TokenHash = Hash(token),
            ExpiresAt = now.Add(Lifetime)
        };
        _db.PortalInvitations.Add(invitation);
        await _db.SaveChangesAsync();
        return (token, invitation.ExpiresAt);
    }

    public async Task<(PortalInvitation Invitation, Patient Patient)?> FindValidAsync(string token, string email)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(email)) return null;
        var hash = Hash(token);
        var invitation = await _db.PortalInvitations.FirstOrDefaultAsync(i => i.TokenHash == hash);
        if (invitation == null || invitation.UsedAt != null || invitation.ExpiresAt < DateTime.UtcNow) return null;

        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == invitation.PatientId);
        if (patient == null || patient.Status != PatientStatus.Active) return null;
        // Email must match the invitation and still be the patient's email on file
        if (!string.Equals(invitation.Email, email, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(patient.Email, invitation.Email, StringComparison.OrdinalIgnoreCase)) return null;
        return (invitation, patient);
    }

    public async Task MarkUsedAsync(PortalInvitation invitation, Patient patient, string userId)
    {
        invitation.UsedAt = DateTime.UtcNow;
        patient.PortalUserId = userId;
        await _db.SaveChangesAsync();
    }

    public async Task RevokeAsync(Patient patient)
    {
        var now = DateTime.UtcNow;
        var pending = await _db.PortalInvitations
            .Where(i => i.PatientId == patient.Id && i.UsedAt == null).ToListAsync();
        foreach (var p in pending) p.UsedAt = now;
        patient.PortalUserId = null;
        await _db.SaveChangesAsync();
    }

    public async Task<string> GetPortalStatusAsync(Patient patient)
    {
        if (patient.PortalUserId != null) return "Active";
        var now = DateTime.UtcNow;
        var invited = await _db.PortalInvitations
            .AnyAsync(i => i.PatientId == patient.Id && i.UsedAt == null && i.ExpiresAt >= now);
        return invited ? "Invited" : "NotInvited";
    }
}

// ── Booking Repository ────────────────────────────────────────────────────────
public class BookingRepository : IBookingRepository
{
    public const int SlotMinutes = 30;
    public const int LeadMinutes = 60;
    public const int HorizonDays = 60;
    public const int MaxUpcomingPerPatient = 3;

    // Serialises bookings per doctor within this process; the serializable transaction covers other instances.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

    private readonly AppDbContext _db;
    public BookingRepository(AppDbContext db) { _db = db; }

    private static string Fmt(TimeOnly t) => t.ToString("HH:mm");

    private static AvailabilityWindowDto ToDto(DoctorAvailability a) =>
        new(a.Id, a.DayOfWeek, Fmt(a.StartTime), Fmt(a.EndTime));

    public async Task<AvailabilityDto> GetAvailabilityAsync(string doctorId)
    {
        var windows = (await _db.DoctorAvailabilities.Where(a => a.DoctorId == doctorId).ToListAsync())
            .OrderBy(a => a.DayOfWeek).ThenBy(a => a.StartTime).Select(ToDto).ToList();
        var blocked = (await _db.DoctorBlockedDates.Where(b => b.DoctorId == doctorId).ToListAsync())
            .OrderBy(b => b.Date).Select(b => new BlockedDateDto(b.Id, b.Date, b.Label)).ToList();
        return new AvailabilityDto(windows, blocked);
    }

    public async Task<IEnumerable<AvailabilityWindowDto>> ReplaceWeeklyAsync(
        string doctorId, IEnumerable<(DayOfWeek Day, TimeOnly Start, TimeOnly End)> windows)
    {
        var existing = await _db.DoctorAvailabilities.Where(a => a.DoctorId == doctorId).ToListAsync();
        _db.DoctorAvailabilities.RemoveRange(existing);
        var added = windows.Select(w => new DoctorAvailability
            { DoctorId = doctorId, DayOfWeek = w.Day, StartTime = w.Start, EndTime = w.End }).ToList();
        await _db.DoctorAvailabilities.AddRangeAsync(added);
        await _db.SaveChangesAsync();
        return added.OrderBy(a => a.DayOfWeek).ThenBy(a => a.StartTime).Select(ToDto).ToList();
    }

    public async Task<BlockedDateDto?> AddBlockedDateAsync(string doctorId, DateOnly date, string? label)
    {
        if (await _db.DoctorBlockedDates.AnyAsync(b => b.DoctorId == doctorId && b.Date == date)) return null;
        var entity = new DoctorBlockedDate { DoctorId = doctorId, Date = date, Label = label };
        _db.DoctorBlockedDates.Add(entity);
        await _db.SaveChangesAsync();
        return new BlockedDateDto(entity.Id, entity.Date, entity.Label);
    }

    public async Task<bool> RemoveBlockedDateAsync(string doctorId, int id)
    {
        var entity = await _db.DoctorBlockedDates.FirstOrDefaultAsync(b => b.Id == id && b.DoctorId == doctorId);
        if (entity == null) return false;
        _db.DoctorBlockedDates.Remove(entity);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>Slot starts allowed by availability, blocked dates, lead time and horizon (not yet checked for clashes).</summary>
    private async Task<List<DateTime>> CandidateSlotsAsync(string doctorId, DateOnly from, DateOnly to)
    {
        var now = DateTime.UtcNow;
        var earliest = now.AddMinutes(LeadMinutes);
        var latestDate = DateOnly.FromDateTime(now.AddDays(HorizonDays));
        var windows = await _db.DoctorAvailabilities.Where(a => a.DoctorId == doctorId).ToListAsync();
        var blocked = (await _db.DoctorBlockedDates.Where(b => b.DoctorId == doctorId).Select(b => b.Date).ToListAsync()).ToHashSet();

        var slots = new List<DateTime>();
        for (var d = from; d <= to && d <= latestDate; d = d.AddDays(1))
        {
            if (blocked.Contains(d)) continue;
            foreach (var w in windows.Where(w => w.DayOfWeek == d.DayOfWeek))
                for (var t = w.StartTime; t.AddMinutes(SlotMinutes) <= w.EndTime && t.AddMinutes(SlotMinutes) > t; t = t.AddMinutes(SlotMinutes))
                {
                    var start = DateTime.SpecifyKind(d.ToDateTime(t), DateTimeKind.Utc);
                    if (start >= earliest) slots.Add(start);
                }
        }
        return slots.OrderBy(s => s).ToList();
    }

    private static bool Overlaps(DateTime start, List<(DateTime Start, int Minutes)> busy) =>
        busy.Any(b => b.Start < start.AddMinutes(SlotMinutes) && b.Start.AddMinutes(b.Minutes) > start);

    private async Task<List<(DateTime Start, int Minutes)>> BusyAsync(Func<IQueryable<Appointment>, IQueryable<Appointment>> scope, DateOnly from, DateOnly to)
    {
        var lo = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc).AddDays(-1);
        var hi = DateTime.SpecifyKind(to.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc).AddDays(2);
        var rows = await scope(_db.Appointments.Where(a => a.Status != AppointmentStatus.Cancelled && a.ScheduledAt >= lo && a.ScheduledAt < hi))
            .Select(a => new { a.ScheduledAt, a.DurationMinutes }).ToListAsync();
        return rows.Select(r => (r.ScheduledAt, r.DurationMinutes)).ToList();
    }

    public async Task<IEnumerable<BookingSlotDto>> GetOpenSlotsAsync(Patient patient, DateOnly from, DateOnly to)
    {
        var candidates = await CandidateSlotsAsync(patient.DoctorId, from, to);
        var doctorBusy = await BusyAsync(q => q.Where(a => a.DoctorId == patient.DoctorId), from, to);
        var patientBusy = await BusyAsync(q => q.Where(a => a.PatientId == patient.Id), from, to);
        return candidates.Where(s => !Overlaps(s, doctorBusy) && !Overlaps(s, patientBusy))
            .Select(s => new BookingSlotDto(s, SlotMinutes)).ToList();
    }

    public async Task<bool> IsSlotOpenAsync(string doctorId, DateTime startsAt)
    {
        var start = startsAt.Kind == DateTimeKind.Utc ? startsAt : startsAt.ToUniversalTime();
        var day = DateOnly.FromDateTime(start);
        var candidates = await CandidateSlotsAsync(doctorId, day, day);
        if (!candidates.Contains(start)) return false;
        var busy = await BusyAsync(q => q.Where(a => a.DoctorId == doctorId), day, day);
        return !Overlaps(start, busy);
    }

    public async Task<(BookingOutcome Outcome, PortalAppointmentDto? Appointment)> BookAsync(
        Patient patient, DateTime startsAt, string? reason)
    {
        var start = startsAt.Kind == DateTimeKind.Utc ? startsAt : startsAt.ToUniversalTime();
        var gate = Locks.GetOrAdd(patient.DoctorId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = _db.Database.IsRelational()
                    ? await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable)
                    : null;

                var day = DateOnly.FromDateTime(start);
                var candidates = await CandidateSlotsAsync(patient.DoctorId, day, day);
                if (!candidates.Contains(start)) return (BookingOutcome.NotAvailable, (PortalAppointmentDto?)null);

                var now = DateTime.UtcNow;
                var upcoming = await _db.Appointments.CountAsync(a =>
                    a.PatientId == patient.Id && a.Status != AppointmentStatus.Cancelled && a.ScheduledAt >= now);
                if (upcoming >= MaxUpcomingPerPatient) return (BookingOutcome.LimitReached, null);

                var doctorBusy = await BusyAsync(q => q.Where(a => a.DoctorId == patient.DoctorId), day, day);
                var patientBusy = await BusyAsync(q => q.Where(a => a.PatientId == patient.Id), day, day);
                if (Overlaps(start, doctorBusy) || Overlaps(start, patientBusy)) return (BookingOutcome.Conflict, null);

                var appt = new Appointment
                {
                    PatientId = patient.Id, DoctorId = patient.DoctorId, ScheduledAt = start,
                    DurationMinutes = SlotMinutes, Type = AppointmentType.Consultation,
                    Status = AppointmentStatus.Pending, Reason = reason
                };
                _db.Appointments.Add(appt);
                await _db.SaveChangesAsync();
                if (tx != null) await tx.CommitAsync();
                return (BookingOutcome.Booked, new PortalAppointmentDto(appt.Id, appt.ScheduledAt, appt.DurationMinutes,
                    appt.Type.ToString(), appt.Status.ToString(), appt.Reason, appt.Location));
            });
        }
        finally { gate.Release(); }
    }
}

// ── Reminder Repository ───────────────────────────────────────────────────────
public class ReminderRepository : IReminderRepository
{
    private readonly AppDbContext _db;
    private readonly IWaitlistService _waitlist;
    public ReminderRepository(AppDbContext db, IWaitlistService waitlist) { _db = db; _waitlist = waitlist; }

    private static bool IsOpen(Appointment a) =>
        a.ScheduledAt > DateTime.UtcNow &&
        (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed);

    // A token only works for the exact appointment time it was issued for
    private async Task<(AppointmentReminder Reminder, Appointment Appt)?> FindAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128) return null;
        var hash = Reminders.ReminderTokens.Hash(token);
        var reminder = await _db.AppointmentReminders.FirstOrDefaultAsync(r => r.TokenHash == hash);
        if (reminder == null || reminder.Status != ReminderStatus.Sent) return null;
        var appt = await _db.Appointments.Include(a => a.Doctor)
            .FirstOrDefaultAsync(a => a.Id == reminder.AppointmentId);
        if (appt == null || appt.ScheduledAt != reminder.ScheduledAt || appt.ScheduledAt <= DateTime.UtcNow) return null;
        return (reminder, appt);
    }

    private static ReminderLookupDto ToDto(Appointment a) => new(
        a.ScheduledAt, a.DurationMinutes, a.Doctor?.FullName ?? "", a.Location,
        a.Status.ToString(), IsOpen(a));

    public async Task<ReminderLookupDto?> LookupAsync(string token)
    {
        var found = await FindAsync(token);
        return found == null ? null : ToDto(found.Value.Appt);
    }

    public async Task<(ReminderRespondResult Result, ReminderLookupDto? Dto)> RespondAsync(string token, ReminderAction action)
    {
        var found = await FindAsync(token);
        if (found == null) return (ReminderRespondResult.Invalid, null);
        var (reminder, appt) = found.Value;
        if (!IsOpen(appt)) return (ReminderRespondResult.Closed, null);

        if (action == ReminderAction.Confirm)
        {
            appt.Status = AppointmentStatus.Confirmed;
            reminder.Response = ReminderResponse.Confirmed;
        }
        else
        {
            appt.Status = AppointmentStatus.Cancelled;
            reminder.Response = ReminderResponse.Cancelled;
        }
        var now = DateTime.UtcNow;
        appt.UpdatedAt = now;
        reminder.UpdatedAt = now;
        reminder.RespondedAt = now;
        await _db.SaveChangesAsync();
        // The cancelled slot may be offered to waitlisted patients (best effort, never throws)
        if (action == ReminderAction.Cancel) await _waitlist.OfferFreedSlotAsync(appt.DoctorId, appt.ScheduledAt);
        return (ReminderRespondResult.Ok, ToDto(appt));
    }

    public async Task<ReminderLogDto?> GetLogAsync(int appointmentId, ClinicScope scope)
    {
        var appt = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId && a.ClinicId == scope.ClinicId);
        if (appt == null) return null;
        var latest = await _db.AppointmentReminders.Where(r => r.AppointmentId == appointmentId)
            .OrderByDescending(r => r.Id).FirstOrDefaultAsync();
        var deliveries = await _db.ReminderDeliveries.Where(d => d.AppointmentId == appointmentId)
            .OrderBy(d => d.AttemptedAt).ThenBy(d => d.Id)
            .Select(d => new ReminderDeliveryDto(d.AttemptedAt, d.Channel, d.Outcome.ToString(), d.Reason))
            .ToListAsync();
        return new ReminderLogDto(
            (latest?.Response ?? ReminderResponse.None).ToString(), latest?.RespondedAt, deliveries);
    }
}
