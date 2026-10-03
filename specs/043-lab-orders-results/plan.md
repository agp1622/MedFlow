# Implementation Plan: Lab Orders and Results

**Branch**: `claude/issue-21-lab-orders` | **Date**: 2026-10-03 | **Spec**: [spec.md](spec.md)

## Summary

Doctors order labs for their own patients, enter numeric results by hand with reference ranges typed from the lab report, and see abnormal values (computed server-side from the entered range) in a Labs tab on the patient page. Doctor-private, audited through `IAuditService`, delivered with an EF Core migration and Spanish/English client strings.

## Technical Context

**Language/Version**: C# / .NET 8; React 18 + TypeScript (Vite)
**Primary Dependencies**: ASP.NET Core Web API, EF Core (SQL Server; in-memory in tests), TanStack Query, react-hook-form + zod, react-i18next
**Storage**: SQL Server via EF migration (new tables `LabOrders`, `LabResults`)
**Testing**: xUnit + `TestApiFactory` in `MedFlow.Api.Tests`; client gates are `npm run build` and `npm run lint`
**Target Platform**: web
**Project Type**: web-service + SPA
**Performance Goals**: patient lab list returns in one query pair; bounded by caps (100 orders x 50 results)
**Constraints**: doctor-only, 404 for non-owned, audit fail-closed, no invented reference ranges
**Scale/Scope**: one controller, one repository, two entities, one client panel

## Constitution Check

| Principle | Status |
|-----------|--------|
| I. Branch-per-feature | Pass with note: branch `claude/issue-21-lab-orders` was dictated by the caller, spec folder is `043-lab-orders-results`; commits stay on the feature branch, nothing pushed. |
| II. Layered architecture | Pass: entities/DTOs/enums/interface in Core, EF repository in Infrastructure, controller delegates to repository; client uses `labsApi` in `services.ts`. |
| III. Consistent API contracts | Pass with one noted deviation: the lab list returns a `LabSummaryDto` (orders plus abnormal count) rather than `PagedResult<T>`, as `clinical` and `vitals` do, because the collection is hard-capped at 100 orders and the page needs the full set to show the abnormal count. Enums serialize as strings; DTO records only. |
| IV. Security | Pass: existing `Roles.Doctor` policy, ownership in every query, uniform 404, audit without values. |
| V. Simplicity | Pass: no attachments link, no portal sharing, no flag storage, no HL7/FHIR. |

## Project Structure

```text
specs/043-lab-orders-results/{spec,plan,research,data-model,quickstart,tasks}.md, contracts/labs-api.md

MedFlow.Core/Entities/LabEntities.cs            # LabOrder, LabResult
MedFlow.Core/Enums/Enums.cs                     # LabOrderStatus, LabFlag, AuditItemKind.LabOrder (appended)
MedFlow.Core/DTOs/Dtos.cs                       # Lab DTOs and requests
MedFlow.Core/Interfaces/IRepositories.cs        # ILabOrderRepository
MedFlow.Infrastructure/Data/AppDbContext.cs     # DbSets, filters, config
MedFlow.Infrastructure/Data/Migrations/         # AddLabOrders
MedFlow.Infrastructure/Repositories/LabOrderRepository.cs
MedFlow.Infrastructure/DependencyInjection.cs   # register repository
MedFlow.Api/Controllers/LabOrdersController.cs
MedFlow.Api/Localization/Messages.cs            # Lab.* messages (es/en)
MedFlow.Api.Tests/LabOrderTests.cs
medflow-client/src/{types/index.ts, api/services.ts, hooks/queries.ts, components/labs/LabsPanel.tsx, pages/PatientsPage.tsx, i18n/resources/{es,en}.ts}
```

**Structure Decision**: extend the existing Core/Infrastructure/Api layering and the `clinical`-style panel pattern; no new projects.

## Complexity Tracking

| Deviation | Why | Simpler alternative rejected because |
|-----------|-----|--------------------------------------|
| Non-paged lab list | Hard cap of 100 orders; abnormal count needs the whole set | Paging would hide abnormal values on later pages |
