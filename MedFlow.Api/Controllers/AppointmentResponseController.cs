using MedFlow.Api.Localization;
using MedFlow.Core.DTOs;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MedFlow.Api.Controllers;

/// <summary>Anonymous, token-only endpoints used from the link in a reminder email.</summary>
[ApiController]
[Route("api/appointment-response")]
[AllowAnonymous]
[EnableRateLimiting("appointment-response")]
public class AppointmentResponseController : ControllerBase
{
    private readonly IReminderRepository _reminders;

    public AppointmentResponseController(IReminderRepository reminders) => _reminders = reminders;

    [HttpPost("lookup")]
    public async Task<ActionResult<ReminderLookupDto>> Lookup([FromBody] ReminderTokenRequest req)
    {
        var dto = await _reminders.LookupAsync(req.Token);
        return dto == null ? NotFound(new { error = this.T("Response.Invalid") }) : Ok(dto);
    }

    [HttpPost("respond")]
    public async Task<ActionResult<ReminderLookupDto>> Respond([FromBody] ReminderRespondRequest req)
    {
        var (result, dto) = await _reminders.RespondAsync(req.Token, req.Action);
        return result switch
        {
            ReminderRespondResult.Ok => Ok(dto),
            ReminderRespondResult.Closed => Conflict(new { error = this.T("Response.Closed") }),
            _ => NotFound(new { error = this.T("Response.Invalid") })
        };
    }
}
