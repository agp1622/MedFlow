using MedFlow.Core;
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
[Authorize(Roles = Roles.Doctor)]
public class PrescriptionsController : ControllerBase
{
    private readonly IPrescriptionRepository _rx;
    private readonly IAuditService _audit;
    public PrescriptionsController(IPrescriptionRepository rx, IAuditService audit) { _rx = rx; _audit = audit; }

    [HttpGet]
    public async Task<ActionResult<PagedResult<PrescriptionDto>>> GetAll([FromQuery] QueryParams q)
        => Ok(await _rx.GetPagedAsync(User.GetUserId(), q));

    [HttpGet("patient/{patientId:int}")]
    public async Task<ActionResult<IEnumerable<PrescriptionDto>>> GetByPatient(int patientId)
    {
        if (!await this.AuditAsync(_audit, patientId, AuditAction.View, AuditItemKind.Prescription, null)) return NotFound();
        return Ok(await _rx.GetByPatientAsync(patientId, User.GetUserId()));
    }

    [HttpPost]
    public async Task<ActionResult<PrescriptionDto>> Create([FromBody] CreatePrescriptionRequest req)
    {
        if (!await this.AuditAsync(_audit, req.PatientId, AuditAction.Change, AuditItemKind.Prescription, null)) return NotFound();
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
        var before = AuditDiff.Snapshot(rx);
        rx.DrugName = req.DrugName;
        rx.Dosage = req.Dosage;
        rx.Frequency = req.Frequency;
        rx.Instructions = req.Instructions;
        rx.ExpiryDate = req.ExpiryDate;
        rx.RefillsRemaining = req.RefillsRemaining;
        rx.Status = req.Status;
        if (!await this.AuditAsync(_audit, rx.PatientId, AuditAction.Change, AuditItemKind.Prescription, rx.Id, AuditDiff.Changed(before, rx))) return NotFound();
        await _rx.UpdateAsync(rx);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var rx = await _rx.GetByIdAsync(id);
        if (rx == null) return NotFound();
        if (!await this.AuditAsync(_audit, rx.PatientId, AuditAction.Change, AuditItemKind.Prescription, id)) return NotFound();
        await _rx.DeleteAsync(id);
        return NoContent();
    }
}

// ── Invoices ──────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Doctor)]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceRepository _invoices;
    private readonly IAuditService _audit;
    public InvoicesController(IInvoiceRepository invoices, IAuditService audit) { _invoices = invoices; _audit = audit; }

    [HttpGet]
    public async Task<ActionResult<PagedResult<InvoiceDto>>> GetAll([FromQuery] QueryParams q)
        => Ok(await _invoices.GetPagedAsync(User.GetUserId(), q));

    [HttpGet("patient/{patientId:int}")]
    public async Task<ActionResult<IEnumerable<InvoiceDto>>> GetByPatient(int patientId)
    {
        if (!await this.AuditAsync(_audit, patientId, AuditAction.View, AuditItemKind.Invoice, null)) return NotFound();
        return Ok(await _invoices.GetByPatientAsync(patientId, User.GetUserId()));
    }

    [HttpPost]
    public async Task<ActionResult<InvoiceDto>> Create([FromBody] CreateInvoiceRequest req)
    {
        var doctorId = User.GetUserId();
        if (!await this.AuditAsync(_audit, req.PatientId, AuditAction.Change, AuditItemKind.Invoice, null)) return NotFound();
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
        var before = AuditDiff.Snapshot(inv);
        inv.ServiceDescription = req.ServiceDescription;
        inv.Amount = req.Amount;
        inv.Status = req.Status;
        inv.DueDate = req.DueDate;
        inv.PaidAmount = req.PaidAmount;
        inv.Notes = req.Notes;
        if (req.Status == InvoiceStatus.Paid && inv.PaidDate == null)
            inv.PaidDate = DateTime.UtcNow;
        if (!await this.AuditAsync(_audit, inv.PatientId, AuditAction.Change, AuditItemKind.Invoice, inv.Id, AuditDiff.Changed(before, inv))) return NotFound();
        await _invoices.UpdateAsync(inv);
        return NoContent();
    }

    [HttpPatch("{id:int}/mark-paid")]
    public async Task<IActionResult> MarkPaid(int id)
    {
        var inv = await _invoices.GetByIdAsync(id);
        if (inv == null) return NotFound();
        var before = AuditDiff.Snapshot(inv);
        inv.Status = InvoiceStatus.Paid;
        inv.PaidDate = DateTime.UtcNow;
        inv.PaidAmount = inv.Amount;
        if (!await this.AuditAsync(_audit, inv.PatientId, AuditAction.Change, AuditItemKind.Invoice, inv.Id, AuditDiff.Changed(before, inv))) return NotFound();
        await _invoices.UpdateAsync(inv);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var inv = await _invoices.GetByIdAsync(id);
        if (inv == null) return NotFound();
        if (!await this.AuditAsync(_audit, inv.PatientId, AuditAction.Change, AuditItemKind.Invoice, id)) return NotFound();
        await _invoices.DeleteAsync(id);
        return NoContent();
    }
}

