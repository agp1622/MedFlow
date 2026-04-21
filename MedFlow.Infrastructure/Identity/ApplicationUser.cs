using Microsoft.AspNetCore.Identity;

namespace MedFlow.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
}
