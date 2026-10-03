# Tasks: Practice Reports

**Input**: `/specs/001-reports/` (spec.md, plan.md, research.md, data-model.md, quickstart.md). Refs #27.
Tests are required by the repo build rules (access control, isolation, validation limits).

## Phase 1: Setup / Foundational

- [X] T001 Create report DTO records and `ReportPeriod` enum in MedFlow.Core/DTOs/ReportDtos.cs (per data-model.md)
- [X] T002 Add `IReportRepository` to MedFlow.Core/Interfaces/IRepositories.cs (GetRevenueAsync, GetVisitsAsync, GetNoShowsAsync, GetArAgingAsync, GetArExportRowsAsync with doctorId, range, period, paging)
- [X] T003 Create `ReportRepository` skeleton and register in MedFlow.Infrastructure/DependencyInjection.cs; add shared day-to-period folding helper (ISO Monday weeks, month start) in MedFlow.Infrastructure/Repositories/ReportRepository.cs
- [X] T004 [P] Create `CsvWriter` (quote/escape, formula-injection guard for `= + - @ TAB CR`, UTF-8 BOM, invariant numbers/ISO dates) in MedFlow.Api/Reports/CsvWriter.cs
- [X] T005 Create `ReportsController` (Doctor-only, query validation: from<=to, <=366 days, period, page size <=100, `lang`) in MedFlow.Api/Controllers/ReportsController.cs

## Phase 2: User Story 1 - Revenue and visits (P1)

**Independent test**: seeded paid invoices / completed appointments in and outside range.

- [X] T006 [US1] Implement revenue (SQL day grouping on PaidDate, status Paid, `PaidAmount ?? Amount`) and visits (Completed) in ReportRepository
- [X] T007 [US1] Add GET /api/reports/revenue, /visits and their /export endpoints in ReportsController
- [X] T008 [P] [US1] Add ReportsTests (revenue/visits totals, range exclusion, period folding, cross-doctor isolation, 401/403 for patient and anonymous, invalid range 400) in MedFlow.Api.Tests/ReportsTests.cs
- [X] T009 [P] [US1] Add report types to medflow-client/src/types/index.ts, `reportsApi` (incl. CSV blob download) to medflow-client/src/api/services.ts, hooks in medflow-client/src/hooks/queries.ts
- [X] T010 [US1] Build ReportsPage with date range + period controls, tabs, Recharts revenue and visits charts, totals, Export CSV button in medflow-client/src/pages/ReportsPage.tsx; add route and sidebar link (medflow-client/src/App.tsx and layout)
- [X] T011 [P] [US1] Add en and es report strings in medflow-client/src/i18n/resources/en.ts and es.ts

## Phase 3: User Story 2 - No-show rate (P2)

- [X] T012 [US2] Implement no-shows (Completed + NoShow counts per day, rate null when denominator 0) in ReportRepository
- [X] T013 [US2] Add GET /api/reports/no-shows and /export in ReportsController
- [X] T014 [P] [US2] Tests: 3 completed + 1 no-show = 0.25; empty range rate null; other statuses excluded in MedFlow.Api.Tests/ReportsTests.cs
- [X] T015 [US2] Add no-show tab (rate tile and chart) to ReportsPage.tsx plus en/es strings

## Phase 4: User Story 3 - A/R aging (P2)

- [X] T016 [US3] Implement A/R aging (grouped bucket sums in SQL by due date, open balance, Pending/Overdue only, paged detail) in ReportRepository
- [X] T017 [US3] Add GET /api/reports/ar-aging and /export (5,000 row cap) in ReportsController
- [X] T018 [P] [US3] Tests: bucket boundaries, partial payment remainder, excluded statuses, page size cap, isolation in MedFlow.Api.Tests/ReportsTests.cs
- [X] T019 [US3] Add A/R tab (bucket chart/table, paged invoice table, "as of today" note) to ReportsPage.tsx plus en/es strings

## Phase 5: User Story 4 - CSV export hardening (P2)

- [X] T020 [P] [US4] Tests: formula-injection neutralization for patient name starting with `=`, `+`, `-`, `@`; quoting of commas/quotes; attachment headers; localized headers; patient token rejected on export endpoints in MedFlow.Api.Tests/ReportsTests.cs

## Phase 6: Polish

- [X] T021 Run dotnet build, dotnet test, npm run build, npm run lint; fix issues caused by this change
