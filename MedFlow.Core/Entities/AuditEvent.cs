using MedFlow.Core.Enums;

namespace MedFlow.Core.Entities;

/// <summary>
/// One immutable record of a view or change of a patient record. Deliberately not a BaseEntity:
/// it is never updated or soft-deleted, and it holds no clinical values (only kinds, ids and field names).
/// </summary>
public class AuditEvent : IClinicScoped
{
    public int Id { get; set; }
    public int ClinicId { get; set; }
    public int PatientId { get; set; }
    public string DoctorId { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string ActorRole { get; set; } = string.Empty;
    public AuditAction Action { get; set; }
    public AuditItemKind ItemKind { get; set; }
    public int? ItemId { get; set; }
    public string? ChangedFields { get; set; } // comma-separated property names
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
