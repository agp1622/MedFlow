using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MedFlow.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Api.Tests;

public class MessagingTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _f;
    public MessagingTests(TestApiFactory f) => _f = f;

    private static int _seq;

    private record Practice(AuthResult Doctor, int PatientId, AuthResult Patient);

    /// <summary>A doctor with one onboarded portal patient.</summary>
    private async Task<Practice> NewPracticeAsync()
    {
        var n = Interlocked.Increment(ref _seq);
        var doctor = await _f.RegisterDoctorAsync($"doc-msg{n}@x.com");
        var patientId = await _f.CreatePatientAsync(doctor.Token, $"pat-msg{n}@x.com", $"Pat{n}");
        var patient = await _f.OnboardPatientAsync(doctor.Token, patientId, $"pat-msg{n}@x.com");
        return new Practice(doctor, patientId, patient);
    }

    private static ByteArrayContent FilePart(byte[] bytes, string contentType)
    {
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return part;
    }

    private static Task<HttpResponseMessage> Send(HttpClient c, string url, string? body,
        params (string Name, string ContentType, byte[] Bytes)[] files)
    {
        var form = new MultipartFormDataContent();
        if (body != null) form.Add(new StringContent(body), "body");
        foreach (var (name, type, bytes) in files) form.Add(FilePart(bytes, type), "files", name);
        return c.PostAsync(url, form);
    }

    private Task<HttpResponseMessage> PatientSend(Practice p, string? body, params (string, string, byte[])[] files) =>
        Send(_f.ClientFor(p.Patient.Token), "/api/portal/messages", body, files);

    private Task<HttpResponseMessage> DoctorSend(Practice p, string? body, params (string, string, byte[])[] files) =>
        Send(_f.ClientFor(p.Doctor.Token), $"/api/messages/patient/{p.PatientId}", body, files);

    private static async Task<JsonElement> Json(HttpResponseMessage r) =>
        await r.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<int> UnreadForPatient(Practice p) =>
        (await Json(await _f.ClientFor(p.Patient.Token).GetAsync("/api/portal/messages/unread-count")))
        .GetProperty("unreadCount").GetInt32();

    private async Task<int> UnreadForDoctor(AuthResult d) =>
        (await Json(await _f.ClientFor(d.Token).GetAsync("/api/messages/unread-count")))
        .GetProperty("unreadCount").GetInt32();

    private async Task<int> MessageCount(int patientId) =>
        await _f.WithDbAsync(db => db.Messages.CountAsync(m =>
            db.MessageThreads.Any(t => t.Id == m.ThreadId && t.PatientId == patientId)));

    private static readonly byte[] Pdf = "%PDF-1.4 test"u8.ToArray();
    private static readonly byte[] Jpg = { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3 };

    // ── US1: patient sends and reads ──────────────────────────────────────────

    [Fact]
    public async Task Patient_messages_are_kept_in_one_thread_in_order()
    {
        var p = await NewPracticeAsync();
        Assert.Equal(HttpStatusCode.Created, (await PatientSend(p, "first")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await PatientSend(p, "second")).StatusCode);

        var thread = await Json(await _f.ClientFor(p.Patient.Token).GetAsync("/api/portal/messages"));
        Assert.Equal(new[] { "first", "second" }, thread.EnumerateArray().Select(m => m.GetProperty("body").GetString()));
        Assert.All(thread.EnumerateArray(), m => Assert.True(m.GetProperty("isMine").GetBoolean()));
        Assert.Equal(1, await _f.WithDbAsync(db => db.MessageThreads.CountAsync(t => t.PatientId == p.PatientId)));
    }

    [Fact]
    public async Task Thread_is_empty_before_any_message()
    {
        var p = await NewPracticeAsync();
        var res = await _f.ClientFor(p.Patient.Token).GetAsync("/api/portal/messages");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(0, (await Json(res)).GetArrayLength());
    }

    [Fact]
    public async Task Request_with_no_fields_at_all_is_rejected()
    {
        var p = await NewPracticeAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await PatientSend(p, null)).StatusCode);
        Assert.Equal(0, await MessageCount(p.PatientId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_message_is_rejected_and_not_stored(string body)
    {
        var p = await NewPracticeAsync();
        var res = await PatientSend(p, body);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("Message cannot be empty.", (await Json(res)).GetProperty("error").GetString());
        Assert.Equal(0, await MessageCount(p.PatientId));
    }

    [Fact]
    public async Task Overlong_message_is_rejected_and_not_stored()
    {
        var p = await NewPracticeAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await PatientSend(p, new string('a', 4001))).StatusCode);
        Assert.Equal(0, await MessageCount(p.PatientId));
        Assert.Equal(HttpStatusCode.Created, (await PatientSend(p, new string('a', 4000))).StatusCode);
    }

    [Fact]
    public async Task Portal_message_routes_require_a_patient_account()
    {
        var p = await NewPracticeAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await _f.ClientFor(p.Doctor.Token).GetAsync("/api/portal/messages")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(_f.ClientFor(p.Doctor.Token), "/api/portal/messages", "hi")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.ClientFor().GetAsync("/api/portal/messages")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.ClientFor().GetAsync("/api/portal/messages/unread-count")).StatusCode);
    }

    [Fact]
    public async Task Deactivated_patient_cannot_read_or_send_but_doctor_keeps_history()
    {
        var p = await NewPracticeAsync();
        await PatientSend(p, "before");
        await _f.WithDbAsync(async db =>
        {
            (await db.Patients.IgnoreQueryFilters().FirstAsync(x => x.Id == p.PatientId)).Status = PatientStatus.Inactive;
            await db.SaveChangesAsync();
            return 0;
        });

        Assert.Equal(HttpStatusCode.Forbidden, (await _f.ClientFor(p.Patient.Token).GetAsync("/api/portal/messages")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PatientSend(p, "after")).StatusCode);
        var history = await Json(await _f.ClientFor(p.Doctor.Token).GetAsync($"/api/messages/patient/{p.PatientId}"));
        Assert.Equal(1, history.GetArrayLength());
    }

    [Fact]
    public async Task Message_dtos_do_not_expose_internal_fields()
    {
        var p = await NewPracticeAsync();
        await PatientSend(p, "hello", ("a.pdf", "application/pdf", Pdf));
        var raw = await (await _f.ClientFor(p.Patient.Token).GetAsync("/api/portal/messages")).Content.ReadAsStringAsync();
        foreach (var leaked in new[] { "senderUserId", "storedFileName", "doctorId", "threadId" })
            Assert.DoesNotContain(leaked, raw, StringComparison.OrdinalIgnoreCase);
    }

    // ── US2: doctor reads and replies ─────────────────────────────────────────

    [Fact]
    public async Task Doctor_reads_patient_message_and_reply_reaches_the_patient()
    {
        var p = await NewPracticeAsync();
        await PatientSend(p, "Question about my dosage");

        var seen = await Json(await _f.ClientFor(p.Doctor.Token).GetAsync($"/api/messages/patient/{p.PatientId}"));
        Assert.Equal("Question about my dosage", seen[0].GetProperty("body").GetString());
        Assert.Equal("Patient", seen[0].GetProperty("senderRole").GetString());
        Assert.False(seen[0].GetProperty("isMine").GetBoolean());

        Assert.Equal(HttpStatusCode.Created, (await DoctorSend(p, "Take it with food")).StatusCode);

        var patientView = await Json(await _f.ClientFor(p.Patient.Token).GetAsync("/api/portal/messages"));
        Assert.Equal(2, patientView.GetArrayLength());
        Assert.Equal("Doctor", patientView[1].GetProperty("senderRole").GetString());
        Assert.False(patientView[1].GetProperty("isMine").GetBoolean());
    }

    [Fact]
    public async Task Another_doctor_gets_404_on_every_thread_route()
    {
        var p = await NewPracticeAsync();
        await PatientSend(p, "private");
        var other = await _f.RegisterDoctorAsync($"doc-msg-other{Interlocked.Increment(ref _seq)}@x.com");
        var c = _f.ClientFor(other.Token);

        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/messages/patient/{p.PatientId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(c, $"/api/messages/patient/{p.PatientId}", "intruder")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsync($"/api/messages/patient/{p.PatientId}/read", null)).StatusCode);
        Assert.Equal(1, await MessageCount(p.PatientId));
    }

    [Fact]
    public async Task Patient_token_cannot_use_doctor_message_routes()
    {
        var p = await NewPracticeAsync();
        var c = _f.ClientFor(p.Patient.Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/messages/threads")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync($"/api/messages/patient/{p.PatientId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(c, $"/api/messages/patient/{p.PatientId}", "x")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/messages/unread-count")).StatusCode);
    }

    [Fact]
    public async Task Each_patient_sees_only_their_own_thread()
    {
        var a = await NewPracticeAsync();
        var b = await NewPracticeAsync();
        await PatientSend(a, "ALPHA-secret");
        await PatientSend(b, "BETA-secret");

        var aView = await (await _f.ClientFor(a.Patient.Token).GetAsync("/api/portal/messages")).Content.ReadAsStringAsync();
        Assert.Contains("ALPHA-secret", aView);
        Assert.DoesNotContain("BETA-secret", aView);
    }

    [Fact]
    public async Task Thread_list_is_scoped_to_the_doctor_and_ordered_by_last_activity()
    {
        var doctor = await _f.RegisterDoctorAsync($"doc-list{Interlocked.Increment(ref _seq)}@x.com");
        var ids = new List<(int Id, AuthResult Auth)>();
        foreach (var name in new[] { "L1", "L2" })
        {
            var email = $"{name.ToLower()}-{Interlocked.Increment(ref _seq)}@x.com";
            var id = await _f.CreatePatientAsync(doctor.Token, email, name);
            ids.Add((id, await _f.OnboardPatientAsync(doctor.Token, id, email)));
        }
        var stranger = await NewPracticeAsync();
        await PatientSend(stranger, "not yours");

        await Send(_f.ClientFor(ids[0].Auth.Token), "/api/portal/messages", "from L1");
        await Task.Delay(20);
        await Send(_f.ClientFor(ids[1].Auth.Token), "/api/portal/messages", "from L2");
        await Task.Delay(20);
        await Send(_f.ClientFor(ids[0].Auth.Token), "/api/portal/messages", "L1 again");

        var threads = await Json(await _f.ClientFor(doctor.Token).GetAsync("/api/messages/threads"));
        Assert.Equal(new[] { ids[0].Id, ids[1].Id }, threads.EnumerateArray().Select(t => t.GetProperty("patientId").GetInt32()));
        Assert.Equal("L1 again", threads[0].GetProperty("lastMessagePreview").GetString());
        Assert.Equal(2, threads[0].GetProperty("unreadCount").GetInt32());
    }

    // ── US3: unread counts ────────────────────────────────────────────────────

    [Fact]
    public async Task Messages_raise_only_the_other_partys_unread_count()
    {
        var p = await NewPracticeAsync();
        await PatientSend(p, "q1");
        await PatientSend(p, "q2");
        Assert.Equal(2, await UnreadForDoctor(p.Doctor));
        Assert.Equal(0, await UnreadForPatient(p));          // own messages never count

        await DoctorSend(p, "answer");
        Assert.Equal(1, await UnreadForPatient(p));
        Assert.Equal(2, await UnreadForDoctor(p.Doctor));
    }

    [Fact]
    public async Task Opening_the_thread_marks_read_only_for_the_viewer()
    {
        var p = await NewPracticeAsync();
        await PatientSend(p, "q");
        await DoctorSend(p, "a");

        Assert.Equal(HttpStatusCode.NoContent, (await _f.ClientFor(p.Doctor.Token).PostAsync($"/api/messages/patient/{p.PatientId}/read", null)).StatusCode);
        Assert.Equal(0, await UnreadForDoctor(p.Doctor));
        Assert.Equal(1, await UnreadForPatient(p));          // the patient has not opened it yet

        Assert.Equal(HttpStatusCode.NoContent, (await _f.ClientFor(p.Patient.Token).PostAsync("/api/portal/messages/read", null)).StatusCode);
        Assert.Equal(0, await UnreadForPatient(p));
    }

    [Fact]
    public async Task Reading_the_thread_with_GET_does_not_change_the_count()
    {
        var p = await NewPracticeAsync();
        await PatientSend(p, "q");
        await _f.ClientFor(p.Doctor.Token).GetAsync($"/api/messages/patient/{p.PatientId}");
        await _f.ClientFor(p.Doctor.Token).GetAsync("/api/messages/threads");
        Assert.Equal(1, await UnreadForDoctor(p.Doctor));
    }

    [Fact]
    public async Task Unread_counts_are_isolated_between_patients_and_doctors()
    {
        var a = await NewPracticeAsync();
        var b = await NewPracticeAsync();
        await PatientSend(a, "for doctor A");
        await DoctorSend(a, "for patient A");

        Assert.Equal(0, await UnreadForDoctor(b.Doctor));
        Assert.Equal(0, await UnreadForPatient(b));
        Assert.Equal(1, await UnreadForDoctor(a.Doctor));
        Assert.Equal(1, await UnreadForPatient(a));
    }

    [Fact]
    public async Task Doctor_count_spans_all_of_their_patients()
    {
        var doctor = await _f.RegisterDoctorAsync($"doc-multi{Interlocked.Increment(ref _seq)}@x.com");
        for (var i = 0; i < 3; i++)
        {
            var email = $"multi{i}-{Interlocked.Increment(ref _seq)}@x.com";
            var id = await _f.CreatePatientAsync(doctor.Token, email, $"M{i}");
            var auth = await _f.OnboardPatientAsync(doctor.Token, id, email);
            await Send(_f.ClientFor(auth.Token), "/api/portal/messages", $"hello {i}");
        }
        Assert.Equal(3, await UnreadForDoctor(doctor));
    }

    // ── US4: attachments ──────────────────────────────────────────────────────

    [Fact]
    public async Task Patient_attachments_can_be_downloaded_by_the_doctor()
    {
        var p = await NewPracticeAsync();
        var res = await PatientSend(p, "see attached", ("labs.pdf", "application/pdf", Pdf), ("rash.jpg", "image/jpeg", Jpg));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var files = (await Json(res)).GetProperty("attachments");
        Assert.Equal(2, files.GetArrayLength());

        foreach (var (file, expected) in new[] { (files[0], Pdf), (files[1], Jpg) })
        {
            var dl = await _f.ClientFor(p.Doctor.Token).GetAsync($"/api/messages/attachments/{file.GetProperty("id").GetInt32()}/download");
            Assert.Equal(HttpStatusCode.OK, dl.StatusCode);
            Assert.Equal(expected, await dl.Content.ReadAsByteArrayAsync());
            Assert.Equal(file.GetProperty("fileName").GetString(), dl.Content.Headers.ContentDisposition!.FileNameStar ?? dl.Content.Headers.ContentDisposition.FileName);
        }
    }

    [Fact]
    public async Task Doctor_attachments_can_be_downloaded_by_the_patient()
    {
        var p = await NewPracticeAsync();
        var res = await DoctorSend(p, "results", ("results.pdf", "application/pdf", Pdf));
        var id = (await Json(res)).GetProperty("attachments")[0].GetProperty("id").GetInt32();
        var dl = await _f.ClientFor(p.Patient.Token).GetAsync($"/api/portal/messages/attachments/{id}/download");
        Assert.Equal(HttpStatusCode.OK, dl.StatusCode);
        Assert.Equal(Pdf, await dl.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task A_file_with_no_text_is_a_valid_message()
    {
        var p = await NewPracticeAsync();
        var res = await PatientSend(p, null, ("only.pdf", "application/pdf", Pdf));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Equal("", (await Json(res)).GetProperty("body").GetString());
    }

    [Fact]
    public async Task Rejected_files_store_nothing()
    {
        var p = await NewPracticeAsync();
        var filesBefore = await _f.WithDbAsync(db => db.PatientAttachments.CountAsync(a => a.PatientId == p.PatientId));

        var badType = await PatientSend(p, "x", ("virus.exe", "application/x-msdownload", new byte[] { 1 }));
        Assert.Equal(HttpStatusCode.BadRequest, badType.StatusCode);
        Assert.Equal("File type 'application/x-msdownload' is not allowed.", (await Json(badType)).GetProperty("error").GetString());

        // one good file plus one bad file: the good one must not be stored either
        var mixed = await PatientSend(p, "x", ("ok.pdf", "application/pdf", Pdf), ("bad.exe", "application/x-msdownload", new byte[] { 1 }));
        Assert.Equal(HttpStatusCode.BadRequest, mixed.StatusCode);

        var tooBig = await PatientSend(p, "x", ("big.pdf", "application/pdf", new byte[50 * 1024 * 1024 + 1]));
        Assert.Equal(HttpStatusCode.BadRequest, tooBig.StatusCode);
        Assert.Equal("File exceeds the 50 MB size limit.", (await Json(tooBig)).GetProperty("error").GetString());

        Assert.Equal(0, await MessageCount(p.PatientId));
        Assert.Equal(filesBefore, await _f.WithDbAsync(db => db.PatientAttachments.CountAsync(a => a.PatientId == p.PatientId)));
    }

    [Fact]
    public async Task More_than_five_files_is_rejected()
    {
        var p = await NewPracticeAsync();
        var six = Enumerable.Range(0, 6).Select(i => ($"f{i}.pdf", "application/pdf", Pdf)).ToArray();
        var res = await PatientSend(p, "x", six);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(0, await MessageCount(p.PatientId));

        var five = six.Take(5).ToArray();
        Assert.Equal(HttpStatusCode.Created, (await PatientSend(p, "x", five)).StatusCode);
    }

    [Fact]
    public async Task Message_files_are_not_reachable_by_outsiders()
    {
        var p = await NewPracticeAsync();
        var other = await NewPracticeAsync();
        var res = await PatientSend(p, "private", ("secret.pdf", "application/pdf", Pdf));
        var id = (await Json(res)).GetProperty("attachments")[0].GetProperty("id").GetInt32();

        // another patient, another doctor, and the wrong route family all see a plain 404 / 403
        Assert.Equal(HttpStatusCode.NotFound,
            (await _f.ClientFor(other.Patient.Token).GetAsync($"/api/portal/messages/attachments/{id}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _f.ClientFor(other.Doctor.Token).GetAsync($"/api/messages/attachments/{id}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _f.ClientFor(p.Doctor.Token).GetAsync("/api/messages/attachments/999999/download")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _f.ClientFor().GetAsync($"/api/messages/attachments/{id}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _f.ClientFor(p.Patient.Token).GetAsync($"/api/messages/attachments/{id}/download")).StatusCode);
    }

    [Fact]
    public async Task Message_files_stay_out_of_the_chart_and_the_portal_attachment_list()
    {
        var p = await NewPracticeAsync();
        var res = await PatientSend(p, "x", ("scan.pdf", "application/pdf", Pdf));
        var id = (await Json(res)).GetProperty("attachments")[0].GetProperty("id").GetInt32();
        var doctor = _f.ClientFor(p.Doctor.Token);

        Assert.Equal(0, (await Json(await doctor.GetAsync($"/api/attachments/patient/{p.PatientId}"))).GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await doctor.GetAsync($"/api/attachments/{id}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await doctor.GetAsync($"/api/attachments/{id}/preview")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await doctor.DeleteAsync($"/api/attachments/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await doctor.PutAsJsonAsync($"/api/attachments/{id}/sharing", new { shared = true })).StatusCode);

        var portal = _f.ClientFor(p.Patient.Token);
        Assert.Equal(0, (await Json(await portal.GetAsync("/api/portal/attachments"))).GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await portal.GetAsync($"/api/portal/attachments/{id}/download")).StatusCode);
    }

    [Fact]
    public async Task Views_and_downloads_are_audited_with_the_actor()
    {
        var p = await NewPracticeAsync();
        var res = await PatientSend(p, "x", ("scan.pdf", "application/pdf", Pdf));
        var id = (await Json(res)).GetProperty("attachments")[0].GetProperty("id").GetInt32();

        await _f.ClientFor(p.Doctor.Token).GetAsync($"/api/messages/patient/{p.PatientId}");
        await _f.ClientFor(p.Doctor.Token).GetAsync($"/api/messages/attachments/{id}/download");

        var logs = await _f.WithDbAsync(db => db.PortalAccessLogs
            .Where(l => l.PatientId == p.PatientId && l.ActorUserId == p.Doctor.UserId).ToListAsync());
        Assert.Contains(logs, l => l.ResourceType == "Message" && l.Action == "View");
        Assert.Contains(logs, l => l.ResourceType == "MessageAttachment" && l.ResourceId == id && l.Action == "Download");
    }
}
