namespace MedFlow.Core.DTOs;

// ── Reports (per signed-in doctor; there is no clinic layer yet) ──────────────
public enum ReportPeriod { Day, Week, Month }

public record RevenuePoint(DateOnly PeriodStart, decimal Revenue, int InvoicesPaid);
public record RevenueReport(DateOnly From, DateOnly To, ReportPeriod Period,
    decimal TotalRevenue, int InvoicesPaid, IReadOnlyList<RevenuePoint> Series);

public record VisitsPoint(DateOnly PeriodStart, int Visits);
public record VisitsReport(DateOnly From, DateOnly To, ReportPeriod Period,
    int TotalVisits, IReadOnlyList<VisitsPoint> Series);

/// <summary>Rate is a fraction 0..1 (NoShows / (Completed + NoShows)); null when there is no denominator.</summary>
public record NoShowPoint(DateOnly PeriodStart, int Completed, int NoShows, decimal? Rate);
public record NoShowReport(DateOnly From, DateOnly To, ReportPeriod Period,
    int Completed, int NoShows, decimal? Rate, IReadOnlyList<NoShowPoint> Series);

public record ArBucketDto(string Bucket, int Invoices, decimal Amount);
public record ArInvoiceDto(int InvoiceId, string InvoiceNumber, string PatientName, DateTime InvoiceDate,
    DateTime? DueDate, int DaysPastDue, string Bucket, decimal Balance);
public record ArAgingReport(DateOnly AsOf, decimal TotalOutstanding, int OpenInvoices,
    IReadOnlyList<ArBucketDto> Buckets, PagedResult<ArInvoiceDto> Invoices);
