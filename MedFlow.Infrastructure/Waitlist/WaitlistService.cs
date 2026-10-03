using System.ComponentModel.DataAnnotations;
using System.Net;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using MedFlow.Infrastructure.Data;
using MedFlow.Infrastructure.Reminders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedFlow.Infrastructure.Waitlist;

/// <summary>
/// Offers a freed slot to the earliest-joined waiting patients. No slot is held: the atomic booking path decides
/// the winner, so several patients may be notified safely. Offers only carry first name, doctor name and time.
/// </summary>
public class WaitlistService : IWaitlistService
{
    private readonly AppDbContext _db;
    private readonly IBookingRepository _booking;
    private readonly IEmailSender _email;
    private readonly WaitlistSettings _settings;
    private readonly IConfiguration _config;
    private readonly ILogger<WaitlistService> _logger;

    public WaitlistService(AppDbContext db, IBookingRepository booking, IEmailSender email,
        IOptions<WaitlistSettings> settings, IConfiguration config, ILogger<WaitlistService> logger)
    {
        _db = db; _booking = booking; _email = email; _settings = settings.Value; _config = config; _logger = logger;
    }

    public async Task<int> OfferFreedSlotAsync(string doctorId, DateTime slotStartsAt)
    {
        try
        {
            var start = slotStartsAt.Kind == DateTimeKind.Utc ? slotStartsAt : slotStartsAt.ToUniversalTime();
            var now = DateTime.UtcNow;
            if (start <= now || !await _booking.IsSlotOpenAsync(doctorId, start)) return 0;

            var waiting = await (from w in _db.WaitlistEntries
                                 join p in _db.Patients on w.PatientId equals p.Id
                                 where w.DoctorId == doctorId && w.Status == WaitlistStatus.Waiting
                                       && p.Status == PatientStatus.Active && p.DoctorId == doctorId
                                 orderby w.CreatedAt, w.Id
                                 select new { w.Id, p.FirstName, p.Email }).ToListAsync();
            var alreadyOffered = (await _db.WaitlistOffers
                .Where(o => o.SlotStartsAt == start).Select(o => o.EntryId).ToListAsync()).ToHashSet();
            var targets = waiting
                .Where(w => !alreadyOffered.Contains(w.Id) && !string.IsNullOrWhiteSpace(w.Email)
                            && new EmailAddressAttribute().IsValid(w.Email))
                .Take(_settings.EffectiveMaxOffersPerSlot).ToList();
            if (targets.Count == 0) return 0;

            var doctorName = await _db.Doctors.Where(d => d.UserId == doctorId)
                .Select(d => d.FirstName + " " + d.LastName).FirstOrDefaultAsync() ?? "";
            var expires = now.AddHours(_settings.EffectiveOfferHours);
            if (expires > start) expires = start;

            var sent = 0;
            foreach (var t in targets)
            {
                try
                {
                    var token = ReminderTokens.Generate();
                    var offer = new WaitlistOffer
                    {
                        EntryId = t.Id, SlotStartsAt = start, ExpiresAt = expires, TokenHash = ReminderTokens.Hash(token)
                    };
                    _db.WaitlistOffers.Add(offer);
                    await _db.SaveChangesAsync(); // claimed before sending; the unique index allows one offer per entry and slot
                    await _email.SendAsync(t.Email, "Hay un horario disponible / A slot has opened up",
                        BuildBody(t.FirstName, doctorName, start, expires, token));
                    offer.SentAt = DateTime.UtcNow;
                    offer.UpdatedAt = offer.SentAt.Value;
                    await _db.SaveChangesAsync();
                    sent++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Waitlist offer failed for entry {EntryId}", t.Id);
                }
            }
            return sent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Waitlist processing failed for a freed slot");
            return 0;
        }
    }

    private string BuildBody(string firstName, string doctor, DateTime start, DateTime expires, string token)
    {
        var frontendUrl = (_config.GetSection("AllowedOrigins").Get<string[]>()?.FirstOrDefault()
            ?? "http://localhost:5173").TrimEnd('/');
        var link = $"{frontendUrl}/waitlist-offer?token={WebUtility.UrlEncode(token)}";
        var name = WebUtility.HtmlEncode(firstName);
        var dr = WebUtility.HtmlEncode(doctor);
        var when = $"{start:yyyy-MM-dd HH:mm} UTC";
        var until = $"{expires:yyyy-MM-dd HH:mm} UTC";
        return $"<p>Hola {name},</p>" +
               $"<p>Se ha liberado un horario con {dr} el <strong>{when}</strong>. Si lo desea, puede reservarlo; se asigna a la primera persona que lo confirme.</p>" +
               $"<p><a href=\"{link}\">Ver el horario y reservarlo</a> (disponible hasta {until}). El mismo enlace permite salir de la lista de espera.</p>" +
               "<hr/>" +
               $"<p>Hello {name},</p>" +
               $"<p>A slot with {dr} has opened up on <strong>{when}</strong>. You can book it if you like; it goes to the first person who confirms.</p>" +
               $"<p><a href=\"{link}\">View the slot and book it</a> (available until {until}). The same link lets you leave the waitlist.</p>" +
               "<p>— The MedFlow team</p>";
    }
}
