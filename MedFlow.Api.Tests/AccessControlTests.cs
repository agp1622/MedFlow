using System.Net;
using System.Net.Http.Json;

namespace MedFlow.Api.Tests;

public class AccessControlTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _f;
    public AccessControlTests(TestApiFactory f) => _f = f;

    public static IEnumerable<object[]> DoctorOnlyGets => new[]
    {
        "/api/patients", "/api/appointments", "/api/prescriptions", "/api/invoices",
        "/api/dashboard", "/api/medicalnotes/patient/1", "/api/attachments/patient/1",
        "/api/vitalsigns/patient/1",
    }.Select(p => new object[] { p });

    private async Task<(AuthResult doctor, AuthResult patient)> SetupAsync(string suffix)
    {
        var doctor = await _f.RegisterDoctorAsync($"doc-{suffix}@x.com");
        var pid = await _f.CreatePatientAsync(doctor.Token, $"pat-{suffix}@x.com");
        var patient = await _f.OnboardPatientAsync(doctor.Token, pid, $"pat-{suffix}@x.com");
        return (doctor, patient);
    }

    [Theory]
    [MemberData(nameof(DoctorOnlyGets))]
    public async Task Patient_token_is_forbidden_on_doctor_endpoints(string path)
    {
        var (_, patient) = await SetupAsync(Guid.NewGuid().ToString("N")[..6]);
        Assert.Equal(HttpStatusCode.Forbidden, (await _f.ClientFor(patient.Token).GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Patient_token_cannot_create_patients()
    {
        var (_, patient) = await SetupAsync("create");
        var res = await _f.ClientFor(patient.Token).PostAsJsonAsync("/api/patients", new
        {
            firstName = "x", lastName = "y", dateOfBirth = "1990-01-01", gender = "Male", bloodType = "OPos",
            email = "z@x.com", phone = "1"
        });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Theory]
    [InlineData("/api/portal/me")]
    [InlineData("/api/portal/appointments")]
    [InlineData("/api/portal/prescriptions")]
    [InlineData("/api/portal/invoices")]
    [InlineData("/api/portal/attachments")]
    [InlineData("/api/portal/notes")]
    [InlineData("/api/portal/attachments/1/download")]
    public async Task Doctor_token_is_forbidden_and_anonymous_is_unauthorized_on_portal(string path)
    {
        var doctor = await _f.RegisterDoctorAsync($"doc-{Guid.NewGuid():N}@x.com");
        Assert.Equal(HttpStatusCode.Forbidden, (await _f.ClientFor(doctor.Token).GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.ClientFor().GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Doctor_registration_and_login_carry_the_clinic_Owner_role()
    {
        var email = $"doc-{Guid.NewGuid():N}@x.com";
        var reg = await _f.RegisterDoctorAsync(email);
        // A self-registered doctor owns their own clinic, so the role shown is Owner
        Assert.Equal("Owner", reg.Role);
        var login = await _f.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password = TestApiFactory.Password });
        Assert.Equal("Owner", (await TestApiFactory.ReadAuth(login)).Role);
        Assert.Equal(HttpStatusCode.OK, (await _f.ClientFor(reg.Token).GetAsync("/api/patients")).StatusCode);
    }

    [Fact]
    public async Task Patient_login_returns_the_Patient_role_and_can_reset_password()
    {
        var (_, patient) = await SetupAsync("recover");
        Assert.Equal("Patient", patient.Role);
        var before = _f.Email.Sent.Count;
        var res = await _f.CreateClient().PostAsJsonAsync("/api/auth/forgot-password", new { email = "pat-recover@x.com" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains(_f.Email.Sent.Skip(before), m => m.To == "pat-recover@x.com" && m.Subject.Contains("Reset"));
    }
}
