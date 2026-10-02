using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;

namespace MedFlow.Api.Services;

/// <summary>Validates and stores a message (text + optional files) as one all-or-nothing operation.</summary>
public class MessageService
{
    public const int MaxBodyLength = 4000;
    public const int MaxFiles = 5;

    private readonly IMessageRepository _messages;
    private readonly AttachmentStorage _storage;

    public MessageService(IMessageRepository messages, AttachmentStorage storage)
    {
        _messages = messages;
        _storage = storage;
    }

    public async Task<(MessageDto? Message, string? Error)> SendAsync(
        Patient patient, MessageSenderRole role, string senderUserId, string? body, IFormFileCollection? files)
    {
        body = (body ?? string.Empty).Trim();
        var uploads = files?.ToList() ?? new List<IFormFile>();

        if (body.Length > MaxBodyLength) return (null, $"Message is too long (max {MaxBodyLength} characters).");
        if (uploads.Count > MaxFiles) return (null, $"A message can include at most {MaxFiles} files.");
        if (body.Length == 0 && uploads.Count == 0) return (null, "Message cannot be empty.");
        foreach (var file in uploads)
        {
            var error = _storage.Validate(file);
            if (error != null) return (null, error);
        }

        var stored = new List<NewMessageFile>();
        try
        {
            foreach (var file in uploads)
                stored.Add(await _storage.SaveAsync(file, patient.Id));
            return (await _messages.SendAsync(patient, role, senderUserId, body, stored), null);
        }
        catch
        {
            foreach (var f in stored) _storage.Delete(patient.Id, f.StoredFileName);
            throw;
        }
    }
}
