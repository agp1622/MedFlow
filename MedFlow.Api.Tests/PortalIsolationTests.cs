using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MedFlow.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Api.Tests;

public class PortalIsolationTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _f;
    public PortalIsolationTests(TestApiFactory f) => _f = f;

    private static async Task<JsonElement> Json(HttpResponseMessage r) =>
        await r.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task Each_patient_sees_only_their_own_records()
    {
        var doctor = await _f.RegisterDoctorAsync("doc-iso@x.com");
        var aId = await _f.CreatePatientAsync(doctor.Token, "a-iso@x.com", "Alice");
        var bId = await _f.CreatePatientAsync(doctor.Token, "b-iso@x.com", "Bob");
        await _f.SeedClinicalDataAsync(aId, doctor.UserId, "ALPHA");
        await _f.SeedClinicalDataAsync(bId, doctor.UserId, "BETA");
        var a = await _f.OnboardPatientAsync(doctor.Token, aId, "a-iso@x.com");
        var b = await _f.OnboardPatientAsync(doctor.Token, bId, "b-iso@x.com");

        foreach (var (who, token, mine, theirs) in new[] { ("A", a.Token, "ALPHA", "BETA"), ("B", b.Token, "BETA", "ALPHA") })
        {
            var c = _f.ClientFor(token);
            var appts = await (await c.GetAsync("/api/portal/appointments")).Content.ReadAsStringAsync();
            var rx = await (await c.GetAsync("/api/portal/prescriptions")).Content.ReadAsStringAsync();
            var inv = await (await c.GetAsync("/api/portal/invoices")).Content.ReadAsStringAsync();
            Assert.True(appts.Contains(mine) && rx.Contains(mine) && inv.Contains(mine), $"{who} should see own data");
            Assert.False(appts.Contains(theirs) || rx.Contains(theirs) || inv.Contains(theirs), $"{who} must not see other data");
        }
    }

    [Fact]
    public async Task Portal_dtos_do_not_expose_internal_fields()
    {
        var doctor = await _f.RegisterDoctorAsync("doc-dto@x.com");
        var pid = await _f.CreatePatientAsync(doctor.Token, "p-dto@x.com");
        await _f.SeedClinicalDataAsync(pid, doctor.UserId, "GAMMA");
        var p = await _f.OnboardPatientAsync(doctor.Token, pid, "p-dto@x.com");
        var body = await (await _f.ClientFor(p.Token).GetAsync("/api/portal/appointments")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("doctorId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("notes", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(PatientStatus.Inactive)]
    [InlineData(PatientStatus.Deceased)]
    public async Task Portal_is_unavailable_for_non_active_patients(PatientStatus status)
    {
        var doctor = await _f.RegisterDoctorAsync($"doc-{status}@x.com");
        var pid = await _f.CreatePatientAsync(doctor.Token, $"p-{status}@x.com");
        var p = await _f.OnboardPatientAsync(doctor.Token, pid, $"p-{status}@x.com");
        Assert.Equal(HttpStatusCode.OK, (await _f.ClientFor(p.Token).GetAsync("/api/portal/appointments")).StatusCode);

        await _f.WithDbAsync(async db =>
        {
            (await db.Patients.FirstAsync(x => x.Id == pid)).Status = status;
            return await db.SaveChangesAsync();
        });
        foreach (var path in new[] { "me", "appointments", "prescriptions", "invoices", "attachments", "notes" })
            Assert.Equal(HttpStatusCode.Forbidden, (await _f.ClientFor(p.Token).GetAsync($"/api/portal/{path}")).StatusCode);
    }

    [Fact]
    public async Task Revoked_portal_access_is_denied_immediately_even_with_a_valid_token()
    {
        var doctor = await _f.RegisterDoctorAsync("doc-rev@x.com");
        var pid = await _f.CreatePatientAsync(doctor.Token, "p-rev@x.com");
        var p = await _f.OnboardPatientAsync(doctor.Token, pid, "p-rev@x.com");
        Assert.Equal(HttpStatusCode.OK, (await _f.ClientFor(p.Token).GetAsync("/api/portal/me")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent,
            (await _f.ClientFor(doctor.Token).DeleteAsync($"/api/patients/{pid}/portal-access")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _f.ClientFor(p.Token).GetAsync("/api/portal/me")).StatusCode);
    }

    [Fact]
    public async Task Patient_role_user_without_a_linked_record_is_forbidden()
    {
        var doctor = await _f.RegisterDoctorAsync("doc-orphan@x.com");
        var pid = await _f.CreatePatientAsync(doctor.Token, "p-orphan@x.com");
        var p = await _f.OnboardPatientAsync(doctor.Token, pid, "p-orphan@x.com");
        await _f.WithDbAsync(async db =>
        {
            (await db.Patients.FirstAsync(x => x.Id == pid)).PortalUserId = null;
            return await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.Forbidden, (await _f.ClientFor(p.Token).GetAsync("/api/portal/me")).StatusCode);
    }
}
