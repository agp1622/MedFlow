# Implementation Plan: Appointment Reminders

**Branch**: `claude/issue-9-appointment-reminders` | **Date**: 2026-10-03 | **Spec**: [spec.md](spec.md)

## Summary

An in-process hosted service runs every N minutes, finds Pending/Confirmed appointments starting within the configured lead time that have no successful reminder for their current scheduled time, emails the patient via the existing `IEmailSender`, and logs each attempt. The email links to a public client page; the page uses a token-based public API to show minimal details and to confirm or cancel. Doctors read the delivery log through a doctor-only endpoint, surfaced on the appointments page.

## Technical Context

**Language/Version**: C# / .NET 8, React + TypeScript (Vite)
**Primary Dependencies**: ASP.NET Core, EF Core (SQL Server), existing `IEmailSender`/`SmtpEmailSender`, TanStack Query, react-hook-form + zod
**Storage**: SQL Server via EF Core; one new migration (2 tables)
**Testing**: xUnit + `TestApiFactory` (in-memory DB, `FakeEmailSender`)
**Project Type**: web-service + SPA
**Performance Goals**: job pass handles hundreds of due appointments per run; trivial
**Constraints**: no new infrastructure; secrets unchanged; single API instance for the job (documented)
**Scale/Scope**: one practice; tens of reminders per day

## Constitution Check

- I. Git workflow: feature branch off `dev`, no direct commits to dev. DEVIATION (requester-mandated): branch is `claude/issue-9-appointment-reminders`, not `009-appointment-reminders`; spec folder keeps the NNN name. No push per instructions.
- II. Layering: entities/DTOs/enums/interfaces in Core; processor, repository, EF config in Infrastructure; controllers and the `BackgroundService` host in Api. Controllers have no EF queries. PASS.
- III. API contracts: DTO records, enums as strings, typed methods in `services.ts` plus TS types. PASS.
- IV. Security: public endpoints are token-only (no parallel auth scheme for patients; tokens are random 256-bit, hashed at rest), uniform failure responses, no secrets committed, patient email never in logs. Doctor route uses existing role attribute and ownership check. PASS.
- V. Simplicity: email only, no feature flags, no new framework. PASS.

## Project Structure

```text
specs/009-appointment-reminders/  spec.md plan.md research.md data-model.md quickstart.md contracts/ tasks.md
MedFlow.Core/        Entities (AppointmentReminder, ReminderDelivery), Enums, DTOs, Interfaces (IReminderRepository, IReminderProcessor)
MedFlow.Infrastructure/ Reminders/ReminderProcessor.cs, ReminderSettings.cs; Repositories; AppDbContext; Data/Migrations
MedFlow.Api/         Controllers/AppointmentResponseController.cs, AppointmentsController (log endpoint), Services/ReminderBackgroundService.cs, Program.cs
MedFlow.Api.Tests/   ReminderTests.cs
medflow-client/      pages/AppointmentResponsePage.tsx, AppointmentsPage.tsx, api/services.ts, types, App.tsx route
```

## Design notes

- Settings `Reminders:LeadTimeHours` (default 24, valid 1-168, invalid falls back to default), `Reminders:IntervalMinutes` (15), `Reminders:MaxAttempts` (3). Bound with `IOptions`.
- One `AppointmentReminder` row per (appointment, scheduled time). Rescheduling creates a new row on a later run because the existing row's `ScheduledAt` differs; old tokens fail the "scheduled time matches" check.
- The token is regenerated on every attempt (raw token only exists inside the sent email), so retries never need the old raw token.
- Attempt is claimed (Attempts incremented and saved) before sending to avoid duplicates within an instance.
- Public API uses POST bodies so tokens do not land in request-log paths; rate-limited by a new policy.
- Cancelled can never be re-confirmed through a link; Completed/NoShow/Cancelled/past all yield "can no longer be changed" (for a valid token) or the generic invalid result.
