using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MedFlow.Api.Tests;

public class ClinicalListTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _f;
    public ClinicalListTests(TestApiFactory f) => _f = f;

    private static string Url(int pid, string tail = "") => $"/api/patients/{pid}/clinical{tail}";

    private async Task<(AuthResult doctor, int pid)> SetupAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var doctor = await _f.RegisterDoctorAsync($"doc-{tag}@x.com");
        return (doctor, await _f.CreatePatientAsync(doctor.Token, $"pat-{tag}@x.com"));
    }

    private static object Allergy(string substance = "Penicillin", string severity = "Severe") =>
        new { substance, reaction = "Rash", severity };
    private static object Problem(string code = "E11.9", string status = "Active") =>
        new { description = "Type 2 diabetes", icd10Code = code, status };
    private static object Med(string name = "Metformin") =>
        new { name, dosage = "500 mg", frequency = "BID" };

    private async Task<int> CreateAsync(string token, int pid, string kind, object body)
    {
        var res = await _f.ClientFor(token).PostAsJsonAsync(Url(pid, "/" + kind), body);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Crud_for_all_three_lists_and_summary()
    {
        var (d, pid) = await SetupAsync();
        var c = _f.ClientFor(d.Token);

        var aId = await CreateAsync(d.Token, pid, "allergies", Allergy());
        var pId = await CreateAsync(d.Token, pid, "problems", Problem("e11.9"));
        var mId = await CreateAsync(d.Token, pid, "medications", Med());

        var s = await c.GetFromJsonAsync<JsonElement>(Url(pid));
        Assert.Equal("Severe", s.GetProperty("allergies")[0].GetProperty("severity").GetString());
        Assert.Equal("E11.9", s.GetProperty("problems")[0].GetProperty("icd10Code").GetString()); // normalised
        Assert.Equal("Active", s.GetProperty("problems")[0].GetProperty("status").GetString());
        Assert.Equal("Metformin", s.GetProperty("medications")[0].GetProperty("name").GetString());

        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync(Url(pid, $"/allergies/{aId}"), Allergy("Penicillin", "Mild"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync(Url(pid, $"/problems/{pId}"), Problem("E11.9", "Resolved"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync(Url(pid, $"/medications/{mId}"), Med("Metformin XR"))).StatusCode);
        s = await c.GetFromJsonAsync<JsonElement>(Url(pid));
        Assert.Equal("Mild", s.GetProperty("allergies")[0].GetProperty("severity").GetString());
        Assert.Equal("Resolved", s.GetProperty("problems")[0].GetProperty("status").GetString());
        Assert.Equal("Metformin XR", s.GetProperty("medications")[0].GetProperty("name").GetString());

        foreach (var (kind, id) in new[] { ("allergies", aId), ("problems", pId), ("medications", mId) })
            Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync(Url(pid, $"/{kind}/{id}"))).StatusCode);
        s = await c.GetFromJsonAsync<JsonElement>(Url(pid));
        Assert.Equal(0, s.GetProperty("allergies").GetArrayLength() + s.GetProperty("problems").GetArrayLength() + s.GetProperty("medications").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await c.DeleteAsync(Url(pid, $"/allergies/{aId}"))).StatusCode);
    }

    [Theory]
    [InlineData("ZZZ")]
    [InlineData("E1")]
    [InlineData("11.9")]
    [InlineData("E11.123456")]
    [InlineData("")]
    public async Task Invalid_icd10_is_rejected_and_nothing_saved(string code)
    {
        var (d, pid) = await SetupAsync();
        var res = await _f.ClientFor(d.Token).PostAsJsonAsync(Url(pid, "/problems"), Problem(code));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var s = await _f.ClientFor(d.Token).GetFromJsonAsync<JsonElement>(Url(pid));
        Assert.Equal(0, s.GetProperty("problems").GetArrayLength());
    }

    [Fact]
    public async Task Validation_limits_are_enforced()
    {
        var (d, pid) = await SetupAsync();
        var c = _f.ClientFor(d.Token);
        var bad = new (string kind, object body)[]
        {
            ("allergies", new { substance = new string('x', 201), severity = "Mild" }),
            ("allergies", new { substance = "   ", severity = "Mild" }),
            ("allergies", new { substance = "Latex" }),                          // severity missing
            ("allergies", new { substance = "Latex", severity = "Catastrophic" }),
            ("allergies", new { substance = "Latex", reaction = new string('r', 501), severity = "Mild" }),
            ("problems", new { description = "", icd10Code = "E11.9", status = "Active" }),
            ("problems", new { description = "x", icd10Code = "E11.9" }),         // status missing
            ("problems", new { description = "x", icd10Code = "E11.9", status = "Active", onsetDate = "2999-01-01" }),
            ("medications", new { name = new string('m', 201) }),
            ("medications", new { name = "Aspirin", dosage = new string('d', 101) }),
            ("medications", new { name = "Aspirin", notes = new string('n', 501) }),
        };
        foreach (var (kind, body) in bad)
            Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Url(pid, "/" + kind), body)).StatusCode);
    }

    [Fact]
    public async Task Duplicate_allergy_is_conflict_case_insensitive_and_cap_is_enforced()
    {
        var (d, pid) = await SetupAsync();
        var c = _f.ClientFor(d.Token);
        await CreateAsync(d.Token, pid, "allergies", Allergy("Penicillin"));
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(Url(pid, "/allergies"), Allergy("  penicillin "))).StatusCode);

        for (var i = 0; i < 100; i++) await CreateAsync(d.Token, pid, "medications", Med("M" + i));
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Url(pid, "/medications"), Med("one too many"))).StatusCode);
    }

    [Fact]
    public async Task Other_doctor_gets_404_for_everything()
    {
        var (owner, pid) = await SetupAsync();
        var aId = await CreateAsync(owner.Token, pid, "allergies", Allergy());
        var pId = await CreateAsync(owner.Token, pid, "problems", Problem());
        var mId = await CreateAsync(owner.Token, pid, "medications", Med());
        var other = _f.ClientFor((await _f.RegisterDoctorAsync($"other-{Guid.NewGuid():N}@x.com")).Token);

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(Url(pid))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync(Url(pid, "/allergies"), Allergy("Latex"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync(Url(pid, "/problems"), Problem())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync(Url(pid, "/medications"), Med())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync(Url(pid, $"/allergies/{aId}"), Allergy("X"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync(Url(pid, $"/problems/{pId}"), Problem())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync(Url(pid, $"/medications/{mId}"), Med("X"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync(Url(pid, $"/allergies/{aId}"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync(Url(pid, $"/problems/{pId}"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync(Url(pid, $"/medications/{mId}"))).StatusCode);

        // owner's data is intact
        var s = await _f.ClientFor(owner.Token).GetFromJsonAsync<JsonElement>(Url(pid));
        Assert.Equal(1, s.GetProperty("allergies").GetArrayLength());
    }

    [Fact]
    public async Task Entry_id_from_another_patient_of_same_doctor_is_404()
    {
        var (d, pidA) = await SetupAsync();
        var pidB = await _f.CreatePatientAsync(d.Token, $"b-{Guid.NewGuid():N}@x.com");
        var aId = await CreateAsync(d.Token, pidA, "allergies", Allergy());
        var c = _f.ClientFor(d.Token);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PutAsJsonAsync(Url(pidB, $"/allergies/{aId}"), Allergy("X"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.DeleteAsync(Url(pidB, $"/allergies/{aId}"))).StatusCode);
    }

    [Fact]
    public async Task Deleted_or_unknown_patient_is_404()
    {
        var (d, pid) = await SetupAsync();
        var c = _f.ClientFor(d.Token);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(Url(999999))).StatusCode);
        await CreateAsync(d.Token, pid, "allergies", Allergy());
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/patients/{pid}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(Url(pid))).StatusCode);
    }

    [Fact]
    public async Task Patient_token_is_forbidden_and_anonymous_unauthorized_and_portal_has_no_clinical_data()
    {
        var (d, pid) = await SetupAsync();
        await CreateAsync(d.Token, pid, "allergies", Allergy("Peanuts-secret"));
        await CreateAsync(d.Token, pid, "problems", Problem());
        var email = (await _f.ClientFor(d.Token).GetFromJsonAsync<JsonElement>($"/api/patients/{pid}")).GetProperty("email").GetString()!;
        var patient = await _f.OnboardPatientAsync(d.Token, pid, email);
        var pc = _f.ClientFor(patient.Token);

        Assert.Equal(HttpStatusCode.Forbidden, (await pc.GetAsync(Url(pid))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pc.PostAsJsonAsync(Url(pid, "/allergies"), Allergy("Latex"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pc.PutAsJsonAsync(Url(pid, "/allergies/1"), Allergy("Latex"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pc.DeleteAsync(Url(pid, "/allergies/1"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.ClientFor().GetAsync(Url(pid))).StatusCode);

        foreach (var path in new[] { "/api/portal/me", "/api/portal/appointments", "/api/portal/prescriptions", "/api/portal/notes" })
        {
            var body = await pc.GetStringAsync(path);
            Assert.DoesNotContain("Peanuts-secret", body);
            Assert.DoesNotContain("E11.9", body);
        }
    }

    [Fact]
    public async Task Existing_free_text_patient_fields_are_never_modified()
    {
        var (d, _) = await SetupAsync();
        var c = _f.ClientFor(d.Token);
        var create = await c.PostAsJsonAsync("/api/patients", new
        {
            firstName = "Free", lastName = "Text", dateOfBirth = "1980-02-02", gender = "Female", bloodType = "APos",
            email = $"ft-{Guid.NewGuid():N}@x.com", phone = "1", allergies = "legacy: nuts", notes = "my notes", primaryCondition = "asthma"
        });
        var pid = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var aId = await CreateAsync(d.Token, pid, "allergies", Allergy("Shellfish"));
        await CreateAsync(d.Token, pid, "problems", Problem());
        await c.PutAsJsonAsync(Url(pid, $"/allergies/{aId}"), Allergy("Shellfish", "Moderate"));
        await c.DeleteAsync(Url(pid, $"/allergies/{aId}"));

        var p = await c.GetFromJsonAsync<JsonElement>($"/api/patients/{pid}");
        Assert.Equal("legacy: nuts", p.GetProperty("allergies").GetString());
        Assert.Equal("my notes", p.GetProperty("notes").GetString());
        Assert.Equal("asthma", p.GetProperty("primaryCondition").GetString());
    }
}
