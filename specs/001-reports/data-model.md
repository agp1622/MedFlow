# Data Model: Practice Reports

No new tables or columns. Read-only over:

- `Appointment`: `DoctorId`, `ScheduledAt`, `Status` (Completed = visit; NoShow = no-show).
- `Invoice`: `DoctorId`, `Amount`, `PaidAmount`, `Status`, `InvoiceDate`, `DueDate`, `PaidDate`.

Soft-deleted rows are excluded by the existing query filters.

## Response records (Core/DTOs/ReportDtos.cs)

- `ReportPeriod` enum: Day, Week, Month.
- `RevenuePoint(DateOnly PeriodStart, decimal Revenue, int InvoicesPaid)`; `RevenueReport(DateOnly From, DateOnly To, ReportPeriod Period, decimal TotalRevenue, int InvoicesPaid, IReadOnlyList<RevenuePoint> Series)`.
- `VisitsPoint(DateOnly PeriodStart, int Visits)`; `VisitsReport(From, To, Period, int TotalVisits, Series)`.
- `NoShowPoint(DateOnly PeriodStart, int Completed, int NoShows, decimal? Rate)`; `NoShowReport(From, To, Period, int Completed, int NoShows, decimal? Rate, Series)`. Rate is a fraction 0..1, null when no denominator.
- `ArBucketDto(string Bucket, int Invoices, decimal Amount)` with keys `current`, `1-30`, `31-60`, `61-90`, `90+`; `ArInvoiceDto(int InvoiceId, string InvoiceNumber, string PatientName, DateTime? DueDate, DateTime InvoiceDate, int DaysPastDue, string Bucket, decimal Balance)`; `ArAgingReport(DateOnly AsOf, decimal TotalOutstanding, int OpenInvoices, IReadOnlyList<ArBucketDto> Buckets, PagedResult<ArInvoiceDto> Invoices)`.