// ── VitalSigns ────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Doctor)]
public class VitalSignsController : ControllerBase
{
    private readonly IVitalSignRepository _vitals;
    private readonly IAuditService _audit;
    public VitalSignsController(IVitalSignRepository vitals, IAuditService audit) { _vitals = vitals; _audit = audit; }

    [HttpGet("patient/{patientId:int}")]
    public async Task<ActionResult<IEnumerable<VitalSignDto>>> GetByPatient(int patientId)
    {
        if (!await this.AuditAsync(_audit, patientId, AuditAction.View, AuditItemKind.VitalSign, null)) return NotFound();
        return Ok(await _vitals.GetByPatientAsync(patientId, User.GetUserId()));
    }

    [HttpGet("patient/{patientId:int}/latest")]
    public async Task<ActionResult<VitalSignDto>> GetLatest(int patientId)
    {
        if (!await this.AuditAsync(_audit, patientId, AuditAction.View, AuditItemKind.VitalSign, null)) return NotFound();
        var v = await _vitals.GetLatestByPatientAsync(patientId);
        if (v == null) return NotFound();
        return Ok(v);
    }

    [HttpPost]
    public async Task<ActionResult<VitalSignDto>> Create([FromBody] CreateVitalSignRequest req)
    {
        if (!await this.AuditAsync(_audit, req.PatientId, AuditAction.Change, AuditItemKind.VitalSign, null)) return NotFound();
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
[Authorize(Roles = Roles.Doctor)]
public class MedicalNotesController : ControllerBase
{
    private readonly IMedicalNoteRepository _notes;
    private readonly IAuditService _audit;
    public MedicalNotesController(IMedicalNoteRepository notes, IAuditService audit) { _notes = notes; _audit = audit; }

    [HttpGet("patient/{patientId:int}")]
    public async Task<ActionResult<IEnumerable<MedicalNoteDto>>> GetByPatient(int patientId)
    {
        if (!await this.AuditAsync(_audit, patientId, AuditAction.View, AuditItemKind.Note, null)) return NotFound();
        return Ok(await _notes.GetByPatientAsync(patientId, User.GetUserId()));
    }

    // Copy-forward source: the caller's own latest note for the patient. 404 for none/unknown patient alike.
    [HttpGet("patient/{patientId:int}/latest")]
    public async Task<ActionResult<CopyForwardDto>> GetLatest(int patientId)
    {
        var latest = await _notes.GetLatestForPatientAsync(patientId, User.GetUserId());
        return latest == null ? NotFound() : Ok(latest);
    }

    [HttpPost]
    public async Task<ActionResult<MedicalNoteDto>> Create([FromBody] CreateMedicalNoteRequest req)
    {
        if (!await this.AuditAsync(_audit, req.PatientId, AuditAction.Change, AuditItemKind.Note, null)) return NotFound();
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

    [HttpPut("{id:int}/sharing")]
    public async Task<ActionResult<MedicalNoteDto>> SetSharing(int id, [FromBody] SetSharingRequest req)
    {
        var note = await _notes.GetWithOwnerCheckAsync(id, User.GetUserId());
        if (note == null) return NotFound();
        var before = AuditDiff.Snapshot(note);
        note.SharedWithPatient = req.Shared;
        if (!await this.AuditAsync(_audit, note.PatientId, AuditAction.Change, AuditItemKind.Note, note.Id, AuditDiff.Changed(before, note))) return NotFound();
        await _notes.UpdateAsync(note);
        return Ok(new MedicalNoteDto(note.Id, note.PatientId, "You",
            note.Content, note.VisitType, note.NoteDate, note.SharedWithPatient));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var note = await _notes.GetByIdAsync(id);
        if (note == null) return NotFound();
        if (!await this.AuditAsync(_audit, note.PatientId, AuditAction.Change, AuditItemKind.Note, id)) return NotFound();
        await _notes.DeleteAsync(id);
        return NoContent();
    }
}

// ── Dashboard ─────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Doctor)]
public class DashboardController : ControllerBase
{
    private readonly IDashboardRepository _dashboard;
    public DashboardController(IDashboardRepository dashboard) => _dashboard = dashboard;

    [HttpGet]
    public async Task<ActionResult<DashboardStatsDto>> GetStats()
        => Ok(await _dashboard.GetStatsAsync(User.GetUserId()));
}
