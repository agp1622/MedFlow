namespace MedFlow.Core.Entities;

/// <summary>Implemented by every record that belongs to exactly one clinic.</summary>
public interface IClinicScoped
{
    int ClinicId { get; set; }
}

public class Clinic : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    /// <summary>Concurrency token rotated on every membership change so concurrent last-owner changes collide.</summary>
    public Guid Stamp { get; set; } = Guid.NewGuid();
}

/// <summary>One user's membership of one clinic (a user belongs to at most one clinic). Deactivated members keep their row.</summary>
public class ClinicMember : BaseEntity
{
    public int ClinicId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ClinicRole Role { get; set; }
    public bool IsActive { get; set; } = true;
}

public class StaffInvitation : BaseEntity
{
    public int ClinicId { get; set; }
    public string Email { get; set; } = string.Empty;
    public ClinicRole Role { get; set; }
    public string TokenHash { get; set; } = string.Empty; // SHA-256 of the emailed token
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }                 // set on acceptance, supersede or revoke
    public string InvitedByUserId { get; set; } = string.Empty;
}
