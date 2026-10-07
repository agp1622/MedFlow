using MedFlow.Api.Authorization;
using MedFlow.Core;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MedFlow.Api.Extensions;

public static class AuditExtensions
{
    /// <summary>
    /// Records an audit event for the signed-in user (staff with their clinic role, or the portal patient).
    /// Returns false when the patient is missing or not accessible to them; callers then answer NotFound,
    /// exactly as for a missing patient.
    /// </summary>
    public static Task<bool> AuditAsync(this ControllerBase c, IAuditService audit, int patientId,
        AuditAction action, AuditItemKind kind, int? itemId = null, IEnumerable<string>? fields = null)
    {
        if (c.User.IsInRole(Roles.Patient))
            return audit.RecordPortalAsync(c.User.GetUserId(), patientId, action, kind, itemId, fields);
        return audit.RecordAsync(c.GetScope(), patientId, action, kind, itemId, fields);
    }
}
