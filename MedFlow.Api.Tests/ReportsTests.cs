using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MedFlow.Core.Entities;
using MedFlow.Core.Enums;

namespace MedFlow.Api.Tests;

/// <summary>Reports are per signed-in doctor, doctor-only, bounded, and CSV exports resist formula injection.</summary>
public class ReportsTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _f;
    public ReportsTests(TestApiFactory f) => _f = f;

    private async Task<(AuthResult doc, int pid)> SetupAsync(string patientFirst = "Pat")
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var doc = await _f.RegisterDoctorAsync($"doc-{tag}@x.com");
        return (doc, await _f.CreatePatientAsync(doc.Token, $"pat-{tag}@x.com", patientFirst));
    }

    private Task SeedAsync(Action<MedFlow.Infrastructure.Data.AppDbContext> seed) =>
        _f.WithDbAsync(async db => { seed(db); await db.SaveChangesAsync(); return 0; });

    private static Appointment Appt(string doc, int pid, string when, AppointmentStatus s) =>
        new() { DoctorId = doc, PatientId = pid, ScheduledAt = DateTime.Parse(when + "T10:00:00Z").ToUniversalTime(), Status = s, Type = AppointmentType.CheckUp };

    private static Invoice Inv(string doc, int pid, decimal amount, decimal? paid, InvoiceStatus s, DateTime? due, string? paidOn = null, string number = "INV-T", DateTime? invoiceDate = null) =>
        new()
        {
            DoctorId = doc, PatientId = pid, Amount = amount, PaidAmount = paid, Status = s, DueDate = due, InvoiceNumber = number,
            InvoiceDate = invoiceDate ?? DateTime.UtcNow.AddDays(-200),
            PaidDate = paidOn == null ? null : DateTime.Parse(paidOn + "T12:00:00Z").ToUniversalTime()
        };

    private async Task<JsonElement> GetJson(string token, string url)
    {
        var res = await _f.ClientFor(token).GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    // ── Access control ─────────────────────────────────────────────────────────
    [Theory]
    [InlineData("/api/reports/revenue")]
    [InlineData("/api/reports/visits")]
    [InlineData("/api/reports/no-shows")]
    [InlineData("/api/reports/ar-aging")]
    [InlineData("/api/reports/revenue/export")]
    [InlineData("/api/reports/visits/export")]
    [InlineData("/api/reports/no-shows/export")]
    [InlineData("/api/reports/ar-aging/export")]
    public async Task Anonymous_gets_401_and_patient_token_gets_403(string url)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.ClientFor().GetAsync(url)).StatusCode);

        var tag = Guid.NewGuid().ToString("N")[..8];
        var doc = await _f.RegisterDoctorAsync($"doc-{tag}@x.com");
        var pid = await _f.CreatePatientAsync(doc.Token, $"pat-{tag}@x.com");
        var patient = await _f.OnboardPatientAsync(doc.Token, pid, $"pat-{tag}@x.com");
        Assert.Equal(HttpStatusCode.Forbidden, (await _f.ClientFor(patient.Token).GetAsync(url)).StatusCode);
    }

    // ── Revenue ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Revenue_sums_paid_in_range_only_and_ignores_other_doctors()
    {
        var (doc, pid) = await SetupAsync();
        var (other, opid) = await SetupAsync();
        await SeedAsync(db => db.Invoices.AddRange(
            Inv(doc.UserId, pid, 100, 100, InvoiceStatus.Paid, null, "2025-03-05"),
            Inv(doc.UserId, pid, 50, null, InvoiceStatus.Paid, null, "2025-03-06"),      // falls back to Amount
            Inv(doc.UserId, pid, 999, 999, InvoiceStatus.Paid, null, "2025-04-20"),     // outside range
            Inv(doc.UserId, pid, 70, null, InvoiceStatus.Pending, null),                // unpaid
            Inv(doc.UserId, pid, 80, 80, InvoiceStatus.Cancelled, null, "2025-03-07"),  // cancelled
            Inv(other.UserId, opid, 5000, 5000, InvoiceStatus.Paid, null, "2025-03-05")));

        var r = await GetJson(doc.Token, "/api/reports/revenue?from=2025-03-01&to=2025-03-31&period=Day");
        Assert.Equal(150m, r.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(2, r.GetProperty("invoicesPaid").GetInt32());
        Assert.Equal(2, r.GetProperty("series").GetArrayLength());
        Assert.Equal("Day", r.GetProperty("period").GetString());
    }

    [Fact]
    public async Task Visits_count_completed_only_and_fold_into_iso_weeks_and_months()
    {
        var (doc, pid) = await SetupAsync();
        var (other, opid) = await SetupAsync();
        await SeedAsync(db => db.Appointments.AddRange(
            Appt(doc.UserId, pid, "2025-03-05", AppointmentStatus.Completed),   // Wed -> week of 03-03
            Appt(doc.UserId, pid, "2025-03-09", AppointmentStatus.Completed),   // Sun -> week of 03-03
            Appt(doc.UserId, pid, "2025-03-10", AppointmentStatus.Completed),   // Mon -> week of 03-10
            Appt(doc.UserId, pid, "2025-03-11", AppointmentStatus.Cancelled),
            Appt(doc.UserId, pid, "2025-03-12", AppointmentStatus.Pending),
            Appt(doc.UserId, pid, "2025-04-02", AppointmentStatus.Completed),   // outside range
            Appt(other.UserId, opid, "2025-03-05", AppointmentStatus.Completed)));

        var weekly = await GetJson(doc.Token, "/api/reports/visits?from=2025-03-01&to=2025-03-31&period=Week");
        Assert.Equal(3, weekly.GetProperty("totalVisits").GetInt32());
        var s = weekly.GetProperty("series");
        Assert.Equal(2, s.GetArrayLength());
        Assert.Equal("2025-03-03", s[0].GetProperty("periodStart").GetString());
        Assert.Equal(2, s[0].GetProperty("visits").GetInt32());
        Assert.Equal("2025-03-10", s[1].GetProperty("periodStart").GetString());

        var monthly = await GetJson(doc.Token, "/api/reports/visits?from=2025-03-01&to=2025-03-31&period=Month");
        Assert.Equal("2025-03-01", monthly.GetProperty("series")[0].GetProperty("periodStart").GetString());
        Assert.Equal(3, monthly.GetProperty("series")[0].GetProperty("visits").GetInt32());
    }

    // ── No-shows ───────────────────────────────────────────────────────────────
    [Fact]
    public async Task NoShow_rate_is_noshows_over_completed_plus_noshows()
    {
        var (doc, pid) = await SetupAsync();
        await SeedAsync(db => db.Appointments.AddRange(
            Appt(doc.UserId, pid, "2025-03-05", AppointmentStatus.Completed),
            Appt(doc.UserId, pid, "2025-03-06", AppointmentStatus.Completed),
            Appt(doc.UserId, pid, "2025-03-07", AppointmentStatus.Completed),
            Appt(doc.UserId, pid, "2025-03-08", AppointmentStatus.NoShow),
            Appt(doc.UserId, pid, "2025-03-09", AppointmentStatus.Cancelled),
            Appt(doc.UserId, pid, "2025-03-10", AppointmentStatus.Confirmed)));

        var r = await GetJson(doc.Token, "/api/reports/no-shows?from=2025-03-01&to=2025-03-31&period=Month");
        Assert.Equal(3, r.GetProperty("completed").GetInt32());
        Assert.Equal(1, r.GetProperty("noShows").GetInt32());
        Assert.Equal(0.25m, r.GetProperty("rate").GetDecimal());
    }

    [Fact]
    public async Task NoShow_rate_is_null_when_nothing_was_due()
    {
        var (doc, _) = await SetupAsync();
        var r = await GetJson(doc.Token, "/api/reports/no-shows?from=2025-03-01&to=2025-03-31");
        Assert.Equal(JsonValueKind.Null, r.GetProperty("rate").ValueKind);
        Assert.Equal(0, r.GetProperty("series").GetArrayLength());
    }

    // ── Validation limits ──────────────────────────────────────────────────────
    [Theory]
    [InlineData("/api/reports/revenue?from=2025-03-10&to=2025-03-01")]
    [InlineData("/api/reports/visits?from=2024-01-01&to=2025-03-01")]                  // > 366 days
    [InlineData("/api/reports/no-shows?from=not-a-date")]
    [InlineData("/api/reports/revenue?period=Decade")]
    [InlineData("/api/reports/ar-aging?pageSize=101")]
    [InlineData("/api/reports/ar-aging?page=0")]
    [InlineData("/api/reports/revenue/export?from=2025-03-10&to=2025-03-01")]
    public async Task Invalid_input_is_rejected_with_400(string url)
    {
        var (doc, _) = await SetupAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await _f.ClientFor(doc.Token).GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Range_of_exactly_366_days_is_accepted()
    {
        var (doc, _) = await SetupAsync();
        var res = await _f.ClientFor(doc.Token).GetAsync("/api/reports/visits?from=2024-03-01&to=2025-03-01");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ── A/R aging ──────────────────────────────────────────────────────────────
    [Fact]
    public async Task ArAging_buckets_balances_by_due_date_and_excludes_closed_invoices()
    {
        var (doc, pid) = await SetupAsync();
        var (other, opid) = await SetupAsync();
        var today = DateTime.UtcNow.Date;
        await SeedAsync(db => db.Invoices.AddRange(
            Inv(doc.UserId, pid, 100, null, InvoiceStatus.Pending, today.AddDays(5)),                // current
            Inv(doc.UserId, pid, 200, null, InvoiceStatus.Pending, today),                           // due today -> current
            Inv(doc.UserId, pid, 300, 100, InvoiceStatus.Pending, today.AddDays(-1)),                // 1 day, remainder 200
            Inv(doc.UserId, pid, 400, null, InvoiceStatus.Overdue, today.AddDays(-30)),              // 30 -> 1-30
            Inv(doc.UserId, pid, 500, null, InvoiceStatus.Overdue, today.AddDays(-31)),              // 31-60
            Inv(doc.UserId, pid, 600, null, InvoiceStatus.Overdue, today.AddDays(-61)),              // 61-90
            Inv(doc.UserId, pid, 700, null, InvoiceStatus.Overdue, today.AddDays(-91)),              // 90+
            Inv(doc.UserId, pid, 800, null, InvoiceStatus.Pending, null, invoiceDate: today.AddDays(-45)), // no due date -> invoice date, 31-60
            Inv(doc.UserId, pid, 900, 900, InvoiceStatus.Paid, today.AddDays(-100), "2025-03-05"),   // excluded
            Inv(doc.UserId, pid, 910, null, InvoiceStatus.Cancelled, today.AddDays(-100)),           // excluded
            Inv(doc.UserId, pid, 920, null, InvoiceStatus.Draft, today.AddDays(-100)),               // excluded
            Inv(doc.UserId, pid, 930, 930, InvoiceStatus.Pending, today.AddDays(-100)),              // fully paid balance 0, excluded
            Inv(other.UserId, opid, 9999, null, InvoiceStatus.Overdue, today.AddDays(-200))));

        var r = await GetJson(doc.Token, "/api/reports/ar-aging?pageSize=5");
        var b = r.GetProperty("buckets").EnumerateArray().ToDictionary(x => x.GetProperty("bucket").GetString()!, x => (x.GetProperty("invoices").GetInt32(), x.GetProperty("amount").GetDecimal()));
        Assert.Equal((2, 300m), b["current"]);
        Assert.Equal((2, 600m), b["1-30"]);
        Assert.Equal((2, 1300m), b["31-60"]);
        Assert.Equal((1, 600m), b["61-90"]);
        Assert.Equal((1, 700m), b["90+"]);
        Assert.Equal(3500m, r.GetProperty("totalOutstanding").GetDecimal());
        Assert.Equal(8, r.GetProperty("openInvoices").GetInt32());

        var inv = r.GetProperty("invoices");
        Assert.Equal(8, inv.GetProperty("totalCount").GetInt32());
        Assert.Equal(5, inv.GetProperty("items").GetArrayLength());
        Assert.Equal("90+", inv.GetProperty("items")[0].GetProperty("bucket").GetString()); // oldest first
    }

    // ── CSV export ─────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("=1+1")]
    [InlineData("+cmd")]
    [InlineData("-2+3")]
    [InlineData("@SUM(A1)")]
    public async Task ArAging_csv_neutralizes_formula_injection_in_patient_names(string name)
    {
        var (doc, pid) = await SetupAsync(name);
        await SeedAsync(db => db.Invoices.Add(Inv(doc.UserId, pid, 10, null, InvoiceStatus.Pending, DateTime.UtcNow.Date.AddDays(-3), number: "=INV")));

        var res = await _f.ClientFor(doc.Token).GetAsync("/api/reports/ar-aging/export");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("text/csv", res.Content.Headers.ContentType!.MediaType);
        Assert.NotNull(res.Content.Headers.ContentDisposition?.FileName);
        var text = Encoding.UTF8.GetString(await res.Content.ReadAsByteArrayAsync()).TrimStart('﻿');
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Contains("'=INV", lines[1]);
        Assert.Contains("'" + name, lines[1]);
        Assert.DoesNotContain(",=", lines[1]);
        Assert.DoesNotContain(",+", lines[1]);
        Assert.DoesNotContain(",@", lines[1]);
        Assert.Equal("no-store", res.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Csv_quotes_special_characters_and_localizes_headers()
    {
        var (doc, pid) = await SetupAsync("Ann, \"AJ\"");
        await SeedAsync(db => db.Invoices.Add(Inv(doc.UserId, pid, 10.5m, null, InvoiceStatus.Pending, DateTime.UtcNow.Date)));

        async Task<string> Get(string lang)
        {
            var c = _f.ClientFor(doc.Token);
            c.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", lang);
            var res = await c.GetAsync("/api/reports/ar-aging/export");
            return Encoding.UTF8.GetString(await res.Content.ReadAsByteArrayAsync()).TrimStart('﻿');
        }

        var en = await Get("en");
        Assert.StartsWith("Invoice,Patient,Invoice date", en);
        Assert.Contains("\"Ann, \"\"AJ\"\" Ient\"", en);
        Assert.Contains(",10.50", en);

        var es = await Get("es");
        Assert.StartsWith("Factura,Paciente,Fecha de factura", es);
    }

    [Fact]
    public async Task Revenue_csv_has_header_and_one_row_per_period()
    {
        var (doc, pid) = await SetupAsync();
        await SeedAsync(db => db.Invoices.AddRange(
            Inv(doc.UserId, pid, 100, 100, InvoiceStatus.Paid, null, "2025-03-05"),
            Inv(doc.UserId, pid, 25.5m, 25.5m, InvoiceStatus.Paid, null, "2025-03-06")));

        var res = await _f.ClientFor(doc.Token).GetAsync("/api/reports/revenue/export?from=2025-03-01&to=2025-03-31&period=Month");
        var lines = Encoding.UTF8.GetString(await res.Content.ReadAsByteArrayAsync()).TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Equal("2025-03-01,125.50,2", lines[1]);
    }
}
