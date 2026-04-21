using MedFlow.Api.Extensions;
using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedFlow.Api.Controllers;

// ── Prescriptions ─────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PrescriptionsController : ControllerBase
{
    private readonly IPrescriptionRepository _rx;
    public PrescriptionsController(IPrescriptionRepository rx) => _rx = rx;

    [HttpGet]
    public async Task<ActionResult<PagedResult<PrescriptionDto>>> GetAll([FromQuery] QueryParams q)
        => Ok(await _rx.GetPagedAsync(User.GetUserId(), q));

    [HttpGet("patient/{patientId:int}")]
    public async Task<ActionResult<IEnumerable<PrescriptionDto>>> GetByPatient(int patientId)
        => Ok(await _rx.GetByPatientAsync(patientId, User.GetUserId()));

    [HttpPost]
    public async Task<ActionResult<PrescriptionDto>> Create([FromBody] CreatePrescriptionRequest req)
    {
        var rx = new Prescription
        {
            PatientId = req.PatientId,
            DoctorId = User.GetUserId(),
            DrugName = req.DrugName,
            Dosage = req.Dosage,
            Frequency = req.Frequency,
            Instructions = req.Instructions,
            IssuedDate = req.IssuedDate,
            ExpiryDate = req.ExpiryDate,
            RefillsRemaining = req.RefillsRemaining,
            Status = PrescriptionStatus.Active
        };
        var created = await _rx.AddAsync(rx);
        return CreatedAtAction(nameof(GetByPatient), new { patientId = created.PatientId }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePrescriptionRequest req)
    {
        var rx = await _rx.GetByIdAsync(id);
        if (rx == null) return NotFound();
        rx.DrugName = req.DrugName;
        rx.Dosage = req.Dosage;
        rx.Frequency = req.Frequency;
        rx.Instructions = req.Instructions;
        rx.ExpiryDate = req.ExpiryDate;
        rx.RefillsRemaining = req.RefillsRemaining;
        rx.Status = req.Status;
        await _rx.UpdateAsync(rx);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _rx.ExistsAsync(id)) return NotFound();
        await _rx.DeleteAsync(id);
        return NoContent();
    }
}

// ── Invoices ──────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceRepository _invoices;
    public InvoicesController(IInvoiceRepository invoices) => _invoices = invoices;

    [HttpGet]
    public async Task<ActionResult<PagedResult<InvoiceDto>>> GetAll([FromQuery] QueryParams q)
        => Ok(await _invoices.GetPagedAsync(User.GetUserId(), q));

    [HttpGet("patient/{patientId:int}")]
    public async Task<ActionResult<IEnumerable<InvoiceDto>>> GetByPatient(int patientId)
        => Ok(await _invoices.GetByPatientAsync(patientId, User.GetUserId()));

    [HttpPost]
    public async Task<ActionResult<InvoiceDto>> Create([FromBody] CreateInvoiceRequest req)
    {
        var doctorId = User.GetUserId();
        var invoice = new Invoice
        {
            PatientId = req.PatientId,
            DoctorId = doctorId,
            AppointmentId = req.AppointmentId,
            InvoiceNumber = await _invoices.GenerateInvoiceNumberAsync(),
            ServiceDescription = req.ServiceDescription,
            Amount = req.Amount,
            DueDate = req.DueDate,
            Notes = req.Notes,
            Status = InvoiceStatus.Pending,
            InvoiceDate = DateTime.UtcNow
        };
        var created = await _invoices.AddAsync(invoice);
        return CreatedAtAction(nameof(GetByPatient), new { patientId = created.PatientId }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateInvoiceRequest req)
    {
        var inv = await _invoices.GetByIdAsync(id);
        if (inv == null) return NotFound();
        inv.ServiceDescription = req.ServiceDescription;
        inv.Amount = req.Amount;
        inv.Status = req.Status;
        inv.DueDate = req.DueDate;
        inv.PaidAmount = req.PaidAmount;
        inv.Notes = req.Notes;
        if (req.Status == InvoiceStatus.Paid && inv.PaidDate == null)
            inv.PaidDate = DateTime.UtcNow;
        await _invoices.UpdateAsync(inv);
        return NoContent();
    }

    [HttpPatch("{id:int}/mark-paid")]
    public async Task<IActionResult> MarkPaid(int id)
    {
        var inv = await _invoices.GetByIdAsync(id);
        if (inv == null) return NotFound();
        inv.Status = InvoiceStatus.Paid;
        inv.PaidDate = DateTime.UtcNow;
        inv.PaidAmount = inv.Amount;
        await _invoices.UpdateAsync(inv);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _invoices.ExistsAsync(id)) return NotFound();
        await _invoices.DeleteAsync(id);
        return NoContent();
    }
}

