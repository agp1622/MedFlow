using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using MedFlow.Infrastructure.Reminders;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Api.Tests;

public class WaitlistTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _f;
    public WaitlistTests(TestApiFactory f) => _f = f;

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private static DateTime Tomorrow(int hour, int minute = 0) =>
        DateTime.UtcNow.Date.AddDays(1).AddHours(hour).AddMinutes(minute);

    private static string TokenFrom(string body) =>
        WebUtility.UrlDecode(Regex.Match(body, @"token=([^&""]+)").Groups[1].Value);

    private (string To, string Subject, string Body)[] SentTo(string email) =>
        _f.Email.Sent.Where(m => m.To == email).ToArray();

    private async Task<AuthResult> DoctorWithAvailabilityAsync(string tag)
    {
        var doc = await _f.RegisterDoctorAsync($"doc-{tag}@x.com");
        var windows = Enum.GetNames<DayOfWeek>().Select(d => new { dayOfWeek = d, startTime = "08:00", endTime = "18:00" });
        (await _f.ClientFor(doc.Token).PutAsJsonAsync("/api/availability/weekly", new { windows })).EnsureSuccessStatusCode();
        return doc;
    }

    private async Task<int> AddWaitingAsync(AuthResult doc, int patientId)
    {
        var res = await _f.ClientFor(doc.Token).PostAsJsonAsync("/api/waitlist", new { patientId });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await Json(res)).GetProperty("id").GetInt32();
    }

    /// <summary>Patient A (portal user) books the slot; returns the appointment id.</summary>
    private async Task<int> BookAsync(AuthResult patient, DateTime at)
    {
        var res = await _f.ClientFor(patient.Token).PostAsJsonAsync("/api/portal/booking", new { startsAt = at, reason = "Secret reason" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await Json(res)).GetProperty("id").GetInt32();
    }

    private Task<HttpResponseMessage> CancelAsync(AuthResult doc, int apptId) =>
        _f.ClientFor(doc.Token).PatchAsJsonAsync($"/api/appointments/{apptId}/status", "Cancelled");

    private record Scene(AuthResult Doctor, AuthResult Booker, int BookerPatientId, int ApptId, DateTime Slot);

    /// <summary>Doctor with availability; a booked tomorrow-10:00 appointment held by a portal patient.</summary>
    private async Task<Scene> BookedSceneAsync(string tag)
    {
        var doc = await DoctorWithAvailabilityAsync(tag);
        var pid = await _f.CreatePatientAsync(doc.Token, $"a-{tag}@x.com");
        var booker = await _f.OnboardPatientAsync(doc.Token, pid, $"a-{tag}@x.com");
        var slot = Tomorrow(10);
        return new Scene(doc, booker, pid, await BookAsync(booker, slot), slot);
    }

    private async Task<int> WaitingPatientAsync(AuthResult doc, string email)
    {
        var pid = await _f.CreatePatientAsync(doc.Token, email, first: email.Split('@')[0]);
        await AddWaitingAsync(doc, pid);
        return pid;
    }

    // ── US1: staff manage the waitlist ───────────────────────────────────────

    [Fact]
    public async Task Doctor_can_add_list_in_join_order_and_remove()
    {
        var doc = await _f.RegisterDoctorAsync("doc-wl-crud@x.com");
        var p1 = await _f.CreatePatientAsync(doc.Token, "wl-crud1@x.com");
        var p2 = await _f.CreatePatientAsync(doc.Token, "wl-crud2@x.com");
        var e1 = await AddWaitingAsync(doc, p1);
        await AddWaitingAsync(doc, p2);

        var c = _f.ClientFor(doc.Token);
        var list = await Json(await c.GetAsync("/api/waitlist"));
        Assert.Equal(2, list.GetProperty("totalCount").GetInt32());
        Assert.Equal(p1, list.GetProperty("items")[0].GetProperty("patientId").GetInt32());

        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync("/api/waitlist", new { patientId = p1 })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/waitlist/{e1}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.DeleteAsync($"/api/waitlist/{e1}")).StatusCode);
        Assert.Equal(1, (await Json(await c.GetAsync("/api/waitlist"))).GetProperty("totalCount").GetInt32());

        // A removed patient can rejoin
        await AddWaitingAsync(doc, p1);
    }

    [Fact]
    public async Task Inactive_patient_cannot_be_added()
    {
        var doc = await _f.RegisterDoctorAsync("doc-wl-inactive@x.com");
        var pid = await _f.CreatePatientAsync(doc.Token, "wl-inactive@x.com");
        await _f.WithDbAsync(async db =>
        {
            (await db.Patients.FirstAsync(p => p.Id == pid)).Status = PatientStatus.Inactive;
            await db.SaveChangesAsync();
            return 0;
        });
        var res = await _f.ClientFor(doc.Token).PostAsJsonAsync("/api/waitlist", new { patientId = pid });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Other_doctors_get_404_and_see_nothing()
    {
        var a = await _f.RegisterDoctorAsync("doc-wl-iso-a@x.com");
        var b = await _f.RegisterDoctorAsync("doc-wl-iso-b@x.com");
        var pid = await _f.CreatePatientAsync(a.Token, "wl-iso@x.com");
        var entry = await AddWaitingAsync(a, pid);

        var cb = _f.ClientFor(b.Token);
        Assert.Equal(HttpStatusCode.NotFound, (await cb.PostAsJsonAsync("/api/waitlist", new { patientId = pid })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cb.DeleteAsync($"/api/waitlist/{entry}")).StatusCode);
        Assert.Equal(0, (await Json(await cb.GetAsync("/api/waitlist"))).GetProperty("totalCount").GetInt32());
        Assert.Equal(1, (await Json(await _f.ClientFor(a.Token).GetAsync("/api/waitlist"))).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Patient_token_is_rejected_on_doctor_routes_and_anonymous_is_unauthorized()
    {
        var doc = await _f.RegisterDoctorAsync("doc-wl-role@x.com");
        var pid = await _f.CreatePatientAsync(doc.Token, "wl-role@x.com");
        var patient = await _f.OnboardPatientAsync(doc.Token, pid, "wl-role@x.com");
        var c = _f.ClientFor(patient.Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/waitlist")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/waitlist", new { patientId = pid })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.DeleteAsync("/api/waitlist/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.ClientFor().GetAsync("/api/waitlist")).StatusCode);
    }

    [Fact]
    public async Task Staff_changes_are_audited()
    {
        var doc = await _f.RegisterDoctorAsync("doc-wl-audit@x.com");
        var pid = await _f.CreatePatientAsync(doc.Token, "wl-audit@x.com");
        var entry = await AddWaitingAsync(doc, pid);
        await _f.ClientFor(doc.Token).DeleteAsync($"/api/waitlist/{entry}");
        var count = await _f.WithDbAsync(db => db.AuditEvents.CountAsync(a =>
            a.PatientId == pid && a.ItemKind == AuditItemKind.Waitlist && a.Action == AuditAction.Change));
        Assert.Equal(2, count);
    }

    // ── US2: offers on cancellation ──────────────────────────────────────────

    [Fact]
    public async Task Cancelling_offers_the_slot_to_waiting_patients_only_of_that_doctor_and_without_phi()
    {
        var s = await BookedSceneAsync("offer");
        await WaitingPatientAsync(s.Doctor, "wl-b@x.com");
        await WaitingPatientAsync(s.Doctor, "wl-c@x.com");
        var other = await DoctorWithAvailabilityAsync("offer-other");
        await WaitingPatientAsync(other, "wl-d@x.com");

        Assert.Equal(HttpStatusCode.NoContent, (await CancelAsync(s.Doctor, s.ApptId)).StatusCode);

        var mail = Assert.Single(SentTo("wl-b@x.com"));
        Assert.Single(SentTo("wl-c@x.com"));
        Assert.Empty(SentTo("wl-d@x.com"));
        Assert.Empty(SentTo($"a-offer@x.com").Where(m => m.Subject.Contains("horario")));
        Assert.Contains("waitlist-offer?token=", mail.Body);
        Assert.Contains(s.Slot.ToString("yyyy-MM-dd HH:mm"), mail.Body);
        Assert.DoesNotContain("Secret reason", mail.Body);
        Assert.Contains("Hola", mail.Body);
        Assert.Contains("Hello", mail.Body);
        // Only the hash is stored
        var raw = TokenFrom(mail.Body);
        Assert.Equal(0, await _f.WithDbAsync(db => db.WaitlistOffers.CountAsync(o => o.TokenHash == raw)));
    }

    [Fact]
    public async Task Doctor_delete_and_put_cancel_also_offer_the_slot()
    {
        var s = await BookedSceneAsync("offer-del");
        await WaitingPatientAsync(s.Doctor, "wl-del-b@x.com");
        Assert.Equal(HttpStatusCode.NoContent, (await _f.ClientFor(s.Doctor.Token).DeleteAsync($"/api/appointments/{s.ApptId}")).StatusCode);
        Assert.Single(SentTo("wl-del-b@x.com"));

        var s2 = await BookedSceneAsync("offer-put");
        await WaitingPatientAsync(s2.Doctor, "wl-put-b@x.com");
        var res = await _f.ClientFor(s2.Doctor.Token).PutAsJsonAsync($"/api/appointments/{s2.ApptId}", new
        {
            scheduledAt = s2.Slot, durationMinutes = 30, type = "Consultation", status = "Cancelled"
        });
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Single(SentTo("wl-put-b@x.com"));
    }

    [Fact]
    public async Task At_most_five_earliest_joined_patients_are_offered()
    {
        var s = await BookedSceneAsync("cap");
        for (var i = 1; i <= 6; i++) await WaitingPatientAsync(s.Doctor, $"wl-cap{i}@x.com");
        await CancelAsync(s.Doctor, s.ApptId);
        for (var i = 1; i <= 5; i++) Assert.Single(SentTo($"wl-cap{i}@x.com"));
        Assert.Empty(SentTo("wl-cap6@x.com"));
    }

    [Fact]
    public async Task Patients_without_a_valid_email_are_skipped_and_do_not_use_the_cap()
    {
        var s = await BookedSceneAsync("noemail");
        var noEmail = await _f.CreatePatientAsync(s.Doctor.Token, "wl-ne@x.com");
        await _f.WithDbAsync(async db =>
        {
            (await db.Patients.FirstAsync(p => p.Id == noEmail)).Email = "not-an-email";
            await db.SaveChangesAsync();
            return 0;
        });
        await AddWaitingAsync(s.Doctor, noEmail);
        await WaitingPatientAsync(s.Doctor, "wl-ne-b@x.com");
        await CancelAsync(s.Doctor, s.ApptId);
        Assert.Single(SentTo("wl-ne-b@x.com"));
    }

    [Fact]
    public async Task No_offer_when_the_slot_is_not_bookable_or_is_in_the_past()
    {
        var doc = await DoctorWithAvailabilityAsync("nobook");
        var booker = await _f.CreatePatientAsync(doc.Token, "wl-nb-a@x.com");
        await WaitingPatientAsync(doc, "wl-nb-b@x.com");

        async Task<int> Create(DateTime at) => (await Json(await _f.ClientFor(doc.Token).PostAsJsonAsync("/api/appointments", new
        {
            patientId = booker, scheduledAt = at, durationMinutes = 30, type = "FollowUp"
        }))).GetProperty("id").GetInt32();

        // Outside availability (20:00) and in the past
        await CancelAsync(doc, await Create(Tomorrow(20)));
        await CancelAsync(doc, await Create(DateTime.UtcNow.AddDays(-2)));
        // Inside availability but another active appointment already holds the slot
        var held = await Create(Tomorrow(12));
        var cancelled = await Create(Tomorrow(12));
        await CancelAsync(doc, cancelled);
        Assert.Empty(SentTo("wl-nb-b@x.com"));
        Assert.NotEqual(held, cancelled);

        // Blocked date
        var blocked = DateTime.UtcNow.Date.AddDays(3);
        await _f.ClientFor(doc.Token).PostAsJsonAsync("/api/availability/blocked-dates", new { date = blocked.ToString("yyyy-MM-dd") });
        await CancelAsync(doc, await Create(blocked.AddHours(10)));
        Assert.Empty(SentTo("wl-nb-b@x.com"));
    }

    [Fact]
    public async Task The_same_patient_is_not_offered_the_same_slot_twice()
    {
        var s = await BookedSceneAsync("dup");
        await WaitingPatientAsync(s.Doctor, "wl-dup-b@x.com");
        await CancelAsync(s.Doctor, s.ApptId);
        // Reopen and cancel again
        await _f.ClientFor(s.Doctor.Token).PatchAsJsonAsync($"/api/appointments/{s.ApptId}/status", "Pending");
        await CancelAsync(s.Doctor, s.ApptId);
        Assert.Single(SentTo("wl-dup-b@x.com"));
    }

    [Fact]
    public async Task A_failing_email_does_not_fail_the_cancellation()
    {
        var s = await BookedSceneAsync("fail");
        await WaitingPatientAsync(s.Doctor, "wl-fail-b@x.com");
        _f.Email.Fail = true;
        try
        {
            Assert.Equal(HttpStatusCode.NoContent, (await CancelAsync(s.Doctor, s.ApptId)).StatusCode);
        }
        finally { _f.Email.Fail = false; }
        Assert.Empty(SentTo("wl-fail-b@x.com"));
        var status = await _f.WithDbAsync(async db => (await db.Appointments.FirstAsync(a => a.Id == s.ApptId)).Status);
        Assert.Equal(AppointmentStatus.Cancelled, status);
    }

    [Fact]
    public async Task Cancelling_through_the_reminder_link_offers_the_slot()
    {
        var s = await BookedSceneAsync("remlink");
        await WaitingPatientAsync(s.Doctor, "wl-rem-b@x.com");
        const string token = "reminder-token-for-waitlist-test";
        await _f.WithDbAsync(async db =>
        {
            db.AppointmentReminders.Add(new AppointmentReminder
            {
                AppointmentId = s.ApptId, ScheduledAt = s.Slot, TokenHash = ReminderTokens.Hash(token),
                Status = ReminderStatus.Sent, SentAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            return 0;
        });
        var res = await _f.ClientFor().PostAsJsonAsync("/api/appointment-response/respond", new { token, action = "Cancel" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Single(SentTo("wl-rem-b@x.com"));
    }

    // ── US3: claim or leave ──────────────────────────────────────────────────

    private async Task<(Scene Scene, string TokenB, string TokenC, int PidB, int PidC)> OfferedSceneAsync(string tag)
    {
        var s = await BookedSceneAsync(tag);
        var pb = await WaitingPatientAsync(s.Doctor, $"wl-{tag}-b@x.com");
        var pc = await WaitingPatientAsync(s.Doctor, $"wl-{tag}-c@x.com");
        await CancelAsync(s.Doctor, s.ApptId);
        return (s, TokenFrom(SentTo($"wl-{tag}-b@x.com").Single().Body), TokenFrom(SentTo($"wl-{tag}-c@x.com").Single().Body), pb, pc);
    }

    private Task<HttpResponseMessage> Offer(string action, string token, string? lang = null)
    {
        var c = _f.ClientFor();
        if (lang != null) c.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", lang);
        return c.PostAsJsonAsync($"/api/waitlist-offer/{action}", new { token });
    }

    private Task<int> ActiveAppointmentsAt(string doctorId, DateTime slot) =>
        _f.WithDbAsync(db => db.Appointments.CountAsync(a =>
            a.DoctorId == doctorId && a.ScheduledAt == slot && a.Status != AppointmentStatus.Cancelled));

    [Fact]
    public async Task Lookup_shows_only_slot_and_doctor_then_first_claim_wins()
    {
        var o = await OfferedSceneAsync("claim");
        var look = await Offer("lookup", o.TokenB);
        Assert.Equal(HttpStatusCode.OK, look.StatusCode);
        var j = await Json(look);
        Assert.Equal(o.Scene.Slot, j.GetProperty("slotStartsAt").GetDateTime().ToUniversalTime());
        Assert.Equal(30, j.GetProperty("durationMinutes").GetInt32());

        var first = await Offer("claim", o.TokenB);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var second = await Offer("claim", o.TokenC);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(1, await ActiveAppointmentsAt(o.Scene.Doctor.UserId, o.Scene.Slot));

        // The winner got a Pending appointment; their entry is Booked and leaves the list
        var booked = await _f.WithDbAsync(db => db.Appointments.FirstAsync(a =>
            a.PatientId == o.PidB && a.ScheduledAt == o.Scene.Slot));
        Assert.Equal(AppointmentStatus.Pending, booked.Status);
        var list = await Json(await _f.ClientFor(o.Scene.Doctor.Token).GetAsync("/api/waitlist"));
        Assert.DoesNotContain(list.GetProperty("items").EnumerateArray(), e => e.GetProperty("patientId").GetInt32() == o.PidB);

        // The used token is now indistinguishable from any invalid one
        Assert.Equal(HttpStatusCode.NotFound, (await Offer("claim", o.TokenB)).StatusCode);
    }

    [Fact]
    public async Task Concurrent_claims_create_exactly_one_appointment()
    {
        var o = await OfferedSceneAsync("race");
        var results = await Task.WhenAll(Offer("claim", o.TokenB), Offer("claim", o.TokenC));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, await ActiveAppointmentsAt(o.Scene.Doctor.UserId, o.Scene.Slot));
    }

    [Fact]
    public async Task Claim_is_rejected_when_the_slot_was_taken_by_someone_else()
    {
        var o = await OfferedSceneAsync("taken");
        // Another patient books the freed slot through ordinary online booking
        var pid = await _f.CreatePatientAsync(o.Scene.Doctor.Token, "wl-taken-x@x.com");
        var x = await _f.OnboardPatientAsync(o.Scene.Doctor.Token, pid, "wl-taken-x@x.com");
        await BookAsync(x, o.Scene.Slot);
        Assert.Equal(HttpStatusCode.Conflict, (await Offer("claim", o.TokenB)).StatusCode);
        Assert.Equal(1, await ActiveAppointmentsAt(o.Scene.Doctor.UserId, o.Scene.Slot));
    }

    [Fact]
    public async Task Invalid_expired_and_left_tokens_all_give_the_same_404()
    {
        var o = await OfferedSceneAsync("bad");
        var unknown = await Offer("lookup", "definitely-not-a-token");
        var unknownBody = await unknown.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        // Expired
        await _f.WithDbAsync(async db =>
        {
            foreach (var off in db.WaitlistOffers.Where(x => x.EntryId == db.WaitlistEntries.First(e => e.PatientId == o.PidB).Id))
                off.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
            return 0;
        });
        foreach (var action in new[] { "lookup", "claim", "leave" })
        {
            var res = await Offer(action, o.TokenB);
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
            Assert.Equal(unknownBody, await res.Content.ReadAsStringAsync());
        }

        // After leaving, the token no longer works either
        Assert.Equal(HttpStatusCode.NoContent, (await Offer("leave", o.TokenC)).StatusCode);
        foreach (var action in new[] { "lookup", "claim", "leave" })
            Assert.Equal(HttpStatusCode.NotFound, (await Offer(action, o.TokenC)).StatusCode);

        // Empty token
        Assert.Equal(HttpStatusCode.NotFound, (await Offer("claim", "")).StatusCode);
        Assert.Equal(0, await ActiveAppointmentsAt(o.Scene.Doctor.UserId, o.Scene.Slot));
    }

    [Fact]
    public async Task Invalid_token_message_follows_accept_language()
    {
        var es = await Json(await Offer("lookup", "nope"));
        var en = await Json(await Offer("lookup", "nope", "en"));
        Assert.Equal("Este enlace no es válido o ha caducado.", es.GetProperty("error").GetString());
        Assert.Equal("This link is not valid or has expired.", en.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Leaving_by_token_stops_further_offers()
    {
        var o = await OfferedSceneAsync("leave");
        Assert.Equal(HttpStatusCode.NoContent, (await Offer("leave", o.TokenB)).StatusCode);

        // Book and cancel another slot: the patient who left gets nothing new, the other still does
        var other = Tomorrow(11);
        var pid = await _f.CreatePatientAsync(o.Scene.Doctor.Token, "wl-leave-x@x.com");
        var x = await _f.OnboardPatientAsync(o.Scene.Doctor.Token, pid, "wl-leave-x@x.com");
        var appt = await BookAsync(x, other);
        await CancelAsync(o.Scene.Doctor, appt);
        Assert.Single(SentTo("wl-leave-b@x.com"));
        Assert.Equal(2, SentTo("wl-leave-c@x.com").Length);
    }

    [Fact]
    public async Task Token_claim_is_audited_for_a_portal_patient()
    {
        var doc = await DoctorWithAvailabilityAsync("claim-audit");
        var a = await _f.CreatePatientAsync(doc.Token, "a-ca@x.com");
        var aAuth = await _f.OnboardPatientAsync(doc.Token, a, "a-ca@x.com");
        var b = await _f.CreatePatientAsync(doc.Token, "wl-ca-b@x.com");
        var bAuth = await _f.OnboardPatientAsync(doc.Token, b, "wl-ca-b@x.com");
        await AddWaitingAsync(doc, b);
        var slot = Tomorrow(9);
        await CancelAsync(doc, await BookAsync(aAuth, slot));
        var token = TokenFrom(SentTo("wl-ca-b@x.com").Single(m => m.Body.Contains("waitlist-offer")).Body);
        Assert.Equal(HttpStatusCode.OK, (await Offer("claim", token)).StatusCode);
        var events = await _f.WithDbAsync(db => db.AuditEvents.CountAsync(e =>
            e.PatientId == b && e.ActorUserId == bAuth.UserId && e.Action == AuditAction.Change));
        Assert.Equal(2, events);
    }

    [Fact]
    public void Public_offer_endpoints_are_rate_limited()
    {
        var attr = typeof(MedFlow.Api.Controllers.WaitlistOfferController)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute), false)
            .Cast<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>().Single();
        Assert.Equal("waitlist-offer", attr.PolicyName);
    }

    // ── US4: portal ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Portal_patient_can_join_see_status_and_leave_and_only_their_own()
    {
        var doc = await _f.RegisterDoctorAsync("doc-wl-portal@x.com");
        var p1 = await _f.CreatePatientAsync(doc.Token, "wl-portal1@x.com");
        var p2 = await _f.CreatePatientAsync(doc.Token, "wl-portal2@x.com");
        var a1 = await _f.OnboardPatientAsync(doc.Token, p1, "wl-portal1@x.com");
        var a2 = await _f.OnboardPatientAsync(doc.Token, p2, "wl-portal2@x.com");
        var c1 = _f.ClientFor(a1.Token);

        Assert.False((await Json(await c1.GetAsync("/api/portal/waitlist"))).GetProperty("onWaitlist").GetBoolean());
        Assert.Equal(HttpStatusCode.Created, (await c1.PostAsync("/api/portal/waitlist", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c1.PostAsync("/api/portal/waitlist", null)).StatusCode);
        Assert.True((await Json(await c1.GetAsync("/api/portal/waitlist"))).GetProperty("onWaitlist").GetBoolean());

        // The other patient and the doctor's list see exactly the right thing
        Assert.False((await Json(await _f.ClientFor(a2.Token).GetAsync("/api/portal/waitlist"))).GetProperty("onWaitlist").GetBoolean());
        var list = await Json(await _f.ClientFor(doc.Token).GetAsync("/api/waitlist"));
        Assert.Equal(p1, list.GetProperty("items").EnumerateArray().Single().GetProperty("patientId").GetInt32());

        Assert.Equal(HttpStatusCode.NotFound, (await _f.ClientFor(a2.Token).DeleteAsync("/api/portal/waitlist")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c1.DeleteAsync("/api/portal/waitlist")).StatusCode);
        Assert.False((await Json(await c1.GetAsync("/api/portal/waitlist"))).GetProperty("onWaitlist").GetBoolean());
    }

    [Fact]
    public async Task Portal_waitlist_rejects_doctor_tokens()
    {
        var doc = await _f.RegisterDoctorAsync("doc-wl-portal-role@x.com");
        var c = _f.ClientFor(doc.Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/portal/waitlist")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsync("/api/portal/waitlist", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.DeleteAsync("/api/portal/waitlist")).StatusCode);
    }
}
