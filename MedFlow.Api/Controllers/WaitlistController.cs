using MedFlow.Api.Authorization;
using MedFlow.Api.Extensions;
using MedFlow.Api.Localization;
using MedFlow.Core;
using MedFlow.Core.DTOs;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedFlow.Api.Controllers;

/// <summary>The doctor's waitlist (staff act through the doctor's account). Always scoped to the caller.</summary>
[ApiController]
[Route("api/[controller]")]
[HasPermission(Permission.WaitlistManage)]
public class WaitlistController : ControllerBase
{
    private readonly IWaitlistRepository _waitlist;
    private readonly IAuditService _audit;

    public WaitlistController(IWaitlistRepository waitlist, IAuditService audit)
    {
        _waitlist = waitlist;
        _audit = audit;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<WaitlistEntryDto>>> GetAll([FromQuery] QueryParams q) =>
        Ok(await _waitlist.GetPagedAsync(this.GetScope(), q));

    [HttpPost]
    public async Task<ActionResult<WaitlistEntryDto>> Add([FromBody] AddWaitlistEntryRequest req)
    {
        var (outcome, entry) = await _waitlist.AddAsync(req.PatientId, this.GetScope());
        switch (outcome)
        {
            case WaitlistAddOutcome.PatientNotFound:
                return NotFound();
            case WaitlistAddOutcome.NotActive:
                return BadRequest(new { error = this.T("Waitlist.NotActive") });
            case WaitlistAddOutcome.AlreadyWaiting:
                return Conflict(new { error = this.T("Waitlist.AlreadyWaiting") });
        }
        if (!await this.AuditAsync(_audit, req.PatientId, AuditAction.Change, AuditItemKind.Waitlist, entry!.Id)) return NotFound();
        return Created($"/api/waitlist/{entry.Id}", entry);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remove(int id)
    {
        var patientId = await _waitlist.RemoveAsync(id, this.GetScope());
        if (patientId == null) return NotFound();
        await this.AuditAsync(_audit, patientId.Value, AuditAction.Change, AuditItemKind.Waitlist, id);
        return NoContent();
    }
}