// ── VitalSigns ────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VitalSignsController : ControllerBase
{
    private readonly IVitalSignRepository _vitals;
    public VitalSignsController(IVitalSignRepository vitals) => _vitals = vitals;

    [HttpGet("patient/{patientId:int}")]
    public async Task<ActionResult<IEnumerable<VitalSignDto>>> GetByPatient(int patientId)
        => Ok(await _vitals.GetByPatientAsync(patientId, User.GetUserId()));

    [HttpGet("patient/{patientId:int}/latest")]
    public async Task<ActionResult<VitalSignDto>> GetLatest(int patientId)
    {
        var v = await _vitals.GetLatestByPatientAsync(patientId);
        if (v == null) return NotFound();
        return Ok(v);
    }

    [HttpPost]
    public async Task<ActionResult<VitalSignDto>> Create([FromBody] CreateVitalSignRequest req)
    {
        var vital = new VitalSign
        {
            PatientId = req.PatientId,
            BloodPressure = req.BloodPressure,
            HeartRate = req.HeartRate,
            Weight = req.Weight,
            Height = req.Height,
            Temperature = req.Temperature,
            OxygenSaturation = req.OxygenSaturation,
            RecordedAt = DateTime.UtcNow,
            RecordedBy = User.GetUserId()
        };

        if (req.Weight.HasValue && req.Height.HasValue && req.Height.Value > 0)
        {
            var heightM = req.Height.Value / 100m;
            vital.Bmi = Math.Round(req.Weight.Value / (heightM * heightM), 1);
        }

        var created = await _vitals.AddAsync(vital);
        return Ok(new VitalSignDto(created.Id, created.PatientId, created.RecordedAt,
            created.BloodPressure, created.HeartRate, created.Weight,
            created.Height, created.Bmi, created.Temperature,
            created.OxygenSaturation, created.RecordedBy));
    }
}

// ── MedicalNotes ──────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MedicalNotesController : ControllerBase
{
    private readonly IMedicalNoteRepository _notes;
    public MedicalNotesController(IMedicalNoteRepository notes) => _notes = notes;

    [HttpGet("patient/{patientId:int}")]
    public async Task<ActionResult<IEnumerable<MedicalNoteDto>>> GetByPatient(int patientId)
        => Ok(await _notes.GetByPatientAsync(patientId, User.GetUserId()));

    [HttpPost]
    public async Task<ActionResult<MedicalNoteDto>> Create([FromBody] CreateMedicalNoteRequest req)
    {
        var note = new MedicalNote
        {
            PatientId = req.PatientId,
            DoctorId = User.GetUserId(),
            Content = req.Content,
            VisitType = req.VisitType,
            NoteDate = DateTime.UtcNow
        };
        var created = await _notes.AddAsync(note);
        return Ok(new MedicalNoteDto(created.Id, created.PatientId, "You",
            created.Content, created.VisitType, created.NoteDate));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _notes.ExistsAsync(id)) return NotFound();
        await _notes.DeleteAsync(id);
        return NoContent();
    }
}

// ── Dashboard ─────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardRepository _dashboard;
    public DashboardController(IDashboardRepository dashboard) => _dashboard = dashboard;

    [HttpGet]
    public async Task<ActionResult<DashboardStatsDto>> GetStats()
        => Ok(await _dashboard.GetStatsAsync(User.GetUserId()));
}
