using MedFlow.Api.Extensions;
using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PatientsController : ControllerBase
{
    private readonly IPatientRepository _patients;

    public PatientsController(IPatientRepository patients) => _patients = patients;

    [HttpGet]
    public async Task<ActionResult<PagedResult<PatientSummaryDto>>> GetAll([FromQuery] QueryParams q)
    {
        var doctorId = User.GetUserId();
        return Ok(await _patients.GetPagedAsync(doctorId, q));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PatientDto>> GetById(int id)
    {
        var doctorId = User.GetUserId();
        var patient = await _patients.GetWithDetailsAsync(id, doctorId);
        if (patient == null) return NotFound();

        var lastVisit = await _patients.GetLastVisitAsync(id);
        var nextAppt = await _patients.GetNextAppointmentAsync(id);

        return Ok(MapToDto(patient, lastVisit, nextAppt));
    }

    [HttpPost]
    public async Task<ActionResult<PatientDto>> Create([FromBody] CreatePatientRequest req)
    {
        var doctorId = User.GetUserId();
        var patient = new Patient
        {
            FirstName = req.FirstName,
            LastName = req.LastName,
            DateOfBirth = req.DateOfBirth,
            Gender = req.Gender,
            BloodType = req.BloodType,
            Email = req.Email,
            Phone = req.Phone,
            Address = req.Address,
            City = req.City,
            State = req.State,
            ZipCode = req.ZipCode,
            PrimaryCondition = req.PrimaryCondition,
            Allergies = req.Allergies,
            Notes = req.Notes,
            InsuranceProvider = req.InsuranceProvider,
            InsurancePolicyNumber = req.InsurancePolicyNumber,
            DoctorId = doctorId
        };

        var created = await _patients.AddAsync(patient);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToDto(created, null, null));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PatientDto>> Update(int id, [FromBody] UpdatePatientRequest req)
    {
        var doctorId = User.GetUserId();
        var patient = await _patients.GetWithDetailsAsync(id, doctorId);
        if (patient == null) return NotFound();

        patient.FirstName = req.FirstName;
        patient.LastName = req.LastName;
        patient.DateOfBirth = req.DateOfBirth;
        patient.Gender = req.Gender;
        patient.BloodType = req.BloodType;
        patient.Status = req.Status;
        patient.Email = req.Email;
        patient.Phone = req.Phone;
        patient.Address = req.Address;
        patient.City = req.City;
        patient.State = req.State;
        patient.ZipCode = req.ZipCode;
        patient.PrimaryCondition = req.PrimaryCondition;
        patient.Allergies = req.Allergies;
        patient.Notes = req.Notes;
        patient.InsuranceProvider = req.InsuranceProvider;
        patient.InsurancePolicyNumber = req.InsurancePolicyNumber;

        await _patients.UpdateAsync(patient);
        return Ok(MapToDto(patient, null, null));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var doctorId = User.GetUserId();
        var patient = await _patients.GetWithDetailsAsync(id, doctorId);
        if (patient == null) return NotFound();
        await _patients.DeleteAsync(id);
        return NoContent();
    }

    private static PatientDto MapToDto(Patient p, DateTime? lastVisit, DateTime? nextAppt) => new(
        p.Id, p.FirstName, p.LastName, p.FullName,
        p.DateOfBirth, p.Age, p.Gender.ToString(), p.BloodType.ToString(),
        p.Status.ToString(), p.Email, p.Phone,
        p.Address, p.City, p.State, p.ZipCode,
        p.PrimaryCondition, p.Allergies, p.Notes,
        p.InsuranceProvider, p.InsurancePolicyNumber,
        lastVisit, nextAppt, p.CreatedAt, p.UpdatedAt);
}
