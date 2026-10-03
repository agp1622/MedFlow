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
    private NotFoundObjectResult InvalidLink() => NotFound(new { error = "This link is invalid or has expired." });

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
        return StatusCode(StatusCodes.Status201Created, new { message = "Thank you. Your form has been submitted." });
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static List<string> Validate(IntakeSubmitRequest r)
    {
        var e = new List<string>();
        void Req(string? v, string name, int max)
        {
            if (string.IsNullOrWhiteSpace(v)) e.Add($"{name} is required.");
            else if (v.Trim().Length > max) e.Add($"{name} must be at most {max} characters.");
        }
        void Opt(string? v, string name, int max)
        {
            if (v != null && v.Trim().Length > max) e.Add($"{name} must be at most {max} characters.");
        }

        Req(r.FirstName, "First name", 100);
        Req(r.LastName, "Last name", 100);
        Req(r.Phone, "Phone", 30);
        if (r.DateOfBirth == null) e.Add("Date of birth is required.");
        else if (r.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow) || r.DateOfBirth < new DateOnly(1900, 1, 1))
            e.Add("Date of birth is not valid.");
        if (r.Gender == null) e.Add("Gender is required.");
        Opt(r.Address, "Address", 200); Opt(r.City, "City", 100); Opt(r.State, "State", 100); Opt(r.ZipCode, "ZIP code", 20);
        Opt(r.InsuranceProvider, "Insurance provider", 200); Opt(r.InsurancePolicyNumber, "Policy number", 100);
        Opt(r.PrimaryCondition, "Primary condition", 500); Opt(r.Allergies, "Allergies", 1000);
        Opt(r.CurrentMedications, "Current medications", 2000); Opt(r.PastHistory, "Past history", 2000);
        Opt(r.AdditionalNotes, "Additional notes", 2000);
        if (!r.ConsentAgreed) e.Add("You must agree to the consent statement.");
        Req(r.SignatureName, "Signature", 200);
        return e;
    }
}
