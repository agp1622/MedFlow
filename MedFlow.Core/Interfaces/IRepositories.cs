using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;

namespace MedFlow.Core.Interfaces;

public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(int id);
    /// <summary>The record when it exists and belongs to the clinic; null otherwise (same as missing). Only for <see cref="IClinicScoped"/> entities.</summary>
    Task<T?> GetInClinicAsync(int id, int clinicId);
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
}

public interface IPatientRepository : IRepository<Patient>
{
    Task<PagedResult<PatientSummaryDto>> GetPagedAsync(ClinicScope scope, QueryParams query);
    Task<Patient?> GetWithDetailsAsync(int id, ClinicScope scope);
    Task<DateTime?> GetLastVisitAsync(int patientId);
    Task<DateTime?> GetNextAppointmentAsync(int patientId);
    Task<IEnumerable<PatientSummaryDto>> GetRecentAsync(ClinicScope scope, int count = 5);
}

public interface IAppointmentRepository : IRepository<Appointment>
{
    Task<PagedResult<AppointmentDto>> GetPagedAsync(ClinicScope scope, QueryParams query);
    Task<IEnumerable<AppointmentDto>> GetTodayAsync(ClinicScope scope);
    Task<IEnumerable<AppointmentDto>> GetByPatientAsync(int patientId, ClinicScope scope);
    Task<IEnumerable<AppointmentDto>> GetUpcomingAsync(ClinicScope scope, int count = 5);
}

public interface IPrescriptionRepository : IRepository<Prescription>
{
    Task<PagedResult<PrescriptionDto>> GetPagedAsync(ClinicScope scope, QueryParams query);
    Task<IEnumerable<PrescriptionDto>> GetByPatientAsync(int patientId, ClinicScope scope);
    Task<int> GetExpiringCountAsync(ClinicScope scope, int daysAhead = 30);
    Task UpdateExpiryStatusesAsync();
    /// <summary>Data for the printable prescription; null unless the prescription and its patient belong to the clinic.</summary>
    Task<PrescriptionDocumentData?> GetDocumentDataAsync(int id, ClinicScope scope);
}

public interface IPrescriptionDocumentRenderer
{
    /// <summary>Renders a one-page PDF with a blank signature block (no electronic signature).</summary>
    byte[] Render(PrescriptionDocumentData data);
}

public interface IInvoiceRepository : IRepository<Invoice>
{
    Task<PagedResult<InvoiceDto>> GetPagedAsync(ClinicScope scope, QueryParams query);
    Task<IEnumerable<InvoiceDto>> GetByPatientAsync(int patientId, ClinicScope scope);
    Task<decimal> GetPendingAmountAsync(ClinicScope scope);
    Task<int> GetOverdueCountAsync(ClinicScope scope);
    Task UpdateOverdueStatusesAsync();
    Task<string> GenerateInvoiceNumberAsync();
    /// <summary>Data for a claim draft; null unless the invoice and its patient both belong to the clinic.</summary>
    Task<ClaimSourceData?> GetClaimSourceAsync(int id, ClinicScope scope);
}

public interface IVitalSignRepository : IRepository<VitalSign>
{
    Task<IEnumerable<VitalSignDto>> GetByPatientAsync(int patientId, ClinicScope scope);
    Task<VitalSignDto?> GetLatestByPatientAsync(int patientId, ClinicScope scope);
}

public interface IMedicalNoteRepository : IRepository<MedicalNote>
{
    Task<IEnumerable<MedicalNoteDto>> GetByPatientAsync(int patientId, ClinicScope scope);
    Task<CopyForwardDto?> GetLatestForPatientAsync(int patientId, ClinicScope scope);
}

public interface INoteTemplateRepository : IRepository<NoteTemplate>
{
    Task<PagedResult<NoteTemplateDto>> GetPagedAsync(string doctorId, QueryParams query);
    Task<NoteTemplate?> GetWithOwnerCheckAsync(int id, string doctorId);
    Task<bool> NameExistsAsync(string doctorId, string name, int? excludeId = null);
}

public interface IPatientAttachmentRepository : IRepository<PatientAttachment>
{
    Task<IEnumerable<PatientAttachmentDto>> GetByPatientAsync(int patientId, ClinicScope scope);
    Task SetSharingAsync(PatientAttachment attachment, bool shared);
}

