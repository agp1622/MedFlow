using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MedFlow.Api.Tests;

public class NoteTemplateTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _f;
    public NoteTemplateTests(TestApiFactory f) => _f = f;

    private Task<HttpResponseMessage> Create(string token, string name, string body = "Body") =>
        _f.ClientFor(token).PostAsJsonAsync("/api/notetemplates", new { name, body });

    private static async Task<int> IdOf(HttpResponseMessage res) =>
        (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

    [Fact]
    public async Task Doctor_can_create_list_edit_and_delete_templates()
    {
        var d = await _f.RegisterDoctorAsync("tpl-crud@x.com");
        var res = await Create(d.Token, "  Cardio follow-up ", "S:\nO:");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var id = await IdOf(res);

        var list = await _f.ClientFor(d.Token).GetFromJsonAsync<JsonElement>("/api/notetemplates");
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
        Assert.Equal("Cardio follow-up", list.GetProperty("items")[0].GetProperty("name").GetString());

        var put = await _f.ClientFor(d.Token).PutAsJsonAsync($"/api/notetemplates/{id}", new { name = "Cardio v2", body = "New" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var again = await _f.ClientFor(d.Token).GetFromJsonAsync<JsonElement>("/api/notetemplates");
        Assert.Equal("New", again.GetProperty("items")[0].GetProperty("body").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await _f.ClientFor(d.Token).DeleteAsync($"/api/notetemplates/{id}")).StatusCode);
        var empty = await _f.ClientFor(d.Token).GetFromJsonAsync<JsonElement>("/api/notetemplates");
        Assert.Equal(0, empty.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Builtin_soap_template_is_available_and_name_is_reserved()
    {
        var d = await _f.RegisterDoctorAsync("tpl-builtin@x.com");
        var b = await _f.ClientFor(d.Token).GetFromJsonAsync<JsonElement>("/api/notetemplates/builtin");
        Assert.True(b[0].GetProperty("isBuiltIn").GetBoolean());
        Assert.Contains("Assessment", b[0].GetProperty("body").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await Create(d.Token, "soap")).StatusCode);
    }

    [Fact]
    public async Task Validation_limits_and_duplicates_are_rejected()
    {
        var d = await _f.RegisterDoctorAsync("tpl-valid@x.com");
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(d.Token, "  ")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(d.Token, "ok", "   ")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(d.Token, new string('n', 101))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Create(d.Token, "big", new string('b', 5001))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Create(d.Token, new string('n', 100), new string('b', 5000))).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await Create(d.Token, "Dup")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Create(d.Token, "dUP")).StatusCode);
        var other = await IdOf(await Create(d.Token, "Other"));
        var clash = await _f.ClientFor(d.Token).PutAsJsonAsync($"/api/notetemplates/{other}", new { name = "dup", body = "x" });
        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
    }

    [Fact]
    public async Task Templates_are_private_to_their_doctor()
    {
        var a = await _f.RegisterDoctorAsync("tpl-a@x.com");
        var b = await _f.RegisterDoctorAsync("tpl-b@x.com");
        var id = await IdOf(await Create(a.Token, "Mine"));

        var list = await _f.ClientFor(b.Token).GetFromJsonAsync<JsonElement>("/api/notetemplates");
        Assert.Equal(0, list.GetProperty("totalCount").GetInt32());
        var put = await _f.ClientFor(b.Token).PutAsJsonAsync($"/api/notetemplates/{id}", new { name = "Hijack", body = "x" });
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _f.ClientFor(b.Token).DeleteAsync($"/api/notetemplates/{id}")).StatusCode);

        // still intact for the owner, and the name is free for the other doctor
        var mine = await _f.ClientFor(a.Token).GetFromJsonAsync<JsonElement>("/api/notetemplates");
        Assert.Equal("Mine", mine.GetProperty("items")[0].GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.OK, (await Create(b.Token, "Mine")).StatusCode);
    }

    [Fact]
    public async Task Patients_and_anonymous_callers_are_rejected()
    {
        var d = await _f.RegisterDoctorAsync("tpl-role@x.com");
        var pid = await _f.CreatePatientAsync(d.Token, "tpl-role-p@x.com");
        var p = await _f.OnboardPatientAsync(d.Token, pid, "tpl-role-p@x.com");
        var pc = _f.ClientFor(p.Token);

        Assert.Equal(HttpStatusCode.Forbidden, (await pc.GetAsync("/api/notetemplates")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pc.GetAsync("/api/notetemplates/builtin")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Create(p.Token, "x")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pc.GetAsync($"/api/medicalnotes/patient/{pid}/latest")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.ClientFor().GetAsync("/api/notetemplates")).StatusCode);
    }

    [Fact]
    public async Task Note_written_from_template_text_is_an_ordinary_unshared_note()
    {
        var d = await _f.RegisterDoctorAsync("tpl-note@x.com");
        var pid = await _f.CreatePatientAsync(d.Token, "tpl-note-p@x.com");
        var patient = await _f.OnboardPatientAsync(d.Token, pid, "tpl-note-p@x.com");
        var res = await _f.ClientFor(d.Token).PostAsJsonAsync("/api/medicalnotes",
            new { patientId = pid, content = "Subjective:\nx", visitType = "Follow-up" });
        res.EnsureSuccessStatusCode();
        Assert.False((await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sharedWithPatient").GetBoolean());
        Assert.Equal("[]", await _f.ClientFor(patient.Token).GetStringAsync("/api/portal/notes"));
    }

    [Fact]
    public async Task Copy_forward_returns_only_own_latest_note_for_that_patient()
    {
        var a = await _f.RegisterDoctorAsync("cf-a@x.com");
        var b = await _f.RegisterDoctorAsync("cf-b@x.com");
        var p1 = await _f.CreatePatientAsync(a.Token, "cf-p1@x.com");
        var p2 = await _f.CreatePatientAsync(a.Token, "cf-p2@x.com");
        var ca = _f.ClientFor(a.Token);

        Assert.Equal(HttpStatusCode.NotFound, (await ca.GetAsync($"/api/medicalnotes/patient/{p1}/latest")).StatusCode);

        async Task Add(HttpClient c, int pid, string text) =>
            (await c.PostAsJsonAsync("/api/medicalnotes", new { patientId = pid, content = text, visitType = "V" })).EnsureSuccessStatusCode();
        await Add(ca, p1, "old");
        await Add(ca, p1, "newest");
        await Add(ca, p2, "other patient");
        await Add(_f.ClientFor(b.Token), p1, "other doctor, same patient");

        var latest = await ca.GetFromJsonAsync<JsonElement>($"/api/medicalnotes/patient/{p1}/latest");
        Assert.Equal("newest", latest.GetProperty("content").GetString());

        // doctor B sees only their own note; doctor with no own notes / unknown patient gets 404
        var bl = await _f.ClientFor(b.Token).GetFromJsonAsync<JsonElement>($"/api/medicalnotes/patient/{p1}/latest");
        Assert.Equal("other doctor, same patient", bl.GetProperty("content").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await _f.ClientFor(b.Token).GetAsync($"/api/medicalnotes/patient/{p2}/latest")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ca.GetAsync("/api/medicalnotes/patient/999999/latest")).StatusCode);
    }
}
