using MedFlow.Core.Enums;

namespace MedFlow.Core.DTOs;

// ── Auth ──────────────────────────────────────────────────────────────────────
public record RegisterRequest(string Email, string Password, string FirstName, string LastName, string Specialty);
public record LoginRequest(string Email, string Password);
public record GoogleLoginRequest(string Credential);
public record AuthResponse(string Token, string RefreshToken, DateTime Expires, UserDto User);
public record UserDto(string Id, string Email, string FirstName, string LastName, string Specialty, string Role = "Doctor");

// ── Auth (Password Recovery) ─────────────────────────────────────────────────
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Email, string Token, string NewPassword, string ConfirmPassword);

// ── Pagination ────────────────────────────────────────────────────────────────
public record PagedResult<T>(IEnumerable<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNext => Page < TotalPages;
    public bool HasPrev => Page > 1;
}

public record QueryParams(int Page = 1, int PageSize = 20, string? Search = null, string? SortBy = null, bool SortDesc = false);

// ── Patient ───────────────────────────────────────────────────────────────────
public record PatientDto(
    int Id, string FirstName, string LastName, string FullName,
    DateOnly DateOfBirth, int Age, string Gender, string BloodType,
    string Status, string Email, string Phone,
    string? Address, string? City, string? State, string? ZipCode,
    string? PrimaryCondition, string? Allergies, string? Notes,
    string? InsuranceProvider, string? InsurancePolicyNumber,
    DateTime? LastVisit, DateTime? NextAppointment,
    DateTime CreatedAt, DateTime UpdatedAt,
    string PortalStatus = "NotInvited"
);

public record PatientSummaryDto(
    int Id, string FullName, int Age, string Gender, string BloodType,
    string Status, string Email, string Phone,
    string? PrimaryCondition, DateTime? LastVisit, DateTime? NextAppointment
);

public record CreatePatientRequest(
    string FirstName, string LastName, DateOnly DateOfBirth,
    Gender Gender, BloodType BloodType,
    string Email, string Phone,
    string? Address, string? City, string? State, string? ZipCode,
    string? PrimaryCondition, string? Allergies, string? Notes,
    string? InsuranceProvider, string? InsurancePolicyNumber
);

public record UpdatePatientRequest(
    string FirstName, string LastName, DateOnly DateOfBirth,
    Gender Gender, BloodType BloodType, PatientStatus Status,
    string Email, string Phone,
    string? Address, string? City, string? State, string? ZipCode,
    string? PrimaryCondition, string? Allergies, string? Notes,
    string? InsuranceProvider, string? InsurancePolicyNumber
);

// ── Appointment ───────────────────────────────────────────────────────────────
public record AppointmentDto(
    int Id, int PatientId, string PatientName,
    DateTime ScheduledAt, int DurationMinutes,
    string Type, string Status,
    string? Reason, string? Notes, string? Location,
    DateTime CreatedAt
);

public record CreateAppointmentRequest(
    int PatientId, DateTime ScheduledAt, int DurationMinutes,
    AppointmentType Type, string? Reason, string? Location
);

public record UpdateAppointmentRequest(
    DateTime ScheduledAt, int DurationMinutes,
    AppointmentType Type, AppointmentStatus Status,
    string? Reason, string? Notes, string? Location
);

// ── Prescription ──────────────────────────────────────────────────────────────
public record PrescriptionDto(
    int Id, int PatientId, string PatientName,
    string DrugName, string Dosage, string Frequency,
    string? Instructions, DateOnly IssuedDate, DateOnly ExpiryDate,
    int RefillsRemaining, string Status, DateTime CreatedAt
);

public record CreatePrescriptionRequest(
    int PatientId, string DrugName, string Dosage, string Frequency,
    string? Instructions, DateOnly IssuedDate, DateOnly ExpiryDate,
    int RefillsRemaining
);

public record UpdatePrescriptionRequest(
    string DrugName, string Dosage, string Frequency,
    string? Instructions, DateOnly ExpiryDate,
    int RefillsRemaining, PrescriptionStatus Status
);

// ── Invoice ───────────────────────────────────────────────────────────────────
public record InvoiceDto(
    int Id, int PatientId, string PatientName,
    int? AppointmentId, string InvoiceNumber,
    string ServiceDescription, decimal Amount, decimal? PaidAmount,
    string Status, DateTime InvoiceDate, DateTime? DueDate,
    DateTime? PaidDate, string? Notes, DateTime CreatedAt
);

