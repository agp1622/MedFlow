using MedFlow.Core.Entities;
using MedFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MedFlow.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var config = services.GetRequiredService<IConfiguration>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<AppDbContext>();

        if (userManager.Users.Any()) return;

        var email = config["SeedUser:Email"] ?? "tpag02@gmail.com";
        var password = config["SeedUser:Password"] ?? "MedFlow2026!";
        var firstName = config["SeedUser:FirstName"] ?? "Pavel";
        var lastName = config["SeedUser:LastName"] ?? "Arias";
        var specialty = config["SeedUser:Specialty"] ?? "General Medicine";

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            Specialty = specialty
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded) return;

        db.Doctors.Add(new Doctor
        {
            UserId = user.Id,
            FirstName = firstName,
            LastName = lastName,
            Specialty = specialty
        });
        await db.SaveChangesAsync();
    }
}
