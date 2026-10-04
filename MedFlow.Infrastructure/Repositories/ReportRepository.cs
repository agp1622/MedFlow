using MedFlow.Core;
using MedFlow.Core.DTOs;
using MedFlow.Core.Enums;
using MedFlow.Core.Interfaces;
using MedFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MedFlow.Infrastructure.Repositories;

/// <summary>
/// Report aggregates: clinic-wide for Owners, limited to the caller's own doctor-linked records for everyone else. Rows are grouped per calendar day by the database (at most one row
/// per day in the requested range), then folded into weeks or months in memory.
/// </summary>
public class ReportRepository : IReportRepository
{
    private static readonly string[] BucketNames = { "current", "1-30", "31-60", "61-90", "90+" };
    private readonly AppDbContext _db;
    public ReportRepository(AppDbContext db) => _db = db;

    private IQueryable<Core.Entities.Invoice> InvoicesOf(ClinicScope scope)
    {
        var q = _db.Invoices.Where(i => i.ClinicId == scope.ClinicId);
        return scope.OwnDataOnly ? q.Where(i => i.DoctorId == scope.UserId) : q;
    }

    private IQueryable<Core.Entities.Appointment> AppointmentsOf(ClinicScope scope)
    {
        var q = _db.Appointments.Where(a => a.ClinicId == scope.ClinicId);
        return scope.OwnDataOnly ? q.Where(a => a.DoctorId == scope.UserId) : q;
    }

    private static DateTime Start(DateOnly d) => d.ToDateTime(TimeOnly.MinValue);

    public static DateOnly PeriodStart(DateOnly day, ReportPeriod period) => period switch
    {
        ReportPeriod.Week => day.AddDays(-(((int)day.DayOfWeek + 6) % 7)), // ISO weeks start on Monday
        ReportPeriod.Month => new DateOnly(day.Year, day.Month, 1),
        _ => day
    };

