using MedFlow.Core.Enums;

namespace MedFlow.Core.Entities;

public class Doctor : BaseEntity
{
    public string UserId { get; set; } = string.Empty; // Links to ApplicationUser
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"Dr. {FirstName} {LastName}";
    public string Specialty { get; set; } = string.Empty;
    public string? LicenseNumber { get; set; }
    public string? Phone { get; set; }

    public ICollection<Patient> Patients { get; set; } = new List<Patient>();
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}

public class Appointment : BaseEntity
{
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }
    public string DoctorId { get; set; } = string.Empty;
    public Doctor? Doctor { get; set; }

    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public AppointmentType Type { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
    public string? Reason { get; set; }
    public string? Notes { get; set; }
    public string? Location { get; set; }
}

public class Prescription : BaseEntity
{
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }
    public string DoctorId { get; set; } = string.Empty;
    public Doctor? Doctor { get; set; }

    public string DrugName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public DateOnly IssuedDate { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public int RefillsRemaining { get; set; }
    public PrescriptionStatus Status { get; set; } = PrescriptionStatus.Active;
}

public class Invoice : BaseEntity
{
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }
    public string DoctorId { get; set; } = string.Empty;
    public Doctor? Doctor { get; set; }
    public int? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;
    public string ServiceDescription { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? PaidAmount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public string? Notes { get; set; }
}

public class VitalSign : BaseEntity
{
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public string? BloodPressure { get; set; }
    public int? HeartRate { get; set; }
    public decimal? Weight { get; set; }
    public decimal? Height { get; set; }
    public decimal? Bmi { get; set; }
    public decimal? Temperature { get; set; }
    public int? OxygenSaturation { get; set; }
    public string? RecordedBy { get; set; }
}

public class MedicalNote : BaseEntity
{
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }
    public string DoctorId { get; set; } = string.Empty;
    public Doctor? Doctor { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? VisitType { get; set; }
    public DateTime NoteDate { get; set; } = DateTime.UtcNow;
    public bool SharedWithPatient { get; set; }
}

public class NoteTemplate : BaseEntity
{
    public string DoctorId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public class PatientAttachment : BaseEntity
{
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }
    public string DoctorId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public bool SharedWithPatient { get; set; }
}

public class PortalInvitation : BaseEntity
{
    public int PatientId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty; // SHA-256 of the emailed token
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }                 // set on acceptance or when superseded/revoked
}

public class PortalAccessLog : BaseEntity
{
    public int PatientId { get; set; }
    public string ResourceType { get; set; } = string.Empty; // Attachment | Note
    public int ResourceId { get; set; }
    public string Action { get; set; } = string.Empty;       // View | Download
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

public class IntakeLink : BaseEntity
{
    public int PatientId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty; // SHA-256 of the emailed token
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }                 // set on submission or when superseded
}

public class IntakeSubmission : BaseEntity
{
    public int PatientId { get; set; }
    public string DoctorId { get; set; } = string.Empty;
    public int IntakeLinkId { get; set; }
    public IntakeStatus Status { get; set; } = IntakeStatus.Pending;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    // Answers
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? InsuranceProvider { get; set; }
    public string? InsurancePolicyNumber { get; set; }
    public string? PrimaryCondition { get; set; }
    public string? Allergies { get; set; }
    public string? CurrentMedications { get; set; }
    public string? PastHistory { get; set; }
    public string? AdditionalNotes { get; set; }

    // Consent (immutable after submission)
    public string ConsentVersion { get; set; } = string.Empty;
    public bool ConsentAgreed { get; set; }
    public string SignatureName { get; set; } = string.Empty;
    public DateTime SignedAt { get; set; }

    // Decision
    public string? DecidedByDoctorId { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? RejectionReason { get; set; }
}

/// <summary>A recurring weekly window (UTC) in which a doctor accepts online bookings.</summary>
public class DoctorAvailability : BaseEntity
{
    public string DoctorId { get; set; } = string.Empty;
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}

/// <summary>A date on which a doctor offers no online slots.</summary>
public class DoctorBlockedDate : BaseEntity
{
    public string DoctorId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string? Label { get; set; }
}

/// <summary>One per (appointment, scheduled time): the emailed response link and its state.</summary>
public class AppointmentReminder : BaseEntity
{
    public int AppointmentId { get; set; }
    public DateTime ScheduledAt { get; set; }             // snapshot of the appointment time this reminder is for
    public string TokenHash { get; set; } = string.Empty; // SHA-256 of the emailed token
    public ReminderStatus Status { get; set; } = ReminderStatus.Pending;
    public int Attempts { get; set; }
    public DateTime? SentAt { get; set; }
    public ReminderResponse Response { get; set; } = ReminderResponse.None;
    public DateTime? RespondedAt { get; set; }
}

public class ReminderDelivery : BaseEntity
{
    public int AppointmentId { get; set; }
    public int ReminderId { get; set; }
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
    public string Channel { get; set; } = "Email";
    public ReminderOutcome Outcome { get; set; }
    public string? Reason { get; set; }
}
