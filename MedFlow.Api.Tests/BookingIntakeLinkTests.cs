using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Api.Tests;

/// <summary>Issue #12: the booking confirmation carries a fresh intake link.</summary>
public class BookingIntakeLinkTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _f;
    public BookingIntakeLinkTests(TestApiFactory f) => _f = f;

    private static DateTime Tomorrow(int hour, int minute = 0) =>
        DateTime.UtcNow.Date.AddDays(1).AddHours(hour).AddMinutes(minute);

    private async Task<(AuthResult Doctor, AuthResult Patient, int PatientId)> Setup(string tag)
    {
        var doctor = await _f.RegisterDoctorAsync($"doc-{tag}@x.com");
        var pid = await _f.CreatePatientAsync(doctor.Token, $"p-{tag}@x.com");
        var patient = await _f.OnboardPatientAsync(doctor.Token, pid, $"p-{tag}@x.com");
        var windows = Enum.GetNames<DayOfWeek>().Select(d => new { dayOfWeek = d, startTime = "08:00", endTime = "18:00" });
        (await _f.ClientFor(doctor.Token).PutAsJsonAsync("/api/availability/weekly", new { windows })).EnsureSuccessStatusCode();
        return (doctor, patient, pid);
    }

    private Task<HttpResponseMessage> Book(string token, DateTime at) =>
        _f.ClientFor(token).PostAsJsonAsync("/api/portal/booking", new { startsAt = at });

    private List<(string To, string Subject, string Body)> EmailsTo(string to)
    {
        lock (_f.Email.Sent) return _f.Email.Sent.Where(e => e.To == to).ToList();
    }

    [Fact]
    public async Task Booking_emails_a_working_intake_link_once()
    {
        var (_, patient, pid) = await Setup("bil1");
        var res = await Book(patient.Token, Tomorrow(9));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var mails = EmailsTo("p-bil1@x.com").Where(e => e.Subject == "Your appointment is booked").ToList();
        var mail = Assert.Single(mails);
        var token = Regex.Match(mail.Body, @"/intake/([0-9A-F]+)").Groups[1].Value;
        Assert.NotEmpty(token);
        Assert.Equal(HttpStatusCode.OK, (await _f.CreateClient().GetAsync($"/api/intake/{token}")).StatusCode);

        // A retry of the same slot is rejected and sends nothing; a second booking reuses the live link (no new email)
        Assert.Equal(HttpStatusCode.Conflict, (await Book(patient.Token, Tomorrow(9))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Book(patient.Token, Tomorrow(10))).StatusCode);
        Assert.Single(EmailsTo("p-bil1@x.com").Where(e => e.Subject == "Your appointment is booked"));
        Assert.Equal(1, await _f.WithDbAsync(db => db.IntakeLinks.CountAsync(l => l.PatientId == pid)));
    }

    [Fact]
    public async Task No_link_when_intake_already_pending_or_accepted()
    {
        var (doctor, patient, pid) = await Setup("bil2");
        await _f.WithDbAsync(async db =>
        {
            var link = new IntakeLink { PatientId = pid, Email = "p-bil2@x.com", TokenHash = "x", ExpiresAt = DateTime.UtcNow.AddDays(1), UsedAt = DateTime.UtcNow };
            db.IntakeLinks.Add(link);
            await db.SaveChangesAsync();
            db.IntakeSubmissions.Add(new IntakeSubmission
            {
                PatientId = pid, DoctorId = doctor.UserId, IntakeLinkId = link.Id, Status = IntakeStatus.Pending,
                FirstName = "a", LastName = "b", Phone = "1", SignatureName = "a b", ConsentAgreed = true
            });
            await db.SaveChangesAsync();
            return 0;
        });
        Assert.Equal(HttpStatusCode.Created, (await Book(patient.Token, Tomorrow(9))).StatusCode);
        Assert.Empty(EmailsTo("p-bil2@x.com").Where(e => e.Subject == "Your appointment is booked"));
    }

    [Fact]
    public async Task No_email_when_patient_has_no_email()
    {
        var (_, patient, pid) = await Setup("bil3");
        await _f.WithDbAsync(async db =>
        {
            (await db.Patients.FirstAsync(p => p.Id == pid)).Email = "";
            await db.SaveChangesAsync();
            return 0;
        });
        Assert.Equal(HttpStatusCode.Created, (await Book(patient.Token, Tomorrow(9))).StatusCode);
        Assert.Empty(EmailsTo("p-bil3@x.com").Where(e => e.Subject == "Your appointment is booked"));
        Assert.Equal(0, await _f.WithDbAsync(db => db.IntakeLinks.CountAsync(l => l.PatientId == pid)));
    }

    [Fact]
    public async Task Email_failure_does_not_fail_the_booking()
    {
        var (_, patient, pid) = await Setup("bil4");
        _f.Email.Fail = true;
        try
        {
            Assert.Equal(HttpStatusCode.Created, (await Book(patient.Token, Tomorrow(9))).StatusCode);
        }
        finally { _f.Email.Fail = false; }
        Assert.Equal(1, await _f.WithDbAsync(db => db.Appointments.CountAsync(a => a.PatientId == pid)));
    }
}
