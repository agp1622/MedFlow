# Tasks: Audit Log

**Input**: Design documents in `/specs/006-audit-log/`
**Tests**: Required (access control, isolation, validation limits, immutability), in `MedFlow.Api.Tests` with `TestApiFactory`.

## Phase 1: Foundational (blocks all stories)

- [ ] T001 Add `AuditAction` (View, Change) and `AuditItemKind` enums in `MedFlow.Core/Enums/Enums.cs`
- [ ] T002 [P] Add `AuditEvent` entity (not a `BaseEntity`) in `MedFlow.Core/Entities/AuditEvent.cs`
- [ ] T003 [P] Add `AuditEventDto` and `AuditLogQuery` records in `MedFlow.Core/DTOs/Dtos.cs`
- [ ] T004 Add `IAuditService` (`RecordAsync` returning false when the patient is not accessible to the actor, `GetLogAsync` returning null when not owned) in `MedFlow.Core/Interfaces/IAuditService.cs`
- [ ] T005 Add `AuditEvents` DbSet, entity configuration (string enums, lengths, index `(PatientId, OccurredAt)`) and a `SaveChangesAsync` guard rejecting modified or deleted `AuditEvent` entries in `MedFlow.Infrastructure/Data/AppDbContext.cs`
- [ ] T006 Implement `AuditService` (ownership check for doctor and patient actors, actor display name, changed-field diff helper, filtered paged query) in `MedFlow.Infrastructure/Repositories/AuditService.cs` and register it in `MedFlow.Infrastructure/DependencyInjection.cs`
- [ ] T007 Generate EF migration `AddAuditEvents` with `dotnet ef migrations add AddAuditEvents --project MedFlow.Infrastructure --startup-project MedFlow.Api` and review it for unintended changes

## Phase 2: User Story 1 - Access is recorded (P1)

**Independent test**: Perform view and change actions as a doctor and as a portal patient; each yields exactly one correct event; denied requests yield none.

- [ ] T008 [US1] Instrument `MedFlow.Api/Controllers/PatientsController.cs` (view, create, update with changed fields, delete); return 404 when `RecordAsync` reports not accessible
- [ ] T009 [P] [US1] Instrument `MedFlow.Api/Controllers/AppointmentsController.cs` (by-patient and by-id views; create, update, status, delete)
- [ ] T010 [P] [US1] Instrument `PrescriptionsController`, `InvoicesController`, `VitalSignsController`, `MedicalNotesController` in `MedFlow.Api/Controllers/DomainControllers.cs` (patient section views; create, update, mark-paid, sharing, delete)
- [ ] T011 [P] [US1] Instrument `MedFlow.Api/Controllers/AttachmentsController.cs` (list, upload before writing the file, download, preview, sharing, delete)
- [ ] T012 [P] [US1] Instrument `MedFlow.Api/Controllers/PortalInvitationsController.cs` (invite and revoke as Change of kind PortalAccess)
- [ ] T013 [P] [US1] Instrument `MedFlow.Api/Controllers/PortalController.cs` (every portal read as a View by the patient actor)
- [ ] T014 [US1] Tests in `MedFlow.Api.Tests/AuditLogTests.cs`: doctor view and change events with user, role, kind and time; changed fields hold names and no values; portal views recorded; foreign-doctor access creates no event; `AuditEvent` modification or deletion is rejected

## Phase 3: User Story 2 - Review the log per patient (P1)

**Independent test**: Events by two users on two patients; patient A log lists only A's events and each filter narrows correctly.

- [ ] T015 [US2] Add `AuditLogController` (`GET api/patients/{patientId}/audit-log`, Doctor role, validation of paging and date range, records an AuditLog view) in `MedFlow.Api/Controllers/AuditLogController.cs`
- [ ] T016 [P] [US2] Add `AuditEventDto`/query types in `medflow-client/src/types/index.ts` and `auditApi.getByPatient` in `medflow-client/src/api/services.ts`
- [ ] T017 [US2] Add `useAuditLog` query hook in `medflow-client/src/hooks/queries.ts`
- [ ] T018 [US2] Add an "Audit log" tab with filters (action, user, date range), paging and empty state to the patient detail view in `medflow-client/src/pages/PatientsPage.tsx` (new component under `medflow-client/src/components/audit/`)
- [ ] T019 [US2] Tests in `MedFlow.Api.Tests/AuditLogTests.cs`: newest first, paging, filters alone and combined, empty log, `from > to` and oversize page rejected, viewing the log is recorded

## Phase 4: User Story 3 - Protected audit data (P2)

- [ ] T020 [US3] Tests in `MedFlow.Api.Tests/AuditLogTests.cs`: patient token, other doctor, and anonymous caller are denied; foreign and missing patient give identical 404 bodies

## Phase 5: Polish

- [ ] T021 Run `dotnet build MedFlow.sln`, `dotnet test MedFlow.Api.Tests`, `npm run build` and `npm run lint` in `medflow-client`; confirm no pre-existing test regressed
- [ ] T022 Walk through `specs/006-audit-log/quickstart.md` (manual items listed in the final report)

## Dependencies

Phase 1 then US1 and US2 (US2 needs T004-T006 only; its UI is independent of T008-T013). US3 tests need T015. Parallel: T002/T003; T009-T013; T016.

## Strategy

MVP is Phase 1 plus US1 and the log endpoint T015; the UI tab follows.
