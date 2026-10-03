# Research: Practice Reports

- **Decision**: Aggregate per day in SQL, fold to week/month in memory.
  **Rationale**: Week/month date functions (`DATEPART(iso_week)`, `EOMONTH`) do not translate in EF reliably and not at all in the InMemory test provider; year/month/day grouping translates everywhere and bounds rows at 366.
  **Alternatives**: Raw SQL per provider (rejected: untestable in InMemory); load rows and group in memory (rejected: unbounded).
- **Decision**: Revenue is cash basis by `PaidDate`, amount = `PaidAmount ?? Amount`, status Paid.
  **Rationale**: Existing `mark-paid` sets both; matches "revenue".
  **Alternatives**: Accrual by `InvoiceDate` (shown nowhere in this version).
- **Decision**: A/R by due date, not by Overdue flag (flag is never auto-updated).
- **Decision**: CSV prefix neutralization with a single quote (OWASP guidance), tab/CR included.
- **Decision**: No new chart or CSV library; hand-rolled writer (~30 lines), Recharts for charts.
- **Decision**: No audit event per report view (matches `GET /api/invoices`).
