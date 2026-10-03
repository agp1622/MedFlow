using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Api.Tests;

public class BookingTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _f;
    public BookingTests(TestApiFactory f) => _f = f;

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private static object[] EveryDay(string start = "08:00", string end = "18:00") =>
        Enum.GetNames<DayOfWeek>().Select(d => (object)new { dayOfWeek = d, startTime = start, endTime = end }).ToArray();

    private static DateTime Tomorrow(int hour, int minute = 0) =>
        DateTime.UtcNow.Date.AddDays(1).AddHours(hour).AddMinutes(minute);

    private static string D(DateTime d) => d.ToString("yyyy-MM-dd");

    private async Task SetWeekly(string token, params object[] windows) =>
        (await _f.ClientFor(token).PutAsJsonAsync("/api/availability/weekly", new { windows })).EnsureSuccessStatusCode();

    private async Task<(AuthResult Doctor, AuthResult Patient, int PatientId)> Setup(string tag, bool availability = true)
    {
        var doctor = await _f.RegisterDoctorAsync($"doc-{tag}@x.com");
        var pid = await _f.CreatePatientAsync(doctor.Token, $"p-{tag}@x.com");
        var patient = await _f.OnboardPatientAsync(doctor.Token, pid, $"p-{tag}@x.com");
        if (availability) await SetWeekly(doctor.Token, EveryDay());
        return (doctor, patient, pid);
    }

    private async Task<List<DateTime>> Slots(string token, DateTime day)
    {
        var res = await _f.ClientFor(token).GetAsync($"/api/portal/booking/slots?from={D(day)}&to={D(day)}");
        res.EnsureSuccessStatusCode();
        return (await Json(res)).EnumerateArray().Select(e => e.GetProperty("startsAt").GetDateTime().ToUniversalTime()).ToList();
    }

    private Task<HttpResponseMessage> Book(string token, DateTime at, string? reason = null) =>
        _f.ClientFor(token).PostAsJsonAsync("/api/portal/booking", new { startsAt = at, reason });

    // ── Availability management ──────────────────────────────────────────────

    [Fact]
    public async Task Doctor_can_save_and_read_weekly_availability_and_blocked_dates()
    {
        var doc = await _f.RegisterDoctorAsync("doc-avail@x.com");
        var c = _f.ClientFor(doc.Token);
        await SetWeekly(doc.Token, new { dayOfWeek = "Monday", startTime = "09:00", endTime = "12:00" },
            new { dayOfWeek = "Monday", startTime = "13:00", endTime = "17:00" });
        var date = DateTime.UtcNow.Date.AddDays(10);
        var add = await c.PostAsJsonAsync("/api/availability/blocked-dates", new { date = D(date), label = "Leave" });
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);

        var got = await Json(await c.GetAsync("/api/availability"));
        Assert.Equal(2, got.GetProperty("windows").GetArrayLength());
        Assert.Equal("Monday", got.GetProperty("windows")[0].GetProperty("dayOfWeek").GetString());
        Assert.Equal("09:00", got.GetProperty("windows")[0].GetProperty("startTime").GetString());
        Assert.Equal(1, got.GetProperty("blockedDates").GetArrayLength());

        var id = (await Json(add)).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/availability/blocked-dates/{id}")).StatusCode);
    }

    [Theory]
    [InlineData("10:00", "10:00")]
    [InlineData("11:00", "10:00")]
    [InlineData("09:10", "10:00")]
    [InlineData("nine", "10:00")]
    public async Task Invalid_windows_are_rejected(string start, string end)
    {
        var doc = await _f.RegisterDoctorAsync($"doc-inv-{start.Replace(":", "")}-{end.Replace(":", "")}@x.com");
        var res = await _f.ClientFor(doc.Token).PutAsJsonAsync("/api/availability/weekly",
            new { windows = new[] { new { dayOfWeek = "Monday", startTime = start, endTime = end } } });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Overlapping_windows_and_too_many_windows_and_bad_blocked_dates_are_rejected()
    {
        var doc = await _f.RegisterDoctorAsync("doc-ovl@x.com");
        var c = _f.ClientFor(doc.Token);
        var overlap = await c.PutAsJsonAsync("/api/availability/weekly", new { windows = new object[] {
            new { dayOfWeek = "Monday", startTime = "09:00", endTime = "12:00" },
            new { dayOfWeek = "Monday", startTime = "11:30", endTime = "13:00" } } });
        Assert.Equal(HttpStatusCode.BadRequest, overlap.StatusCode);

        var many = Enumerable.Range(0, 51).Select(_ => new { dayOfWeek = "Monday", startTime = "09:00", endTime = "09:30" });
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync("/api/availability/weekly", new { windows = many })).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/availability/blocked-dates",
            new { date = D(DateTime.UtcNow.Date.AddDays(-2)) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/availability/blocked-dates",
            new { date = D(DateTime.UtcNow.Date.AddDays(5)), label = new string('x', 201) })).StatusCode);
        var ok = new { date = D(DateTime.UtcNow.Date.AddDays(6)) };
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/availability/blocked-dates", ok)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/availability/blocked-dates", ok)).StatusCode);
    }

    [Fact]
    public async Task Patients_cannot_manage_availability_and_anonymous_is_rejected()
    {
        var (_, patient, _) = await Setup("avail-deny");
        var c = _f.ClientFor(patient.Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/availability")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PutAsJsonAsync("/api/availability/weekly", new { windows = EveryDay() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/availability/blocked-dates", new { date = D(DateTime.UtcNow.Date.AddDays(3)) })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.ClientFor().GetAsync("/api/availability")).StatusCode);
    }

    [Fact]
    public async Task Doctors_cannot_see_or_delete_each_others_availability()
    {
        var a = await _f.RegisterDoctorAsync("doc-a-av@x.com");
        var b = await _f.RegisterDoctorAsync("doc-b-av@x.com");
        await SetWeekly(a.Token, EveryDay());
        var add = await _f.ClientFor(a.Token).PostAsJsonAsync("/api/availability/blocked-dates", new { date = D(DateTime.UtcNow.Date.AddDays(4)) });
        var id = (await Json(add)).GetProperty("id").GetInt32();

        var seen = await Json(await _f.ClientFor(b.Token).GetAsync("/api/availability"));
        Assert.Equal(0, seen.GetProperty("windows").GetArrayLength());
        Assert.Equal(0, seen.GetProperty("blockedDates").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await _f.ClientFor(b.Token).DeleteAsync($"/api/availability/blocked-dates/{id}")).StatusCode);
    }

    // ── Slots ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Slots_come_from_availability_in_30_minute_units()
    {
        var (_, patient, _) = await Setup("slots");
        var slots = await Slots(patient.Token, Tomorrow(0));
        Assert.Equal(20, slots.Count); // 08:00-18:00
        Assert.Equal(Tomorrow(8), slots.First());
        Assert.Equal(Tomorrow(17, 30), slots.Last());
    }

    [Fact]
    public async Task Slots_exclude_blocked_dates_past_and_lead_time()
    {
        var (doc, patient, _) = await Setup("slots-ex");
        await _f.ClientFor(doc.Token).PostAsJsonAsync("/api/availability/blocked-dates", new { date = D(Tomorrow(0)) });
        Assert.Empty(await Slots(patient.Token, Tomorrow(0)));
        Assert.Empty(await Slots(patient.Token, DateTime.UtcNow.Date.AddDays(-1)));

        var today = await Slots(patient.Token, DateTime.UtcNow.Date);
        Assert.All(today, s => Assert.True(s >= DateTime.UtcNow.AddMinutes(60)));
    }

    [Fact]
    public async Task Slots_exclude_taken_and_overlapping_appointments_and_cancelled_ones_free_the_slot()
    {
        var (doc, patient, pid) = await Setup("slots-taken");
        var otherPid = await _f.CreatePatientAsync(doc.Token, "p-other-taken@x.com");
        int apptId = await _f.WithDbAsync(async db =>
        {
            // a manual 60 minute appointment at 10:00 blocks the 10:00 and 10:30 slots
            var a = new Appointment { PatientId = otherPid, DoctorId = doc.UserId, ScheduledAt = Tomorrow(10),
                DurationMinutes = 60, Type = AppointmentType.CheckUp, Status = AppointmentStatus.Confirmed };
            db.Appointments.Add(a);
            await db.SaveChangesAsync();
            return a.Id;
        });
        var slots = await Slots(patient.Token, Tomorrow(0));
        Assert.DoesNotContain(Tomorrow(10), slots);
        Assert.DoesNotContain(Tomorrow(10, 30), slots);
        Assert.Contains(Tomorrow(9, 30), slots);
        Assert.Contains(Tomorrow(11), slots);

        await _f.WithDbAsync(async db =>
        {
            (await db.Appointments.FirstAsync(a => a.Id == apptId)).Status = AppointmentStatus.Cancelled;
            await db.SaveChangesAsync();
            return 0;
        });
        Assert.Contains(Tomorrow(10), await Slots(patient.Token, Tomorrow(0)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(31)]
    public async Task Slot_range_is_validated(int extraDays)
    {
        var (_, patient, _) = await Setup($"range{extraDays}");
        var from = DateTime.UtcNow.Date.AddDays(1);
        var to = from.AddDays(extraDays);
        var res = await _f.ClientFor(patient.Token).GetAsync($"/api/portal/booking/slots?from={D(from)}&to={D(to)}");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        // exactly 31 days (30 days apart) is allowed
        var ok = await _f.ClientFor(patient.Token).GetAsync($"/api/portal/booking/slots?from={D(from)}&to={D(from.AddDays(30))}");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task Slots_only_show_the_patients_own_doctor()
    {
        var (_, patient, _) = await Setup("own-doc");
        var otherDoc = await _f.RegisterDoctorAsync("doc-other-own@x.com");
        await SetWeekly(otherDoc.Token, EveryDay("06:00", "07:00"));
        var slots = await Slots(patient.Token, Tomorrow(0));
        Assert.DoesNotContain(Tomorrow(6), slots);
        Assert.Contains(Tomorrow(8), slots);
    }

    // ── Booking ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Booking_creates_a_pending_appointment_visible_to_doctor_and_patient_and_removes_the_slot()
    {
        var (doc, patient, pid) = await Setup("book");
        var at = Tomorrow(9);
        var res = await Book(patient.Token, at, "Sore throat");
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain("doctorId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("notes", body, StringComparison.OrdinalIgnoreCase);

        var list = await Json(await _f.ClientFor(doc.Token).GetAsync("/api/appointments?pageSize=100"));
        var mine = list.GetProperty("items").EnumerateArray().Single(a => a.GetProperty("reason").GetString() == "Sore throat");
        Assert.Equal("Pending", mine.GetProperty("status").GetString());
        Assert.Equal(pid, mine.GetProperty("patientId").GetInt32());
        Assert.Equal(30, mine.GetProperty("durationMinutes").GetInt32());

        Assert.Contains("Sore throat", await (await _f.ClientFor(patient.Token).GetAsync("/api/portal/appointments")).Content.ReadAsStringAsync());
        Assert.DoesNotContain(at, await Slots(patient.Token, Tomorrow(0)));
    }

    [Fact]
    public async Task Booking_the_same_slot_twice_conflicts()
    {
        var (doc, p1, _) = await Setup("dbl");
        var pid2 = await _f.CreatePatientAsync(doc.Token, "p2-dbl@x.com");
        var p2 = await _f.OnboardPatientAsync(doc.Token, pid2, "p2-dbl@x.com");
        var at = Tomorrow(9);
        Assert.Equal(HttpStatusCode.Created, (await Book(p1.Token, at)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Book(p2.Token, at)).StatusCode);
        // overlapping with the 09:00-09:30 booking only by being the same unit; adjacent slot is fine
        Assert.Equal(HttpStatusCode.Created, (await Book(p2.Token, Tomorrow(9, 30))).StatusCode);
    }

    [Fact]
    public async Task Concurrent_bookings_of_one_slot_yield_exactly_one_appointment()
    {
        var (doc, p1, _) = await Setup("conc");
        var tokens = new List<string> { p1.Token };
        for (var i = 0; i < 4; i++)
        {
            var pid = await _f.CreatePatientAsync(doc.Token, $"p{i}-conc@x.com");
            tokens.Add((await _f.OnboardPatientAsync(doc.Token, pid, $"p{i}-conc@x.com")).Token);
        }
        var at = Tomorrow(14);
        var results = await Task.WhenAll(tokens.Select(t => Book(t, at)));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(4, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        var count = await _f.WithDbAsync(db => db.Appointments.CountAsync(a => a.DoctorId == doc.UserId && a.ScheduledAt == at));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Times_that_are_not_open_slots_are_rejected()
    {
        var (doc, patient, _) = await Setup("notopen");
        var blockedDay = DateTime.UtcNow.Date.AddDays(3);
        await _f.ClientFor(doc.Token).PostAsJsonAsync("/api/availability/blocked-dates", new { date = D(blockedDay) });
        var bad = new[]
        {
            Tomorrow(7),                         // before availability
            Tomorrow(18),                        // after availability
            Tomorrow(9, 15),                     // not on a slot boundary
            blockedDay.AddHours(10),             // blocked date
            DateTime.UtcNow.AddHours(-3),        // past
            DateTime.UtcNow.Date.AddDays(90).AddHours(10), // beyond horizon
        };
        foreach (var at in bad)
            Assert.Equal(HttpStatusCode.BadRequest, (await Book(patient.Token, at)).StatusCode);
        // within the 60 minute lead time
        var soon = new DateTime((DateTime.UtcNow.AddMinutes(20).Ticks / TimeSpan.TicksPerMinute / 30 + 1) * TimeSpan.TicksPerMinute * 30, DateTimeKind.Utc);
        if (soon < DateTime.UtcNow.AddMinutes(60))
            Assert.Equal(HttpStatusCode.BadRequest, (await Book(patient.Token, soon)).StatusCode);
    }

    [Fact]
    public async Task Booking_without_any_availability_is_rejected()
    {
        var (_, patient, _) = await Setup("noavail", availability: false);
        Assert.Empty(await Slots(patient.Token, Tomorrow(0)));
        Assert.Equal(HttpStatusCode.BadRequest, (await Book(patient.Token, Tomorrow(9))).StatusCode);
    }

    [Fact]
    public async Task Reason_length_is_limited()
    {
        var (_, patient, _) = await Setup("reason");
        Assert.Equal(HttpStatusCode.BadRequest, (await Book(patient.Token, Tomorrow(9), new string('r', 501))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Book(patient.Token, Tomorrow(9), new string('r', 500))).StatusCode);
    }

    [Fact]
    public async Task A_patient_may_hold_at_most_three_upcoming_appointments_and_cannot_overlap_themselves()
    {
        var (doc, patient, pid) = await Setup("limit");
        Assert.Equal(HttpStatusCode.Created, (await Book(patient.Token, Tomorrow(9))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Book(patient.Token, Tomorrow(10))).StatusCode);
        // patient overlap: a manual appointment by this patient (other time-slot unit) blocks that slot
        await _f.WithDbAsync(async db =>
        {
            db.Appointments.Add(new Appointment { PatientId = pid, DoctorId = doc.UserId, ScheduledAt = Tomorrow(12),
                DurationMinutes = 30, Type = AppointmentType.CheckUp, Status = AppointmentStatus.Confirmed });
            await db.SaveChangesAsync();
            return 0;
        });
        Assert.Equal(HttpStatusCode.Conflict, (await Book(patient.Token, Tomorrow(12))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Book(patient.Token, Tomorrow(15))).StatusCode); // 3 upcoming already
    }

    [Fact]
    public async Task Doctor_tokens_and_anonymous_cannot_use_portal_booking()
    {
        var (doc, _, _) = await Setup("booking-deny");
        var day = D(Tomorrow(0));
        Assert.Equal(HttpStatusCode.Forbidden, (await _f.ClientFor(doc.Token).GetAsync($"/api/portal/booking/slots?from={day}&to={day}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Book(doc.Token, Tomorrow(9))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Book(null!, Tomorrow(9))).StatusCode);
    }

    [Theory]
    [InlineData(PatientStatus.Inactive)]
    [InlineData(PatientStatus.Deceased)]
    public async Task Non_active_patients_cannot_list_slots_or_book(PatientStatus status)
    {
        var (_, patient, pid) = await Setup($"status-{status}");
        await _f.WithDbAsync(async db =>
        {
            (await db.Patients.FirstAsync(p => p.Id == pid)).Status = status;
            await db.SaveChangesAsync();
            return 0;
        });
        var day = D(Tomorrow(0));
        Assert.Equal(HttpStatusCode.Forbidden, (await _f.ClientFor(patient.Token).GetAsync($"/api/portal/booking/slots?from={day}&to={day}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Book(patient.Token, Tomorrow(9))).StatusCode);
    }

    [Fact]
    public async Task Booking_is_made_for_the_token_patient_and_never_leaks_other_patients()
    {
        var (doc, p1, pid1) = await Setup("iso-b");
        var pid2 = await _f.CreatePatientAsync(doc.Token, "p2-iso-b@x.com", "Zed");
        var p2 = await _f.OnboardPatientAsync(doc.Token, pid2, "p2-iso-b@x.com");
        Assert.Equal(HttpStatusCode.Created, (await Book(p1.Token, Tomorrow(9), "ONLY-P1")).StatusCode);

        var p2Appts = await (await _f.ClientFor(p2.Token).GetAsync("/api/portal/appointments")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("ONLY-P1", p2Appts);
        var slotsBody = await (await _f.ClientFor(p2.Token).GetAsync($"/api/portal/booking/slots?from={D(Tomorrow(0))}&to={D(Tomorrow(0))}")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("ONLY-P1", slotsBody);
        Assert.DoesNotContain("patient", slotsBody, StringComparison.OrdinalIgnoreCase);
        var owner = await _f.WithDbAsync(db => db.Appointments.Where(a => a.Reason == "ONLY-P1").Select(a => a.PatientId).SingleAsync());
        Assert.Equal(pid1, owner);
    }
}
