# Implementation Plan: Digital Intake Forms

**Branch**: `claude/issue-12-intake-forms` | **Date**: 2026-10-03 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/012-intake-forms/spec.md` (GitHub issue #12)

## Summary

A doctor emails a patient a personal, expiring, hashed-token link (modelled on the existing `PortalInvitation` mechanism). The patient, with no account, opens a public form page, submits demographics/history plus consent (typed-name e-signature, timestamp, consent text version). The submission is stored Pending; a doctor reviews it beside the current record and accepts (whole-submission update of the patient record) or rejects. Booking-confirmation delivery is deferred to issue #10.

## Technical Context

**Language/Version**: C# / .NET 8; React + TypeScript (Vite)

**Primary Dependencies**: ASP.NET Core Web API, EF Core (SQL Server), existing `IEmailSender`, ASP.NET rate limiter, TanStack Query, react-hook-form + zod

**Storage**: SQL Server via EF Core migration (new tables `IntakeLinks`, `IntakeSubmissions`)

**Testing**: xUnit with `TestApiFactory` in `MedFlow.Api.Tests`

**Target Platform**: Web (API + SPA)

**Project Type**: web-service + SPA

**Performance Goals**: standard CRUD latency; no special targets

**Constraints**: public endpoints leak nothing; constant-shape responses for invalid links; per-IP rate limit

**Scale/Scope**: one practice; low volume

## Constitution Check

- I Git workflow: feature branch off `dev`; branch name deviates (`claude/issue-12-intake-forms`) at the requester's explicit instruction, folder is `specs/012-intake-forms`. Flagged in report. No push.
- II Layered: entities/DTOs/interfaces in Core; repo in Infrastructure; thin controllers; client via `intakeApi` in services.ts. PASS.
- III Contracts: DTO records, string enums, `PagedResult<T>` for the submissions list, typed client methods. PASS.
- IV Security: new anonymous endpoints are token-gated; token hashed (SHA-256) like invitations; uniform invalid-link responses; rate limited; doctor routes `[Authorize(Roles = Doctor)]` and scoped by DoctorId; no secrets. Uses no parallel auth identity (token is a capability link, same pattern as the existing invitation). PASS.
- V Simplicity: no form builder, no uploads, fixed consent text. PASS.

Post-design re-check: PASS.

## Project Structure

```text
specs/012-intake-forms/  spec.md plan.md research.md data-model.md quickstart.md contracts/ tasks.md
MedFlow.Core/Entities/DomainEntities.cs         (+IntakeLink, IntakeSubmission)
MedFlow.Core/Enums                              (+IntakeStatus)
MedFlow.Core/DTOs/Dtos.cs                       (+intake DTOs)
MedFlow.Core/Interfaces/IRepositories.cs        (+IIntakeRepository)
MedFlow.Infrastructure/Data/AppDbContext.cs     (+sets, config) and a new migration
MedFlow.Infrastructure/Repositories/Repositories.cs (+IntakeRepository), DependencyInjection.cs
MedFlow.Api/Controllers/IntakeController.cs     (public form) and IntakeReviewController.cs (doctor)
MedFlow.Api/Program.cs                          (+"intake-public" rate limit policy)
MedFlow.Api.Tests/IntakeTests.cs
medflow-client/src/{types,api/services.ts,pages/IntakePages.tsx,App.tsx, patient page hooks}
```

**Structure Decision**: Extend existing three-project layout; mirror PortalInvitation patterns.
