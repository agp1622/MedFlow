using MedFlow.Core.Enums;

namespace MedFlow.Core.Entities;

public class Patient : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public DateOnly DateOfBirth { get; set; }
    public int Age => CalculateAge();
    public Gender Gender { get; set; }
    public BloodType BloodType { get; set; }
    public PatientStatus Status { get; set; } = PatientStatus.Active;

    // Contact
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }

    // Medical
    public string? PrimaryCondition { get; set; }
    public string? Allergies { get; set; }
    public string? Notes { get; set; }
    public string? InsuranceProvider { get; set; }
    public string? InsurancePolicyNumber { get; set; }
    public string? InsuranceGroupNumber { get; set; }
    public string? InsurancePayerId { get; set; }
    public string? InsuranceSubscriberName { get; set; }
    public DateOnly? InsuranceSubscriberDateOfBirth { get; set; }
    public InsuranceRelationship? InsuranceSubscriberRelationship { get; set; }

    // Navigation
    // Linked portal (patient-role) account, if the patient has accepted an invitation
    public string? PortalUserId { get; set; }

    public string DoctorId { get; set; } = string.Empty;
    public Doctor? Doctor { get; set; }
    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<VitalSign> VitalSigns { get; set; } = new List<VitalSign>();
    public ICollection<MedicalNote> MedicalNotes { get; set; } = new List<MedicalNote>();
    public ICollection<PatientAttachment> Attachments { get; set; } = new List<PatientAttachment>();

    private int CalculateAge()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - DateOfBirth.Year;
        if (DateOfBirth > today.AddYears(-age)) age--;
        return age;
    }
}
