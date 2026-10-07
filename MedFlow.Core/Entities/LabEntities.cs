using MedFlow.Core.Enums;

namespace MedFlow.Core.Entities;

/// <summary>A lab test ordered by a doctor for one of their patients. Doctor-private.</summary>
public class LabOrder : BaseEntity, IClinicScoped
{
    public int ClinicId { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }
    public string DoctorId { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateOnly OrderedDate { get; set; }
    public LabOrderStatus Status { get; set; } = LabOrderStatus.Ordered;
    public ICollection<LabResult> Results { get; set; } = new List<LabResult>();
}

/// <summary>One numeric result value. The reference range is entered by the doctor; the abnormal flag is derived, never stored.</summary>
public class LabResult : BaseEntity
{
    public int LabOrderId { get; set; }
    public string AnalyteName { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string? Unit { get; set; }
    public decimal? ReferenceLow { get; set; }
    public decimal? ReferenceHigh { get; set; }

    public LabFlag Flag =>
        ReferenceLow.HasValue && Value < ReferenceLow.Value ? LabFlag.Low
        : ReferenceHigh.HasValue && Value > ReferenceHigh.Value ? LabFlag.High
        : LabFlag.None;
}
