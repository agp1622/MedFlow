using System.Net;
using MedFlow.Api.Extensions;
using MedFlow.Core;
using MedFlow.Core.DTOs;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedFlow.Api.Controllers;

[ApiController]
[Route("api/patients/{patientId:int}")]
[Authorize(Roles = Roles.Doctor)]
public class PortalInvitationsController : ControllerBase
{
    private readonly IPatientRepository _patients;
    private readonly IPortalInvitationRepository _invitations;
    private readonly IEmailSender _email;
    private readonly IConfiguration _config;
    private readonly ILogger<PortalInvitationsController> _logger;
    private readonly IAuditService _audit;

    public PortalInvitationsController(IPatientRepository patients, IPortalInvitationRepository invitations,
        IEmailSender email, IConfiguration config, ILogger<PortalInvitationsController> logger, IAuditService audit)
    {
        _audit = audit;
        _patients = patients;
        _invitations = invitations;
        _email = email;
        _config = config;
        _logger = logger;
    }

    [HttpPost("portal-invitation")]
    public async Task<ActionResult<InvitationResultDto>> Invite(int patientId)
    {
        var patient = await _patients.GetWithDetailsAsync(patientId, User.GetUserId());
        if (patient == null) return NotFound();

        if (string.IsNullOrWhiteSpace(patient.Email) ||
            !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(patient.Email))
            return BadRequest(new { errors = new[] { "Patient has no email on file." } });
        if (patient.Status != PatientStatus.Active)
            return BadRequest(new { errors = new[] { "Only active patients can be invited." } });
        if (patient.PortalUserId != null)
            return BadRequest(new { errors = new[] { "Patient already has portal access." } });

        if (!await this.AuditAsync(_audit, patientId, AuditAction.Change, AuditItemKind.PortalAccess, null)) return NotFound();
        var (token, expiresAt) = await _invitations.CreateAsync(patient);

        var frontendUrl = (_config.GetSection("AllowedOrigins").Get<string[]>()?.FirstOrDefault()
            ?? "http://localhost:5173").TrimEnd('/');
        var link = $"{frontendUrl}/accept-invite?token={WebUtility.UrlEncode(token)}&email={WebUtility.UrlEncode(patient.Email)}";

        try
        {
            await _email.SendAsync(patient.Email, "You're invited to the MedFlow patient portal",
                $"<p>Hello {WebUtility.HtmlEncode(patient.FirstName)},</p>" +
                "<p>Your doctor has invited you to the <strong>MedFlow</strong> patient portal, where you can see your " +
                "appointments, prescriptions, invoices and documents they share with you.</p>" +
                $"<p><a href=\"{link}\">Set up my account</a></p>" +
                $"<p>This link expires on {expiresAt:yyyy-MM-dd}. If you weren't expecting this, you can ignore this email.</p>" +
                "<p>— The MedFlow team</p>");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send portal invitation for patient {PatientId}", patientId);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "The invitation could not be emailed. Please try again." });
        }

        _logger.LogInformation("Portal invitation sent for patient {PatientId}", patientId);
        return Ok(new InvitationResultDto("Invitation sent.", expiresAt));
    }

    [HttpDelete("portal-access")]
    public async Task<IActionResult> Revoke(int patientId)
    {
        var patient = await _patients.GetWithDetailsAsync(patientId, User.GetUserId());
        if (patient == null) return NotFound();
        if (!await this.AuditAsync(_audit, patientId, AuditAction.Change, AuditItemKind.PortalAccess, null)) return NotFound();
        await _invitations.RevokeAsync(patient);
        return NoContent();
    }
}
