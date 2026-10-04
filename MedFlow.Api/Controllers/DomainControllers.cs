using MedFlow.Core;
using MedFlow.Api.Authorization;
using MedFlow.Api.Extensions;
using MedFlow.Api.Localization;
using MedFlow.Api.Services;
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
[HasPermission(Permission.ClinicRead)]
public class PrescriptionsController : ControllerBase
{
    private readonly IPrescriptionRepository _rx;
    private readonly IAuditService _audit;
    private readonly IPrescriptionDocumentRenderer _renderer;
    public PrescriptionsController(IPrescriptionRepository rx, IAuditService audit, IPrescriptionDocumentRenderer renderer)
    { _rx = rx; _audit = audit; _renderer = renderer; }

    /// <summary>Printable PDF. 404 (no body) when missing or not the caller's patient; audited as a View on success.</summary>
    [HttpGet("{id:int}/pdf")]
    [HasPermission(Permission.PrescriptionsRead)]
    public async Task<IActionResult> GetPdf(int id)
    {
        var data = await _rx.GetDocumentDataAsync(id, this.GetScope());
        if (data == null) return NotFound();
        var pdf = _renderer.Render(data);
        if (!await this.AuditAsync(_audit, data.PatientId, AuditAction.View, AuditItemKind.Prescription, id)) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return File(pdf, "application/pdf", $"prescription-{id}.pdf");
    }

    [HttpGet]
    [HasPermission(Permission.PrescriptionsRead)]
    public async Task<ActionResult<PagedResult<PrescriptionDto>>> GetAll([FromQuery] QueryParams q)
        => Ok(await _rx.GetPagedAsync(this.GetScope(), q));

    [HttpGet("patient/{patientId:int}")]
    [HasPermission(Permission.PrescriptionsRead)]
    public async Task<ActionResult<IEnumerable<PrescriptionDto>>> GetByPatient(int patientId)
    {
        if (!await this.AuditAsync(_audit, patientId, AuditAction.View, AuditItemKind.Prescription, null)) return NotFound();
        return Ok(await _rx.GetByPatientAsync(patientId, this.GetScope()));
    }

    [HttpPost]
    [HasPermission(Permission.PrescriptionsWrite)]
    public async Task<ActionResult<PrescriptionDto>> Create([FromBody] CreatePrescriptionRequest req)
    {
        if (!await this.AuditAsync(_audit, req.PatientId, AuditAction.Change, AuditItemKind.Prescription, null)) return NotFound();
        var rx = new Prescription
        {
            PatientId = req.PatientId,
            DoctorId = User.GetUserId(), // the prescriber
            ClinicId = this.GetScope().ClinicId,
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
    [HasPermission(Permission.PrescriptionsWrite)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePrescriptionRequest req)
    {
        var rx = await _rx.GetInClinicAsync(id, this.GetScope().ClinicId);
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
    [HasPermission(Permission.PrescriptionsWrite)]
    public async Task<IActionResult> Delete(int id)
    {
        var rx = await _rx.GetInClinicAsync(id, this.GetScope().ClinicId);
        if (rx == null) return NotFound();
        if (!await this.AuditAsync(_audit, rx.PatientId, AuditAction.Change, AuditItemKind.Prescription, id)) return NotFound();
        await _rx.DeleteAsync(id);
        return NoContent();
    }
}

// ── Invoices ──────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[HasPermission(Permission.ClinicRead)]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceRepository _invoices;
    private readonly IPatientRepository _patients;
    private readonly IAppointmentRepository _appointments;
    private readonly IAuditService _audit;
    public InvoicesController(IInvoiceRepository invoices, IPatientRepository patients, IAppointmentRepository appointments, IAuditService audit)
    { _invoices = invoices; _patients = patients; _appointments = appointments; _audit = audit; }

    [HttpGet]
    [HasPermission(Permission.InvoicesRead)]
    public async Task<ActionResult<PagedResult<InvoiceDto>>> GetAll([FromQuery] QueryParams q)
        => Ok(await _invoices.GetPagedAsync(this.GetScope(), q));

    /// <summary>
    /// DRAFT claim worksheet under CMS-1500 (02/12) item numbers, as JSON (default) or CSV. It is not the official form,
    /// not an X12 837 file and not validated by any payer. 404 (no body) when missing or not the caller's; audited on success.
    /// </summary>
    [HttpGet("{id:int}/claim-export")]
    [HasPermission(Permission.InvoicesRead)]
    public async Task<IActionResult> ClaimExport(int id, [FromQuery] string? format = null)
    {
        var fmt = string.IsNullOrWhiteSpace(format) ? "json" : format.Trim().ToLowerInvariant();
        if (fmt != "json" && fmt != "csv")
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = this.T("Error.BadRequest"),
                Detail = this.T("Claim.UnsupportedFormat"),
                Instance = Request.Path
            });

        var source = await _invoices.GetClaimSourceAsync(id, this.GetScope());
        if (source == null) return NotFound();

        var draft = ClaimDraftBuilder.Build(source);
        var dto = new ClaimDraftDto(
            "DRAFT", this.T("Claim.Disclaimer"), DateTime.UtcNow, source.InvoiceNumber, source.InvoiceStatus,
            draft.Items.Select(i => new ClaimItemDto(i.Item, i.Key, this.T($"Claim.Label.{i.Key}"), i.Value)).ToList(),
            draft.Missing.Select(m => new ClaimMissingDto(m.Item, m.Key, this.T($"Claim.Label.{m.Key}"))).ToList());

        if (!await this.AuditAsync(_audit, source.PatientId, AuditAction.View, AuditItemKind.Invoice, id)) return NotFound();

        Response.Headers.CacheControl = "no-store";
        var safeNumber = new string(source.InvoiceNumber.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
        if (fmt == "csv")
            return File(System.Text.Encoding.UTF8.GetBytes(ClaimCsvWriter.Write(dto)),
                "text/csv; charset=utf-8", $"claim-draft-{safeNumber}.csv");
        var json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(dto, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true });
        return File(json, "application/json", $"claim-draft-{safeNumber}.json");
    }

