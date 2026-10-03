# Tasks: Allergy and Problem List

**Input**: specs/018-allergy-problem-list/ (spec, plan, data-model, contracts/api.md). Refs #18.
Tests are required by the build rules (access control, isolation, validation limits).

## Phase 1: Foundational (blocks all stories)

- [ ] T001 Add `AllergySeverity` and `ProblemStatus` enums in MedFlow.Core/Enums/Enums.cs
- [ ] T002 Add abstract `ClinicalEntry`, `PatientAllergy`, `PatientProblem`, `PatientMedication` in MedFlow.Core/Entities/ClinicalEntities.cs
- [ ] T003 Add clinical DTO records and validated request records in MedFlow.Core/DTOs/Dtos.cs
- [ ] T004 Add `IPatientClinicalRepository` in MedFlow.Core/Interfaces/IRepositories.cs
- [ ] T005 Add DbSets, soft-delete filters, mappings in MedFlow.Infrastructure/Data/AppDbContext.cs
- [ ] T006 Implement `PatientClinicalRepository` in MedFlow.Infrastructure/Repositories/PatientClinicalRepository.cs and register in MedFlow.Infrastructure/DependencyInjection.cs
- [ ] T007 Generate EF migration `AddClinicalLists` (dotnet ef) in MedFlow.Infrastructure/Data/Migrations and verify Up only creates tables/indexes

## Phase 2: US2 + US3 - Maintain lists, privately (P1) backend

**Independent test**: API tests for CRUD, validation, isolation, role rejection.

- [ ] T008 [US2] Implement `PatientClinicalController` (summary GET, allergy/problem/medication POST/PUT/DELETE, ICD-10 normalise, duplicate allergy 409, cap 100, onset not future) in MedFlow.Api/Controllers/PatientClinicalController.cs
- [ ] T009 [US3] Ensure doctor-only role attribute and owner-scoped 404 on every action (same file)
- [ ] T010 [US2] [US3] Write tests in MedFlow.Api.Tests/ClinicalListTests.cs: CRUD per list, ICD-10 validation/normalisation, max lengths, missing severity, duplicate allergy, cap, cross-doctor 404 (read/write/by id), cross-patient entry id 404, patient token 403, anonymous 401, portal profile contains no clinical data, Patient.Allergies/Notes unchanged

## Phase 3: US1 + US2 - Client panel (P1)

**Independent test**: `npm run build`; manual pass from quickstart.md.

- [ ] T011 [P] [US1] Add clinical types in medflow-client/src/types/index.ts
- [ ] T012 [US1] Add `clinicalApi` in medflow-client/src/api/services.ts
- [ ] T013 [US1] Add query keys and hooks (summary, add/update/remove x3) in medflow-client/src/hooks/queries.ts
- [ ] T014 [US1] [US2] Build `ClinicalPanel` (summary, empty states, severe emphasis, legacy allergy text read-only, add/edit forms with react-hook-form + zod, confirm on remove, resolved toggle) in medflow-client/src/components/clinical/ClinicalPanel.tsx
- [ ] T015 [US1] Render the panel above the tabs in medflow-client/src/pages/PatientsPage.tsx

## Phase 4: Polish

- [ ] T016 Run dotnet build, dotnet test, npm run build; fix issues
