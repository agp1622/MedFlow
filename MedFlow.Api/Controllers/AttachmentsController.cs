using MedFlow.Api.Extensions;
using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;
using MedFlow.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AttachmentsController : ControllerBase
{
    private readonly IPatientAttachmentRepository _attachments;
    private readonly IWebHostEnvironment _env;

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

    public AttachmentsController(IPatientAttachmentRepository attachments, IWebHostEnvironment env)
    {
        _attachments = attachments;
        _env = env;
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
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        if (file.Length > MaxFileSize)
            return BadRequest("File exceeds the 50 MB size limit.");

        if (!AllowedContentTypes.Contains(file.ContentType))
            return BadRequest($"File type '{file.ContentType}' is not allowed.");

        var doctorId = User.GetUserId();
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
    public async Task<IActionResult> Download(int id)
    {
        var attachment = await _attachments.GetWithOwnerCheckAsync(id, User.GetUserId());
        if (attachment == null) return NotFound();

        var filePath = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "attachments",
            attachment.PatientId.ToString(), attachment.StoredFileName);

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

        var filePath = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "attachments",
            attachment.PatientId.ToString(), attachment.StoredFileName);

        if (!System.IO.File.Exists(filePath))
            return NotFound("File not found on disk.");

        var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
        // Inline content disposition for in-browser preview
        return File(bytes, attachment.ContentType);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var attachment = await _attachments.GetWithOwnerCheckAsync(id, User.GetUserId());
        if (attachment == null) return NotFound();

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
