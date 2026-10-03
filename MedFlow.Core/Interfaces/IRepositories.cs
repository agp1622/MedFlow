using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;

namespace MedFlow.Core.Interfaces;

public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
}

public interface IPatientRepository : IRepository<Patient>
{
    Task<PagedResult<PatientSummaryDto>> GetPagedAsync(string doctorId, QueryParams query);
    Task<Patient?> GetWithDetailsAsync(int id, string doctorId);
    Task<DateTime?> GetLastVisitAsync(int patientId);
    Task<DateTime?> GetNextAppointmentAsync(int patientId);
    Task<IEnumerable<PatientSummaryDto>> GetRecentAsync(string doctorId, int count = 5);
}

public interface IAppointmentRepository : IRepository<Appointment>
{
    Task<PagedResult<AppointmentDto>> GetPagedAsync(string doctorId, QueryParams query);
    Task<IEnumerable<AppointmentDto>> GetTodayAsync(string doctorId);
    Task<IEnumerable<AppointmentDto>> GetByPatientAsync(int patientId, string doctorId);
    Task<IEnumerable<AppointmentDto>> GetUpcomingAsync(string doctorId, int count = 5);
}

public interface IPrescriptionRepository : IRepository<Prescription>
{
    Task<PagedResult<PrescriptionDto>> GetPagedAsync(string doctorId, QueryParams query);
    Task<IEnumerable<PrescriptionDto>> GetByPatientAsync(int patientId, string doctorId);
    Task<int> GetExpiringCountAsync(string doctorId, int daysAhead = 30);
    Task UpdateExpiryStatusesAsync();
}

public interface IInvoiceRepository : IRepository<Invoice>
{
    Task<PagedResult<InvoiceDto>> GetPagedAsync(string doctorId, QueryParams query);
    Task<IEnumerable<InvoiceDto>> GetByPatientAsync(int patientId, string doctorId);
    Task<decimal> GetPendingAmountAsync(string doctorId);
    Task<int> GetOverdueCountAsync(string doctorId);
    Task UpdateOverdueStatusesAsync();
    Task<string> GenerateInvoiceNumberAsync();
}

public interface IVitalSignRepository : IRepository<VitalSign>
{
    Task<IEnumerable<VitalSignDto>> GetByPatientAsync(int patientId, string doctorId);
    Task<VitalSignDto?> GetLatestByPatientAsync(int patientId);
}

public interface IMedicalNoteRepository : IRepository<MedicalNote>
{
    Task<IEnumerable<MedicalNoteDto>> GetByPatientAsync(int patientId, string doctorId);
    Task<MedicalNote?> GetWithOwnerCheckAsync(int id, string doctorId);
}

public interface IPatientAttachmentRepository : IRepository<PatientAttachment>
{
    Task<IEnumerable<PatientAttachmentDto>> GetByPatientAsync(int patientId, string doctorId);
    Task<PatientAttachment?> GetWithOwnerCheckAsync(int id, string doctorId);
    Task SetSharingAsync(PatientAttachment attachment, bool shared);
}

public interface IDashboardRepository
{
    Task<DashboardStatsDto> GetStatsAsync(string doctorId);
}

public interface IPortalRepository
{
    /// <summary>Resolves the patient linked to a portal user (null if none).</summary>
    Task<Patient?> GetPatientByUserIdAsync(string userId);
    Task<PortalProfileDto?> GetProfileAsync(Patient patient);
    Task<IEnumerable<PortalAppointmentDto>> GetAppointmentsAsync(int patientId);
    Task<IEnumerable<PortalPrescriptionDto>> GetPrescriptionsAsync(int patientId);
    Task<IEnumerable<PortalInvoiceDto>> GetInvoicesAsync(int patientId);
    Task<IEnumerable<PortalAttachmentDto>> GetSharedAttachmentsAsync(int patientId);
    Task<PatientAttachment?> GetSharedAttachmentAsync(int id, int patientId);
    Task<IEnumerable<PortalNoteDto>> GetSharedNotesAsync(int patientId);
    Task LogAccessAsync(int patientId, string resourceType, IEnumerable<int> resourceIds, string action);
}

public interface IPortalInvitationRepository
{
    /// <summary>Creates a new invitation (superseding earlier pending ones) and returns the raw token.</summary>
    Task<(string Token, DateTime ExpiresAt)> CreateAsync(Patient patient);
    /// <summary>Returns the patient for a valid (unused, unexpired, email-matching, active) invitation.</summary>
    Task<(PortalInvitation Invitation, Patient Patient)?> FindValidAsync(string token, string email);
    Task MarkUsedAsync(PortalInvitation invitation, Patient patient, string userId);
    Task RevokeAsync(Patient patient);
    Task<string> GetPortalStatusAsync(Patient patient);
}

public interface IBookingRepository
{
    Task<AvailabilityDto> GetAvailabilityAsync(string doctorId);
    Task<IEnumerable<AvailabilityWindowDto>> ReplaceWeeklyAsync(string doctorId, IEnumerable<(DayOfWeek Day, TimeOnly Start, TimeOnly End)> windows);
    /// <summary>Returns null when the date is already blocked.</summary>
    Task<BlockedDateDto?> AddBlockedDateAsync(string doctorId, DateOnly date, string? label);
    Task<bool> RemoveBlockedDateAsync(string doctorId, int id);
    /// <summary>Open slots of the patient's doctor between the two dates (inclusive).</summary>
    Task<IEnumerable<BookingSlotDto>> GetOpenSlotsAsync(Patient patient, DateOnly from, DateOnly to);
    /// <summary>Atomically re-validates the slot and creates a Pending appointment.</summary>
    Task<(BookingOutcome Outcome, PortalAppointmentDto? Appointment)> BookAsync(Patient patient, DateTime startsAt, string? reason);
}
