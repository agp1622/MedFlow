# Tasks: Structured Note Templates

**Input**: specs/016-note-templates/ (spec, plan, data-model, contracts/api.md). Issue: #16.
Tests are required by the build rules (access control, isolation, validation limits).

## Phase 1: Foundational (blocks all stories)

- [ ] T001 Add `NoteTemplate` entity in MedFlow.Core/Entities/DomainEntities.cs
- [ ] T002 Add DTO records (NoteTemplateDto, CreateNoteTemplateRequest, UpdateNoteTemplateRequest, CopyForwardDto) in MedFlow.Core/DTOs/Dtos.cs
- [ ] T003 Add `INoteTemplateRepository` and `IMedicalNoteRepository.GetLatestForPatientAsync` in MedFlow.Core/Interfaces/IRepositories.cs
- [ ] T004 Add DbSet, soft-delete filter and config in MedFlow.Infrastructure/Data/AppDbContext.cs; implement `NoteTemplateRepository` and latest-note query in MedFlow.Infrastructure/Repositories/Repositories.cs; register in DependencyInjection.cs
- [ ] T005 Generate EF migration `AddNoteTemplates` (MedFlow.Infrastructure/Migrations) and review it for unintended changes
- [ ] T006 [P] Add TS types in medflow-client/src/types and `noteTemplatesApi` + `medicalNotesApi.getLatest` in medflow-client/src/api/services.ts

## Phase 2: User Story 2 - Create/edit own templates (P1)

- [ ] T007 [US2] Add `NoteTemplatesController` (list paged, builtin, create, update, delete; validation 400, duplicate 409, owner-scoped 404) in MedFlow.Api/Controllers
- [ ] T008 [US2] Tests in MedFlow.Api.Tests/NoteTemplateTests.cs: CRUD, name/body limits, empty, duplicate (case-insensitive, built-in name), cross-doctor 404 on GET/PUT/DELETE and list isolation, patient token 403, anonymous 401
- [ ] T009 [US2] Client template manager (list/create/edit/delete with react-hook-form + zod, TanStack Query) in medflow-client/src and route/nav link

## Phase 3: User Story 1 - Pick template when writing a note (P1)

- [ ] T010 [US1] Add template picker to the existing new-note form in medflow-client/src (built-in + own; confirm before replacing non-empty body)
- [ ] T011 [US1] Test that a note created with template text is an ordinary unshared note in MedFlow.Api.Tests/NoteTemplateTests.cs

## Phase 4: User Story 3 - Copy forward (P2)

- [ ] T012 [US3] Add `GET api/medicalnotes/patient/{patientId}/latest` to MedicalNotesController in MedFlow.Api/Controllers/DomainControllers.cs
- [ ] T013 [US3] Tests: returns own latest note; 404 when none; never returns other doctor's note; never another patient's; patient token 403 in MedFlow.Api.Tests/NoteTemplateTests.cs
- [ ] T014 [US3] "Copy from last visit" button on the new-note form in medflow-client/src

## Phase 5: Polish

- [ ] T015 Run dotnet build, dotnet test, npm run build, npm run lint; fix failures caused by this feature
