using MedFlow.Api.Authorization;
using MedFlow.Api.Localization;
using MedFlow.Core;
using MedFlow.Api.Extensions;
using MedFlow.Core.DTOs;
using MedFlow.Core.Enums;
using MedFlow.Core.Entities;
using MedFlow.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[HasPermission(Permission.ClinicRead)]
public class AttachmentsController : ControllerBase
{
    private readonly IPatientAttachmentRepository _attachments;
    private readonly IWebHostEnvironment _env;
    private readonly IAuditService _audit;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Images
        "image/jpeg", "image/png", "image/gif", "image/webp", "image/bmp", "image/svg+xml",
        // Videos
        "video/mp4", "video/webm", "video/quicktime", "video/x-msvideo",
        // Documents
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    };

    private const long MaxFileSize = 50 * 1024 * 1024; // 50 MB

    public AttachmentsController(IPatientAttachmentRepository attachments, IWebHostEnvironment env, IAuditService audit)
    {
        _attachments = attachments;
        _env = env;
        _audit = audit;
    }

    [HttpGet("patient/{patientId:int}")]
    [HasPermission(Permission.AttachmentsRead)]
    public async Task<ActionResult<IEnumerable<PatientAttachmentDto>>> GetByPatient(int patientId)
    {
        if (!await this.AuditAsync(_audit, patientId, AuditAction.View, AuditItemKind.Attachment, null)) return NotFound();
        return Ok(await _attachments.GetByPatientAsync(patientId, this.GetScope()));
    }

    [HttpPost]
    [HasPermission(Permission.AttachmentsWrite)]
    [RequestSizeLimit(52_428_800)] // 50 MB
    public async Task<ActionResult<PatientAttachmentDto>> Upload(
        [FromForm] IFormFile file,
        [FromForm] int patientId,
        [FromForm] string? category,
        [FromForm] string? description)
    {
        if (file == null || file.Length == 0)
            return BadRequest(this.T("Attachment.NoFile"));

        if (file.Length > MaxFileSize)
            return BadRequest(this.T("Attachment.TooLarge"));

        if (!AllowedContentTypes.Contains(file.ContentType))
            return BadRequest(this.T("Attachment.TypeNotAllowed", file.ContentType));

        var doctorId = User.GetUserId();
        var clinicId = this.GetScope().ClinicId;
        // Checked before anything is written to disk
        if (!await this.AuditAsync(_audit, patientId, AuditAction.Change, AuditItemKind.Attachment, null)) return NotFound();
        var storedFileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var uploadDir = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "attachments", patientId.ToString());
        Directory.CreateDirectory(uploadDir);

        var filePath = Path.Combine(uploadDir, storedFileName);
        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        var attachment = new PatientAttachment
        {
            PatientId = patientId,
            DoctorId = doctorId,
            ClinicId = clinicId,
            FileName = file.FileName,
            StoredFileName = storedFileName,
            ContentType = file.ContentType,
            FileSize = file.Length,
            Category = category,
            Description = description
        };

        var created = await _attachments.AddAsync(attachment);

        return CreatedAtAction(nameof(GetByPatient), new { patientId },
            new PatientAttachmentDto(created.Id, created.PatientId, created.FileName,
                created.ContentType, created.FileSize, created.Category, created.Description, created.CreatedAt));
    }

    [HttpGet("{id:int}/download")]
    [HasPermission(Permission.AttachmentsRead)]
    public async Task<IActionResult> Download(int id)
    {
        var attachment = await _attachments.GetInClinicAsync(id, this.GetScope().ClinicId);
        if (attachment == null) return NotFound();
        if (!await this.AuditAsync(_audit, attachment.PatientId, AuditAction.View, AuditItemKind.Attachment, attachment.Id)) return NotFound();

        var filePath = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "attachments",
            attachment.PatientId.ToString(), attachment.StoredFileName);

        if (!System.IO.File.Exists(filePath))
            return NotFound(this.T("Attachment.Missing"));

        var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
        return File(bytes, attachment.ContentType, attachment.FileName);
    }

    [HttpGet("{id:int}/preview")]
    [HasPermission(Permission.AttachmentsRead)]
    public async Task<IActionResult> Preview(int id)
    {
        var attachment = await _attachments.GetInClinicAsync(id, this.GetScope().ClinicId);
        if (attachment == null) return NotFound();
        if (!await this.AuditAsync(_audit, attachment.PatientId, AuditAction.View, AuditItemKind.Attachment, attachment.Id)) return NotFound();

        var filePath = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "attachments",
            attachment.PatientId.ToString(), attachment.StoredFileName);

        if (!System.IO.File.Exists(filePath))
            return NotFound(this.T("Attachment.Missing"));

        var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
        // Inline content disposition for in-browser preview
        return File(bytes, attachment.ContentType);
    }

    [HttpPut("{id:int}/sharing")]
    [HasPermission(Permission.AttachmentsWrite)]
    public async Task<ActionResult<PatientAttachmentDto>> SetSharing(int id, [FromBody] SetSharingRequest req)
    {
        var attachment = await _attachments.GetInClinicAsync(id, this.GetScope().ClinicId);
        if (attachment == null) return NotFound();
        if (!await this.AuditAsync(_audit, attachment.PatientId, AuditAction.Change, AuditItemKind.Attachment, attachment.Id, new[] { "SharedWithPatient" })) return NotFound();
        await _attachments.SetSharingAsync(attachment, req.Shared);
        return Ok(new PatientAttachmentDto(attachment.Id, attachment.PatientId, attachment.FileName,
            attachment.ContentType, attachment.FileSize, attachment.Category, attachment.Description,
            attachment.CreatedAt, attachment.SharedWithPatient));
    }

    [HttpDelete("{id:int}")]
    [HasPermission(Permission.AttachmentsWrite)]
    public async Task<IActionResult> Delete(int id)
    {
        var attachment = await _attachments.GetInClinicAsync(id, this.GetScope().ClinicId);
        if (attachment == null) return NotFound();
        if (!await this.AuditAsync(_audit, attachment.PatientId, AuditAction.Change, AuditItemKind.Attachment, id)) return NotFound();

        // Delete file from disk
        var filePath = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "attachments",
            attachment.PatientId.ToString(), attachment.StoredFileName);

        if (System.IO.File.Exists(filePath))
            System.IO.File.Delete(filePath);

        // Soft-delete DB record
        await _attachments.DeleteAsync(id);
        return NoContent();
    }
}