    [HttpGet("patient/{patientId:int}")]
    [HasPermission(Permission.InvoicesRead)]
    public async Task<ActionResult<IEnumerable<InvoiceDto>>> GetByPatient(int patientId)
    {
        if (!await this.AuditAsync(_audit, patientId, AuditAction.View, AuditItemKind.Invoice, null)) return NotFound();
        return Ok(await _invoices.GetByPatientAsync(patientId, this.GetScope()));
    }

    [HttpPost]
    [HasPermission(Permission.InvoicesWrite)]
    public async Task<ActionResult<InvoiceDto>> Create([FromBody] CreateInvoiceRequest req)
    {
        var scope = this.GetScope();
        if (!await this.AuditAsync(_audit, req.PatientId, AuditAction.Change, AuditItemKind.Invoice, null)) return NotFound();
        // The linked appointment must be the same patient's, in this clinic
        if (req.AppointmentId != null)
        {
            var appt = await _appointments.GetInClinicAsync(req.AppointmentId.Value, scope.ClinicId);
            if (appt == null || appt.PatientId != req.PatientId) return NotFound();
        }
        // Billing doctor: the caller when they are a clinician, otherwise the patient's treating doctor
        var patient = await _patients.GetInClinicAsync(req.PatientId, scope.ClinicId);
        if (patient == null) return NotFound();
        var doctorId = scope.IsClinician ? scope.UserId : patient.DoctorId;
        var invoice = new Invoice
        {
            PatientId = req.PatientId,
            DoctorId = doctorId,
            ClinicId = scope.ClinicId,
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
        var dto = new InvoiceDto(created.Id, created.PatientId, patient.FullName, created.AppointmentId, created.InvoiceNumber,
            created.ServiceDescription, created.Amount, created.PaidAmount, created.Status.ToString(),
            created.InvoiceDate, created.DueDate, created.PaidDate, created.Notes, created.CreatedAt);
        return CreatedAtAction(nameof(GetByPatient), new { patientId = created.PatientId }, dto);
    }

    [HttpPut("{id:int}")]
    [HasPermission(Permission.InvoicesWrite)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateInvoiceRequest req)
    {
        var inv = await _invoices.GetInClinicAsync(id, this.GetScope().ClinicId);
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
    [HasPermission(Permission.InvoicesWrite)]
    public async Task<IActionResult> MarkPaid(int id)
    {
        var inv = await _invoices.GetInClinicAsync(id, this.GetScope().ClinicId);
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
    [HasPermission(Permission.InvoicesWrite)]
    public async Task<IActionResult> Delete(int id)
    {
        var inv = await _invoices.GetInClinicAsync(id, this.GetScope().ClinicId);
        if (inv == null) return NotFound();
        if (!await this.AuditAsync(_audit, inv.PatientId, AuditAction.Change, AuditItemKind.Invoice, id)) return NotFound();
        await _invoices.DeleteAsync(id);
        return NoContent();
    }
}

// ── VitalSigns ────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[HasPermission(Permission.ClinicRead)]
public class VitalSignsController : ControllerBase
{
    private readonly IVitalSignRepository _vitals;
    private readonly IAuditService _audit;
    public VitalSignsController(IVitalSignRepository vitals, IAuditService audit) { _vitals = vitals; _audit = audit; }

    [HttpGet("patient/{patientId:int}")]
    [HasPermission(Permission.VitalsRead)]
    public async Task<ActionResult<IEnumerable<VitalSignDto>>> GetByPatient(int patientId)
    {
        if (!await this.AuditAsync(_audit, patientId, AuditAction.View, AuditItemKind.VitalSign, null)) return NotFound();
        return Ok(await _vitals.GetByPatientAsync(patientId, this.GetScope()));
    }

    [HttpGet("patient/{patientId:int}/latest")]
    [HasPermission(Permission.VitalsRead)]
    public async Task<ActionResult<VitalSignDto>> GetLatest(int patientId)
    {
        if (!await this.AuditAsync(_audit, patientId, AuditAction.View, AuditItemKind.VitalSign, null)) return NotFound();
        var v = await _vitals.GetLatestByPatientAsync(patientId, this.GetScope());
        if (v == null) return NotFound();
        return Ok(v);
    }

    [HttpPost]
    [HasPermission(Permission.VitalsWrite)]
    public async Task<ActionResult<VitalSignDto>> Create([FromBody] CreateVitalSignRequest req)
    {
        if (!await this.AuditAsync(_audit, req.PatientId, AuditAction.Change, AuditItemKind.VitalSign, null)) return NotFound();
        var vital = new VitalSign
        {
            PatientId = req.PatientId,
            ClinicId = this.GetScope().ClinicId,
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
[HasPermission(Permission.ClinicRead)]
public class MedicalNotesController : ControllerBase
{
    private readonly IMedicalNoteRepository _notes;
    private readonly IAuditService _audit;
    public MedicalNotesController(IMedicalNoteRepository notes, IAuditService audit) { _notes = notes; _audit = audit; }

    [HttpGet("patient/{patientId:int}")]
    [HasPermission(Permission.NotesRead)]
    public async Task<ActionResult<IEnumerable<MedicalNoteDto>>> GetByPatient(int patientId)
    {
        if (!await this.AuditAsync(_audit, patientId, AuditAction.View, AuditItemKind.Note, null)) return NotFound();
        return Ok(await _notes.GetByPatientAsync(patientId, this.GetScope()));
    }

    // Copy-forward source: the patient's latest note in the clinic. 404 for none/unknown patient alike.
    [HttpGet("patient/{patientId:int}/latest")]
    [HasPermission(Permission.NotesRead)]
    public async Task<ActionResult<CopyForwardDto>> GetLatest(int patientId)
    {
        var latest = await _notes.GetLatestForPatientAsync(patientId, this.GetScope());
        return latest == null ? NotFound() : Ok(latest);
    }

    [HttpPost]
    [HasPermission(Permission.NotesWrite)]
    public async Task<ActionResult<MedicalNoteDto>> Create([FromBody] CreateMedicalNoteRequest req)
    {
        if (!await this.AuditAsync(_audit, req.PatientId, AuditAction.Change, AuditItemKind.Note, null)) return NotFound();
        var note = new MedicalNote
        {
            PatientId = req.PatientId,
            DoctorId = User.GetUserId(), // author (doctor, owner or nurse)
            ClinicId = this.GetScope().ClinicId,
            Content = req.Content,
            VisitType = req.VisitType,
            NoteDate = DateTime.UtcNow
        };
        var created = await _notes.AddAsync(note);
        return Ok(new MedicalNoteDto(created.Id, created.PatientId, "You",
            created.Content, created.VisitType, created.NoteDate));
    }

    [HttpPut("{id:int}/sharing")]
    [HasPermission(Permission.NotesManage)]
    public async Task<ActionResult<MedicalNoteDto>> SetSharing(int id, [FromBody] SetSharingRequest req)
    {
        var note = await _notes.GetInClinicAsync(id, this.GetScope().ClinicId);
        if (note == null) return NotFound();
        var before = AuditDiff.Snapshot(note);
        note.SharedWithPatient = req.Shared;
        if (!await this.AuditAsync(_audit, note.PatientId, AuditAction.Change, AuditItemKind.Note, note.Id, AuditDiff.Changed(before, note))) return NotFound();
        await _notes.UpdateAsync(note);
        return Ok(new MedicalNoteDto(note.Id, note.PatientId, "You",
            note.Content, note.VisitType, note.NoteDate, note.SharedWithPatient));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permission.NotesManage)]
    public async Task<IActionResult> Delete(int id)
    {
        var note = await _notes.GetInClinicAsync(id, this.GetScope().ClinicId);
        if (note == null) return NotFound();
        if (!await this.AuditAsync(_audit, note.PatientId, AuditAction.Change, AuditItemKind.Note, id)) return NotFound();
        await _notes.DeleteAsync(id);
        return NoContent();
    }
}

// ── Dashboard ─────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[HasPermission(Permission.ClinicRead)]
public class DashboardController : ControllerBase
{
    private readonly IDashboardRepository _dashboard;
    public DashboardController(IDashboardRepository dashboard) => _dashboard = dashboard;

    [HttpGet]
    [HasPermission(Permission.DashboardRead)]
    public async Task<ActionResult<DashboardStatsDto>> GetStats()
        => Ok(await _dashboard.GetStatsAsync(this.GetScope()));
}