public record CreateInvoiceRequest(
    int PatientId, int? AppointmentId,
    string ServiceDescription, decimal Amount,
    DateTime? DueDate, string? Notes
);

public record UpdateInvoiceRequest(
    string ServiceDescription, decimal Amount,
    InvoiceStatus Status, DateTime? DueDate,
    decimal? PaidAmount, string? Notes
);

// ── VitalSign ─────────────────────────────────────────────────────────────────
public record VitalSignDto(
    int Id, int PatientId, DateTime RecordedAt,
    string? BloodPressure, int? HeartRate, decimal? Weight,
    decimal? Height, decimal? Bmi, decimal? Temperature,
    int? OxygenSaturation, string? RecordedBy
);

public record CreateVitalSignRequest(
    int PatientId, string? BloodPressure, int? HeartRate,
    decimal? Weight, decimal? Height, decimal? Temperature,
    int? OxygenSaturation
);

// ── MedicalNote ───────────────────────────────────────────────────────────────
public record MedicalNoteDto(
    int Id, int PatientId, string DoctorName,
    string Content, string? VisitType, DateTime NoteDate,
    bool SharedWithPatient = false
);

public record CreateMedicalNoteRequest(int PatientId, string Content, string? VisitType);

// ── NoteTemplate ──────────────────────────────────────────────────────────────
public record NoteTemplateDto(int Id, string Name, string Body, bool IsBuiltIn, DateTime? UpdatedAt);
public record CreateNoteTemplateRequest(string Name, string Body);
public record UpdateNoteTemplateRequest(string Name, string Body);
public record CopyForwardDto(int NoteId, string Content, string? VisitType, DateTime NoteDate);

// ── PatientAttachment ────────────────────────────────────────────────────────
public record PatientAttachmentDto(
    int Id, int PatientId, string FileName, string ContentType,
    long FileSize, string? Category, string? Description,
    DateTime CreatedAt, bool SharedWithPatient = false
);

// ── Dashboard ─────────────────────────────────────────────────────────────────
public record DashboardStatsDto(
    int TotalPatients, int ActivePatients,
    int TodayAppointments, int UpcomingAppointments,
    int ActivePrescriptions, int ExpiringPrescriptions,
    decimal PendingInvoicesAmount, int OverdueInvoices,
    IEnumerable<AppointmentDto> TodaySchedule,
    IEnumerable<PatientSummaryDto> RecentPatients
);

// ── Patient portal ────────────────────────────────────────────────────────────
public record AcceptInvitationRequest(string Token, string Email, string Password, string ConfirmPassword);
public record InvitationResultDto(string Message, DateTime ExpiresAt);
public record SetSharingRequest(bool Shared);

public record PortalProfileDto(string FirstName, string LastName, string DoctorName);
public record PortalAppointmentDto(
    int Id, DateTime ScheduledAt, int DurationMinutes, string Type, string Status,
    string? Reason, string? Location);
public record PortalPrescriptionDto(
    int Id, string DrugName, string Dosage, string Frequency, string? Instructions,
    DateOnly IssuedDate, DateOnly ExpiryDate, int RefillsRemaining, string Status);
public record PortalInvoiceDto(
    int Id, string InvoiceNumber, string ServiceDescription, decimal Amount, decimal? PaidAmount,
    string Status, DateTime InvoiceDate, DateTime? DueDate, DateTime? PaidDate);
public record PortalAttachmentDto(
    int Id, string FileName, string ContentType, long FileSize,
    string? Category, string? Description, DateTime CreatedAt);
public record PortalNoteDto(int Id, string DoctorName, string? VisitType, string Content, DateTime NoteDate);

// ── Audit log ─────────────────────────────────────────────────────────────────
public record AuditEventDto(int Id, DateTime OccurredAt, string ActorUserId, string ActorName, string ActorRole,
    string Action, string ItemKind, int? ItemId, string[] ChangedFields);
public record AuditLogQuery(AuditAction? Action = null, string? Actor = null, DateTime? From = null,
    DateTime? To = null, int Page = 1, int PageSize = 20);
