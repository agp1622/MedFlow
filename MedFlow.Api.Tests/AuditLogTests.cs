using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Api.Tests;

public class AuditLogTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _f;
    public AuditLogTests(TestApiFactory f) => _f = f;

    private Task<List<AuditEvent>> EventsAsync(int patientId) =>
        _f.WithDbAsync(db => db.AuditEvents.Where(e => e.PatientId == patientId).OrderBy(e => e.Id).ToListAsync());

    private async Task<JsonElement> LogAsync(string token, int patientId, string query = "")
    {
        var res = await _f.ClientFor(token).GetAsync($"/api/patients/{patientId}/audit-log{query}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static object UpdateBody(string phone) => new
    {
        firstName = "Pat", lastName = "Ient", dateOfBirth = "1990-01-01", gender = "Male", bloodType = "OPos",
        status = "Active", email = "p@x.com", phone, allergies = "SECRET-ALLERGY-VALUE"
    };

    [Fact]
    public async Task Doctor_views_and_changes_are_recorded_with_user_and_time_but_no_values()
    {
        var doctor = await _f.RegisterDoctorAsync("doc-audit1@x.com");
        var pid = await _f.CreatePatientAsync(doctor.Token, "p@x.com");
        var c = _f.ClientFor(doctor.Token);
        var before = DateTime.UtcNow.AddSeconds(-5);

        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/api/patients/{pid}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync($"/api/patients/{pid}", UpdateBody("555-1234"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/api/medicalnotes",
            new { patientId = pid, content = "SECRET-NOTE-TEXT", visitType = "Checkup" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/api/medicalnotes/patient/{pid}")).StatusCode);

        var events = await EventsAsync(pid);
        Assert.Contains(events, e => e.Action == AuditAction.View && e.ItemKind == AuditItemKind.Patient);
        var edit = Assert.Single(events, e => e.Action == AuditAction.Change && e.ItemKind == AuditItemKind.Patient && e.ChangedFields != null);
        Assert.Contains("Phone", edit.ChangedFields!.Split(','));
        Assert.Contains(events, e => e.Action == AuditAction.Change && e.ItemKind == AuditItemKind.Note);
        Assert.Contains(events, e => e.Action == AuditAction.View && e.ItemKind == AuditItemKind.Note);
        Assert.All(events, e =>
        {
            Assert.Equal(doctor.UserId, e.ActorUserId);
            Assert.Equal("Doctor", e.ActorRole);
            Assert.Equal("Doc Tor", e.ActorName);
            Assert.True(e.OccurredAt >= before);
            Assert.DoesNotContain("SECRET", e.ChangedFields ?? "");
        });
        var body = (await LogAsync(doctor.Token, pid)).ToString();
        Assert.DoesNotContain("SECRET", body);
        Assert.DoesNotContain("555-1234", body);
    }

    [Fact]
    public async Task Portal_patient_views_are_recorded_as_the_patient()
    {
        var doctor = await _f.RegisterDoctorAsync("doc-audit2@x.com");
        var pid = await _f.CreatePatientAsync(doctor.Token, "p-audit2@x.com");
        var patient = await _f.OnboardPatientAsync(doctor.Token, pid, "p-audit2@x.com");
        Assert.Equal(HttpStatusCode.OK, (await _f.ClientFor(patient.Token).GetAsync("/api/portal/appointments")).StatusCode);

        var events = await EventsAsync(pid);
        var view = Assert.Single(events, e => e.ActorRole == "Patient" && e.ItemKind == AuditItemKind.Appointment);
        Assert.Equal(patient.UserId, view.ActorUserId);
        Assert.Equal(AuditAction.View, view.Action);
        Assert.Equal(doctor.UserId, view.DoctorId);
    }

    [Fact]
    public async Task Other_doctors_requests_are_denied_and_create_no_events()
    {
        var owner = await _f.RegisterDoctorAsync("doc-audit3a@x.com");
        var other = await _f.RegisterDoctorAsync("doc-audit3b@x.com");
        var pid = await _f.CreatePatientAsync(owner.Token, "p-audit3@x.com");
        var baseline = (await EventsAsync(pid)).Count;
        var c = _f.ClientFor(other.Token);

        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/patients/{pid}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/medicalnotes/patient/{pid}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsJsonAsync("/api/medicalnotes",
            new { patientId = pid, content = "x", visitType = "Checkup" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/patients/{pid}/audit-log")).StatusCode);

        Assert.Equal(baseline, (await EventsAsync(pid)).Count);
    }

    [Fact]
    public async Task Log_is_newest_first_filterable_and_paged()
    {
        var doctor = await _f.RegisterDoctorAsync("doc-audit4@x.com");
        var other = await _f.RegisterDoctorAsync("doc-audit4b@x.com");
        var pid = await _f.CreatePatientAsync(doctor.Token, "p-audit4@x.com");
        var pid2 = await _f.CreatePatientAsync(doctor.Token, "p2-audit4@x.com");
        var c = _f.ClientFor(doctor.Token);
        await c.GetAsync($"/api/patients/{pid}");
        await c.PutAsJsonAsync($"/api/patients/{pid}", UpdateBody("555-9999"));
        await c.GetAsync($"/api/patients/{pid2}");
        // an event by a different user on the same patient (e.g. a past owner or the portal) for the user filter
        await _f.WithDbAsync(async db =>
        {
            db.AuditEvents.Add(new AuditEvent { PatientId = pid, DoctorId = doctor.UserId, ActorUserId = "u-x",
                ActorName = "Zed Zebra", ActorRole = "Doctor", Action = AuditAction.View, ItemKind = AuditItemKind.Note,
                OccurredAt = DateTime.UtcNow.AddDays(-30) });
            await db.SaveChangesAsync();
            return 0;
        });

        var all = await LogAsync(doctor.Token, pid);
        var items = all.GetProperty("items").EnumerateArray().ToList();
        var times = items.Select(i => i.GetProperty("occurredAt").GetDateTime()).ToList();
        Assert.Equal(times.OrderByDescending(t => t), times);
        Assert.Equal("Zed Zebra", items.Last().GetProperty("actorName").GetString());

        var changes = await LogAsync(doctor.Token, pid, "?action=Change");
        Assert.All(changes.GetProperty("items").EnumerateArray(), i => Assert.Equal("Change", i.GetProperty("action").GetString()));
        Assert.Equal(2, changes.GetProperty("totalCount").GetInt32()); // patient creation + the edit

        var zed = await LogAsync(doctor.Token, pid, "?actor=zebra");
        Assert.Equal(1, zed.GetProperty("totalCount").GetInt32());

        var old = DateTime.UtcNow.AddDays(-31).ToString("yyyy-MM-dd");
        var oldEnd = DateTime.UtcNow.AddDays(-29).ToString("yyyy-MM-dd");
        var ranged = await LogAsync(doctor.Token, pid, $"?from={old}&to={oldEnd}");
        Assert.Equal(1, ranged.GetProperty("totalCount").GetInt32());

        var combined = await LogAsync(doctor.Token, pid, $"?action=Change&actor=zebra");
        Assert.Equal(0, combined.GetProperty("totalCount").GetInt32());

        var paged = await LogAsync(doctor.Token, pid, "?page=2&pageSize=2");
        Assert.Equal(2, paged.GetProperty("page").GetInt32());
        Assert.True(paged.GetProperty("items").GetArrayLength() <= 2);

        // events on other patients never leak into this log
        var otherLog = await LogAsync(doctor.Token, pid2);
        Assert.DoesNotContain("Zed Zebra", otherLog.ToString());
        _ = other;
    }

    [Fact]
    public async Task Empty_log_and_invalid_filters_are_handled()
    {
        var doctor = await _f.RegisterDoctorAsync("doc-audit5@x.com");
        var pid = await _f.CreatePatientAsync(doctor.Token, "p-audit5@x.com");
        var c = _f.ClientFor(doctor.Token);

        var empty = await LogAsync(doctor.Token, pid, "?actor=nobodyatall");
        Assert.Equal(0, empty.GetProperty("totalCount").GetInt32());

        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"/api/patients/{pid}/audit-log?from=2026-02-01&to=2026-01-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"/api/patients/{pid}/audit-log?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"/api/patients/{pid}/audit-log?pageSize=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"/api/patients/{pid}/audit-log?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"/api/patients/{pid}/audit-log?actor={new string('a', 201)}")).StatusCode);
    }

    [Fact]
    public async Task Reading_the_log_is_itself_recorded()
    {
        var doctor = await _f.RegisterDoctorAsync("doc-audit6@x.com");
        var pid = await _f.CreatePatientAsync(doctor.Token, "p-audit6@x.com");
        await LogAsync(doctor.Token, pid);
        Assert.Contains(await EventsAsync(pid), e => e.ItemKind == AuditItemKind.AuditLog && e.Action == AuditAction.View);
    }

    [Fact]
    public async Task Patients_other_doctors_and_anonymous_cannot_read_the_log_and_missing_equals_foreign()
    {
        var owner = await _f.RegisterDoctorAsync("doc-audit7a@x.com");
        var other = await _f.RegisterDoctorAsync("doc-audit7b@x.com");
        var pid = await _f.CreatePatientAsync(owner.Token, "p-audit7@x.com");
        var patient = await _f.OnboardPatientAsync(owner.Token, pid, "p-audit7@x.com");

        Assert.Equal(HttpStatusCode.Forbidden, (await _f.ClientFor(patient.Token).GetAsync($"/api/patients/{pid}/audit-log")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.CreateClient().GetAsync($"/api/patients/{pid}/audit-log")).StatusCode);

        var foreign = await _f.ClientFor(other.Token).GetAsync($"/api/patients/{pid}/audit-log");
        var missing = await _f.ClientFor(other.Token).GetAsync("/api/patients/999999/audit-log");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(missing.StatusCode, foreign.StatusCode);
        static string NoTrace(string b) => System.Text.RegularExpressions.Regex.Replace(b, "\"traceId\":\"[^\"]*\"", "");
        Assert.Equal(NoTrace(await missing.Content.ReadAsStringAsync()), NoTrace(await foreign.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Audit_events_cannot_be_modified_or_deleted()
    {
        var doctor = await _f.RegisterDoctorAsync("doc-audit8@x.com");
        var pid = await _f.CreatePatientAsync(doctor.Token, "p-audit8@x.com");
        await _f.ClientFor(doctor.Token).GetAsync($"/api/patients/{pid}");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _f.WithDbAsync(async db =>
        {
            var e = await db.AuditEvents.FirstAsync(x => x.PatientId == pid);
            e.ActorName = "Tampered";
            return await db.SaveChangesAsync();
        }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _f.WithDbAsync(async db =>
        {
            db.AuditEvents.Remove(await db.AuditEvents.FirstAsync(x => x.PatientId == pid));
            return await db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task Events_survive_patient_deletion()
    {
        var doctor = await _f.RegisterDoctorAsync("doc-audit9@x.com");
        var pid = await _f.CreatePatientAsync(doctor.Token, "p-audit9@x.com");
        var c = _f.ClientFor(doctor.Token);
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/patients/{pid}")).StatusCode);
        Assert.Contains(await EventsAsync(pid), e => e.Action == AuditAction.Change && e.ItemKind == AuditItemKind.Patient);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/patients/{pid}/audit-log")).StatusCode);
    }
}
