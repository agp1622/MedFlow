using MedFlow.Api.Extensions;
using MedFlow.Core;
using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedFlow.Api.Controllers;

/// <summary>
/// Read-only patient portal. The patient is ALWAYS resolved from the authenticated user,
/// never from a client-supplied id, so a patient can only ever reach their own records.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Patient)]
public class PortalController : ControllerBase
{
    private readonly IPortalRepository _portal;
    private readonly IWebHostEnvironment _env;
    private readonly IAuditService _audit;

    public PortalController(IPortalRepository portal, IWebHostEnvironment env, IAuditService audit)
    {
        _portal = portal;
        _env = env;
        _audit = audit;
    }

    /// <summary>Records the patient's own view before any data is returned (fail closed).</summary>
    private Task<bool> AuditViewAsync(Patient patient, AuditItemKind kind, int? itemId = null) =>
        this.AuditAsync(_audit, patient.Id, AuditAction.View, kind, itemId);

    private async Task<Patient?> ResolvePatientAsync()
    {
        var patient = await _portal.GetPatientByUserIdAsync(User.GetUserId());
        return patient is { Status: PatientStatus.Active } ? patient : null;
    }

    private ObjectResult Unavailable() =>
        StatusCode(StatusCodes.Status403Forbidden, new { error = "Portal access is unavailable." });

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var patient = await ResolvePatientAsync();
        if (patient == null) return Unavailable();
        if (!await AuditViewAsync(patient, AuditItemKind.Patient, patient.Id)) return Unavailable();
        return Ok(await _portal.GetProfileAsync(patient));
    }

    [HttpGet("appointments")]
    public async Task<IActionResult> Appointments()
    {
        var patient = await ResolvePatientAsync();
        if (patient == null) return Unavailable();
        if (!await AuditViewAsync(patient, AuditItemKind.Appointment)) return Unavailable();
        return Ok(await _portal.GetAppointmentsAsync(patient.Id));
    }

    [HttpGet("prescriptions")]
    public async Task<IActionResult> Prescriptions()
    {
        var patient = await ResolvePatientAsync();
        if (patient == null) return Unavailable();
        if (!await AuditViewAsync(patient, AuditItemKind.Prescription)) return Unavailable();
        return Ok(await _portal.GetPrescriptionsAsync(patient.Id));
    }

    [HttpGet("invoices")]
    public async Task<IActionResult> Invoices()
    {
        var patient = await ResolvePatientAsync();
        if (patient == null) return Unavailable();
        if (!await AuditViewAsync(patient, AuditItemKind.Invoice)) return Unavailable();
        return Ok(await _portal.GetInvoicesAsync(patient.Id));
    }

    [HttpGet("attachments")]
    public async Task<IActionResult> Attachments()
    {
        var patient = await ResolvePatientAsync();
        if (patient == null) return Unavailable();
        if (!await AuditViewAsync(patient, AuditItemKind.Attachment)) return Unavailable();
        return Ok(await _portal.GetSharedAttachmentsAsync(patient.Id));
    }

    [HttpGet("attachments/{id:int}/download")]
    public async Task<IActionResult> Download(int id)
    {
        var patient = await ResolvePatientAsync();
        if (patient == null) return Unavailable();

        // Missing, unshared and someone-else's all look identical: 404
        var attachment = await _portal.GetSharedAttachmentAsync(id, patient.Id);
        if (attachment == null) return NotFound();

        var filePath = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "attachments",
            attachment.PatientId.ToString(), attachment.StoredFileName);
        if (!System.IO.File.Exists(filePath)) return NotFound();

        if (!await AuditViewAsync(patient, AuditItemKind.Attachment, id)) return Unavailable();
        await _portal.LogAccessAsync(patient.Id, "Attachment", new[] { id }, "Download");
        var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
        return File(bytes, attachment.ContentType, attachment.FileName);
    }

    [HttpGet("notes")]
    public async Task<IActionResult> Notes()
    {
        var patient = await ResolvePatientAsync();
        if (patient == null) return Unavailable();
        if (!await AuditViewAsync(patient, AuditItemKind.Note)) return Unavailable();
        var notes = (await _portal.GetSharedNotesAsync(patient.Id)).ToList();
        if (notes.Count > 0)
            await _portal.LogAccessAsync(patient.Id, "Note", notes.Select(n => n.Id), "View");
        return Ok(notes);
    }
}
