# Tasks: Digital Intake Forms

**Input**: `specs/012-intake-forms/` (spec, plan, data-model, contracts). Refs #12. Tests are required by repo build rules.

## Phase 1: Foundational (blocks all stories)

- [x] T001 [P] Add `IntakeStatus` enum in `MedFlow.Core/Enums/` (Pending, Accepted, Rejected)
- [x] T002 [P] Add `IntakeLink` and `IntakeSubmission` entities in `MedFlow.Core/Entities/DomainEntities.cs`
- [x] T003 [P] Add intake DTO records (submit request, form info, summary, detail, reject request, link result) in `MedFlow.Core/DTOs/Dtos.cs`
- [x] T004 Add `IIntakeRepository` in `MedFlow.Core/Interfaces/IRepositories.cs`
- [x] T005 Configure DbSets/entities (hash index, string enum, max lengths, no Patient nav) in `MedFlow.Infrastructure/Data/AppDbContext.cs`
- [x] T006 Implement `IntakeRepository` in `MedFlow.Infrastructure/Repositories/Repositories.cs` and register in `MedFlow.Infrastructure/DependencyInjection.cs`
- [x] T007 Add EF migration `AddIntakeForms` (`dotnet ef migrations add`), review for unintended drops
- [x] T008 Add `intake-public` rate limit policy in `MedFlow.Api/Program.cs` (config key `RateLimiting:IntakePermitLimit`, default 10 per 15 min)

## Phase 2: US1 Doctor sends link (P1)

**Test**: link emailed; no email refused; new link supersedes; other doctor's patient 404; patient token 403.

- [x] T009 [US1] `POST /api/patients/{patientId}/intake-link` in `MedFlow.Api/Controllers/IntakeReviewController.cs`
- [x] T010 [US1] Tests in `MedFlow.Api.Tests/IntakeTests.cs`: send ok, no email 400, supersede, cross-doctor 404, patient role 403

## Phase 3: US2 + US4 Patient submits with consent (P1)

**Test**: valid submit stores Pending with consent; validation limits; invalid/expired/used/superseded links identical 404; record unchanged.

- [x] T011 [US2] Public `GET/POST /api/intake/{token}` in `MedFlow.Api/Controllers/IntakeController.cs` (anonymous, rate limited, uniform 404, server timestamp, consent version constant, atomic single use)
- [x] T012 [US2] Tests in `MedFlow.Api.Tests/IntakeTests.cs`: submit ok + consent fields, missing consent/signature 400, over-length 400, future DOB 400, uniform 404 for unknown/expired/used/superseded, double submit only one succeeds, rate limit returns 429 after the configured limit, record unchanged
- [x] T013 [P] [US2] Client types in `medflow-client/src/types/` and `intakeApi` in `medflow-client/src/api/services.ts`
- [x] T014 [US2] Public intake page with react-hook-form+zod in `medflow-client/src/pages/IntakePages.tsx`; route `/intake/:token` (unauthenticated) in `medflow-client/src/App.tsx`

## Phase 4: US3 Doctor review (P1)

**Test**: list/detail scoped; accept updates record; reject leaves it; double decision 409; cross-doctor 404; patient 403.

- [x] T015 [US3] Doctor endpoints list/detail/accept/reject in `MedFlow.Api/Controllers/IntakeReviewController.cs` (repository applies accept in one transaction; Notes append, never overwrite)
- [x] T016 [US3] Tests in `MedFlow.Api.Tests/IntakeTests.cs`: accept updates fields and appends Notes, reject leaves record, repeat decision 409, cross-doctor 404 on view/accept/reject, patient role 403, consent immutability (no mutation route)
- [x] T017 [US3] Client: review list + detail page with accept/reject (TanStack Query) in `medflow-client/src/pages/IntakePages.tsx`, nav entry and routes in `medflow-client/src/App.tsx`, "Send intake form" button on the patient page in `medflow-client/src/pages/PatientsPage.tsx`

## Phase 5: Polish

- [x] T018 Verify `Patient.Notes` is not exposed through portal DTOs; note outcome in tests or research
- [x] T019 Run `dotnet build`, `dotnet test`, `npm run build`, `npm run lint`; walk `quickstart.md`

## Dependencies

Phase 1 -> US1 -> US2 (needs link) -> US3 (needs submissions). Client tasks T013/T014 parallel to backend of the later phase.