    public async Task<RevenueReport> GetRevenueAsync(ClinicScope scope, DateOnly from, DateOnly to, ReportPeriod period)
    {
        var lo = Start(from);
        var hi = Start(to).AddDays(1);
        var days = await InvoicesOf(scope)
            .Where(i => i.Status == InvoiceStatus.Paid && i.PaidDate != null
                        && i.PaidDate >= lo && i.PaidDate < hi)
            .GroupBy(i => new { i.PaidDate!.Value.Year, i.PaidDate!.Value.Month, i.PaidDate!.Value.Day })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Amount = g.Sum(i => i.PaidAmount ?? i.Amount), Count = g.Count() })
            .ToListAsync();

        var series = days
            .GroupBy(d => PeriodStart(new DateOnly(d.Year, d.Month, d.Day), period))
            .OrderBy(g => g.Key)
            .Select(g => new RevenuePoint(g.Key, g.Sum(d => d.Amount), g.Sum(d => d.Count)))
            .ToList();
        return new RevenueReport(from, to, period, series.Sum(p => p.Revenue), series.Sum(p => p.InvoicesPaid), series);
    }

    public async Task<VisitsReport> GetVisitsAsync(ClinicScope scope, DateOnly from, DateOnly to, ReportPeriod period)
    {
        var lo = Start(from);
        var hi = Start(to).AddDays(1);
        var days = await AppointmentsOf(scope)
            .Where(a => a.Status == AppointmentStatus.Completed
                        && a.ScheduledAt >= lo && a.ScheduledAt < hi)
            .GroupBy(a => new { a.ScheduledAt.Year, a.ScheduledAt.Month, a.ScheduledAt.Day })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Count = g.Count() })
            .ToListAsync();

        var series = days
            .GroupBy(d => PeriodStart(new DateOnly(d.Year, d.Month, d.Day), period))
            .OrderBy(g => g.Key)
            .Select(g => new VisitsPoint(g.Key, g.Sum(d => d.Count)))
            .ToList();
        return new VisitsReport(from, to, period, series.Sum(p => p.Visits), series);
    }

    public async Task<NoShowReport> GetNoShowsAsync(ClinicScope scope, DateOnly from, DateOnly to, ReportPeriod period)
    {
        var lo = Start(from);
        var hi = Start(to).AddDays(1);
        var days = await AppointmentsOf(scope)
            .Where(a => (a.Status == AppointmentStatus.Completed || a.Status == AppointmentStatus.NoShow)
                        && a.ScheduledAt >= lo && a.ScheduledAt < hi)
            .GroupBy(a => new { a.ScheduledAt.Year, a.ScheduledAt.Month, a.ScheduledAt.Day })
            .Select(g => new
            {
                g.Key.Year, g.Key.Month, g.Key.Day,
                Completed = g.Count(a => a.Status == AppointmentStatus.Completed),
                NoShows = g.Count(a => a.Status == AppointmentStatus.NoShow)
            })
            .ToListAsync();

        var series = days
            .GroupBy(d => PeriodStart(new DateOnly(d.Year, d.Month, d.Day), period))
            .OrderBy(g => g.Key)
            .Select(g => { var c = g.Sum(d => d.Completed); var n = g.Sum(d => d.NoShows); return new NoShowPoint(g.Key, c, n, Rate(n, c)); })
            .ToList();
        var completed = series.Sum(p => p.Completed);
        var noShows = series.Sum(p => p.NoShows);
        return new NoShowReport(from, to, period, completed, noShows, Rate(noShows, completed), series);
    }

    private static decimal? Rate(int noShows, int completed) =>
        noShows + completed == 0 ? null : Math.Round((decimal)noShows / (noShows + completed), 4);

    // Open = still owes money. Reference date is the due date (invoice date when none); the Pending/Overdue
    // flag is not trusted to be current, so aging is computed from dates only.
    private IQueryable<Core.Entities.Invoice> OpenInvoices(ClinicScope scope) =>
        InvoicesOf(scope).Where(i => i.Patient != null // invoices of deleted patients drop out of totals and list alike
            && (i.Status == InvoiceStatus.Pending || i.Status == InvoiceStatus.Overdue)
            && i.Amount > (i.PaidAmount ?? 0m));

    public async Task<ArAgingReport> GetArAgingAsync(ClinicScope scope, DateOnly asOf, int page, int pageSize)
    {
        var today = Start(asOf);
        var d30 = today.AddDays(-30);
        var d60 = today.AddDays(-60);
        var d90 = today.AddDays(-90);
        var open = OpenInvoices(scope);

        var grouped = await open
            .GroupBy(i => (i.DueDate ?? i.InvoiceDate) >= today ? 0
                        : (i.DueDate ?? i.InvoiceDate) >= d30 ? 1
                        : (i.DueDate ?? i.InvoiceDate) >= d60 ? 2
                        : (i.DueDate ?? i.InvoiceDate) >= d90 ? 3 : 4)
            .Select(g => new { Index = g.Key, Count = g.Count(), Amount = g.Sum(i => i.Amount - (i.PaidAmount ?? 0m)) })
            .ToListAsync();
        var buckets = BucketNames
            .Select((name, idx) =>
            {
                var g = grouped.FirstOrDefault(x => x.Index == idx);
                return new ArBucketDto(name, g?.Count ?? 0, g?.Amount ?? 0m);
            })
            .ToList();

        var total = buckets.Sum(b => b.Invoices);
        var rows = await ToRowsAsync(open.OrderBy(i => i.DueDate ?? i.InvoiceDate).ThenBy(i => i.Id)
            .Skip((page - 1) * pageSize).Take(pageSize), asOf);
        return new ArAgingReport(asOf, buckets.Sum(b => b.Amount), total, buckets,
            new PagedResult<ArInvoiceDto>(rows, total, page, pageSize));
    }

    public async Task<IReadOnlyList<ArInvoiceDto>> GetArRowsAsync(ClinicScope scope, DateOnly asOf, int max) =>
        await ToRowsAsync(OpenInvoices(scope).OrderBy(i => i.DueDate ?? i.InvoiceDate).ThenBy(i => i.Id).Take(max), asOf);

    private static async Task<List<ArInvoiceDto>> ToRowsAsync(IQueryable<Core.Entities.Invoice> q, DateOnly asOf)
    {
        var raw = await q.Select(i => new
        {
            i.Id, i.InvoiceNumber,
            PatientName = i.Patient != null ? i.Patient.FirstName + " " + i.Patient.LastName : "",
            i.InvoiceDate, i.DueDate, Balance = i.Amount - (i.PaidAmount ?? 0m)
        }).ToListAsync();
        return raw.Select(r =>
        {
            var reference = DateOnly.FromDateTime(r.DueDate ?? r.InvoiceDate);
            var days = Math.Max(0, asOf.DayNumber - reference.DayNumber);
            var bucket = days == 0 ? 0 : days <= 30 ? 1 : days <= 60 ? 2 : days <= 90 ? 3 : 4;
            return new ArInvoiceDto(r.Id, r.InvoiceNumber, r.PatientName, r.InvoiceDate, r.DueDate, days, BucketNames[bucket], r.Balance);
        }).ToList();
    }
}
