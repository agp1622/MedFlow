# Feature Specification: Practice Reports

**Feature Branch**: `claude/issue-27-reports` (spec folder `001-reports`; branch name set by the requester)

**Created**: 2026-10-03

**Status**: Draft

**Input**: GitHub issue #27 "Reports" (Refs #27): "As a clinic owner, I want revenue, no-show rate, visits per period and A/R aging reports so that I can track the practice's performance. Acceptance: reports with date range filter; CSV export."

## Scope Note

MedFlow has no clinic/organization layer yet: every appointment and invoice belongs to a single doctor. "Clinic owner" in the issue therefore maps to the signed-in doctor, and all reports cover **only that doctor's own records**. A clinic-wide rollup is out of scope until a clinic layer exists.

## Clarifications

### Session 2026-10-03

- Q: Is revenue counted when billed or when paid? → A: When paid (cash basis, by payment date); invoices with a payment date in range and status Paid.
- Q: How are weeks bucketed? → A: ISO weeks starting Monday (UTC); the bucket label is the Monday date; months use first-of-month.
- Q: What is the row cap for the A/R detail list and CSV? → A: List is paginated (page size max 100); CSV export is capped at 5,000 rows; the CSV simply stops at the cap (no extra flag).
- Q: Should viewing the A/R detail (patient names) write per-patient audit events? → A: No per-patient events, consistent with the existing invoice list endpoint; access is doctor-only and scoped to the caller. (Stricter alternative would require one audit row per patient per view.)
- Q: Are overdue invoices determined by status or by due date? → A: By due date as of today (UTC), regardless of the Pending/Overdue status flag, because the status flag is not updated automatically.
- Q: Which locale formats numbers and dates in CSV? → A: Locale-neutral: ISO dates (yyyy-MM-dd) and invariant decimals with two places, so the file is stable regardless of UI language; only column headers are localized from the request Accept-Language header (es default, en).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Revenue and visits for a period (Priority: P1)

A doctor opens Reports, picks a date range (default: last 30 days) and sees total revenue collected, the number of completed visits, and trend charts of both over the range.

**Why this priority**: Core performance view; delivers value on its own.

**Independent Test**: Seed paid invoices and completed appointments inside and outside a range; verify totals and per-period series only include in-range data.

**Acceptance Scenarios**:

1. **Given** paid invoices with payment dates inside and outside the range, **When** the doctor requests revenue for the range, **Then** only payments dated inside the range are summed, and a per-period series (day, week or month) is shown.
2. **Given** completed, cancelled and pending appointments, **When** the doctor requests visits per period, **Then** only completed appointments scheduled in the range are counted, bucketed by the chosen period.
3. **Given** another doctor's invoices and appointments, **When** this doctor requests any report, **Then** none of that data is included.

---

### User Story 2 - No-show rate (Priority: P2)

A doctor sees the no-show rate for the range, with counts of no-shows and of appointments that were due, plus the rate per period.

**Independent Test**: Seed completed and no-show appointments; verify rate = no-shows / (completed + no-shows).

**Acceptance Scenarios**:

1. **Given** 3 completed and 1 no-show appointment in range, **When** requested, **Then** the rate is 25%.
2. **Given** no completed or no-show appointments in range, **Then** the rate is shown as "not available", never as a division error.

---

### User Story 3 - A/R aging (Priority: P2)

A doctor sees outstanding unpaid balances as of today grouped into aging buckets (current / not yet due, 1-30, 31-60, 61-90, over 90 days past due), with a list of the open invoices (capped and paginated).

**Independent Test**: Seed unpaid invoices with different due dates; verify bucket totals and counts.

**Acceptance Scenarios**:

1. **Given** open invoices with various due dates, **When** requested, **Then** each lands in exactly one bucket according to days past due, and bucket totals sum to total outstanding.
2. **Given** a partially paid invoice, **Then** only the unpaid remainder is counted.
3. **Given** paid, cancelled or draft invoices, **Then** they are excluded.

---

### User Story 4 - CSV export (Priority: P2)

