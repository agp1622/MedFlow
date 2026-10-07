using MedFlow.Core.DTOs;
using MedFlow.Core.Enums;

namespace MedFlow.Core.Interfaces;

public interface IAuditService
{
    /// <summary>
    /// Records an event by a staff member against a patient of their clinic. Returns false, recording nothing,
    /// when the patient is missing or in another clinic, so callers answer exactly as they would for a missing patient.
    /// The event stores the acting user, name, role and clinic. Throws if it cannot be stored (fail closed).
    /// </summary>
    Task<bool> RecordAsync(ClinicScope staff, int patientId, AuditAction action,
        AuditItemKind itemKind, int? itemId = null, IEnumerable<string>? changedFields = null);

    /// <summary>Records an event by a portal patient (must be the portal user of that patient). False otherwise.</summary>
    Task<bool> RecordPortalAsync(string portalUserId, int patientId, AuditAction action,
        AuditItemKind itemKind, int? itemId = null, IEnumerable<string>? changedFields = null);

    /// <summary>
    /// True when the staff member may read the patient's log: Owners for any patient of the clinic, Doctors only for
    /// patients whose treating doctor they are. Missing, other-clinic and not-permitted look the same (false).
    /// </summary>
    Task<bool> CanReadLogAsync(ClinicScope staff, int patientId);

    /// <summary>The patient's clinic log, newest first; null when <see cref="CanReadLogAsync"/> would be false.</summary>
    Task<PagedResult<AuditEventDto>?> GetLogAsync(ClinicScope staff, int patientId, AuditLogQuery query);

    /// <summary>Records a security event of a user (for example 2FA enabled). Not tied to a patient; never holds secrets.</summary>
    Task RecordSecurityAsync(string userId, SecurityEventKind kind);
}
