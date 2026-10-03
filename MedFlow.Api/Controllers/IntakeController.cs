using MedFlow.Api.Localization;
using MedFlow.Core;
using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MedFlow.Core.Interfaces;

namespace MedFlow.Api.Controllers;

/// <summary>Public (token-gated) intake form. The patient is identified only by the emailed link.</summary>
[ApiController]
[Route("api/intake/{token}")]
[AllowAnonymous]
[EnableRateLimiting("intake-public")]
public class IntakeController : ControllerBase
{
    private readonly IIntakeRepository _intake;
    private readonly ILogger<IntakeController> _logger;

    public IntakeController(IIntakeRepository intake, ILogger<IntakeController> logger)
    {
        _intake = intake;
        _logger = logger;
    }

    // Every failure of the link (unknown, expired, used, superseded, inactive patient) looks the same.
    private NotFoundObjectResult InvalidLink() => NotFound(new { error = this.T("Intake.InvalidLink") });

    [HttpGet]
    public async Task<ActionResult<IntakeFormInfoDto>> GetForm(string token)
    {
        var found = await _intake.FindValidLinkAsync(token);
        if (found == null) return InvalidLink();
        return Ok(new IntakeFormInfoDto(found.Value.Patient.FirstName, IntakeConsent.Version, IntakeConsent.Text));
    }

    [HttpPost]
    public async Task<IActionResult> Submit(string token, [FromBody] IntakeSubmitRequest req)
    {
        var found = await _intake.FindValidLinkAsync(token);
        if (found == null) return InvalidLink();

        var errors = Validate(req);
        if (errors.Count > 0) return BadRequest(new { errors });

        var (link, patient) = found.Value;
        var submission = new IntakeSubmission
        {
            FirstName = req.FirstName!.Trim(),
            LastName = req.LastName!.Trim(),
            DateOfBirth = req.DateOfBirth!.Value,
            Gender = req.Gender!.Value,
            Phone = req.Phone!.Trim(),
            Address = Clean(req.Address), City = Clean(req.City), State = Clean(req.State), ZipCode = Clean(req.ZipCode),
            InsuranceProvider = Clean(req.InsuranceProvider), InsurancePolicyNumber = Clean(req.InsurancePolicyNumber),
            PrimaryCondition = Clean(req.PrimaryCondition), Allergies = Clean(req.Allergies),
            CurrentMedications = Clean(req.CurrentMedications), PastHistory = Clean(req.PastHistory),
            AdditionalNotes = Clean(req.AdditionalNotes),
            ConsentAgreed = true,
            SignatureName = req.SignatureName!.Trim()
        };

        if (!await _intake.SubmitAsync(link, patient, submission)) return InvalidLink();

        _logger.LogInformation("Intake form submitted for patient {PatientId}", patient.Id);
        return StatusCode(StatusCodes.Status201Created, new { message = this.T("Intake.Thanks") });
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private List<string> Validate(IntakeSubmitRequest r)
    {
        var e = new List<string>();
        void Req(string? v, string name, int max)
        {
            if (string.IsNullOrWhiteSpace(v)) e.Add(this.T("Intake.Required", this.T(name)));
            else if (v.Trim().Length > max) e.Add(this.T("Intake.MaxLength", this.T(name), max));
        }
        void Opt(string? v, string name, int max)
        {
            if (v != null && v.Trim().Length > max) e.Add(this.T("Intake.MaxLength", this.T(name), max));
        }

        Req(r.FirstName, "Intake.Field.FirstName", 100);
        Req(r.LastName, "Intake.Field.LastName", 100);
        Req(r.Phone, "Intake.Field.Phone", 30);
        if (r.DateOfBirth == null) e.Add(this.T("Intake.DobRequired"));
        else if (r.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow) || r.DateOfBirth < new DateOnly(1900, 1, 1))
            e.Add(this.T("Intake.DobInvalid"));
        if (r.Gender == null) e.Add(this.T("Intake.GenderRequired"));
        Opt(r.Address, "Intake.Field.Address", 200); Opt(r.City, "Intake.Field.City", 100); Opt(r.State, "Intake.Field.State", 100); Opt(r.ZipCode, "Intake.Field.ZipCode", 20);
        Opt(r.InsuranceProvider, "Intake.Field.InsuranceProvider", 200); Opt(r.InsurancePolicyNumber, "Intake.Field.InsurancePolicyNumber", 100);
        Opt(r.PrimaryCondition, "Intake.Field.PrimaryCondition", 500); Opt(r.Allergies, "Intake.Field.Allergies", 1000);
        Opt(r.CurrentMedications, "Intake.Field.CurrentMedications", 2000); Opt(r.PastHistory, "Intake.Field.PastHistory", 2000);
        Opt(r.AdditionalNotes, "Intake.Field.AdditionalNotes", 2000);
        if (!r.ConsentAgreed) e.Add(this.T("Intake.ConsentRequired"));
        Req(r.SignatureName, "Intake.Field.Signature", 200);
        return e;
    }
}
