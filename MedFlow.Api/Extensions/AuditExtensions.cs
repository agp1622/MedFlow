using MedFlow.Core;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MedFlow.Api.Extensions;

public static class AuditExtensions
{
    /// <summary>
    /// Records an audit event for the signed-in user. Returns false when the patient is missing or not
    /// accessible to them; callers then answer NotFound, exactly as for a missing patient.
    /// </summary>
    public static Task<bool> AuditAsync(this ControllerBase c, IAuditService audit, int patientId,
        AuditAction action, AuditItemKind kind, int? itemId = null, IEnumerable<string>? fields = null)
    {
        var role = c.User.IsInRole(Roles.Patient) ? Roles.Patient : Roles.Doctor;
        return audit.RecordAsync(c.User.GetUserId(), role, patientId, action, kind, itemId, fields);
    }
}
