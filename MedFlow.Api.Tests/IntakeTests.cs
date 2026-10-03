using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Api.Tests;

public class IntakeTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _f;
    public IntakeTests(TestApiFactory f) => _f = f;

    private async Task<string> SendLinkAsync(string doctorToken, int patientId)
    {
        var before = _f.Email.Sent.Count;
        var res = await _f.ClientFor(doctorToken).PostAsync($"/api/patients/{patientId}/intake-link", null);
        res.EnsureSuccessStatusCode();
        var body = _f.Email.Sent[before].Body;
        return Regex.Match(body, @"/intake/([0-9A-F]+)").Groups[1].Value;
    }

    private static object Form(string? consent = "yes", string? signature = "Pat Ient", Dictionary<string, object?>? over = null)
    {
        var d = new Dictionary<string, object?>
        {
            ["firstName"] = "Patricia", ["lastName"] = "Ient", ["dateOfBirth"] = "1985-05-05", ["gender"] = "Female",
            ["phone"] = "555-1234", ["address"] = "1 Main St", ["city"] = "Town", ["state"] = "TS", ["zipCode"] = "12345",
            ["insuranceProvider"] = "Acme", ["insurancePolicyNumber"] = "P-1",
            ["primaryCondition"] = "Asthma", ["allergies"] = "Penicillin",
            ["currentMedications"] = "Salbutamol", ["pastHistory"] = "Appendectomy 2010",
            ["consentAgreed"] = consent != null, ["signatureName"] = signature
        };
        if (over != null) foreach (var kv in over) d[kv.Key] = kv.Value;
        return d;
    }

    private async Task AssertInvalidLink(HttpResponseMessage res)
    {
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal("This link is invalid or has expired.",
            (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
    }

    private async Task<int> SubmissionIdAsync(int patientId) =>
        await _f.WithDbAsync(db => db.IntakeSubmissions.Where(s => s.PatientId == patientId).Select(s => s.Id).FirstAsync());

    [Fact]
    public async Task Send_link_rejects_missing_email_other_doctor_inactive_patient_and_patient_role()
    {
        var d1 = await _f.RegisterDoctorAsync("d1-in@x.com");
        var d2 = await _f.RegisterDoctorAsync("d2-in@x.com");
        var noEmail = await _f.CreatePatientAsync(d1.Token, "");
        var pid = await _f.CreatePatientAsync(d1.Token, "p-in1@x.com");

        Assert.Equal(HttpStatusCode.BadRequest, (await _f.ClientFor(d1.Token).PostAsync($"/api/patients/{noEmail}/intake-link", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _f.ClientFor(d2.Token).PostAsync($"/api/patients/{pid}/intake-link", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.ClientFor().PostAsync($"/api/patients/{pid}/intake-link", null)).StatusCode);

        var patient = await _f.OnboardPatientAsync(d1.Token, pid, "p-in1@x.com");
        Assert.Equal(HttpStatusCode.Forbidden, (await _f.ClientFor(patient.Token).PostAsync($"/api/patients/{pid}/intake-link", null)).StatusCode);

        await _f.WithDbAsync(async db =>
        {
            (await db.Patients.FirstAsync(x => x.Id == pid)).Status = MedFlow.Core.Enums.PatientStatus.Inactive;
            return await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await _f.ClientFor(d1.Token).PostAsync($"/api/patients/{pid}/intake-link", null)).StatusCode);
    }

    [Fact]
    public async Task Submitting_stores_a_pending_submission_with_consent_and_leaves_the_record_unchanged()
    {
        var doc = await _f.RegisterDoctorAsync("d-sub@x.com");
        var pid = await _f.CreatePatientAsync(doc.Token, "p-sub@x.com", "Orig");
        var token = await SendLinkAsync(doc.Token, pid);
        var anon = _f.ClientFor();

        var info = await (await anon.GetAsync($"/api/intake/{token}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Orig", info.GetProperty("firstName").GetString());
        Assert.False(string.IsNullOrEmpty(info.GetProperty("consentText").GetString()));

        var before = DateTime.UtcNow.AddSeconds(-2);
        Assert.Equal(HttpStatusCode.Created, (await anon.PostAsJsonAsync($"/api/intake/{token}", Form())).StatusCode);

        var sub = await _f.WithDbAsync(db => db.IntakeSubmissions.FirstAsync(s => s.PatientId == pid));
        Assert.Equal(MedFlow.Core.Enums.IntakeStatus.Pending, sub.Status);
        Assert.Equal("Pat Ient", sub.SignatureName);
        Assert.True(sub.ConsentAgreed);
        Assert.Equal(MedFlow.Core.IntakeConsent.Version, sub.ConsentVersion);
        Assert.True(sub.SignedAt >= before && sub.SignedAt <= DateTime.UtcNow.AddSeconds(2));

        var patient = await _f.WithDbAsync(db => db.Patients.FirstAsync(p => p.Id == pid));
        Assert.Equal("Orig", patient.FirstName);
        Assert.Null(patient.Allergies);

        // single use
        await AssertInvalidLink(await anon.PostAsJsonAsync($"/api/intake/{token}", Form()));
        await AssertInvalidLink(await anon.GetAsync($"/api/intake/{token}"));
    }

    [Fact]
    public async Task Validation_requires_consent_and_signature_and_enforces_limits()
    {
        var doc = await _f.RegisterDoctorAsync("d-val@x.com");
        var pid = await _f.CreatePatientAsync(doc.Token, "p-val@x.com");
        var token = await SendLinkAsync(doc.Token, pid);
        var anon = _f.ClientFor();

        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsJsonAsync($"/api/intake/{token}", Form(consent: null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsJsonAsync($"/api/intake/{token}", Form(signature: "  "))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsJsonAsync($"/api/intake/{token}",
            Form(over: new() { ["allergies"] = new string('a', 1001) }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsJsonAsync($"/api/intake/{token}",
            Form(over: new() { ["currentMedications"] = new string('a', 2001) }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsJsonAsync($"/api/intake/{token}",
            Form(over: new() { ["dateOfBirth"] = "2999-01-01" }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsJsonAsync($"/api/intake/{token}",
            Form(over: new() { ["firstName"] = "" }))).StatusCode);

        Assert.Empty(await _f.WithDbAsync(db => db.IntakeSubmissions.Where(s => s.PatientId == pid).ToListAsync()));
        // the failed attempts did not burn the link
        Assert.Equal(HttpStatusCode.Created, (await anon.PostAsJsonAsync($"/api/intake/{token}", Form())).StatusCode);
    }

    [Fact]
    public async Task Unknown_expired_superseded_and_inactive_links_fail_identically()
    {
        var doc = await _f.RegisterDoctorAsync("d-inv2@x.com");
        var pid = await _f.CreatePatientAsync(doc.Token, "p-inv2@x.com");
        var anon = _f.ClientFor();

        await AssertInvalidLink(await anon.GetAsync("/api/intake/not-a-token"));
        await AssertInvalidLink(await anon.PostAsJsonAsync("/api/intake/not-a-token", Form()));

        var first = await SendLinkAsync(doc.Token, pid);
        var second = await SendLinkAsync(doc.Token, pid); // supersedes first
        await AssertInvalidLink(await anon.PostAsJsonAsync($"/api/intake/{first}", Form()));

        await _f.WithDbAsync(async db =>
        {
            foreach (var l in db.IntakeLinks.Where(l => l.PatientId == pid && l.UsedAt == null))
                l.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            return await db.SaveChangesAsync();
        });
        await AssertInvalidLink(await anon.PostAsJsonAsync($"/api/intake/{second}", Form()));

        var third = await SendLinkAsync(doc.Token, pid);
        await _f.WithDbAsync(async db =>
        {
            (await db.Patients.FirstAsync(x => x.Id == pid)).Status = MedFlow.Core.Enums.PatientStatus.Inactive;
            return await db.SaveChangesAsync();
        });
        await AssertInvalidLink(await anon.PostAsJsonAsync($"/api/intake/{third}", Form()));
    }

    [Fact]
    public async Task Accept_applies_answers_and_appends_notes_without_overwriting()
    {
        var doc = await _f.RegisterDoctorAsync("d-acc@x.com");
        var pid = await _f.CreatePatientAsync(doc.Token, "p-acc@x.com");
        await _f.WithDbAsync(async db =>
        {
            (await db.Patients.FirstAsync(x => x.Id == pid)).Notes = "Existing note";
            return await db.SaveChangesAsync();
        });
        var token = await SendLinkAsync(doc.Token, pid);
        await _f.ClientFor().PostAsJsonAsync($"/api/intake/{token}", Form());
        var id = await SubmissionIdAsync(pid);
        var client = _f.ClientFor(doc.Token);

        var detail = await (await client.GetAsync($"/api/intake-submissions/{id}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", detail.GetProperty("status").GetString());
        Assert.Equal("Pat Ient", detail.GetProperty("consent").GetProperty("signatureName").GetString());
        Assert.Equal("Penicillin", detail.GetProperty("answers").GetProperty("allergies").GetString());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("current").GetProperty("allergies").ValueKind);

        var pending = await (await client.GetAsync("/api/intake-submissions?status=Pending")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(pending.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetInt32() == id);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/intake-submissions/{id}/accept", null)).StatusCode);
        var p = await _f.WithDbAsync(db => db.Patients.FirstAsync(x => x.Id == pid));
        Assert.Equal("Patricia", p.FirstName);
        Assert.Equal("Penicillin", p.Allergies);
        Assert.Equal("Asthma", p.PrimaryCondition);
        Assert.Equal("1 Main St", p.Address);
        Assert.StartsWith("Existing note", p.Notes);
        Assert.Contains("Salbutamol", p.Notes);
        Assert.Contains("Appendectomy 2010", p.Notes);

        // decisions are final
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/intake-submissions/{id}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/intake-submissions/{id}/reject", new { reason = "x" })).StatusCode);
    }

    [Fact]
    public async Task Reject_leaves_the_record_unchanged_and_is_final()
    {
        var doc = await _f.RegisterDoctorAsync("d-rej@x.com");
        var pid = await _f.CreatePatientAsync(doc.Token, "p-rej@x.com", "Keep");
        var token = await SendLinkAsync(doc.Token, pid);
        await _f.ClientFor().PostAsJsonAsync($"/api/intake/{token}", Form());
        var id = await SubmissionIdAsync(pid);
        var client = _f.ClientFor(doc.Token);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/intake-submissions/{id}/reject", new { reason = new string('r', 501) })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/intake-submissions/{id}/reject", new { reason = "Incomplete" })).StatusCode);

        var p = await _f.WithDbAsync(db => db.Patients.FirstAsync(x => x.Id == pid));
        Assert.Equal("Keep", p.FirstName);
        Assert.Null(p.Allergies);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/intake-submissions/{id}/accept", null)).StatusCode);
        var sub = await _f.WithDbAsync(db => db.IntakeSubmissions.FirstAsync(s => s.Id == id));
        Assert.Equal(MedFlow.Core.Enums.IntakeStatus.Rejected, sub.Status);
        Assert.Equal("Pat Ient", sub.SignatureName); // consent evidence retained
    }

    [Fact]
    public async Task Review_routes_are_isolated_between_doctors_and_closed_to_patients()
    {
        var d1 = await _f.RegisterDoctorAsync("d1-iso@x.com");
        var d2 = await _f.RegisterDoctorAsync("d2-iso@x.com");
        var pid = await _f.CreatePatientAsync(d1.Token, "p-iso@x.com");
        var token = await SendLinkAsync(d1.Token, pid);
        await _f.ClientFor().PostAsJsonAsync($"/api/intake/{token}", Form());
        var id = await SubmissionIdAsync(pid);

        var other = _f.ClientFor(d2.Token);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/intake-submissions/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/intake-submissions/{id}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/intake-submissions/{id}/reject", new { reason = "x" })).StatusCode);
        var list = await (await other.GetAsync("/api/intake-submissions")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, list.GetProperty("totalCount").GetInt32());

        var patient = await _f.OnboardPatientAsync(d1.Token, pid, "p-iso@x.com");
        var pc = _f.ClientFor(patient.Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await pc.GetAsync("/api/intake-submissions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pc.GetAsync($"/api/intake-submissions/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pc.PostAsync($"/api/intake-submissions/{id}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.ClientFor().GetAsync("/api/intake-submissions")).StatusCode);

        // still pending after all the denied attempts
        Assert.Equal(MedFlow.Core.Enums.IntakeStatus.Pending,
            (await _f.WithDbAsync(db => db.IntakeSubmissions.FirstAsync(s => s.Id == id))).Status);
    }

    [Fact]
    public async Task Public_intake_endpoints_are_rate_limited()
    {
        using var limited = _f.WithWebHostBuilder(b => b.UseSetting("RateLimiting:IntakePermitLimit", "3"));
        var client = limited.CreateClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
            statuses.Add((await client.GetAsync("/api/intake/not-a-token")).StatusCode);
        Assert.Equal(new[] { HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.NotFound,
            HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests }, statuses);
    }
}
