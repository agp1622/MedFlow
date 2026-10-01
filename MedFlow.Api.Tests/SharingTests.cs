using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Api.Tests;

public class SharingTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _f;
    public SharingTests(TestApiFactory f) => _f = f;

    private async Task<int> UploadAsync(string doctorToken, int patientId, string name)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(patientId.ToString()), "patientId" }
        };
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4 test " + name));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", name + ".pdf");
        var res = await _f.ClientFor(doctorToken).PostAsync("/api/attachments", form);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private Task<HttpResponseMessage> Share(string token, string kind, int id, bool shared) =>
        _f.ClientFor(token).PutAsJsonAsync($"/api/{kind}/{id}/sharing", new { shared });

    private async Task<int> AddNoteAsync(string token, int patientId, string content)
    {
        var res = await _f.ClientFor(token).PostAsJsonAsync("/api/medicalnotes",
            new { patientId, content, visitType = "Checkup" });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Attachments_are_hidden_until_shared_and_hidden_again_when_unshared()
    {
        var doctor = await _f.RegisterDoctorAsync("doc-share@x.com");
        var pid = await _f.CreatePatientAsync(doctor.Token, "p-share@x.com");
        var patient = await _f.OnboardPatientAsync(doctor.Token, pid, "p-share@x.com");
        var shared = await UploadAsync(doctor.Token, pid, "shared");
        var hidden = await UploadAsync(doctor.Token, pid, "hidden");
        var pc = _f.ClientFor(patient.Token);

        // default: nothing shared
        Assert.Equal("[]", (await pc.GetStringAsync("/api/portal/attachments")));
        Assert.Equal(HttpStatusCode.NotFound, (await pc.GetAsync($"/api/portal/attachments/{shared}/download")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await Share(doctor.Token, "attachments", shared, true)).StatusCode);
        var list = await pc.GetFromJsonAsync<JsonElement>("/api/portal/attachments");
        Assert.Equal(new[] { shared }, list.EnumerateArray().Select(e => e.GetProperty("id").GetInt32()));
        Assert.Equal(HttpStatusCode.OK, (await pc.GetAsync($"/api/portal/attachments/{shared}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await pc.GetAsync($"/api/portal/attachments/{hidden}/download")).StatusCode);

        // revoke: the previous id stops working at once
        await Share(doctor.Token, "attachments", shared, false);
        Assert.Equal(HttpStatusCode.NotFound, (await pc.GetAsync($"/api/portal/attachments/{shared}/download")).StatusCode);
        Assert.Equal("[]", await pc.GetStringAsync("/api/portal/attachments"));

        // the download that succeeded was audited
        var logs = await _f.WithDbAsync(db => db.PortalAccessLogs.Where(l => l.PatientId == pid).ToListAsync());
        Assert.Contains(logs, l => l.ResourceType == "Attachment" && l.ResourceId == shared && l.Action == "Download");
    }

    [Fact]
    public async Task Another_patients_shared_attachment_is_not_reachable()
    {
        var doctor = await _f.RegisterDoctorAsync("doc-share2@x.com");
        var aId = await _f.CreatePatientAsync(doctor.Token, "a-share2@x.com");
        var bId = await _f.CreatePatientAsync(doctor.Token, "b-share2@x.com");
        var b = await _f.OnboardPatientAsync(doctor.Token, bId, "b-share2@x.com");
        var attA = await UploadAsync(doctor.Token, aId, "a-file");
        await Share(doctor.Token, "attachments", attA, true);
        Assert.Equal(HttpStatusCode.NotFound, (await _f.ClientFor(b.Token).GetAsync($"/api/portal/attachments/{attA}/download")).StatusCode);
    }

    [Fact]
    public async Task Notes_follow_the_sharing_flag_and_views_are_logged()
    {
        var doctor = await _f.RegisterDoctorAsync("doc-note@x.com");
        var pid = await _f.CreatePatientAsync(doctor.Token, "p-note@x.com");
        var patient = await _f.OnboardPatientAsync(doctor.Token, pid, "p-note@x.com");
        var open = await AddNoteAsync(doctor.Token, pid, "visible note");
        await AddNoteAsync(doctor.Token, pid, "private note");
        await Share(doctor.Token, "medicalnotes", open, true);

        var body = await _f.ClientFor(patient.Token).GetStringAsync("/api/portal/notes");
        Assert.Contains("visible note", body);
        Assert.DoesNotContain("private note", body);

        var logs = await _f.WithDbAsync(db => db.PortalAccessLogs.Where(l => l.ResourceType == "Note" && l.PatientId == pid).ToListAsync());
        Assert.Contains(logs, l => l.ResourceId == open && l.Action == "View");

        await Share(doctor.Token, "medicalnotes", open, false);
        Assert.Equal("[]", await _f.ClientFor(patient.Token).GetStringAsync("/api/portal/notes"));
    }

    [Fact]
    public async Task A_doctor_cannot_change_sharing_on_another_doctors_items()
    {
        var d1 = await _f.RegisterDoctorAsync("d1-share@x.com");
        var d2 = await _f.RegisterDoctorAsync("d2-share@x.com");
        var pid = await _f.CreatePatientAsync(d1.Token, "p-own@x.com");
        var att = await UploadAsync(d1.Token, pid, "own");
        var note = await AddNoteAsync(d1.Token, pid, "own note");
        Assert.Equal(HttpStatusCode.NotFound, (await Share(d2.Token, "attachments", att, true)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Share(d2.Token, "medicalnotes", note, true)).StatusCode);
    }
}
