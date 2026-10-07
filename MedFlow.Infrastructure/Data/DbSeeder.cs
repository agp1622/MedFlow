using MedFlow.Core;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using MedFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MedFlow.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var config = services.GetRequiredService<IConfiguration>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<AppDbContext>();

        // Roles must exist before anything else (also covers databases seeded before roles existed)
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { Roles.Patient })
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        if (userManager.Users.Any()) return;

        // No built-in credentials: seeding needs explicitly configured values (user-secrets / environment)
        var email = config["SeedUser:Email"];
        var password = config["SeedUser:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            services.GetService<ILoggerFactory>()?.CreateLogger("DbSeeder")
                .LogWarning("Seeding skipped: SeedUser:Email and SeedUser:Password are not configured.");
            return;
        }
        var firstName = config["SeedUser:FirstName"] ?? "Demo";
        var lastName = config["SeedUser:LastName"] ?? "Doctor";
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

        // Staff roles come from the clinic membership: the first doctor owns the clinic
        await services.GetRequiredService<IClinicService>().ProvisionOwnerAsync(user.Id, $"{firstName} {lastName}");

        await SeedDemoPortalPatientAsync(userManager, db, user.Id, config);
    }

    // Development convenience: a patient with portal access and a little data to look at.
    private static async Task SeedDemoPortalPatientAsync(
        UserManager<ApplicationUser> userManager, AppDbContext db, string doctorId, IConfiguration config)
    {
        var email = config["SeedUser:PatientEmail"];
        var password = config["SeedUser:PatientPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        var portalUser = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            FirstName = "Demo", LastName = "Patient", Specialty = string.Empty
        };
        if (!(await userManager.CreateAsync(portalUser, password)).Succeeded) return;
        await userManager.AddToRoleAsync(portalUser, Roles.Patient);

        var patient = new Patient
        {
            FirstName = "Demo", LastName = "Patient", DateOfBirth = new DateOnly(1985, 4, 12),
            Gender = Gender.PreferNotToSay, BloodType = BloodType.OPos, Email = email, Phone = "555-0100",
            DoctorId = doctorId, PortalUserId = portalUser.Id
        };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        db.Appointments.Add(new Appointment
        {
            PatientId = patient.Id, DoctorId = doctorId, ScheduledAt = DateTime.UtcNow.AddDays(3),
            Type = AppointmentType.FollowUp, Status = AppointmentStatus.Confirmed,
            Reason = "Follow-up visit", Location = "Room 2"
        });
        db.Prescriptions.Add(new Prescription
        {
            PatientId = patient.Id, DoctorId = doctorId, DrugName = "Lisinopril", Dosage = "10 mg",
            Frequency = "Once daily", IssuedDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(80)), RefillsRemaining = 2
        });
        db.Invoices.Add(new Invoice
        {
            PatientId = patient.Id, DoctorId = doctorId, InvoiceNumber = "INV-DEMO-001",
            ServiceDescription = "Consultation", Amount = 120m, Status = InvoiceStatus.Pending,
            DueDate = DateTime.UtcNow.AddDays(14)
        });
        await db.SaveChangesAsync();
    }
}