public interface IDashboardRepository
{
    Task<DashboardStatsDto> GetStatsAsync(ClinicScope scope);
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
    /// <summary>True when the (non-deleted) patient exists and belongs to the clinic.</summary>
    Task<bool> OwnsPatientAsync(int patientId, ClinicScope scope);
    Task<ClinicalSummaryDto> GetSummaryAsync(int patientId, ClinicScope scope);
    Task<T?> GetOwnedAsync<T>(int id, int patientId, ClinicScope scope) where T : ClinicalEntry;
    Task<int> CountAsync<T>(int patientId, ClinicScope scope) where T : ClinicalEntry;
    Task<bool> AllergyExistsAsync(int patientId, ClinicScope scope, string substance, int? exceptId = null);
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
    Task<PagedResult<IntakeSubmissionSummaryDto>> GetPagedAsync(ClinicScope scope, IntakeStatus? status, int page, int pageSize);
    Task<IntakeSubmissionDetailDto?> GetDetailAsync(int id, ClinicScope scope);
    /// <summary>Accepts (applies the answers to the patient record) or rejects a pending submission.</summary>
    Task<DecisionResult> DecideAsync(int id, ClinicScope scope, bool accept, string? reason);
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
    /// <summary>True when the start is an offered slot of the doctor (availability, lead time, horizon) with no active clash.</summary>
    Task<bool> IsSlotOpenAsync(string doctorId, DateTime startsAt);
}

public enum ReminderRespondResult { Ok, Invalid, Closed }

public interface IReminderRepository
{
    /// <summary>Takes the raw emailed token. Null for any unknown, expired, stale or tampered token (callers must not distinguish).</summary>
    Task<ReminderLookupDto?> LookupAsync(string token);
    Task<(ReminderRespondResult Result, ReminderLookupDto? Dto)> RespondAsync(string token, ReminderAction action);
    /// <summary>Null when the appointment does not belong to the clinic.</summary>
    Task<ReminderLogDto?> GetLogAsync(int appointmentId, ClinicScope scope);
}

/// <summary>Report aggregates: clinic-wide for Owners, limited to the caller's own doctor-linked records otherwise. Dates are inclusive UTC calendar days.</summary>
public interface IReportRepository
{
    Task<RevenueReport> GetRevenueAsync(ClinicScope scope, DateOnly from, DateOnly to, ReportPeriod period);
    Task<VisitsReport> GetVisitsAsync(ClinicScope scope, DateOnly from, DateOnly to, ReportPeriod period);
    Task<NoShowReport> GetNoShowsAsync(ClinicScope scope, DateOnly from, DateOnly to, ReportPeriod period);
    Task<ArAgingReport> GetArAgingAsync(ClinicScope scope, DateOnly asOf, int page, int pageSize);
    /// <summary>Open invoices, oldest first, at most <paramref name="max"/> rows.</summary>
    Task<IReadOnlyList<ArInvoiceDto>> GetArRowsAsync(ClinicScope scope, DateOnly asOf, int max);
}

public interface IReminderProcessor
{
    /// <summary>Sends every reminder that is due. Returns the number of emails sent.</summary>
    Task<int> ProcessDueAsync(CancellationToken ct = default);
}

public interface ILabOrderRepository
{
    Task<bool> OwnsPatientAsync(int patientId, ClinicScope scope);
    Task<LabSummaryDto> GetSummaryAsync(int patientId, ClinicScope scope);
    Task<LabOrder?> GetOrderAsync(int orderId, int patientId, ClinicScope scope);
    Task<int> CountOrdersAsync(int patientId, ClinicScope scope);
    Task<LabOrderDto> AddOrderAsync(LabOrder order);
    Task<LabOrderDto> SaveOrderAsync(LabOrder order);
    Task DeleteOrderAsync(LabOrder order);
    Task<LabOrderDto> AddResultAsync(LabOrder order, LabResult result);
    Task<LabOrderDto> SaveResultAsync(LabOrder order, LabResult result);
    Task<LabOrderDto> RemoveResultAsync(LabOrder order, LabResult result);
}

public interface IWaitlistRepository
{
    Task<PagedResult<WaitlistEntryDto>> GetPagedAsync(ClinicScope scope, QueryParams q);
    Task<(WaitlistAddOutcome Outcome, WaitlistEntryDto? Entry)> AddAsync(int patientId, ClinicScope scope);
    /// <summary>Null when the entry is not a Waiting entry of the clinic (otherwise the removed entry's patient id).</summary>
    Task<int?> RemoveAsync(int entryId, ClinicScope scope);
    Task<PortalWaitlistDto> GetForPatientAsync(int patientId);
    Task<(WaitlistAddOutcome Outcome, WaitlistEntryDto? Entry)> JoinAsync(Patient patient);
    Task<bool> LeaveAsync(int patientId);
    /// <summary>Null for any unknown, expired, used or otherwise unusable token.</summary>
    Task<WaitlistOfferDto?> LookupOfferAsync(string token);
    Task<(WaitlistClaimOutcome Outcome, WaitlistClaimDto? Claim)> ClaimOfferAsync(string token);
    Task<bool> LeaveByTokenAsync(string token);
}

public interface IWaitlistService
{
    /// <summary>Emails offers for a just-freed slot to the earliest waiting patients. Returns the number of emails sent; never throws.</summary>
    Task<int> OfferFreedSlotAsync(string doctorId, DateTime slotStartsAt);
}
