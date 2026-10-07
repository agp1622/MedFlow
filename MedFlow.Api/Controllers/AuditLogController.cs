using MedFlow.Api.Authorization;
using MedFlow.Api.Localization;
using MedFlow.Api.Extensions;
using MedFlow.Core;
using MedFlow.Core.DTOs;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedFlow.Api.Controllers;

[ApiController]
[Route("api/patients/{patientId:int}/audit-log")]
[HasPermission(Permission.AuditLogRead)]
public class AuditLogController : ControllerBase
{
    private const int MaxPageSize = 100;
    private readonly IAuditService _audit;
    public AuditLogController(IAuditService audit) => _audit = audit;

    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditEventDto>>> Get(int patientId, [FromQuery] AuditLogQuery q)
    {
        if (q.Page < 1 || q.PageSize < 1 || q.PageSize > MaxPageSize)
            return BadRequest(new { error = this.T("Audit.PageRange", MaxPageSize) });
        if (q.From.HasValue && q.To.HasValue && q.From.Value.Date > q.To.Value.Date)
            return BadRequest(new { error = this.T("Audit.DateRange") });
        if (q.Actor is { Length: > 200 })
            return BadRequest(new { error = this.T("Audit.UserTooLong") });

        // Owners read any clinic patient's log, Doctors only their own patients'. Missing, foreign and not-permitted look the same.
        var scope = this.GetScope();
        if (!await _audit.CanReadLogAsync(scope, patientId)) return NotFound();
        // Reading the log is itself recorded
        if (!await this.AuditAsync(_audit, patientId, AuditAction.View, AuditItemKind.AuditLog)) return NotFound();

        var result = await _audit.GetLogAsync(scope, patientId, q);
        return result == null ? NotFound() : Ok(result);
    }
}
