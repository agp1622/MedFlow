using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using MedFlow.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Api.Tests;

public class ReminderTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _f;
    public ReminderTests(TestApiFactory f) => _f = f;

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<int> CreateAppointmentAsync(string doctorToken, int patientId, DateTime at)
    {
        var res = await _f.ClientFor(doctorToken).PostAsJsonAsync("/api/appointments", new
        {
            patientId, scheduledAt = at, durationMinutes = 30, type = "FollowUp", reason = "Checkup", location = "Room 1"
        });
        res.EnsureSuccessStatusCode();
        return (await Json(res)).GetProperty("id").GetInt32();
    }

    private async Task SetAppointmentAsync(int id, Action<MedFlow.Core.Entities.Appointment> change) =>
        await _f.WithDbAsync(async db =>
        {
            var a = await db.Appointments.FirstAsync(x => x.Id == id);
            change(a);
            await db.SaveChangesAsync();
            return 0;
        });

    private static string TokenFrom(string body) =>
        System.Net.WebUtility.UrlDecode(Regex.Match(body, @"token=([^&""]+)").Groups[1].Value);

    /// <summary>Doctor + patient + appointment due in 2 hours; returns (doctorToken, doctorUserId, patientId, appointmentId).</summary>
    private async Task<(string Token, string UserId, int PatientId, int ApptId)> SetupAsync(string tag, DateTime? at = null, string? patientEmail = null)
    {
        var doc = await _f.RegisterDoctorAsync($"doc-{tag}@x.com");
        var pid = await _f.CreatePatientAsync(doc.Token, patientEmail ?? $"pat-{tag}@x.com");
        var appt = await CreateAppointmentAsync(doc.Token, pid, at ?? DateTime.UtcNow.AddHours(2));
        return (doc.Token, doc.UserId, pid, appt);
    }

    private (string To, string Subject, string Body)[] SentTo(string email) =>
        _f.Email.Sent.Where(m => m.To == email).ToArray();

    // ── US1: sending ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Due_appointment_gets_exactly_one_reminder_and_log_entry()
    {
        var s = await SetupAsync("send");
        await _f.RunRemindersAsync();
        await _f.RunRemindersAsync();

        Assert.Single(SentTo("pat-send@x.com"));
        var log = await Json(await _f.ClientFor(s.Token).GetAsync($"/api/appointments/{s.ApptId}/reminders"));
        var deliveries = log.GetProperty("deliveries");
        Assert.Equal(1, deliveries.GetArrayLength());
        Assert.Equal("Sent", deliveries[0].GetProperty("outcome").GetString());
        Assert.Equal("None", log.GetProperty("response").GetString());
    }

    [Fact]
    public async Task Appointment_outside_the_lead_window_is_not_reminded()
    {
        await SetupAsync("far", DateTime.UtcNow.AddDays(10));
        await _f.RunRemindersAsync();
        Assert.Empty(SentTo("pat-far@x.com"));
    }

    [Theory]
    [InlineData(AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.NoShow)]
    public async Task Closed_appointments_are_not_reminded(AppointmentStatus status)
    {
        var s = await SetupAsync($"closed-{status}");
        await SetAppointmentAsync(s.ApptId, a => a.Status = status);
        await _f.RunRemindersAsync();
        Assert.Empty(SentTo($"pat-closed-{status}@x.com"));
    }

    [Fact]
    public async Task Past_appointment_is_not_reminded()
    {
        var s = await SetupAsync("past");
        await SetAppointmentAsync(s.ApptId, a => a.ScheduledAt = DateTime.UtcNow.AddHours(-1));
        await _f.RunRemindersAsync();
        Assert.Empty(SentTo("pat-past@x.com"));
    }

    [Fact]
    public async Task Patient_without_valid_email_is_skipped_and_logged_once()
    {
        var s = await SetupAsync("noemail");
        await _f.WithDbAsync(async db =>
        {
            (await db.Patients.FirstAsync(p => p.Id == s.PatientId)).Email = "not-an-email";
            await db.SaveChangesAsync();
            return 0;
        });
        await _f.RunRemindersAsync();
        await _f.RunRemindersAsync();

        Assert.DoesNotContain(_f.Email.Sent, m => m.To == "not-an-email");
        var log = await Json(await _f.ClientFor(s.Token).GetAsync($"/api/appointments/{s.ApptId}/reminders"));
        Assert.Equal(1, log.GetProperty("deliveries").GetArrayLength());
        Assert.Equal("Skipped", log.GetProperty("deliveries")[0].GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task Failed_send_is_logged_retried_and_bounded()
    {
        var s = await SetupAsync("fail");
        _f.Email.Fail = true;
        try
        {
            for (var i = 0; i < 5; i++) await _f.RunRemindersAsync();
        }
        finally { _f.Email.Fail = false; }

        var log = await Json(await _f.ClientFor(s.Token).GetAsync($"/api/appointments/{s.ApptId}/reminders"));
        var d = log.GetProperty("deliveries");
        Assert.Equal(3, d.GetArrayLength()); // default max attempts
        Assert.All(d.EnumerateArray(), x => Assert.Equal("Failed", x.GetProperty("outcome").GetString()));
        Assert.DoesNotContain("smtp", d.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Failed_send_succeeds_on_a_later_run()
    {
        await SetupAsync("retry");
        _f.Email.Fail = true;
        try { await _f.RunRemindersAsync(); } finally { _f.Email.Fail = false; }
        await _f.RunRemindersAsync();
        Assert.Single(SentTo("pat-retry@x.com"));
    }

    [Fact]
    public async Task Rescheduled_appointment_gets_a_new_reminder()
    {
        var s = await SetupAsync("resched");
        await _f.RunRemindersAsync();
        await SetAppointmentAsync(s.ApptId, a => a.ScheduledAt = DateTime.UtcNow.AddHours(5));
        await _f.RunRemindersAsync();
        Assert.Equal(2, SentTo("pat-resched@x.com").Length);
    }

    // ── US2: confirm / cancel ────────────────────────────────────────────────

    private async Task<(string Token, string Doctor, int ApptId, string LinkToken)> SentReminderAsync(string tag)
    {
        var s = await SetupAsync(tag);
        await _f.RunRemindersAsync();
        return (s.Token, s.UserId, s.ApptId, TokenFrom(SentTo($"pat-{tag}@x.com")[0].Body));
    }

    private async Task<string> StatusAsync(int apptId) =>
        await _f.WithDbAsync(async db => (await db.Appointments.FirstAsync(a => a.Id == apptId)).Status.ToString());

    [Fact]
    public async Task Lookup_shows_minimal_details_and_does_not_change_state()
    {
        var r = await SentReminderAsync("look");
        var res = await _f.CreateClient().PostAsJsonAsync("/api/appointment-response/lookup", new { token = r.LinkToken });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain("reason", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("notes", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("patient", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Pending", await StatusAsync(r.ApptId));
    }

    [Fact]
    public async Task Patient_can_confirm_then_cancel_and_doctor_sees_it()
    {
        var r = await SentReminderAsync("resp");
        var c = _f.CreateClient();
        var res = await c.PostAsJsonAsync("/api/appointment-response/respond", new { token = r.LinkToken, action = "Confirm" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("Confirmed", await StatusAsync(r.ApptId));

        res = await c.PostAsJsonAsync("/api/appointment-response/respond", new { token = r.LinkToken, action = "Cancel" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("Cancelled", await StatusAsync(r.ApptId));

        var log = await Json(await _f.ClientFor(r.Token).GetAsync($"/api/appointments/{r.ApptId}/reminders"));
        Assert.Equal("Cancelled", log.GetProperty("response").GetString());
    }

    [Fact]
    public async Task Cancelled_appointment_cannot_be_reconfirmed_through_the_link()
    {
        var r = await SentReminderAsync("recon");
        var c = _f.CreateClient();
        await c.PostAsJsonAsync("/api/appointment-response/respond", new { token = r.LinkToken, action = "Cancel" });
        var res = await c.PostAsJsonAsync("/api/appointment-response/respond", new { token = r.LinkToken, action = "Confirm" });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("Cancelled", await StatusAsync(r.ApptId));
    }

    [Fact]
    public async Task Doctor_closed_appointment_cannot_be_changed_through_the_link()
    {
        var r = await SentReminderAsync("done");
        await SetAppointmentAsync(r.ApptId, a => a.Status = AppointmentStatus.Completed);
        var res = await _f.CreateClient().PostAsJsonAsync("/api/appointment-response/respond", new { token = r.LinkToken, action = "Cancel" });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("Completed", await StatusAsync(r.ApptId));
    }

    [Fact]
    public async Task Invalid_expired_and_stale_tokens_all_get_the_same_response()
    {
        var r = await SentReminderAsync("inval");
        var other = await SentReminderAsync("inval2");
        var c = _f.CreateClient();

        // expired: appointment start has passed
        await SetAppointmentAsync(other.ApptId, a => a.ScheduledAt = DateTime.UtcNow.AddMinutes(-5));
        // stale: rescheduled after the reminder was sent
        await SetAppointmentAsync(r.ApptId, a => a.ScheduledAt = a.ScheduledAt.AddHours(1));

        var bodies = new List<string>();
        foreach (var token in new[] { "garbage", new string('a', 500), "", r.LinkToken + "x", other.LinkToken, r.LinkToken })
        {
            foreach (var path in new[] { "lookup", "respond" })
            {
                var res = await c.PostAsJsonAsync($"/api/appointment-response/{path}", new { token, action = "Cancel" });
                Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
                bodies.Add(await res.Content.ReadAsStringAsync());
            }
        }
        Assert.Single(bodies.Distinct());
        Assert.Equal("Pending", await StatusAsync(other.ApptId));
        Assert.Equal("Pending", await StatusAsync(r.ApptId));
    }

    [Fact]
    public async Task A_link_only_affects_its_own_appointment()
    {
        var a = await SentReminderAsync("iso-a");
        var b = await SentReminderAsync("iso-b");
        await _f.CreateClient().PostAsJsonAsync("/api/appointment-response/respond", new { token = a.LinkToken, action = "Cancel" });
        Assert.Equal("Cancelled", await StatusAsync(a.ApptId));
        Assert.Equal("Pending", await StatusAsync(b.ApptId));
    }

    [Fact]
    public async Task Tokens_are_stored_hashed_not_raw()
    {
        var r = await SentReminderAsync("hash");
        var stored = await _f.WithDbAsync(db => db.AppointmentReminders.Where(x => x.AppointmentId == r.ApptId)
            .Select(x => x.TokenHash).ToListAsync());
        Assert.DoesNotContain(r.LinkToken, stored);
    }

    // ── US3: doctor log access control ───────────────────────────────────────

    [Fact]
    public async Task Another_doctor_cannot_read_the_log()
    {
        var s = await SetupAsync("acl");
        await _f.RunRemindersAsync();
        var other = await _f.RegisterDoctorAsync("doc-acl-other@x.com");
        var res = await _f.ClientFor(other.Token).GetAsync($"/api/appointments/{s.ApptId}/reminders");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Patient_token_and_anonymous_are_rejected_from_the_log()
    {
        var s = await SetupAsync("acl2");
        var patient = await _f.OnboardPatientAsync(s.Token, s.PatientId, "pat-acl2@x.com");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _f.ClientFor(patient.Token).GetAsync($"/api/appointments/{s.ApptId}/reminders")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _f.CreateClient().GetAsync($"/api/appointments/{s.ApptId}/reminders")).StatusCode);
    }
}

public class ReminderSettingsTests
{
    [Theory]
    [InlineData(24, 24)]
    [InlineData(48, 48)]
    [InlineData(1, 1)]
    [InlineData(168, 168)]
    [InlineData(0, 24)]
    [InlineData(-5, 24)]
    [InlineData(169, 24)]
    public void Lead_time_outside_1_to_168_hours_falls_back_to_default(int configured, int expected) =>
        Assert.Equal(expected, new MedFlow.Infrastructure.Reminders.ReminderSettings { LeadTimeHours = configured }.EffectiveLeadTimeHours);
}
