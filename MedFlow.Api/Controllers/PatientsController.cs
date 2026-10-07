using MedFlow.Core;
using MedFlow.Api.Authorization;
using MedFlow.Api.Extensions;
using MedFlow.Api.Localization;
using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[HasPermission(Permission.ClinicRead)]
public class PatientsController : ControllerBase
{
    private readonly IPatientRepository _patients;
    private readonly IPortalInvitationRepository _invitations;
    private readonly IAuditService _audit;
    private readonly IClinicService _clinics;

    public PatientsController(IPatientRepository patients, IPortalInvitationRepository invitations, IAuditService audit,
        IClinicService clinics)
    {
        _patients = patients;
        _invitations = invitations;
        _audit = audit;
        _clinics = clinics;
    }

    [HttpGet]
    [HasPermission(Permission.PatientsRead)]
    public async Task<ActionResult<PagedResult<PatientSummaryDto>>> GetAll([FromQuery] QueryParams q)
        => Ok(await _patients.GetPagedAsync(this.GetScope(), q));

    [HttpGet("{id:int}")]
    [HasPermission(Permission.PatientsRead)]
    public async Task<ActionResult<PatientDto>> GetById(int id)
    {
        var scope = this.GetScope();
        var patient = await _patients.GetWithDetailsAsync(id, scope);
        if (patient == null) return NotFound();
        if (!await this.AuditAsync(_audit, id, AuditAction.View, AuditItemKind.Patient, id)) return NotFound();

        var lastVisit = await _patients.GetLastVisitAsync(id);
        var nextAppt = await _patients.GetNextAppointmentAsync(id);

        return Ok(MapToDto(patient, lastVisit, nextAppt, scope.Has(Permission.PatientClinicalFields), await _invitations.GetPortalStatusAsync(patient)));
    }

