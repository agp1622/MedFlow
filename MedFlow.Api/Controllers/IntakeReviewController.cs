using MedFlow.Api.Localization;
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
[Authorize(Roles = Roles.Doctor)]
public class IntakeReviewController : ControllerBase
{
    private readonly IPatientRepository _patients;
    private readonly IIntakeRepository _intake;
    private readonly IEmailSender _email;
    private readonly IConfiguration _config;
    private readonly ILogger<IntakeReviewController> _logger;

    public IntakeReviewController(IPatientRepository patients, IIntakeRepository intake, IEmailSender email,
        IConfiguration config, ILogger<IntakeReviewController> logger)
    {
        _patients = patients;
        _intake = intake;
        _email = email;
        _config = config;
        _logger = logger;
    }

    [HttpPost("api/patients/{patientId:int}/intake-link")]
    public async Task<ActionResult<InvitationResultDto>> SendLink(int patientId)
    {
        var patient = await _patients.GetWithDetailsAsync(patientId, User.GetUserId());
        if (patient == null) return NotFound();

        if (string.IsNullOrWhiteSpace(patient.Email) ||
            !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(patient.Email))
            return BadRequest(new { errors = new[] { this.T("Invite.NoEmail") } });
        if (patient.Status != PatientStatus.Active)
            return BadRequest(new { errors = new[] { this.T("IntakeLink.OnlyActive") } });

        var (token, expiresAt) = await _intake.CreateLinkAsync(patient);

        var frontendUrl = (_config.GetSection("AllowedOrigins").Get<string[]>()?.FirstOrDefault()
            ?? "http://localhost:5173").TrimEnd('/');
        var link = $"{frontendUrl}/intake/{token}";

        try
        {
            await _email.SendAsync(patient.Email, "Please complete your intake form",
                $"<p>Hello {WebUtility.HtmlEncode(patient.FirstName)},</p>" +
                "<p>Before your first visit, please fill in your details, medical history and consent form online. It only takes a few minutes.</p>" +
                $"<p><a href=\"{link}\">Complete my intake form</a></p>" +
                $"<p>This link is personal to you and expires on {expiresAt:yyyy-MM-dd}. If you weren't expecting this, you can ignore this email.</p>" +
                "<p>— The MedFlow team</p>");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send intake link for patient {PatientId}", patientId);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = this.T("IntakeLink.EmailFailed") });
        }

        _logger.LogInformation("Intake link sent for patient {PatientId}", patientId);
        return Ok(new InvitationResultDto(this.T("IntakeLink.Sent"), expiresAt));
    }

    [HttpGet("api/intake-submissions")]
    public async Task<ActionResult<PagedResult<IntakeSubmissionSummaryDto>>> List(
        [FromQuery] IntakeStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Ok(await _intake.GetPagedAsync(User.GetUserId(), status, page, pageSize));

    [HttpGet("api/intake-submissions/{id:int}")]
    public async Task<ActionResult<IntakeSubmissionDetailDto>> Get(int id)
    {
        var dto = await _intake.GetDetailAsync(id, User.GetUserId());
        return dto == null ? NotFound() : Ok(dto);
    }

    [HttpPost("api/intake-submissions/{id:int}/accept")]
    public async Task<IActionResult> Accept(int id) => Result(await _intake.DecideAsync(id, User.GetUserId(), true, null));

    [HttpPost("api/intake-submissions/{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] IntakeRejectRequest? req)
    {
        if (req?.Reason is { Length: > 500 })
            return BadRequest(new { errors = new[] { this.T("Error.ReasonMax", 500) } });
        return Result(await _intake.DecideAsync(id, User.GetUserId(), false, req?.Reason));
    }

    private IActionResult Result(DecisionResult r) => r switch
    {
        DecisionResult.Done => NoContent(),
        DecisionResult.AlreadyDecided => Conflict(new { error = this.T("IntakeLink.AlreadyDecided") }),
        _ => NotFound()
    };
}
