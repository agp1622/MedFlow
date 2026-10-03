# Implementation Plan: Audit Log

**Branch**: `006-audit-log` | **Date**: 2026-10-02 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/006-audit-log/spec.md` (GitHub issue #24)

## Summary

Record an append-only `AuditEvent` for every view and change of a patient record by a doctor or a patient
(portal), and expose a doctor-only, paged, filterable per-patient log. Recording goes through one
`IAuditService` called explicitly by the controllers after the caller is identified and before data is
returned or a write is applied, so a recording failure fails the request (fail closed). The service also
checks that the patient belongs to the actor and reports "not accessible", so denied requests never create
events and cross-doctor requests look identical to a missing patient. Events store only item kind, item id and
changed field names, never values. See [research.md](research.md).

## Technical Context

**Language/Version**: C# / .NET 8; TypeScript + React (Vite)

**Primary Dependencies**: ASP.NET Core Web API, EF Core (SQL Server), Identity, JWT; React, TanStack Query, react-hook-form + zod

**Storage**: SQL Server via EF Core; one new table `AuditEvents` (migration `AddAuditEvents`)

**Testing**: xUnit + `WebApplicationFactory` in `MedFlow.Api.Tests` using `TestApiFactory` (EF InMemory)

**Project Type**: web-application (Api / Core / Infrastructure + `medflow-client`)

**Performance Goals**: First page of a patient log under 2 s at 10,000 events (SC-003): composite index `(PatientId, OccurredAt)`

**Constraints**: No clinical values in events; events immutable; no new auth mechanism; list/search/dashboard screens not audited

**Scale/Scope**: 1 entity, 1 service, 1 controller, ~30 instrumented endpoints, 1 client tab

## Constitution Check

| Principle | Status | Notes |
|---|---|---|
| I. Git workflow | PASS | Branch `006-audit-log` off the working line; PR back to `dev` by pr-shipper. |
| II. Layered architecture | PASS | Entity, enums, DTOs, `IAuditService` in Core; EF + service in Infrastructure; controllers call the service only; client uses `services.ts`. |
| III. Consistent API contracts | PASS | Record DTOs, string enums, `PagedResult<T>`, typed `auditApi` + types. |
| IV. Security & least privilege | PASS | Doctor role only; patient scope by ownership; identical 404 for foreign/missing; no values stored. |
| V. Simplicity | PASS | One service, no filters/interceptors/queues, no export, no admin role. |

Post-design re-check: unchanged, PASS.

## Project Structure

```text
specs/006-audit-log/{plan,research,data-model,quickstart}.md, contracts/audit-api.md

MedFlow.Core/      Entities/AuditEvent.cs, Enums (AuditAction, AuditItemKind), DTOs (AuditEventDto, AuditLogQuery), Interfaces/IAuditService.cs
MedFlow.Infrastructure/  Data (DbSet, config, immutability guard, migration), Repositories/AuditService.cs, DI registration
MedFlow.Api/       Controllers/AuditLogController.cs (new); calls added to Patients, Appointments, Prescriptions,
                   Invoices, VitalSigns, MedicalNotes, Attachments, PortalInvitations, Portal controllers
MedFlow.Api.Tests/ AuditLogTests.cs
medflow-client/src/ api/services.ts (auditApi), types/index.ts, hooks/queries.ts, patient detail "Audit log" tab
```

**Structure Decision**: Extend the existing three-project layout; no new projects.