    [HttpPost]
    [HasPermission(Permission.PatientsWrite)]
    public async Task<ActionResult<PatientDto>> Create([FromBody] CreatePatientRequest req)
    {
        var invalid = ValidateInsurance(req.InsuranceGroupNumber, req.InsurancePayerId, req.InsuranceSubscriberName,
            req.InsuranceSubscriberDateOfBirth, req.InsuranceSubscriberRelationship);
        if (invalid != null) return invalid;
        var scope = this.GetScope();
        // A doctor of another clinic (or a non-doctor) looks exactly like a missing doctor
        var doctorId = await _clinics.ResolveTreatingDoctorAsync(scope, req.DoctorId);
        if (doctorId == null) return NotFound();
        var clinical = scope.Has(Permission.PatientClinicalFields);
        var patient = new Patient
        {
            FirstName = req.FirstName,
            LastName = req.LastName,
            DateOfBirth = req.DateOfBirth,
            Gender = req.Gender,
            BloodType = clinical ? req.BloodType : BloodType.Unknown,
            Email = req.Email,
            Phone = req.Phone,
            Address = req.Address,
            City = req.City,
            State = req.State,
            ZipCode = req.ZipCode,
            PrimaryCondition = clinical ? req.PrimaryCondition : null,
            Allergies = clinical ? req.Allergies : null,
            Notes = clinical ? req.Notes : null,
            InsuranceProvider = req.InsuranceProvider,
            InsurancePolicyNumber = req.InsurancePolicyNumber,
            InsuranceGroupNumber = Clean(req.InsuranceGroupNumber),
            InsurancePayerId = Clean(req.InsurancePayerId),
            InsuranceSubscriberName = Clean(req.InsuranceSubscriberName),
            InsuranceSubscriberDateOfBirth = req.InsuranceSubscriberDateOfBirth,
            InsuranceSubscriberRelationship = req.InsuranceSubscriberRelationship,
            DoctorId = doctorId,
            ClinicId = scope.ClinicId
        };

        var created = await _patients.AddAsync(patient);
        // The patient does not exist before this point, so this one event is written right after creation
        await this.AuditAsync(_audit, created.Id, AuditAction.Change, AuditItemKind.Patient, created.Id);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToDto(created, null, null, clinical));
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permission.PatientsWrite)]
    public async Task<ActionResult<PatientDto>> Update(int id, [FromBody] UpdatePatientRequest req)
    {
        var invalid = ValidateInsurance(req.InsuranceGroupNumber, req.InsurancePayerId, req.InsuranceSubscriberName,
            req.InsuranceSubscriberDateOfBirth, req.InsuranceSubscriberRelationship);
        if (invalid != null) return invalid;
        var scope = this.GetScope();
        var clinical = scope.Has(Permission.PatientClinicalFields);
        var patient = await _patients.GetWithDetailsAsync(id, scope);
        if (patient == null) return NotFound();

        var before = AuditDiff.Snapshot(patient);
        patient.FirstName = req.FirstName;
        patient.LastName = req.LastName;
        patient.DateOfBirth = req.DateOfBirth;
        patient.Gender = req.Gender;
        // Clinical fields are only touched by roles allowed to see them; others keep the stored values
        if (clinical)
        {
            patient.BloodType = req.BloodType;
            patient.PrimaryCondition = req.PrimaryCondition;
            patient.Allergies = req.Allergies;
            patient.Notes = req.Notes;
        }
        patient.Status = req.Status;
        patient.Email = req.Email;
        patient.Phone = req.Phone;
        patient.Address = req.Address;
        patient.City = req.City;
        patient.State = req.State;
        patient.ZipCode = req.ZipCode;
        patient.InsuranceProvider = req.InsuranceProvider;
        patient.InsurancePolicyNumber = req.InsurancePolicyNumber;
        patient.InsuranceGroupNumber = Clean(req.InsuranceGroupNumber);
        patient.InsurancePayerId = Clean(req.InsurancePayerId);
        patient.InsuranceSubscriberName = Clean(req.InsuranceSubscriberName);
        patient.InsuranceSubscriberDateOfBirth = req.InsuranceSubscriberDateOfBirth;
        patient.InsuranceSubscriberRelationship = req.InsuranceSubscriberRelationship;

        // Recording saves the in-memory edit together with the event; if it cannot be stored nothing is saved
        if (!await this.AuditAsync(_audit, id, AuditAction.Change, AuditItemKind.Patient, id, AuditDiff.Changed(before, patient)))
            return NotFound();
        await _patients.UpdateAsync(patient);
        return Ok(MapToDto(patient, null, null, clinical, await _invitations.GetPortalStatusAsync(patient)));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permission.PatientsDelete)]
    public async Task<IActionResult> Delete(int id)
    {
        var patient = await _patients.GetWithDetailsAsync(id, this.GetScope());
        if (patient == null) return NotFound();
        if (!await this.AuditAsync(_audit, id, AuditAction.Change, AuditItemKind.Patient, id)) return NotFound();
        await _patients.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>Roles without clinical access get empty primary condition, allergies and notes and Unknown blood type.</summary>
    private static PatientDto MapToDto(Patient p, DateTime? lastVisit, DateTime? nextAppt, bool clinical, string portalStatus = "NotInvited") => new(
        p.Id, p.FirstName, p.LastName, p.FullName,
        p.DateOfBirth, p.Age, p.Gender.ToString(), clinical ? p.BloodType.ToString() : nameof(BloodType.Unknown),
        p.Status.ToString(), p.Email, p.Phone,
        p.Address, p.City, p.State, p.ZipCode,
        clinical ? p.PrimaryCondition : null, clinical ? p.Allergies : null, clinical ? p.Notes : null,
        p.InsuranceProvider, p.InsurancePolicyNumber,
        lastVisit, nextAppt, p.CreatedAt, p.UpdatedAt, portalStatus,
        p.InsuranceGroupNumber, p.InsurancePayerId, p.InsuranceSubscriberName,
        p.InsuranceSubscriberDateOfBirth, p.InsuranceSubscriberRelationship?.ToString());

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Localized 400 when any new insurance field breaks its limit; null when valid.</summary>
    private BadRequestObjectResult? ValidateInsurance(string? group, string? payerId, string? subscriber,
        DateOnly? subscriberDob, InsuranceRelationship? relationship)
    {
        var errors = new List<string>();
        void Max(string? v, string key, int max)
        {
            if (v != null && v.Trim().Length > max) errors.Add(this.T("Patient.Insurance.MaxLength", this.T(key), max));
        }
        Max(group, "Patient.Insurance.Field.Group", 100);
        Max(payerId, "Patient.Insurance.Field.PayerId", 50);
        Max(subscriber, "Patient.Insurance.Field.Subscriber", 200);
        if (subscriberDob != null && (subscriberDob > DateOnly.FromDateTime(DateTime.UtcNow) || subscriberDob < new DateOnly(1900, 1, 1)))
            errors.Add(this.T("Patient.Insurance.SubscriberDobInvalid"));
        if (relationship != null && !Enum.IsDefined(relationship.Value))
            errors.Add(this.T("Patient.Insurance.RelationshipInvalid"));
        if (errors.Count == 0) return null;
        return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { ["insurance"] = errors.ToArray() })
        {
            Status = StatusCodes.Status400BadRequest,
            Title = this.T("Error.Validation"),
            Instance = Request.Path
        });
    }
}
