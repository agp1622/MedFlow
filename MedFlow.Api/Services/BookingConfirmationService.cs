using System.ComponentModel.DataAnnotations;
using System.Net;
using MedFlow.Core.DTOs;
using MedFlow.Core.Entities;
using MedFlow.Core.Interfaces;

namespace MedFlow.Api.Services;

/// <summary>
/// Emails a booking confirmation carrying a fresh intake-form link, when the patient has an email and no
/// pending/accepted intake (or still-valid link). Best effort: failures are logged and never fail the booking.
/// </summary>
public class BookingConfirmationService
{
    private readonly IIntakeRepository _intake;
    private readonly IEmailSender _email;
    private readonly IConfiguration _config;
    private readonly ILogger<BookingConfirmationService> _logger;

    public BookingConfirmationService(IIntakeRepository intake, IEmailSender email, IConfiguration config,
        ILogger<BookingConfirmationService> logger)
    {
        _intake = intake;
        _email = email;
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(Patient patient, PortalAppointmentDto appt)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(patient.Email) || !new EmailAddressAttribute().IsValid(patient.Email)) return;
            if (!await _intake.NeedsLinkAsync(patient.Id)) return;

            var (token, expiresAt) = await _intake.CreateLinkAsync(patient);
            var frontendUrl = (_config.GetSection("AllowedOrigins").Get<string[]>()?.FirstOrDefault()
                ?? "http://localhost:5173").TrimEnd('/');
            var link = $"{frontendUrl}/intake/{token}";

            await _email.SendAsync(patient.Email, "Your appointment is booked",
                $"<p>Hello {WebUtility.HtmlEncode(patient.FirstName)},</p>" +
                $"<p>Your appointment on {appt.ScheduledAt:yyyy-MM-dd HH:mm} UTC has been booked.</p>" +
                "<p>To speed up check-in, please complete your intake form online before your visit.</p>" +
                $"<p><a href=\"{link}\">Complete my intake form</a></p>" +
                $"<p>This link is personal to you and expires on {expiresAt:yyyy-MM-dd}. If you weren't expecting this, you can ignore this email.</p>" +
                "<p>— The MedFlow team</p>");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send booking confirmation for patient {PatientId}", patient.Id);
        }
    }
}
