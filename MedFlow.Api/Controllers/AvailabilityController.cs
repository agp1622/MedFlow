using System.Globalization;
using MedFlow.Api.Extensions;
using MedFlow.Core;
using MedFlow.Core.DTOs;
using MedFlow.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedFlow.Api.Controllers;

/// <summary>Doctor-managed weekly availability and blocked dates. Always scoped to the caller.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = Roles.Doctor)]
public class AvailabilityController : ControllerBase
{
    private const int MaxWindows = 50;
    private const int MaxLabel = 200;
    private readonly IBookingRepository _booking;

    public AvailabilityController(IBookingRepository booking) => _booking = booking;

    [HttpGet]
    public async Task<ActionResult<AvailabilityDto>> Get() =>
        Ok(await _booking.GetAvailabilityAsync(User.GetUserId()));

    [HttpPut("weekly")]
    public async Task<IActionResult> SetWeekly([FromBody] SetWeeklyAvailabilityRequest req)
    {
        if (req.Windows == null || req.Windows.Count > MaxWindows)
            return BadRequest(new { error = $"Provide at most {MaxWindows} windows." });

        var parsed = new List<(DayOfWeek Day, TimeOnly Start, TimeOnly End)>();
        foreach (var w in req.Windows)
        {
            if (!Enum.IsDefined(w.DayOfWeek)
                || !TimeOnly.TryParseExact(w.StartTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var s)
                || !TimeOnly.TryParseExact(w.EndTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var e))
                return BadRequest(new { error = "Windows need a valid day and HH:mm start and end times." });
            if (e <= s) return BadRequest(new { error = "End time must be after start time." });
            if (s.Minute % 30 != 0 || e.Minute % 30 != 0)
                return BadRequest(new { error = "Times must be on 30-minute boundaries." });
            parsed.Add((w.DayOfWeek, s, e));
        }
        foreach (var day in parsed.GroupBy(p => p.Day))
        {
            var ordered = day.OrderBy(p => p.Start).ToList();
            for (var i = 1; i < ordered.Count; i++)
                if (ordered[i].Start < ordered[i - 1].End)
                    return BadRequest(new { error = "Windows on the same day must not overlap." });
        }
        return Ok(await _booking.ReplaceWeeklyAsync(User.GetUserId(), parsed));
    }

    [HttpPost("blocked-dates")]
    public async Task<IActionResult> AddBlockedDate([FromBody] CreateBlockedDateRequest req)
    {
        if (req.Date < DateOnly.FromDateTime(DateTime.UtcNow))
            return BadRequest(new { error = "Blocked date cannot be in the past." });
        var label = string.IsNullOrWhiteSpace(req.Label) ? null : req.Label.Trim();
        if (label != null && label.Length > MaxLabel)
            return BadRequest(new { error = $"Label must be at most {MaxLabel} characters." });
        var created = await _booking.AddBlockedDateAsync(User.GetUserId(), req.Date, label);
        if (created == null) return BadRequest(new { error = "That date is already blocked." });
        return Created($"/api/availability/blocked-dates/{created.Id}", created);
    }

    [HttpDelete("blocked-dates/{id:int}")]
    public async Task<IActionResult> RemoveBlockedDate(int id) =>
        await _booking.RemoveBlockedDateAsync(User.GetUserId(), id) ? NoContent() : NotFound();
}
