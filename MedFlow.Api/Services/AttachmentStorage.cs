using MedFlow.Core.DTOs;

namespace MedFlow.Api.Services;

/// <summary>
/// Validation rules and on-disk storage shared by patient attachments and message attachments,
/// so both follow exactly the same allowed types, size limit and folder layout.
/// </summary>
public class AttachmentStorage
{
    private readonly IWebHostEnvironment _env;

    public const long MaxFileSize = 50 * 1024 * 1024; // 50 MB

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

    public AttachmentStorage(IWebHostEnvironment env) => _env = env;

    /// <summary>Returns the user-facing rejection message, or null when the file is acceptable.</summary>
    public string? Validate(IFormFile? file)
    {
        if (file == null || file.Length == 0) return "No file uploaded.";
        if (file.Length > MaxFileSize) return "File exceeds the 50 MB size limit.";
        if (!AllowedContentTypes.Contains(file.ContentType)) return $"File type '{file.ContentType}' is not allowed.";
        return null;
    }

    public string PathFor(int patientId, string storedFileName) =>
        Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "attachments", patientId.ToString(), storedFileName);

    /// <summary>Writes an already-validated file to disk and returns its stored descriptor.</summary>
    public async Task<NewMessageFile> SaveAsync(IFormFile file, int patientId)
    {
        var storedFileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var path = PathFor(patientId, storedFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = new FileStream(path, FileMode.Create);
        await file.CopyToAsync(stream);
        return new NewMessageFile(file.FileName, storedFileName, file.ContentType, file.Length);
    }

    public void Delete(int patientId, string storedFileName)
    {
        var path = PathFor(patientId, storedFileName);
        if (File.Exists(path)) File.Delete(path);
    }
}
