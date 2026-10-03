using System.Net;
using MedFlow.Api.Authorization;
using MedFlow.Api.Localization;
using MedFlow.Core;
using MedFlow.Core.DTOs;
using MedFlow.Core.Interfaces;
using MedFlow.Infrastructure.Clinics;
using Microsoft.AspNetCore.Mvc;

namespace MedFlow.Api.Controllers;

/// <summary>Staff management for the caller's clinic. Owners only.</summary>
[ApiController]
[Route("api/staff")]
[HasPermission(Permission.StaffManage)]
public class StaffController : ControllerBase
{
    private readonly IClinicService _clinics;
    private readonly IEmailSender _email;
    private readonly IConfiguration _config;
    private readonly ILogger<StaffController> _logger;

    public StaffController(IClinicService clinics, IEmailSender email, IConfiguration config, ILogger<StaffController> logger)
    {
        _clinics = clinics;
        _email = email;
        _config = config;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<StaffMemberDto>>> List([FromQuery] QueryParams q) =>
        Ok(await _clinics.ListStaffAsync(this.GetScope(), q));

    [HttpGet("invitations")]
    public async Task<ActionResult<PagedResult<StaffInvitationDto>>> ListInvitations([FromQuery] QueryParams q) =>
        Ok(await _clinics.ListInvitationsAsync(this.GetScope(), q));

    /// <summary>
    /// The response is identical whether or not the address could be invited (an address that already has an
    /// account gets nothing), so the endpoint cannot be used to find out which emails are registered.
    /// </summary>
    [HttpPost("invitations")]
    public async Task<ActionResult<InvitationResultDto>> Invite([FromBody] InviteStaffRequest req)
    {
        var role = req.Role!.Value;
        if (role == ClinicRole.Owner || !Enum.IsDefined(role))
            return BadRequest(new { errors = new[] { this.T("Staff.InvalidRole") } });

        var scope = this.GetScope();
        var created = await _clinics.InviteAsync(scope, req.Email, role);
        var uniform = new InvitationResultDto(this.T("Staff.Invited"), DateTime.UtcNow.Add(ClinicService.InvitationLifetime));
        if (created == null)
        {
            _logger.LogInformation("Staff invitation skipped for clinic {ClinicId}: address not invitable", scope.ClinicId);
            return Ok(uniform);
        }

        var (token, expiresAt) = created.Value;
        var frontendUrl = (_config.GetSection("AllowedOrigins").Get<string[]>()?.FirstOrDefault()
            ?? "http://localhost:5173").TrimEnd('/');
        var link = $"{frontendUrl}/accept-staff-invite?token={WebUtility.UrlEncode(token)}&email={WebUtility.UrlEncode(req.Email.Trim())}";
        var clinicName = (await _clinics.GetClinicAsync(scope))?.Name ?? "MedFlow";
        try
        {
            await _email.SendAsync(req.Email.Trim(), "You're invited to join a clinic on MedFlow",
                "<p>Hello,</p>" +
                $"<p>You have been invited to join <strong>{WebUtility.HtmlEncode(clinicName)}</strong> on <strong>MedFlow</strong> as {role}.</p>" +
                $"<p><a href=\"{link}\">Set up my account</a></p>" +
                $"<p>This link expires on {expiresAt:yyyy-MM-dd}. If you weren't expecting this, you can ignore this email.</p>" +
                "<p>— The MedFlow team</p>");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send staff invitation for clinic {ClinicId}", scope.ClinicId);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = this.T("Staff.EmailFailed") });
        }

        _logger.LogInformation("Staff invitation sent for clinic {ClinicId} as {Role}", scope.ClinicId, role);
        return Ok(new InvitationResultDto(uniform.Message, expiresAt));
    }

    [HttpDelete("invitations/{id:int}")]
    public async Task<IActionResult> Revoke(int id) =>
        await _clinics.RevokeInvitationAsync(this.GetScope(), id) ? NoContent() : NotFound();

    [HttpPut("{id:int}/role")]
    public async Task<ActionResult<StaffMemberDto>> ChangeRole(int id, [FromBody] ChangeRoleRequest req)
    {
        var role = req.Role!.Value;
        if (!Enum.IsDefined(role)) return BadRequest(new { errors = new[] { this.T("Staff.InvalidRole") } });
        return Result(await _clinics.ChangeRoleAsync(this.GetScope(), id, role));
    }

    [HttpPost("{id:int}/deactivate")]
    public async Task<ActionResult<StaffMemberDto>> Deactivate(int id) =>
        Result(await _clinics.SetActiveAsync(this.GetScope(), id, false));

    [HttpPost("{id:int}/reactivate")]
    public async Task<ActionResult<StaffMemberDto>> Reactivate(int id) =>
        Result(await _clinics.SetActiveAsync(this.GetScope(), id, true));

    private ActionResult<StaffMemberDto> Result((StaffChangeOutcome Outcome, StaffMemberDto? Member) r) => r.Outcome switch
    {
        StaffChangeOutcome.Done => Ok(r.Member),
        StaffChangeOutcome.LastOwner => Conflict(new { error = this.T("Staff.LastOwner") }),
        _ => NotFound()
    };
}
