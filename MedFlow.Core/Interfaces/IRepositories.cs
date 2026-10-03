using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;

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
    /// <summary>Data for the printable prescription; null unless the prescription and its patient belong to the doctor.</summary>
    Task<PrescriptionDocumentData?> GetDocumentDataAsync(int id, string doctorId);
}

public interface IPrescriptionDocumentRenderer
{
    /// <summary>Renders a one-page PDF with a blank signature block (no electronic signature).</summary>
    byte[] Render(PrescriptionDocumentData data);
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
    Task<CopyForwardDto?> GetLatestForPatientAsync(int patientId, string doctorId);
}

public interface INoteTemplateRepository : IRepository<NoteTemplate>
{
    Task<PagedResult<NoteTemplateDto>> GetPagedAsync(string doctorId, QueryParams query);
    Task<NoteTemplate?> GetWithOwnerCheckAsync(int id, string doctorId);
    Task<bool> NameExistsAsync(string doctorId, string name, int? excludeId = null);
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

public interface IPatientClinicalRepository
{
    /// <summary>True when the (non-deleted) patient exists and belongs to the doctor.</summary>
    Task<bool> OwnsPatientAsync(int patientId, string doctorId);
    Task<ClinicalSummaryDto> GetSummaryAsync(int patientId, string doctorId);
    Task<T?> GetOwnedAsync<T>(int id, int patientId, string doctorId) where T : ClinicalEntry;
    Task<int> CountAsync<T>(int patientId, string doctorId) where T : ClinicalEntry;
    Task<bool> AllergyExistsAsync(int patientId, string doctorId, string substance, int? exceptId = null);
    Task<T> AddAsync<T>(T entry) where T : ClinicalEntry;
    Task UpdateAsync<T>(T entry) where T : ClinicalEntry;
    Task DeleteAsync<T>(T entry) where T : ClinicalEntry;
}

public interface IIntakeRepository
{
    /// <summary>Creates a link (superseding earlier unused ones) and returns the raw token.</summary>
    Task<(string Token, DateTime ExpiresAt)> CreateLinkAsync(Patient patient);
    /// <summary>False when the patient already has a Pending/Accepted submission or a still-valid unused link.</summary>
    Task<bool> NeedsLinkAsync(int patientId);
    /// <summary>Returns the link and patient for a valid (unused, unexpired, active patient, email unchanged) token.</summary>
    Task<(IntakeLink Link, Patient Patient)?> FindValidLinkAsync(string token);
    /// <summary>Consumes the link and stores the submission in one save; false if the link was already used.</summary>
    Task<bool> SubmitAsync(IntakeLink link, Patient patient, IntakeSubmission submission);
    Task<PagedResult<IntakeSubmissionSummaryDto>> GetPagedAsync(string doctorId, IntakeStatus? status, int page, int pageSize);
    Task<IntakeSubmissionDetailDto?> GetDetailAsync(int id, string doctorId);
    /// <summary>Accepts (applies the answers to the patient record) or rejects a pending submission.</summary>
    Task<DecisionResult> DecideAsync(int id, string doctorId, bool accept, string? reason);
}

public enum DecisionResult { Done, NotFound, AlreadyDecided }

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

public enum ReminderRespondResult { Ok, Invalid, Closed }

public interface IReminderRepository
{
    /// <summary>Takes the raw emailed token. Null for any unknown, expired, stale or tampered token (callers must not distinguish).</summary>
    Task<ReminderLookupDto?> LookupAsync(string token);
    Task<(ReminderRespondResult Result, ReminderLookupDto? Dto)> RespondAsync(string token, ReminderAction action);
    /// <summary>Null when the appointment does not belong to the doctor.</summary>
    Task<ReminderLogDto?> GetLogAsync(int appointmentId, string doctorId);
}

public interface IReminderProcessor
{
    /// <summary>Sends every reminder that is due. Returns the number of emails sent.</summary>
    Task<int> ProcessDueAsync(CancellationToken ct = default);
}
