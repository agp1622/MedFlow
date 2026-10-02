using MedFlow.Api.Extensions;
using MedFlow.Api.Services;
using MedFlow.Core;
using MedFlow.Core.DTOs;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedFlow.Api.Controllers;

/// <summary>
/// Doctor side of secure messaging. A doctor can only reach threads of their own patients;
/// anything else (missing or someone else's) answers 404 so nothing is revealed.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Doctor)]
public class MessagesController : ControllerBase
{
    // Up to 5 files of 50 MB each, plus the text fields
    private const long MessageRequestLimit = 5L * 50 * 1024 * 1024 + 1024 * 1024;

    private readonly IMessageRepository _messages;
    private readonly IPortalRepository _portal;
    private readonly MessageService _messageService;
    private readonly AttachmentStorage _storage;

    public MessagesController(IMessageRepository messages, IPortalRepository portal,
        MessageService messageService, AttachmentStorage storage)
    {
        _messages = messages;
        _portal = portal;
        _messageService = messageService;
        _storage = storage;
    }

    [HttpGet("threads")]
    public async Task<ActionResult<IEnumerable<MessageThreadSummaryDto>>> Threads()
        => Ok(await _messages.GetThreadsForDoctorAsync(User.GetUserId()));

    [HttpGet("patient/{patientId:int}")]
    public async Task<IActionResult> Thread(int patientId)
    {
        var patient = await _messages.GetPatientForDoctorAsync(patientId, User.GetUserId());
        if (patient == null) return NotFound();
        var thread = await _messages.GetMessagesAsync(patient.Id, MessageSenderRole.Doctor);
        if (thread.Count > 0)
            await _portal.LogAccessAsync(patient.Id, "Message", new[] { thread[^1].Id }, "View", User.GetUserId());
        return Ok(thread);
    }

    [HttpPost("patient/{patientId:int}")]
    [RequestSizeLimit(MessageRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = MessageRequestLimit)]
    public async Task<IActionResult> Send(int patientId, [FromForm] string? body, [FromForm] IFormFileCollection? files)
    {
        var patient = await _messages.GetPatientForDoctorAsync(patientId, User.GetUserId());
        if (patient == null) return NotFound();
        var (message, error) = await _messageService.SendAsync(
            patient, MessageSenderRole.Doctor, User.GetUserId(), body, files);
        if (error != null) return BadRequest(new { error });
        return StatusCode(StatusCodes.Status201Created, message);
    }

    [HttpPost("patient/{patientId:int}/read")]
    public async Task<IActionResult> MarkRead(int patientId)
    {
        var patient = await _messages.GetPatientForDoctorAsync(patientId, User.GetUserId());
        if (patient == null) return NotFound();
        await _messages.MarkReadAsync(patient.Id, MessageSenderRole.Doctor);
        return NoContent();
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> UnreadCount()
        => Ok(new UnreadCountDto(await _messages.GetUnreadCountForDoctorAsync(User.GetUserId())));

    [HttpGet("attachments/{id:int}/download")]
    public async Task<IActionResult> DownloadAttachment(int id)
    {
        var attachment = await _messages.GetAttachmentForDoctorAsync(id, User.GetUserId());
        if (attachment == null) return NotFound();

        var filePath = _storage.PathFor(attachment.PatientId, attachment.StoredFileName);
        if (!System.IO.File.Exists(filePath)) return NotFound();

        await _portal.LogAccessAsync(attachment.PatientId, "MessageAttachment", new[] { id }, "Download", User.GetUserId());
        var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
        return File(bytes, attachment.ContentType, attachment.FileName);
    }
}
