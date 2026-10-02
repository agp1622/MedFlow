using MedFlow.Api.Extensions;
using MedFlow.Api.Services;
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
    private readonly IMessageRepository _messages;
    private readonly MessageService _messageService;
    private readonly AttachmentStorage _storage;

    public PortalController(IPortalRepository portal, IMessageRepository messages,
        MessageService messageService, AttachmentStorage storage)
    {
        _portal = portal;
        _messages = messages;
        _messageService = messageService;
        _storage = storage;
    }

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
        return Ok(await _portal.GetProfileAsync(patient));
    }

    [HttpGet("appointments")]
    public async Task<IActionResult> Appointments()
    {
        var patient = await ResolvePatientAsync();
        if (patient == null) return Unavailable();
        return Ok(await _portal.GetAppointmentsAsync(patient.Id));
    }

    [HttpGet("prescriptions")]
    public async Task<IActionResult> Prescriptions()
    {
        var patient = await ResolvePatientAsync();
        if (patient == null) return Unavailable();
        return Ok(await _portal.GetPrescriptionsAsync(patient.Id));
    }

    [HttpGet("invoices")]
    public async Task<IActionResult> Invoices()
    {
        var patient = await ResolvePatientAsync();
        if (patient == null) return Unavailable();
        return Ok(await _portal.GetInvoicesAsync(patient.Id));
    }

    [HttpGet("attachments")]
    public async Task<IActionResult> Attachments()
    {
        var patient = await ResolvePatientAsync();
        if (patient == null) return Unavailable();
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

        var filePath = _storage.PathFor(attachment.PatientId, attachment.StoredFileName);
        if (!System.IO.File.Exists(filePath)) return NotFound();

        await _portal.LogAccessAsync(patient.Id, "Attachment", new[] { id }, "Download", User.GetUserId());
        var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
        return File(bytes, attachment.ContentType, attachment.FileName);
    }

    [HttpGet("notes")]
    public async Task<IActionResult> Notes()
    {
        var patient = await ResolvePatientAsync();
        if (patient == null) return Unavailable();
        var notes = (await _portal.GetSharedNotesAsync(patient.Id)).ToList();
        if (notes.Count > 0)
            await _portal.LogAccessAsync(patient.Id, "Note", notes.Select(n => n.Id), "View", User.GetUserId());
        return Ok(notes);
    }

    // ── Secure messaging ──────────────────────────────────────────────────────
    [HttpGet("messages")]
    public async Task<IActionResult> Messages()
    {
        var patient = await ResolvePatientAsync();
        if (patient == null) return Unavailable();
        var thread = await _messages.GetMessagesAsync(patient.Id, MessageSenderRole.Patient);
        if (thread.Count > 0)
            await _portal.LogAccessAsync(patient.Id, "Message", new[] { thread[^1].Id }, "View", User.GetUserId());
        return Ok(thread);
    }

    [HttpPost("messages")]
    [RequestSizeLimit(MessageRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = MessageRequestLimit)]
    public async Task<IActionResult> SendMessage([FromForm] string? body, [FromForm] IFormFileCollection? files)
    {
        var patient = await ResolvePatientAsync();
        if (patient == null) return Unavailable();
        var (message, error) = await _messageService.SendAsync(
            patient, MessageSenderRole.Patient, User.GetUserId(), body, files);
        if (error != null) return BadRequest(new { error });
        return StatusCode(StatusCodes.Status201Created, message);
    }

    [HttpPost("messages/read")]
    public async Task<IActionResult> MarkMessagesRead()
    {
        var patient = await ResolvePatientAsync();
        if (patient == null) return Unavailable();
        await _messages.MarkReadAsync(patient.Id, MessageSenderRole.Patient);
        return NoContent();
    }

    [HttpGet("messages/unread-count")]
    public async Task<IActionResult> MessagesUnreadCount()
    {
        var patient = await ResolvePatientAsync();
        if (patient == null) return Unavailable();
        return Ok(new UnreadCountDto(await _messages.GetUnreadCountForPatientAsync(patient.Id)));
    }

    [HttpGet("messages/attachments/{id:int}/download")]
    public async Task<IActionResult> DownloadMessageAttachment(int id)
    {
        var patient = await ResolvePatientAsync();
        if (patient == null) return Unavailable();

        // Missing and someone-else's look identical: 404
        var attachment = await _messages.GetAttachmentForPatientAsync(id, patient.Id);
        if (attachment == null) return NotFound();

        var filePath = _storage.PathFor(attachment.PatientId, attachment.StoredFileName);
        if (!System.IO.File.Exists(filePath)) return NotFound();

        await _portal.LogAccessAsync(patient.Id, "MessageAttachment", new[] { id }, "Download", User.GetUserId());
        var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
        return File(bytes, attachment.ContentType, attachment.FileName);
    }

    // Up to 5 files of 50 MB each, plus the text fields
    private const long MessageRequestLimit = 5L * 50 * 1024 * 1024 + 1024 * 1024;
}
