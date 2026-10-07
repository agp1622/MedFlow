using System.ComponentModel.DataAnnotations;
using System.Net;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using MedFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedFlow.Infrastructure.Reminders;

public class ReminderProcessor : IReminderProcessor
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _email;
    private readonly ReminderSettings _settings;
    private readonly IConfiguration _config;
    private readonly ILogger<ReminderProcessor> _logger;

    public ReminderProcessor(AppDbContext db, IEmailSender email, IOptions<ReminderSettings> settings,
        IConfiguration config, ILogger<ReminderProcessor> logger)
    {
        _db = db; _email = email; _settings = settings.Value; _config = config; _logger = logger;
    }

    public async Task<int> ProcessDueAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var horizon = now.AddHours(_settings.EffectiveLeadTimeHours);
        var maxAttempts = _settings.EffectiveMaxAttempts;

        var due = await _db.Appointments.Include(a => a.Patient).Include(a => a.Doctor)
            .Where(a => a.ScheduledAt > now && a.ScheduledAt <= horizon &&
                        (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed))
            .ToListAsync(ct);

        var sent = 0;
        foreach (var appt in due)
        {
            ct.ThrowIfCancellationRequested();
            try { if (await ProcessOneAsync(appt, maxAttempts, ct)) sent++; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reminder processing failed for appointment {AppointmentId}", appt.Id);
            }
        }
        if (sent > 0) _logger.LogInformation("Sent {Count} appointment reminders", sent);
        return sent;
    }

    private async Task<bool> ProcessOneAsync(Appointment appt, int maxAttempts, CancellationToken ct)
    {
        var reminder = await _db.AppointmentReminders
            .FirstOrDefaultAsync(r => r.AppointmentId == appt.Id && r.ScheduledAt == appt.ScheduledAt, ct);

        if (reminder?.Status == ReminderStatus.Sent) return false;
        if (reminder?.Status == ReminderStatus.Failed && reminder.Attempts >= maxAttempts) return false;

        var patient = appt.Patient;
        var email = patient?.Email;
        var hasEmail = !string.IsNullOrWhiteSpace(email) && new EmailAddressAttribute().IsValid(email);

        if (reminder == null)
        {
            reminder = new AppointmentReminder { AppointmentId = appt.Id, ScheduledAt = appt.ScheduledAt };
            _db.AppointmentReminders.Add(reminder);
        }

        if (!hasEmail)
        {
            // Logged once; re-evaluated on later runs in case an email is added
            if (reminder.Status != ReminderStatus.Skipped)
            {
                reminder.Status = ReminderStatus.Skipped;
                await SaveAsync(reminder, ReminderOutcome.Skipped, "Patient has no valid email address", ct);
            }
            return false;
        }

        // Claim the attempt before sending so an overlapping run cannot double-send
        var token = ReminderTokens.Generate();
        reminder.TokenHash = ReminderTokens.Hash(token);
        reminder.Attempts++;
        reminder.Status = ReminderStatus.Failed;
        await _db.SaveChangesAsync(ct);

        try
        {
            await _email.SendAsync(email!, "Reminder: your upcoming appointment", BuildBody(appt, token));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reminder email failed for appointment {AppointmentId} (attempt {Attempt})",
                appt.Id, reminder.Attempts);
            await SaveAsync(reminder, ReminderOutcome.Failed, "Email could not be delivered", ct);
            return false;
        }

        reminder.Status = ReminderStatus.Sent;
        reminder.SentAt = DateTime.UtcNow;
        await SaveAsync(reminder, ReminderOutcome.Sent, null, ct);
        return true;
    }

    private async Task SaveAsync(AppointmentReminder reminder, ReminderOutcome outcome, string? reason, CancellationToken ct)
    {
        reminder.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct); // ensures reminder.Id exists
        _db.ReminderDeliveries.Add(new ReminderDelivery
        {
            AppointmentId = reminder.AppointmentId, ReminderId = reminder.Id,
            Outcome = outcome, Reason = reason
        });
        await _db.SaveChangesAsync(ct);
    }

    private string BuildBody(Appointment appt, string token)
    {
        var frontendUrl = (_config.GetSection("AllowedOrigins").Get<string[]>()?.FirstOrDefault()
            ?? "http://localhost:5173").TrimEnd('/');
        var link = $"{frontendUrl}/appointment-response?token={WebUtility.UrlEncode(token)}";
        var doctor = appt.Doctor?.FullName ?? "your doctor";
        var where = string.IsNullOrWhiteSpace(appt.Location) ? "" : $"<p>Location: {WebUtility.HtmlEncode(appt.Location)}</p>";
        return $"<p>Hello {WebUtility.HtmlEncode(appt.Patient?.FirstName ?? "")},</p>" +
               $"<p>This is a reminder of your appointment with {WebUtility.HtmlEncode(doctor)} on " +
               $"<strong>{appt.ScheduledAt:yyyy-MM-dd HH:mm} UTC</strong>.</p>" + where +
               $"<p><a href=\"{link}\">Confirm or cancel my appointment</a></p>" +
               "<p>— The MedFlow team</p>";
    }
}
