# Tasks: Multi-user Clinic with Roles

**Input**: specs/045-clinic-roles (spec, plan, research, data-model, contracts)
Format: `- [ ] T### [P] [US#] description with path`. Paths are relative to the repo root.

## Phase 1: Setup and Core model (blocking)

- [X] T001 Add `ClinicRole`, `Permission`, `PermissionMatrix`, `ClinicScope` to `MedFlow.Core/Roles.cs` (matrix exactly as `contracts/permission-matrix.md`)
- [X] T002 [P] Add `Clinic`, `ClinicMember`, `StaffInvitation`, `IClinicScoped` in `MedFlow.Core/Entities/ClinicEntities.cs`; add `ClinicId` + `IClinicScoped` to Patient, Appointment, Prescription, Invoice, VitalSign, MedicalNote (remove `Doctor` nav), PatientAttachment, ClinicalEntry, LabOrder, IntakeSubmission, WaitlistEntry, AuditEvent
- [X] T003 [P] Add DTO records (`ClinicDto`, `ClinicDoctorDto`, `StaffMemberDto`, `StaffInvitationDto`, `InviteStaffRequest`, `ChangeRoleRequest`, `RenameClinicRequest`, `AcceptStaffInvitationRequest`), `UserDto` clinic fields, optional `DoctorId` on create patient/appointment requests in `MedFlow.Core/DTOs/Dtos.cs`
- [X] T004 Add `IClinicService` and change repository, report, audit and dashboard interfaces to take `ClinicScope` in `MedFlow.Core/Interfaces/`
- [X] T005 Configure new entities, `ClinicId` indexes/FKs, drop MedicalNote->Doctor relationship, and `ClinicId` stamping/validation in `MedFlow.Infrastructure/Data/AppDbContext.cs`
- [X] T006 Generate migration `AddClinicsAndRoles`, hand-order it with the backfill SQL, THROW guard, temp column drop and safe Down; read it for unintended drops (`MedFlow.Infrastructure/Migrations/`)

## Phase 2: Foundational - central authorization and scope

- [X] T007 `HasPermissionAttribute`, `PermissionRequirement`, `PermissionAuthorizationHandler`, `ClinicScopeExtensions.Scope()` in `MedFlow.Api/Authorization/`
- [X] T008 Implement `ClinicService` (resolve scope, provision owner, invite, accept, list, change role, deactivate, reactivate, revoke, doctors) in `MedFlow.Infrastructure/Clinics/ClinicService.cs`; register in `DependencyInjection.cs`
- [X] T009 Register policies, handler and `staff-invitation` rate limit in `MedFlow.Api/Program.cs`
- [X] T010 Registration and Google sign-in provision Doctor + Clinic + Owner; `BuildAuthResponse` role/clinic; `DbSeeder` seeds clinic and membership (`MedFlow.Api/Controllers/AuthController.cs`, `MedFlow.Infrastructure/Data/DbSeeder.cs`)

## Phase 3: User Story 1 - clinic ownership and isolation (P1)

**Test**: two clinics, each sees only its own data, cross-clinic ids give 404, no membership gives 403.

- [X] T011 [US1] Patients: repository and `PatientsController` scoped by clinic with permissions, treating-doctor resolution, receptionist redaction/preservation and search rule (`Repositories.cs`, `PatientsController.cs`)
- [X] T012 [P] [US1] Appointments + reminders log scoped (`Repositories.cs`, `AppointmentsController.cs`)
- [X] T013 [P] [US1] Prescriptions, invoices (incl. claim export), dashboard scoped with role redaction (`Repositories.cs`, `DomainControllers.cs`)
- [X] T014 [P] [US1] Vitals, notes (author name from Doctors/Users), note templates scoped (`Repositories.cs`, `DomainControllers.cs`, `NoteTemplatesController.cs`)
- [X] T015 [P] [US1] Attachments, clinical lists, labs scoped (`AttachmentsController.cs`, `PatientClinicalController.cs`, `PatientClinicalRepository.cs`, `LabOrdersController.cs`, `LabOrderRepository.cs`)
- [X] T016 [P] [US1] Waitlist, intake review, portal invitations, availability scoped (`WaitlistController.cs`, `WaitlistRepository.cs`, `IntakeReviewController.cs`, `IntakeRepository.cs`, `PortalInvitationsController.cs`, `AvailabilityController.cs`)
- [X] T017 [US1] Remove every `[Authorize(Roles = Roles.Doctor)]`; every staff action carries `[HasPermission]`
- [X] T018 [US1] Tests: `MedFlow.Api.Tests/ClinicIsolationTests.cs` (cross-clinic 404 per area, no membership 403, patient token 403, stamping rules)