From any report the doctor downloads a CSV of what is on screen (same filters), openable in a spreadsheet.

**Independent Test**: Export each report; verify headers, rows, and that cells beginning with `=`, `+`, `-` or `@` are neutralized.

**Acceptance Scenarios**:

1. **Given** a patient name or description that starts with `=`, `+`, `-` or `@`, **When** exported, **Then** the cell is prefixed so a spreadsheet treats it as text.
2. **Given** a patient token, **When** it calls any report or export endpoint, **Then** access is denied.

### Edge Cases

- Start date after end date, or a missing/invalid date: rejected with a clear validation error.
- Range longer than 366 days: rejected (keeps queries bounded).
- Empty range: zero totals, empty series, no errors.
- Day boundaries are evaluated in UTC (as stored).
- Very large A/R detail: capped/paginated; CSV export of detail capped at a documented maximum rows.
- Soft-deleted records are excluded.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Only signed-in doctors can access reports and exports; patient and anonymous callers are rejected. Data is always scoped to the signed-in doctor.
- **FR-002**: Revenue report: total collected (cash basis) for payments dated within the range, with per-period series and count of invoices paid.
- **FR-003**: Visits report: number of completed appointments in the range, per period (day, week, month).
- **FR-004**: No-show report: no-shows, completed count, and rate for the range and per period.
- **FR-005**: A/R aging report: outstanding balance as of the report date in five buckets, plus a paginated list of open invoices.
- **FR-006**: Revenue, visits and no-show reports accept a start and end date (inclusive) and a period granularity; the maximum range is 366 days; default is the last 30 days.
- **FR-007**: A/R aging is a point-in-time view and is not date range filtered (it is as of today); this is stated in the UI.
- **FR-008**: All figures are aggregated by the data store; no unbounded in-memory loading. Detail lists are paginated and capped.
- **FR-009**: Each report can be exported as CSV with the same filters. Cells that begin with `=`, `+`, `-`, `@`, tab or carriage return are neutralized, and fields are correctly quoted/escaped.
- **FR-010**: UI strings are available in English and Spanish (Spanish primary); charts use the existing chart library.
- **FR-011**: No database schema change.
- **FR-012**: The A/R detail list shows patient names and follows the existing invoice-list behaviour (doctor-only, caller-scoped, no per-patient audit event).
- **FR-013**: A/R detail CSV is capped at 5,000 rows; the A/R detail page size is at most 100.
- **FR-014**: CSV uses ISO dates and invariant two-decimal numbers; headers are localized (en/es).

### Key Entities

- **Appointment**: scheduled time and status (completed / no-show / other) used for visits and no-shows.
- **Invoice**: amount, paid amount, payment date, due date and status used for revenue and A/R.
- **Report definitions**: see Assumptions for the exact meaning of visit, no-show, revenue, and aging.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A doctor sees all report figures for a 12-month range within 3 seconds with 50,000 appointments and invoices on file.
- **SC-002**: Report totals match a hand count of the underlying records in 100% of seeded test scenarios.
- **SC-003**: 100% of exported cells starting with a formula trigger character are neutralized.
- **SC-004**: Zero records from other doctors appear in any report or export.

## Assumptions

- **Visit** = appointment with status Completed, bucketed by its scheduled date.
- **No-show rate** = NoShow / (Completed + NoShow) among appointments in range. Pending, Confirmed and Cancelled are excluded from the denominator.
- **Revenue** = sum of paid amount (falling back to invoice amount when status is Paid and paid amount is empty) for non-deleted, non-cancelled invoices whose payment date is within the range. Cash basis.
- **A/R open balance** = Amount minus PaidAmount (never below zero) for invoices in status Pending or Overdue (Draft, Paid and Cancelled excluded). Aging is by days past due date as of today (UTC), independent of the Pending/Overdue flag; invoices with no due date use the invoice date.
- Buckets: Current (not yet due), 1-30, 31-60, 61-90, 90+ days past due.
- Currency is a single unspecified currency as in existing invoices; no conversion.
- Audit logging of report views follows the existing mechanism only where patient-identifying data is listed.
