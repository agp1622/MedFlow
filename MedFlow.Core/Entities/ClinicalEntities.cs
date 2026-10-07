using MedFlow.Core.Enums;

namespace MedFlow.Core.Entities;

/// <summary>Doctor-maintained clinical list entry; always scoped to a patient and the owning doctor.</summary>
public abstract class ClinicalEntry : BaseEntity, IClinicScoped
{
    public int ClinicId { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }
    public string DoctorId { get; set; } = string.Empty;
}

public class PatientAllergy : ClinicalEntry
{
    public string Substance { get; set; } = string.Empty;
    public string? Reaction { get; set; }
    public AllergySeverity Severity { get; set; }
}

public class PatientProblem : ClinicalEntry
{
    public string Description { get; set; } = string.Empty;
    public string Icd10Code { get; set; } = string.Empty;
    public ProblemStatus Status { get; set; } = ProblemStatus.Active;
    public DateOnly? OnsetDate { get; set; }
}

public class PatientMedication : ClinicalEntry
{
    public string Name { get; set; } = string.Empty;
    public string? Dosage { get; set; }
    public string? Frequency { get; set; }
    public string? Notes { get; set; }
}