## Phase 4: User Story 2 - permission matrix (P1)

**Test**: role-by-area allow/deny table.

- [X] T019 [US2] Tests: `MedFlow.Api.Tests/ClinicRolesTests.cs` role x area matrix (every Permission), receptionist redaction, nurse vitals/notes, reflection test that every staff action has a permission, `PermissionMatrix` unit checks

## Phase 5: User Story 3 - staff invitation and management (P1)

**Test**: invite, accept, change role, deactivate, last owner.

- [X] T020 [US3] `StaffController` and `ClinicController` per `contracts/api.md`; `POST /api/auth/accept-staff-invitation`; messages in `MedFlow.Api/Localization/Messages.cs`
- [X] T021 [US3] Tests: `MedFlow.Api.Tests/StaffManagementTests.cs` (invite email/token hashed at rest, expiry, reuse, supersede, wrong email, existing-email uniform response, role changes take effect immediately, deactivate/reactivate, last owner incl. concurrent demotion, non-owner denied)

## Phase 6: User Story 4 and 5 - audit and reports (P2)

- [X] T022 [US4] `IAuditService`/`AuditService`/`AuditExtensions`/`AuditLogController`: staff scope + role, patient overload, Owner vs treating Doctor read rule; waitlist repository uses the patient overload (`MedFlow.Infrastructure/Repositories/AuditService.cs`, `MedFlow.Api/Extensions/AuditExtensions.cs`)
- [X] T023 [US5] `ReportRepository` and `ReportsController` clinic-wide for Owner, own for Doctor (`ReportRepository.cs`, `ReportsController.cs`)
- [X] T024 [US4] [US5] Tests: audit actor role + clinic + reader rules, reports scoping, in `ClinicRolesTests.cs`/`ClinicIsolationTests.cs`
- [X] T025 Premise-changed existing assertions Doctor -> Owner: `AccessControlTests.cs`, `AuditLogTests.cs`, `InsuranceClaimTests.cs`, `PrescriptionPdfTests.cs`
- [X] T026 [P] Backfill guard test `MedFlow.Api.Tests/ClinicBackfillTests.cs` (every `IClinicScoped` table is backfilled in the migration source; migration Down restores MedicalNotes FK)

## Phase 7: User Story 6 - client (P2)

- [X] T027 [US6] Types, `clinicApi`/`staffApi`, auth accept-staff-invitation in `medflow-client/src/types/index.ts`, `src/api/services.ts`; hooks in `src/hooks/queries.ts`
- [X] T028 [US6] `authStore` keeps role/clinic; permission helper `src/utils/permissions.ts` mirroring the matrix
- [X] T029 [US6] `src/pages/StaffPage.tsx` (list, invite, role change, deactivate/reactivate, revoke, clinic rename) and accept-staff-invite page in `src/pages/AuthPages.tsx`; routes in `src/App.tsx`
- [X] T030 [US6] Role-aware navigation, hidden clinical sections and doctor select for staff-created patients/appointments (`src/components/layout/*`, `src/pages/PatientsPage.tsx`, `AppointmentsPage.tsx`, `DashboardPage.tsx`, `BillingPrescriptionsPages.tsx`, `ReportsPage.tsx`)
- [X] T031 [P] [US6] i18n Spanish primary + English in `src/i18n/resources/*`

## Phase 8: Docs and verification

- [X] T032 README section "Clinics and roles" (matrix, behaviour changes, follow-ups)
- [X] T033 Verify: `dotnet build MedFlow.sln`, `dotnet test MedFlow.Api.Tests`, `npm run build`, `npm run lint`, `dotnet ef migrations has-pending-model-changes`
- [X] T034 Update this file's checkboxes and commit in logical commits referencing `Refs #23`

## Dependencies

Phase 1 -> Phase 2 -> Phase 3 (T011-T017 mostly parallel after T011) -> tests T018/T019. Phase 5 needs T008. Phase 6 needs T004/T008. Phase 7 needs the API contracts only. Phase 8 last.

## Implementation strategy

Core + authorization + migration first (T001-T010), then area-by-area scoping keeping the build green with the existing 275 tests, then staff features, audit/reports, client, and finally the new tests and docs. MVP = Phases 1-5.

## Phase 9: Convergence

- [X] T035 Show a specific "not available for this patient" message in the audit tab when the API answers 404 (a Doctor who is not the treating doctor), in `medflow-client/src/components/audit/AuditLogTab.tsx` and i18n es/en per FR-012 (partial)
- [X] T036 Record in the README and final report that the migration SQL (US1/AC2) and the client UI (US6) were verified structurally/by build only, not against a live SQL Server or browser, per SC-001 (partial)
