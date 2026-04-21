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
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentRepository _appointments;

    public AppointmentsController(IAppointmentRepository appointments) => _appointments = appointments;

    [HttpGet]
    public async Task<ActionResult<PagedResult<AppointmentDto>>> GetAll([FromQuery] QueryParams q)
        => Ok(await _appointments.GetPagedAsync(User.GetUserId(), q));

    [HttpGet("today")]
    public async Task<ActionResult<IEnumerable<AppointmentDto>>> GetToday()
        => Ok(await _appointments.GetTodayAsync(User.GetUserId()));

    [HttpGet("upcoming")]
    public async Task<ActionResult<IEnumerable<AppointmentDto>>> GetUpcoming([FromQuery] int count = 5)
        => Ok(await _appointments.GetUpcomingAsync(User.GetUserId(), count));

    [HttpGet("patient/{patientId:int}")]
    public async Task<ActionResult<IEnumerable<AppointmentDto>>> GetByPatient(int patientId)
        => Ok(await _appointments.GetByPatientAsync(patientId, User.GetUserId()));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AppointmentDto>> GetById(int id)
    {
        var appt = await _appointments.GetByIdAsync(id);
        if (appt == null) return NotFound();
        return Ok(appt);
    }

    [HttpPost]
    public async Task<ActionResult<AppointmentDto>> Create([FromBody] CreateAppointmentRequest req)
    {
        var doctorId = User.GetUserId();
        var appt = new Appointment
        {
            PatientId = req.PatientId,
            DoctorId = doctorId,
            ScheduledAt = req.ScheduledAt,
            DurationMinutes = req.DurationMinutes,
            Type = req.Type,
            Status = AppointmentStatus.Pending,
            Reason = req.Reason,
            Location = req.Location
        };
        var created = await _appointments.AddAsync(appt);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAppointmentRequest req)
    {
        var appt = await _appointments.GetByIdAsync(id);
        if (appt == null) return NotFound();

        appt.ScheduledAt = req.ScheduledAt;
        appt.DurationMinutes = req.DurationMinutes;
        appt.Type = req.Type;
        appt.Status = req.Status;
        appt.Reason = req.Reason;
        appt.Notes = req.Notes;
        appt.Location = req.Location;

        await _appointments.UpdateAsync(appt);
        return NoContent();
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] AppointmentStatus status)
    {
        var appt = await _appointments.GetByIdAsync(id);
        if (appt == null) return NotFound();
        appt.Status = status;
        await _appointments.UpdateAsync(appt);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _appointments.ExistsAsync(id)) return NotFound();
        await _appointments.DeleteAsync(id);
        return NoContent();
    }
}
