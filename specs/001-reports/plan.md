# Implementation Plan: Practice Reports

**Branch**: `claude/issue-27-reports` | **Date**: 2026-10-03 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-reports/spec.md` (Refs #27)

## Summary

Add four doctor-scoped reports (revenue, visits, no-shows, A/R aging) with a date range filter and CSV export. Backend: a new `IReportRepository` (Core) implemented in Infrastructure with database-side aggregation, a doctor-only `ReportsController` with JSON and `/export` CSV endpoints, and a CSV writer that neutralizes formula injection. Frontend: a Reports page with tabs, Recharts charts, typed `reportsApi`, en/es strings. No schema change.

## Technical Context

**Language/Version**: C# / .NET 8; TypeScript + React (Vite)

**Primary Dependencies**: ASP.NET Core Web API, EF Core (SQL Server in prod, InMemory provider in tests), TanStack Query, Recharts (already installed), react-i18next

**Storage**: Existing `Invoices` and `Appointments` tables; no migration

**Testing**: xUnit via `MedFlow.Api.Tests` with `TestApiFactory`

**Target Platform**: Web (Linux server, browser)

**Project Type**: Web application (API + SPA)

**Performance Goals**: 12-month range under 3 s with ~50k rows per doctor (aggregates in SQL, bounded result sets)

**Constraints**: Queries must translate on both SQL Server and the InMemory test provider; day-level `GroupBy(Year, Month, Day)` only; max range 366 days; A/R page size <= 100; CSV A/R cap 5,000 rows

**Scale/Scope**: One doctor's data; at most 366 daily buckets per series

## Constitution Check

| Principle | Status |
|-----------|--------|
| I Git workflow | Work on feature branch `claude/issue-27-reports` off `dev` (name dictated by requester; spec folder `001-reports`). Deviation from `NNN-name` branch naming noted. Not pushed (pr-shipper/requester handles). |
| II Layered architecture | Interface + DTOs in Core (no EF/ASP.NET), implementation in Infrastructure, controller thin. CSV writer lives in Api (HTTP concern). Client uses `services.ts`. PASS |
| III API contracts | DTO records, string enums, `PagedResult<T>` for A/R detail, typed `reportsApi` + types. PASS |
| IV Security | `[Authorize(Roles = Roles.Doctor)]`, doctor id from token only, no account-existence leaks, no secrets. PASS |
| V Simplicity | Reuses Invoice/Appointment, no migration, no flags. PASS |

Re-check after design: unchanged, PASS.

## Project Structure

### Documentation

```text
specs/001-reports/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── tasks.md
```

### Source Code

```text
MedFlow.Core/DTOs/ReportDtos.cs                       # report records
MedFlow.Core/Interfaces/IRepositories.cs              # + IReportRepository
MedFlow.Infrastructure/Repositories/ReportRepository.cs
MedFlow.Infrastructure/DependencyInjection.cs         # register
MedFlow.Api/Controllers/ReportsController.cs
MedFlow.Api/Reports/CsvWriter.cs                      # escaping + formula guard
MedFlow.Api.Tests/ReportsTests.cs
medflow-client/src/types/index.ts                     # report types
medflow-client/src/api/services.ts                    # reportsApi
medflow-client/src/hooks/queries.ts                   # report hooks
medflow-client/src/pages/ReportsPage.tsx
medflow-client/src/App.tsx + layout nav               # route /reports + link
medflow-client/src/i18n/resources/{en,es}.ts
```

**Structure Decision**: Extend the existing three-project layout and client modules.

## API Contract (folded into plan; no separate contracts/ file)

All endpoints: `[Authorize(Roles = Doctor)]`, scoped to the token's doctor. 401 anonymous, 403 patient.

Common query (revenue, visits, no-shows): `from` (yyyy-MM-dd, default today-29), `to` (default today), `period` = Day|Week|Month (default Day). 400 if from > to, range > 366 days, or invalid period/date.

| Method | Path | Response |
|--------|------|----------|
| GET | /api/reports/revenue | `RevenueReport` |
| GET | /api/reports/visits | `VisitsReport` |
| GET | /api/reports/no-shows | `NoShowReport` |
| GET | /api/reports/ar-aging?page=&pageSize= | `ArAgingReport` (pageSize max 100) |
| GET | /api/reports/{revenue, visits, no-shows, ar-aging}/export?... | `text/csv; charset=utf-8` attachment |

Record shapes: see [data-model.md](data-model.md). CSV: localized header row, one row per series point (or per open invoice, max 5,000, for A/R); ISO dates; invariant decimals; text cells starting with `= + - @ TAB CR` get a leading `'`.

## Design Notes

- Per-day aggregation in SQL (`GroupBy` year/month/day, `Sum`/`Count`), then fold into week (ISO Monday) or month buckets in memory; at most 366 rows ever materialized.
- Totals are computed from the same daily rows (no second query), so totals equal series sums.
- A/R aging: one grouped query keyed by bucket index computed from due date (invoice date if no due date) relative to today UTC; open balance = Amount - PaidAmount where positive; Pending/Overdue only. Detail: paged `Select` to `ArInvoiceDto` ordered by oldest due first.
- CSV: `CsvWriter` quotes fields with comma/quote/newline, doubles quotes, prefixes a single quote when the first char is `=`, `+`, `-`, `@`, tab or CR (applied to every text cell, including numbers that are negative, handled by writing numbers via invariant format separately: numeric cells are not prefixed). UTF-8 with BOM for Excel and Spanish characters. `Content-Disposition` attachment, `Cache-Control: no-store`.
- Header localization follows the existing Accept-Language mechanism (Spanish default) — headers only.
- Validation errors return 400 ProblemDetails/`{message}` consistent with existing controllers.
