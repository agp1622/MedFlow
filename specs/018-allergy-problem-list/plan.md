# Implementation Plan: Allergy and Problem List

**Branch**: `claude/next-medflow-issue-slda0y` (spec `018-allergy-problem-list`) | **Date**: 2026-10-03 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/018-allergy-problem-list/spec.md` (Refs #18)

## Summary

Add three doctor-maintained, per-patient lists (allergies, problems with ICD-10, current medications).
New EF entities share a small abstract base (`ClinicalEntry`: PatientId, DoctorId) and are scoped by
the owning doctor. One `PatientClinicalController` (`Doctor` role only) exposes a combined summary
GET plus add/edit/remove per list. The patient page gets a prominent panel above the tabs showing the
summary with inline add/edit/remove. Existing `Patient.Allergies`/`Notes` are never written; legacy
allergy text is displayed read-only. Additive migration only.

## Technical Context

**Language/Version**: C# / .NET 8; TypeScript + React (Vite)

**Primary Dependencies**: ASP.NET Core Web API, EF Core (SQL Server), Identity/JWT; React, TanStack Query, react-hook-form + zod

**Storage**: SQL Server via EF Core; three new tables, additive migration

**Testing**: xUnit + `TestApiFactory` in `MedFlow.Api.Tests` (EF in-memory)

**Target Platform**: Web

**Project Type**: web-application (Api / Core / Infrastructure + `medflow-client`)

**Performance Goals**: Summary is one request returning three small lists

**Constraints**: Doctor-scoped, 404 for non-owners, patient tokens rejected, no writes to existing text fields, create-only migration

**Scale/Scope**: At most 100 entries per list per patient; 10 endpoints, 3 entities

## Constitution Check

| Principle | Status | Notes |
|---|---|---|
| I. Git workflow | PASS (noted) | Work is on the requester-fixed branch `claude/next-medflow-issue-slda0y` off `dev`, not named `NNN-name`; the requester explicitly set this. Not pushed (requester pushes). |
| II. Layered architecture | PASS | Entities, DTOs, `IPatientClinicalRepository` in Core; EF in Infrastructure; thin controller; client via `services.ts`. |
| III. Consistent API contracts | PASS | Record DTOs, string enums, typed `clinicalApi` + types. The GET returns one summary object (three bounded lists), not an unbounded list, so `PagedResult` does not apply. |
| IV. Security | PASS | `[Authorize(Roles = Roles.Doctor)]`, doctor scoping in every query, uniform 404 for foreign/missing resources. |
| V. Simplicity | PASS | Format-only ICD-10 validation, no catalogue, no prescription sync, no portal exposure. |

Post-design re-check: unchanged, PASS.

## Project Structure

```text
specs/018-allergy-problem-list/{spec,plan,research,data-model,quickstart,tasks}.md, contracts/api.md

MedFlow.Core/Entities/ClinicalEntities.cs        # ClinicalEntry, PatientAllergy, PatientProblem, PatientMedication
MedFlow.Core/Enums/Enums.cs                      # AllergySeverity, ProblemStatus
MedFlow.Core/DTOs/Dtos.cs                        # Clinical DTOs + validated requests
MedFlow.Core/Interfaces/IRepositories.cs         # IPatientClinicalRepository
MedFlow.Infrastructure/Repositories/PatientClinicalRepository.cs
MedFlow.Infrastructure/Data/AppDbContext.cs + Migrations/*AddClinicalLists*
MedFlow.Infrastructure/DependencyInjection.cs
MedFlow.Api/Controllers/PatientClinicalController.cs
MedFlow.Api.Tests/ClinicalListTests.cs
medflow-client/src/{types/index.ts, api/services.ts, hooks/queries.ts, components/clinical/ClinicalPanel.tsx, pages/PatientsPage.tsx}
```

**Structure Decision**: Follow the existing Api/Core/Infrastructure + client layout; one repository
class with generic owned-entry helpers rather than three near-identical repositories.
