using Microsoft.AspNetCore.Identity;

namespace MedFlow.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    /// <summary>Last accepted TOTP time step; a step at or below it is a replay and is rejected.</summary>
    public long? LastTotpStep { get; set; }
    public string FullName => $"{FirstName} {LastName}";
}
