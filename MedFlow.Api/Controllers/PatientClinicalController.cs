using MedFlow.Api.Extensions;
using MedFlow.Core;
using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;
using MedFlow.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedFlow.Api.Controllers;

/// <summary>Structured allergies, problems (ICD-10) and current medications. Doctor-facing, owner-scoped.</summary>
[ApiController]
[Route("api/patients/{patientId:int}/clinical")]
[Authorize(Roles = Roles.Doctor)]
public class PatientClinicalController : ControllerBase
{
    private const int MaxPerList = 100;
    private readonly IPatientClinicalRepository _clinical;

    public PatientClinicalController(IPatientClinicalRepository clinical) => _clinical = clinical;

    private string DoctorId => User.GetUserId();
    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    [HttpGet]
    public async Task<ActionResult<ClinicalSummaryDto>> GetSummary(int patientId)
    {
        if (!await _clinical.OwnsPatientAsync(patientId, DoctorId)) return NotFound();
        return Ok(await _clinical.GetSummaryAsync(patientId, DoctorId));
    }

    // ── Allergies ─────────────────────────────────────────────────────────────
    [HttpPost("allergies")]
    public async Task<ActionResult<AllergyDto>> AddAllergy(int patientId, [FromBody] SaveAllergyRequest req)
    {
        if (!await _clinical.OwnsPatientAsync(patientId, DoctorId)) return NotFound();
        if (await _clinical.CountAsync<PatientAllergy>(patientId, DoctorId) >= MaxPerList) return ListFull("allergies");
        var substance = req.Substance.Trim();
        if (await _clinical.AllergyExistsAsync(patientId, DoctorId, substance)) return Conflict("This allergy is already recorded.");

        var created = await _clinical.AddAsync(new PatientAllergy
        {
            PatientId = patientId, DoctorId = DoctorId, Substance = substance,
            Reaction = Clean(req.Reaction), Severity = req.Severity!.Value
        });
        return Created(string.Empty, ToDto(created));
    }

    [HttpPut("allergies/{id:int}")]
    public async Task<ActionResult<AllergyDto>> UpdateAllergy(int patientId, int id, [FromBody] SaveAllergyRequest req)
    {
        var entry = await _clinical.GetOwnedAsync<PatientAllergy>(id, patientId, DoctorId);
        if (entry == null) return NotFound();
        var substance = req.Substance.Trim();
        if (await _clinical.AllergyExistsAsync(patientId, DoctorId, substance, id)) return Conflict("This allergy is already recorded.");

        entry.Substance = substance;
        entry.Reaction = Clean(req.Reaction);
        entry.Severity = req.Severity!.Value;
        await _clinical.UpdateAsync(entry);
        return Ok(ToDto(entry));
    }

    [HttpDelete("allergies/{id:int}")]
    public Task<IActionResult> DeleteAllergy(int patientId, int id) => DeleteAsync<PatientAllergy>(patientId, id);

    // ── Problems ──────────────────────────────────────────────────────────────
    [HttpPost("problems")]
    public async Task<ActionResult<ProblemDto>> AddProblem(int patientId, [FromBody] SaveProblemRequest req)
    {
        if (!await _clinical.OwnsPatientAsync(patientId, DoctorId)) return NotFound();
        if (OnsetInFuture(req.OnsetDate)) return OnsetError();
        if (await _clinical.CountAsync<PatientProblem>(patientId, DoctorId) >= MaxPerList) return ListFull("problems");

        var created = await _clinical.AddAsync(new PatientProblem
        {
            PatientId = patientId, DoctorId = DoctorId, Description = req.Description.Trim(),
            Icd10Code = req.Icd10Code.Trim().ToUpperInvariant(), Status = req.Status!.Value, OnsetDate = req.OnsetDate
        });
        return Created(string.Empty, ToDto(created));
    }

    [HttpPut("problems/{id:int}")]
    public async Task<ActionResult<ProblemDto>> UpdateProblem(int patientId, int id, [FromBody] SaveProblemRequest req)
    {
        var entry = await _clinical.GetOwnedAsync<PatientProblem>(id, patientId, DoctorId);
        if (entry == null) return NotFound();
        if (OnsetInFuture(req.OnsetDate)) return OnsetError();

        entry.Description = req.Description.Trim();
        entry.Icd10Code = req.Icd10Code.Trim().ToUpperInvariant();
        entry.Status = req.Status!.Value;
        entry.OnsetDate = req.OnsetDate;
        await _clinical.UpdateAsync(entry);
        return Ok(ToDto(entry));
    }

    [HttpDelete("problems/{id:int}")]
    public Task<IActionResult> DeleteProblem(int patientId, int id) => DeleteAsync<PatientProblem>(patientId, id);

    // ── Medications ───────────────────────────────────────────────────────────
    [HttpPost("medications")]
    public async Task<ActionResult<MedicationDto>> AddMedication(int patientId, [FromBody] SaveMedicationRequest req)
    {
        if (!await _clinical.OwnsPatientAsync(patientId, DoctorId)) return NotFound();
        if (await _clinical.CountAsync<PatientMedication>(patientId, DoctorId) >= MaxPerList) return ListFull("medications");

        var created = await _clinical.AddAsync(new PatientMedication
        {
            PatientId = patientId, DoctorId = DoctorId, Name = req.Name.Trim(),
            Dosage = Clean(req.Dosage), Frequency = Clean(req.Frequency), Notes = Clean(req.Notes)
        });
        return Created(string.Empty, ToDto(created));
    }

    [HttpPut("medications/{id:int}")]
    public async Task<ActionResult<MedicationDto>> UpdateMedication(int patientId, int id, [FromBody] SaveMedicationRequest req)
    {
        var entry = await _clinical.GetOwnedAsync<PatientMedication>(id, patientId, DoctorId);
        if (entry == null) return NotFound();

        entry.Name = req.Name.Trim();
        entry.Dosage = Clean(req.Dosage);
        entry.Frequency = Clean(req.Frequency);
        entry.Notes = Clean(req.Notes);
        await _clinical.UpdateAsync(entry);
        return Ok(ToDto(entry));
    }

    [HttpDelete("medications/{id:int}")]
    public Task<IActionResult> DeleteMedication(int patientId, int id) => DeleteAsync<PatientMedication>(patientId, id);

    // ── Helpers ───────────────────────────────────────────────────────────────
    private async Task<IActionResult> DeleteAsync<T>(int patientId, int id) where T : ClinicalEntry
    {
        var entry = await _clinical.GetOwnedAsync<T>(id, patientId, DoctorId);
        if (entry == null) return NotFound();
        await _clinical.DeleteAsync(entry);
        return NoContent();
    }

    private static bool OnsetInFuture(DateOnly? d) => d.HasValue && d.Value > DateOnly.FromDateTime(DateTime.UtcNow);
    private BadRequestObjectResult OnsetError() => BadRequest("Onset date cannot be in the future.");
    private BadRequestObjectResult ListFull(string what) => BadRequest($"A patient can have at most {MaxPerList} {what}.");

    private static AllergyDto ToDto(PatientAllergy a) => new(a.Id, a.Substance, a.Reaction, a.Severity.ToString(), a.CreatedAt, a.UpdatedAt);
    private static ProblemDto ToDto(PatientProblem p) => new(p.Id, p.Description, p.Icd10Code, p.Status.ToString(), p.OnsetDate, p.CreatedAt, p.UpdatedAt);
    private static MedicationDto ToDto(PatientMedication m) => new(m.Id, m.Name, m.Dosage, m.Frequency, m.Notes, m.CreatedAt, m.UpdatedAt);
}
