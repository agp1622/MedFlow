using MedFlow.Core;
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
[Authorize(Roles = Roles.Doctor)]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentRepository _appointments;
    private readonly IAuditService _audit;

    public AppointmentsController(IAppointmentRepository appointments, IAuditService audit)
    {
        _appointments = appointments;
        _audit = audit;
    }

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
    {
        if (!await this.AuditAsync(_audit, patientId, AuditAction.View, AuditItemKind.Appointment, null)) return NotFound();
        return Ok(await _appointments.GetByPatientAsync(patientId, User.GetUserId()));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AppointmentDto>> GetById(int id)
    {
        var appt = await _appointments.GetByIdAsync(id);
        if (appt == null) return NotFound();
        if (!await this.AuditAsync(_audit, appt.PatientId, AuditAction.View, AuditItemKind.Appointment, appt.Id)) return NotFound();
        return Ok(appt);
    }

    [HttpPost]
    public async Task<ActionResult<AppointmentDto>> Create([FromBody] CreateAppointmentRequest req)
    {
        var doctorId = User.GetUserId();
        if (!await this.AuditAsync(_audit, req.PatientId, AuditAction.Change, AuditItemKind.Appointment, null)) return NotFound();
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

        var before = AuditDiff.Snapshot(appt);
        appt.ScheduledAt = req.ScheduledAt;
        appt.DurationMinutes = req.DurationMinutes;
        appt.Type = req.Type;
        appt.Status = req.Status;
        appt.Reason = req.Reason;
        appt.Notes = req.Notes;
        appt.Location = req.Location;

        // Recording saves the in-memory edit together with the event
        if (!await this.AuditAsync(_audit, appt.PatientId, AuditAction.Change, AuditItemKind.Appointment, appt.Id, AuditDiff.Changed(before, appt))) return NotFound();
        await _appointments.UpdateAsync(appt);
        return NoContent();
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] AppointmentStatus status)
    {
        var appt = await _appointments.GetByIdAsync(id);
        if (appt == null) return NotFound();
        var before = AuditDiff.Snapshot(appt);
        appt.Status = status;
        if (!await this.AuditAsync(_audit, appt.PatientId, AuditAction.Change, AuditItemKind.Appointment, appt.Id, AuditDiff.Changed(before, appt))) return NotFound();
        await _appointments.UpdateAsync(appt);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var appt = await _appointments.GetByIdAsync(id);
        if (appt == null) return NotFound();
        if (!await this.AuditAsync(_audit, appt.PatientId, AuditAction.Change, AuditItemKind.Appointment, id)) return NotFound();
        await _appointments.DeleteAsync(id);
        return NoContent();
    }
}
