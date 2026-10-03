using MedFlow.Api.Extensions;
using MedFlow.Api.Localization;
using MedFlow.Api.Reports;
using MedFlow.Core;
using MedFlow.Core.DTOs;
using MedFlow.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedFlow.Api.Controllers;

/// <summary>Practice reports for the signed-in doctor. There is no clinic layer yet, so every figure is scoped to the caller.</summary>
[ApiController]
[Route("api/reports")]
[Authorize(Roles = Roles.Doctor)]
public class ReportsController : ControllerBase
{
    public const int MaxRangeDays = 366;
    public const int MaxPageSize = 100;
    public const int MaxCsvRows = 5000;

    private readonly IReportRepository _reports;
    public ReportsController(IReportRepository reports) => _reports = reports;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Defaults to the last 30 days; returns an error result when the range is invalid.</summary>
    private bool TryRange(DateOnly? from, DateOnly? to, out DateOnly f, out DateOnly t, out ActionResult? error)
    {
        t = to ?? Today;
        f = from ?? t.AddDays(-29);
        error = null;
        if (f > t) error = BadRequest(new { error = this.T("Reports.DateRange") });
        else if (t.DayNumber - f.DayNumber + 1 > MaxRangeDays) error = BadRequest(new { error = this.T("Reports.RangeTooLong", MaxRangeDays) });
        return error == null;
    }

    [HttpGet("revenue")]
    public async Task<ActionResult<RevenueReport>> Revenue([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] ReportPeriod period = ReportPeriod.Day)
    {
        if (!TryRange(from, to, out var f, out var t, out var err)) return err!;
        return Ok(await _reports.GetRevenueAsync(User.GetUserId(), f, t, period));
    }

    [HttpGet("revenue/export")]
    public async Task<IActionResult> RevenueExport([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] ReportPeriod period = ReportPeriod.Day)
    {
        if (!TryRange(from, to, out var f, out var t, out var err)) return err!;
        var r = await _reports.GetRevenueAsync(User.GetUserId(), f, t, period);
        var csv = new CsvWriter().Row(H("Period"), H("Revenue"), H("InvoicesPaid"));
        foreach (var p in r.Series) csv.Row(CsvWriter.Date(p.PeriodStart), CsvWriter.Number(p.Revenue), CsvWriter.Number(p.InvoicesPaid));
        return Csv(csv, "revenue", f, t);
    }

    [HttpGet("visits")]
    public async Task<ActionResult<VisitsReport>> Visits([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] ReportPeriod period = ReportPeriod.Day)
    {
        if (!TryRange(from, to, out var f, out var t, out var err)) return err!;
        return Ok(await _reports.GetVisitsAsync(User.GetUserId(), f, t, period));
    }

    [HttpGet("visits/export")]
    public async Task<IActionResult> VisitsExport([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] ReportPeriod period = ReportPeriod.Day)
    {
        if (!TryRange(from, to, out var f, out var t, out var err)) return err!;
        var r = await _reports.GetVisitsAsync(User.GetUserId(), f, t, period);
        var csv = new CsvWriter().Row(H("Period"), H("Visits"));
        foreach (var p in r.Series) csv.Row(CsvWriter.Date(p.PeriodStart), CsvWriter.Number(p.Visits));
        return Csv(csv, "visits", f, t);
    }

    [HttpGet("no-shows")]
    public async Task<ActionResult<NoShowReport>> NoShows([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] ReportPeriod period = ReportPeriod.Day)
    {
        if (!TryRange(from, to, out var f, out var t, out var err)) return err!;
        return Ok(await _reports.GetNoShowsAsync(User.GetUserId(), f, t, period));
    }

    [HttpGet("no-shows/export")]
    public async Task<IActionResult> NoShowsExport([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] ReportPeriod period = ReportPeriod.Day)
    {
        if (!TryRange(from, to, out var f, out var t, out var err)) return err!;
        var r = await _reports.GetNoShowsAsync(User.GetUserId(), f, t, period);
        var csv = new CsvWriter().Row(H("Period"), H("Completed"), H("NoShows"), H("NoShowRate"));
        foreach (var p in r.Series)
            csv.Row(CsvWriter.Date(p.PeriodStart), CsvWriter.Number(p.Completed), CsvWriter.Number(p.NoShows),
                p.Rate.HasValue ? CsvWriter.Number(p.Rate.Value * 100m) : "");
        return Csv(csv, "no-shows", f, t);
    }

    [HttpGet("ar-aging")]
    public async Task<ActionResult<ArAgingReport>> ArAging([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize)
            return BadRequest(new { error = this.T("Reports.PageRange", MaxPageSize) });
        return Ok(await _reports.GetArAgingAsync(User.GetUserId(), Today, page, pageSize));
    }

    [HttpGet("ar-aging/export")]
    public async Task<IActionResult> ArAgingExport()
    {
        var rows = await _reports.GetArRowsAsync(User.GetUserId(), Today, MaxCsvRows);
        var csv = new CsvWriter().Row(H("Invoice"), H("Patient"), H("InvoiceDate"), H("DueDate"), H("DaysPastDue"), H("Bucket"), H("Balance"));
        foreach (var r in rows)
            csv.Row(CsvWriter.Text(r.InvoiceNumber), CsvWriter.Text(r.PatientName), CsvWriter.Date(r.InvoiceDate),
                r.DueDate.HasValue ? CsvWriter.Date(r.DueDate.Value) : "", CsvWriter.Number(r.DaysPastDue),
                CsvWriter.Text(r.Bucket), CsvWriter.Number(r.Balance));
        return Csv(csv, "ar-aging", Today, Today);
    }

    private string H(string col) => CsvWriter.Text(this.T("Reports.Col." + col));

    private FileContentResult Csv(CsvWriter csv, string name, DateOnly from, DateOnly to)
    {
        Response.Headers.CacheControl = "no-store";
        return File(csv.ToBytes(), "text/csv; charset=utf-8", $"{name}-{CsvWriter.Date(from)}-{CsvWriter.Date(to)}.csv");
    }
}
