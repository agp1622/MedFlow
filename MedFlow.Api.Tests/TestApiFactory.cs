using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using MedFlow.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MedFlow.Api.Tests;

public class FakeEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Body)> Sent { get; } = new();
    public Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        lock (Sent) Sent.Add((toEmail, subject, htmlBody));
        return Task.CompletedTask;
    }
}

public class FakePaymentGateway : IPaymentGateway
{
    public bool IsConfigured { get; set; } = true;
    public List<CheckoutRequest> Requests { get; } = new();
    public Task<string> CreateCheckoutSessionAsync(CheckoutRequest request)
    {
        lock (Requests) Requests.Add(request);
        return Task.FromResult($"https://checkout.test/pay/{request.InvoiceId}");
    }
}

public record AuthResult(string Token, string UserId, string Role);

public class TestApiFactory : WebApplicationFactory<Program>
{
    public const string Password = "Passw0rdTest";
    private readonly string _dbName = Guid.NewGuid().ToString();
    private string? _uploadsRoot;
    private HashSet<string> _preexistingUploads = new();
    public FakeEmailSender Email { get; } = new();
    public FakePaymentGateway Payments { get; } = new();
    public const string WebhookSecret = "whsec_test_secret";

    static TestApiFactory()
    {
        // Program.cs reads these while building, so they must be environment variables
        Environment.SetEnvironmentVariable("Jwt__Key", "test-signing-key-test-signing-key-test-signing-key-1234");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "MedFlowTests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "MedFlowTests");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Server=unused;Database=unused");
        Environment.SetEnvironmentVariable("Payments__WebhookSecret", WebhookSecret);
        Environment.SetEnvironmentVariable("RateLimiting__AcceptInvitationPermitLimit", "1000");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing"); // skips the Development-only migrate/seed block

        // Uploads land under the API project's wwwroot; remember what is there so Dispose removes only test files
        _uploadsRoot = Path.Combine(builder.GetSetting(WebHostDefaults.ContentRootKey)!, "wwwroot", "uploads", "attachments");
        _preexistingUploads = Directory.Exists(_uploadsRoot)
            ? Directory.GetFiles(_uploadsRoot, "*", SearchOption.AllDirectories).ToHashSet()
            : new();
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_dbName));
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Email);
            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(Payments);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing || _uploadsRoot == null || !Directory.Exists(_uploadsRoot)) return;
        foreach (var file in Directory.GetFiles(_uploadsRoot, "*", SearchOption.AllDirectories))
            if (!_preexistingUploads.Contains(file)) File.Delete(file);
        foreach (var dir in Directory.GetDirectories(_uploadsRoot))
            if (!Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
    }

    public HttpClient ClientFor(string? token = null)
    {
        var c = CreateClient();
        if (token != null) c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task<AuthResult> RegisterDoctorAsync(string email)
    {
        var res = await CreateClient().PostAsJsonAsync("/api/auth/register",
            new { email, password = Password, firstName = "Doc", lastName = "Tor", specialty = "GP" });
        res.EnsureSuccessStatusCode();
        return await ReadAuth(res);
    }

    public async Task<int> CreatePatientAsync(string doctorToken, string email, string first = "Pat")
    {
        var res = await ClientFor(doctorToken).PostAsJsonAsync("/api/patients", new
        {
            firstName = first, lastName = "Ient", dateOfBirth = "1990-01-01", gender = "Male", bloodType = "OPos",
            email, phone = "555-0000"
        });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("id").GetInt32();
    }

    /// <summary>Invites the patient and returns the raw token parsed from the captured email.</summary>
    public async Task<string> InviteAsync(string doctorToken, int patientId)
    {
        var before = Email.Sent.Count;
        var res = await ClientFor(doctorToken).PostAsync($"/api/patients/{patientId}/portal-invitation", null);
        res.EnsureSuccessStatusCode();
        var body = Email.Sent[before].Body;
        return System.Net.WebUtility.UrlDecode(Regex.Match(body, @"token=([^&""]+)").Groups[1].Value);
    }

    public async Task<HttpResponseMessage> AcceptAsync(string token, string email, string password = Password) =>
        await CreateClient().PostAsJsonAsync("/api/auth/accept-invitation",
            new { token, email, password, confirmPassword = password });

    /// <summary>Full happy path: invite + accept. Returns the patient's auth.</summary>
    public async Task<AuthResult> OnboardPatientAsync(string doctorToken, int patientId, string email)
    {
        var token = await InviteAsync(doctorToken, patientId);
        var res = await AcceptAsync(token, email);
        res.EnsureSuccessStatusCode();
        return await ReadAuth(res);
    }

    public static async Task<AuthResult> ReadAuth(HttpResponseMessage res)
    {
        var j = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var user = j.GetProperty("user");
        return new AuthResult(j.GetProperty("token").GetString()!, user.GetProperty("id").GetString()!,
            user.GetProperty("role").GetString()!);
    }

    public async Task SeedClinicalDataAsync(int patientId, string doctorUserId, string tag) =>
        await WithDbAsync(async db =>
        {
            db.Appointments.Add(new Appointment { PatientId = patientId, DoctorId = doctorUserId,
                ScheduledAt = DateTime.UtcNow.AddDays(2), Type = AppointmentType.FollowUp,
                Status = AppointmentStatus.Confirmed, Reason = tag });
            db.Prescriptions.Add(new Prescription { PatientId = patientId, DoctorId = doctorUserId,
                DrugName = tag, Dosage = "1", Frequency = "daily",
                IssuedDate = DateOnly.FromDateTime(DateTime.UtcNow), ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)) });
            db.Invoices.Add(new Invoice { PatientId = patientId, DoctorId = doctorUserId,
                InvoiceNumber = "INV-" + tag, ServiceDescription = tag, Amount = 10m });
            await db.SaveChangesAsync();
            return 0;
        });
}
