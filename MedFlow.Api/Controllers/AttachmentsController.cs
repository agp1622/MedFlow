using MedFlow.Core;
using MedFlow.Api.Extensions;
using MedFlow.Api.Services;
using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;
using MedFlow.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Doctor)]
public class AttachmentsController : ControllerBase
{
    private readonly IPatientAttachmentRepository _attachments;
    private readonly AttachmentStorage _storage;

    public AttachmentsController(IPatientAttachmentRepository attachments, AttachmentStorage storage)
    {
        _attachments = attachments;
        _storage = storage;
    }

    [HttpGet("patient/{patientId:int}")]
    public async Task<ActionResult<IEnumerable<PatientAttachmentDto>>> GetByPatient(int patientId)
        => Ok(await _attachments.GetByPatientAsync(patientId, User.GetUserId()));

    [HttpPost]
    [RequestSizeLimit(52_428_800)] // 50 MB
    public async Task<ActionResult<PatientAttachmentDto>> Upload(
        [FromForm] IFormFile file,
        [FromForm] int patientId,
        [FromForm] string? category,
        [FromForm] string? description)
    {
        var error = _storage.Validate(file);
        if (error != null) return BadRequest(error);

        var doctorId = User.GetUserId();
        var stored = await _storage.SaveAsync(file, patientId);

        var attachment = new PatientAttachment
        {
            PatientId = patientId,
            DoctorId = doctorId,
            FileName = stored.FileName,
            StoredFileName = stored.StoredFileName,
            ContentType = stored.ContentType,
            FileSize = stored.FileSize,
            Category = category,
            Description = description
        };

        var created = await _attachments.AddAsync(attachment);

        return CreatedAtAction(nameof(GetByPatient), new { patientId },
            new PatientAttachmentDto(created.Id, created.PatientId, created.FileName,
                created.ContentType, created.FileSize, created.Category, created.Description, created.CreatedAt));
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id)
    {
        var attachment = await _attachments.GetWithOwnerCheckAsync(id, User.GetUserId());
        if (attachment == null) return NotFound();

        var filePath = _storage.PathFor(attachment.PatientId, attachment.StoredFileName);

        if (!System.IO.File.Exists(filePath))
            return NotFound("File not found on disk.");

        var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
        return File(bytes, attachment.ContentType, attachment.FileName);
    }

    [HttpGet("{id:int}/preview")]
    public async Task<IActionResult> Preview(int id)
    {
        var attachment = await _attachments.GetWithOwnerCheckAsync(id, User.GetUserId());
        if (attachment == null) return NotFound();

        var filePath = _storage.PathFor(attachment.PatientId, attachment.StoredFileName);

        if (!System.IO.File.Exists(filePath))
            return NotFound("File not found on disk.");

        var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
        // Inline content disposition for in-browser preview
        return File(bytes, attachment.ContentType);
    }

    [HttpPut("{id:int}/sharing")]
    public async Task<ActionResult<PatientAttachmentDto>> SetSharing(int id, [FromBody] SetSharingRequest req)
    {
        var attachment = await _attachments.GetWithOwnerCheckAsync(id, User.GetUserId());
        if (attachment == null) return NotFound();
        await _attachments.SetSharingAsync(attachment, req.Shared);
        return Ok(new PatientAttachmentDto(attachment.Id, attachment.PatientId, attachment.FileName,
            attachment.ContentType, attachment.FileSize, attachment.Category, attachment.Description,
            attachment.CreatedAt, attachment.SharedWithPatient));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var attachment = await _attachments.GetWithOwnerCheckAsync(id, User.GetUserId());
        if (attachment == null) return NotFound();

        // Delete file from disk
        _storage.Delete(attachment.PatientId, attachment.StoredFileName);

        // Soft-delete DB record
        await _attachments.DeleteAsync(id);
        return NoContent();
    }
}
