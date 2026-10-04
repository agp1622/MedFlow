using MedFlow.Core.Enums;

namespace MedFlow.Core.Entities;

/// <summary>One single-use recovery code of a user. Only a salted hash is stored.</summary>
public class TwoFactorRecoveryCode
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UsedAt { get; set; }
}

/// <summary>Append-only security event of a user (no patient, no secrets or codes).</summary>
public class SecurityEvent
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public SecurityEventKind Kind { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
