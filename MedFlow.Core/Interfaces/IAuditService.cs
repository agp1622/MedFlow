using MedFlow.Core.DTOs;
using MedFlow.Core.Enums;

namespace MedFlow.Core.Interfaces;

public interface IAuditService
{
    /// <summary>
    /// Records an event against a patient. Returns false, recording nothing, when the patient is missing
    /// or not accessible to the actor (a doctor must own the patient; a Patient actor must be the portal
    /// user of that patient), so callers answer exactly as they would for a missing patient.
    /// Throws if the event cannot be stored, so the audited request fails (fail closed).
    /// </summary>
    Task<bool> RecordAsync(string actorUserId, string actorRole, int patientId, AuditAction action,
        AuditItemKind itemKind, int? itemId = null, IEnumerable<string>? changedFields = null);

    /// <summary>The patient's log for the owning doctor, newest first; null if the patient is missing or not theirs.</summary>
    Task<PagedResult<AuditEventDto>?> GetLogAsync(int patientId, string doctorId, AuditLogQuery query);
}
