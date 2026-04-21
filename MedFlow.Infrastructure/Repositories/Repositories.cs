using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using MedFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Infrastructure.Repositories;

// ── Base Repository ───────────────────────────────────────────────────────────
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly AppDbContext _db;
    protected readonly DbSet<T> _set;

    public Repository(AppDbContext db) { _db = db; _set = db.Set<T>(); }

    public async Task<T?> GetByIdAsync(int id) => await _set.FindAsync(id);
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

    public async Task<PagedResult<PatientSummaryDto>> GetPagedAsync(string doctorId, QueryParams q)
    {
        var query = _db.Patients.Where(p => p.DoctorId == doctorId);

        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(p =>
                p.FirstName.Contains(q.Search) ||
                p.LastName.Contains(q.Search) ||
                p.Email.Contains(q.Search) ||
                (p.PrimaryCondition != null && p.PrimaryCondition.Contains(q.Search)));

        var total = await query.CountAsync();

        query = q.SortBy switch
        {
            "name" => q.SortDesc ? query.OrderByDescending(p => p.LastName) : query.OrderBy(p => p.LastName),
            "age" => q.SortDesc ? query.OrderByDescending(p => p.DateOfBirth) : query.OrderBy(p => p.DateOfBirth),
            _ => query.OrderByDescending(p => p.UpdatedAt)
        };

        var patients = await query.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToListAsync();

        var dtos = patients.Select(p => new PatientSummaryDto(
            p.Id, p.FullName, p.Age, p.Gender.ToString(), p.BloodType.ToString(),
            p.Status.ToString(), p.Email, p.Phone, p.PrimaryCondition,
            null, null // Last/next visit populated separately if needed
        ));

        return new PagedResult<PatientSummaryDto>(dtos, total, q.Page, q.PageSize);
    }

    public async Task<Patient?> GetWithDetailsAsync(int id, string doctorId) =>
        await _db.Patients
            .Include(p => p.Appointments.Where(a => !a.IsDeleted).OrderByDescending(a => a.ScheduledAt).Take(10))
            .Include(p => p.Prescriptions.Where(rx => !rx.IsDeleted))
            .Include(p => p.Invoices.Where(i => !i.IsDeleted))
            .Include(p => p.VitalSigns.Where(v => !v.IsDeleted).OrderByDescending(v => v.RecordedAt).Take(5))
            .Include(p => p.MedicalNotes.Where(n => !n.IsDeleted).OrderByDescending(n => n.NoteDate).Take(10))
            .FirstOrDefaultAsync(p => p.Id == id && p.DoctorId == doctorId);

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

    public async Task<IEnumerable<PatientSummaryDto>> GetRecentAsync(string doctorId, int count = 5) =>
        await _db.Patients
            .Where(p => p.DoctorId == doctorId)
            .OrderByDescending(p => p.UpdatedAt)
            .Take(count)
            .Select(p => new PatientSummaryDto(
                p.Id, p.FullName, p.Age, p.Gender.ToString(), p.BloodType.ToString(),
                p.Status.ToString(), p.Email, p.Phone, p.PrimaryCondition, null, null))
            .ToListAsync();
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

    public async Task<PagedResult<AppointmentDto>> GetPagedAsync(string doctorId, QueryParams q)
    {
        var query = _db.Appointments
            .Include(a => a.Patient)
            .Where(a => a.DoctorId == doctorId);

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

    public async Task<IEnumerable<AppointmentDto>> GetTodayAsync(string doctorId)
    {
        var today = DateTime.UtcNow.Date;
        return await _db.Appointments
            .Include(a => a.Patient)
            .Where(a => a.DoctorId == doctorId && a.ScheduledAt.Date == today)
            .OrderBy(a => a.ScheduledAt)
            .Select(a => new AppointmentDto(
                a.Id, a.PatientId, a.Patient != null ? a.Patient.FullName : "",
                a.ScheduledAt, a.DurationMinutes,
                a.Type.ToString(), a.Status.ToString(),
                a.Reason, a.Notes, a.Location, a.CreatedAt))
            .ToListAsync();
    }

    public async Task<IEnumerable<AppointmentDto>> GetByPatientAsync(int patientId, string doctorId) =>
        await _db.Appointments
            .Include(a => a.Patient)
            .Where(a => a.PatientId == patientId && a.DoctorId == doctorId)
            .OrderByDescending(a => a.ScheduledAt)
            .Select(a => new AppointmentDto(
                a.Id, a.PatientId, a.Patient != null ? a.Patient.FullName : "",
                a.ScheduledAt, a.DurationMinutes,
                a.Type.ToString(), a.Status.ToString(),
                a.Reason, a.Notes, a.Location, a.CreatedAt))
            .ToListAsync();

    public async Task<IEnumerable<AppointmentDto>> GetUpcomingAsync(string doctorId, int count = 5) =>
        await _db.Appointments
            .Include(a => a.Patient)
            .Where(a => a.DoctorId == doctorId && a.ScheduledAt > DateTime.UtcNow
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

    public async Task<PagedResult<PrescriptionDto>> GetPagedAsync(string doctorId, QueryParams q)
    {
        var query = _db.Prescriptions.Include(p => p.Patient).Where(p => p.DoctorId == doctorId);
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

    public async Task<IEnumerable<PrescriptionDto>> GetByPatientAsync(int patientId, string doctorId) =>
        await _db.Prescriptions.Include(p => p.Patient)
            .Where(p => p.PatientId == patientId && p.DoctorId == doctorId)
            .OrderByDescending(p => p.IssuedDate)
            .Select(p => new PrescriptionDto(
                p.Id, p.PatientId, p.Patient != null ? p.Patient.FullName : "",
                p.DrugName, p.Dosage, p.Frequency, p.Instructions,
                p.IssuedDate, p.ExpiryDate, p.RefillsRemaining, p.Status.ToString(), p.CreatedAt))
            .ToListAsync();

    public async Task<int> GetExpiringCountAsync(string doctorId, int daysAhead = 30) =>
        await _db.Prescriptions
            .Where(p => p.DoctorId == doctorId && p.Status == PrescriptionStatus.Active
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

    public async Task<PagedResult<InvoiceDto>> GetPagedAsync(string doctorId, QueryParams q)
    {
        var query = _db.Invoices.Include(i => i.Patient).Where(i => i.DoctorId == doctorId);
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

    public async Task<IEnumerable<InvoiceDto>> GetByPatientAsync(int patientId, string doctorId) =>
        await _db.Invoices.Include(i => i.Patient)
            .Where(i => i.PatientId == patientId && i.DoctorId == doctorId)
            .OrderByDescending(i => i.InvoiceDate)
            .Select(i => new InvoiceDto(
                i.Id, i.PatientId, i.Patient != null ? i.Patient.FullName : "",
                i.AppointmentId, i.InvoiceNumber, i.ServiceDescription,
                i.Amount, i.PaidAmount, i.Status.ToString(),
                i.InvoiceDate, i.DueDate, i.PaidDate, i.Notes, i.CreatedAt))
            .ToListAsync();

    public async Task<decimal> GetPendingAmountAsync(string doctorId) =>
        await _db.Invoices
            .Where(i => i.DoctorId == doctorId && (i.Status == InvoiceStatus.Pending || i.Status == InvoiceStatus.Overdue))
            .SumAsync(i => i.Amount);

    public async Task<int> GetOverdueCountAsync(string doctorId) =>
        await _db.Invoices.Where(i => i.DoctorId == doctorId && i.Status == InvoiceStatus.Overdue).CountAsync();

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

    public async Task<IEnumerable<VitalSignDto>> GetByPatientAsync(int patientId, string doctorId) =>
        await _db.VitalSigns.Where(v => v.PatientId == patientId)
            .OrderByDescending(v => v.RecordedAt)
            .Select(v => new VitalSignDto(v.Id, v.PatientId, v.RecordedAt,
                v.BloodPressure, v.HeartRate, v.Weight, v.Height,
                v.Bmi, v.Temperature, v.OxygenSaturation, v.RecordedBy))
            .ToListAsync();

    public async Task<VitalSignDto?> GetLatestByPatientAsync(int patientId) =>
        await _db.VitalSigns.Where(v => v.PatientId == patientId)
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

    public async Task<IEnumerable<MedicalNoteDto>> GetByPatientAsync(int patientId, string doctorId) =>
        await _db.MedicalNotes.Include(n => n.Doctor)
            .Where(n => n.PatientId == patientId && n.DoctorId == doctorId)
            .OrderByDescending(n => n.NoteDate)
            .Select(n => new MedicalNoteDto(n.Id, n.PatientId,
                n.Doctor != null ? n.Doctor.FullName : "Unknown",
                n.Content, n.VisitType, n.NoteDate))
            .ToListAsync();
}

// ── Dashboard Repository ──────────────────────────────────────────────────────
public class DashboardRepository : IDashboardRepository
{
    private readonly AppDbContext _db;
    public DashboardRepository(AppDbContext db) { _db = db; }

    public async Task<DashboardStatsDto> GetStatsAsync(string doctorId)
    {
        var today = DateTime.UtcNow.Date;
        var soon = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));

        var totalPatients = await _db.Patients.CountAsync(p => p.DoctorId == doctorId);
        var activePatients = await _db.Patients.CountAsync(p => p.DoctorId == doctorId && p.Status == PatientStatus.Active);
        var todayAppts = await _db.Appointments.CountAsync(a => a.DoctorId == doctorId && a.ScheduledAt.Date == today);
        var upcomingAppts = await _db.Appointments.CountAsync(a => a.DoctorId == doctorId && a.ScheduledAt > DateTime.UtcNow && a.Status != AppointmentStatus.Cancelled);
        var activeRx = await _db.Prescriptions.CountAsync(p => p.DoctorId == doctorId && p.Status == PrescriptionStatus.Active);
        var expiringRx = await _db.Prescriptions.CountAsync(p => p.DoctorId == doctorId && p.Status == PrescriptionStatus.ExpiringSoon);
        var pendingAmount = await _db.Invoices.Where(i => i.DoctorId == doctorId && (i.Status == InvoiceStatus.Pending || i.Status == InvoiceStatus.Overdue)).SumAsync(i => i.Amount);
        var overdueInv = await _db.Invoices.CountAsync(i => i.DoctorId == doctorId && i.Status == InvoiceStatus.Overdue);

        var todaySchedule = await _db.Appointments
            .Include(a => a.Patient)
            .Where(a => a.DoctorId == doctorId && a.ScheduledAt.Date == today)
            .OrderBy(a => a.ScheduledAt)
            .Select(a => new AppointmentDto(a.Id, a.PatientId, a.Patient != null ? a.Patient.FullName : "",
                a.ScheduledAt, a.DurationMinutes, a.Type.ToString(), a.Status.ToString(),
                a.Reason, a.Notes, a.Location, a.CreatedAt))
            .ToListAsync();

        var recentPatients = await _db.Patients
            .Where(p => p.DoctorId == doctorId)
            .OrderByDescending(p => p.UpdatedAt)
            .Take(5)
            .Select(p => new PatientSummaryDto(p.Id, p.FullName, p.Age, p.Gender.ToString(),
                p.BloodType.ToString(), p.Status.ToString(), p.Email, p.Phone, p.PrimaryCondition, null, null))
            .ToListAsync();

        return new DashboardStatsDto(
            totalPatients, activePatients, todayAppts, upcomingAppts,
            activeRx, expiringRx, pendingAmount, overdueInv,
            todaySchedule, recentPatients);
    }
}
