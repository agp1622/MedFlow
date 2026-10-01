---

description: "Task list for Patient Portal – My Records"
---

# Tasks: Patient Portal – My Records

**Input**: Design documents from `/specs/005-patient-portal-records/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/portal-api.md](contracts/portal-api.md), [quickstart.md](quickstart.md)

**Tests**: Only the access-control suite called for by the plan (SC-002) is included, in US2. Everything else is verified via quickstart.md.

**Organization**: Grouped by user story. US1 and US2 are both P1 and ship together; US3 and US4 are P2.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an unfinished task)
- **[Story]**: US1–US4 from spec.md
- Paths are relative to the repo root

## Path Conventions

Backend: `MedFlow.Core/`, `MedFlow.Infrastructure/`, `MedFlow.Api/`. Frontend: `medflow-client/src/`. Tests: `MedFlow.Api.Tests/`.

---

## Phase 1: Setup

- [X] T001 Create xUnit project `MedFlow.Api.Tests/MedFlow.Api.Tests.csproj` (net8.0) referencing `MedFlow.Api`, with `Microsoft.AspNetCore.Mvc.Testing` and an in-memory/SQLite EF provider for tests; add it to `MedFlow.sln`
- [X] T002 [P] Add a `WebApplicationFactory<Program>` fixture in `MedFlow.Api.Tests/TestApiFactory.cs` that swaps SQL Server for the test provider, supplies a test `Jwt` config, and exposes helpers to create a doctor, a patient account and their JWTs (needs `public partial class Program {}` at the end of `MedFlow.Api/Program.cs`)

---

## Phase 2: Foundational (blocks all user stories)

**Purpose**: roles, schema, and the lock-down of existing endpoints. Nothing else may ship before this.

- [X] T003 Add `PortalUserId` (string?) to `Patient` in `MedFlow.Core/Entities/Patient.cs`
- [X] T004 [P] Add `SharedWithPatient` (bool, default false) to `PatientAttachment` and `MedicalNote` in `MedFlow.Core/Entities/DomainEntities.cs`
- [X] T005 [P] Add `PortalInvitation` and `PortalAccessLog` entities per data-model.md in `MedFlow.Core/Entities/DomainEntities.cs`
- [X] T006 Configure the new entities and columns in `MedFlow.Infrastructure/Data/AppDbContext.cs` (filtered unique index on `Patient.PortalUserId`, index on `PortalInvitation.TokenHash` and `PatientId`, `SharedWithPatient` default false)
- [X] T007 Generate EF migration `AddPatientPortal` in `MedFlow.Infrastructure/Data/Migrations/`; add `migrationBuilder.Sql` that inserts the `Doctor` and `Patient` roles and grants `Doctor` to every user having a row in `Doctors` (idempotent, with a correct `Down`)
- [X] T008 Ensure the `Doctor`/`Patient` roles also exist in fresh Development databases by creating missing roles in `MedFlow.Infrastructure/Data/DbSeeder.cs` and assigning `Doctor` to the seeded user (note: seeder currently returns early when users exist, so role creation must run before that check)
- [X] T009 Add `Role` to `UserDto` in `MedFlow.Core/DTOs/Dtos.cs` and update all constructor call sites in `MedFlow.Api/Controllers/AuthController.cs`
- [X] T010 Extend `GenerateToken` in `MedFlow.Api/Extensions/JwtExtensions.cs` to accept the user's roles and emit `ClaimTypes.Role` claims; update the three callers in `MedFlow.Api/Controllers/AuthController.cs` (register, login, google-login) to load roles via `UserManager.GetRolesAsync`
- [X] T011 In `MedFlow.Api/Controllers/AuthController.cs` assign the `Doctor` role in `Register` and when `GoogleLogin` creates a new user
- [X] T012 [P] Change `[Authorize]` to `[Authorize(Roles = "Doctor")]` on every controller in `MedFlow.Api/Controllers/DomainControllers.cs`, `AppointmentsController.cs`, `PatientsController.cs` and `AttachmentsController.cs` (all 5 controllers in DomainControllers.cs; leave `AuthController` untouched)
- [X] T013 In `MedFlow.Api/Controllers/AuthController.cs` make `GoogleLogin` return the existing `Invalid Google token.` 401 for users in the `Patient` role (FR-011, research D8)
- [X] T014 [P] Add `role` to the `UserDto` type in `medflow-client/src/types/index.ts` and make `authStore` expose it in `medflow-client/src/store/authStore.ts`
- [X] T015 Verify `dotnet build` and `dotnet ef database update`, then sign in as the seeded doctor and confirm existing pages still work (quickstart §0)

**Checkpoint**: roles exist, existing endpoints are doctor-only, schema is in place.

---

## Phase 3: User Story 1 – Patient views their own records (P1)

**Goal**: a signed-in patient sees their upcoming appointments, prescriptions and invoices.

**Independent Test**: sign in as a seeded patient account that has data in all three areas; each list shows only that patient's rows; empty sections show an empty state (quickstart §2).

- [X] T016 [P] [US1] Add `PortalProfileDto`, `PortalAppointmentDto`, `PortalPrescriptionDto`, `PortalInvoiceDto` records to `MedFlow.Core/DTOs/Dtos.cs` per contracts/portal-api.md
- [X] T017 [P] [US1] Add `IPortalRepository` to `MedFlow.Core/Interfaces/IRepositories.cs` with `GetPatientByUserIdAsync`, `GetAppointmentsAsync`, `GetPrescriptionsAsync`, `GetInvoicesAsync` (all keyed by patient id, upcoming = not cancelled and `ScheduledAt >= now`, soonest first)
- [X] T018 [US1] Implement `PortalRepository` in `MedFlow.Infrastructure/Repositories/Repositories.cs` projecting straight into the portal DTOs (no internal notes, no doctor ids) and register it in `MedFlow.Infrastructure/DependencyInjection.cs`
- [X] T019 [US1] Create `MedFlow.Api/Controllers/PortalController.cs` with `[Authorize(Roles = "Patient")]`, a private helper that resolves the patient from `User.GetUserId()` (403 `Portal access is unavailable.` when none linked or `Status != Active`), and endpoints `GET portal/me`, `portal/appointments`, `portal/prescriptions`, `portal/invoices`
- [X] T020 [P] [US1] Add a Development-only demo patient account (role `Patient`, linked via `PortalUserId` to a seeded patient with an appointment, prescription and invoice) in `MedFlow.Infrastructure/Data/DbSeeder.cs`, guarded so it never runs outside Development
- [X] T021 [P] [US1] Add portal types in `medflow-client/src/types/index.ts` and a `portalApi` (me, appointments, prescriptions, invoices) in `medflow-client/src/api/services.ts`
- [X] T022 [P] [US1] Add `usePortalMe/Appointments/Prescriptions/Invoices` hooks in `medflow-client/src/hooks/queries.ts`
- [X] T023 [P] [US1] Create `medflow-client/src/components/layout/PortalLayout.tsx` (header with patient name, theme toggle, sign out; no doctor navigation)
- [X] T024 [US1] Create `medflow-client/src/pages/PortalPage.tsx` "My Records" with Appointments, Prescriptions and Invoices sections, loading state and empty states, read-only
- [X] T025 [US1] Run quickstart §2 against the demo patient and fix defects

**Checkpoint**: a patient can see their own three lists.

---

## Phase 4: User Story 2 – Access limited to own data (P1)

**Goal**: patients cannot see other patients' data or reach doctor functionality; doctors are unaffected.

**Independent Test**: the access-control suite passes, and quickstart §4 by hand.

- [X] T026 [US2] In `medflow-client/src/App.tsx` add role-aware guards: patients are redirected to `/portal` from every doctor route, doctors are redirected away from `/portal`, login and the root route send each role to its home; add the `/portal` route using `PortalLayout`/`PortalPage`
- [X] T027 [P] [US2] Test: a Patient token gets 403 on `GET/POST /api/patients`, `/api/appointments`, `/api/prescriptions`, `/api/invoices`, `/api/medicalnotes`, `/api/attachments`, `/api/dashboard` in `MedFlow.Api.Tests/AccessControlTests.cs`
- [X] T028 [P] [US2] Test: a Doctor token gets 403 on every `/api/portal/*` route, and an unauthenticated call gets 401, in `MedFlow.Api.Tests/AccessControlTests.cs`
- [X] T029 [P] [US2] Test: patient A's token returns only A's appointments, prescriptions and invoices, never B's, in `MedFlow.Api.Tests/PortalIsolationTests.cs`
- [X] T030 [P] [US2] Test: portal routes return 403 for a patient whose status is `Inactive` or `Deceased`, and for a Patient-role user with no linked record, in `MedFlow.Api.Tests/PortalIsolationTests.cs`
- [X] T031 [P] [US2] Test: Google login for a Patient-role user returns 401, and existing doctor login/register still succeed with the `Doctor` role claim, in `MedFlow.Api.Tests/AuthRoleTests.cs`
- [X] T032 [US2] Run `dotnet test MedFlow.Api.Tests` and fix any failures; run quickstart §4 manually

**Checkpoint**: access boundary proven. US1 + US2 together are the testable core.

---

## Phase 5: User Story 3 – Doctor chooses what to share (P2)

**Goal**: doctors toggle sharing per attachment and note; patients see only shared items.

**Independent Test**: quickstart §3.

- [X] T033 [P] [US3] Add `sharedWithPatient` to `PatientAttachmentDto` and `MedicalNoteDto`, add `SetSharingRequest(bool Shared)`, and add `PortalAttachmentDto` and `PortalNoteDto` in `MedFlow.Core/DTOs/Dtos.cs`; update the DTO projections in `MedFlow.Infrastructure/Repositories/Repositories.cs` and the note mapping in `MedFlow.Api/Controllers/DomainControllers.cs`
- [X] T034 [P] [US3] Extend `IPortalRepository` in `MedFlow.Core/Interfaces/IRepositories.cs` with shared-only `GetAttachmentsAsync`, `GetNotesAsync`, `GetSharedAttachmentAsync(id, patientId)` and `LogAccessAsync(...)`
- [X] T035 [US3] Implement those methods in `MedFlow.Infrastructure/Repositories/Repositories.cs` (filter `SharedWithPatient == true` and patient id together; writes `PortalAccessLog`)
- [X] T036 [US3] Add `PUT api/attachments/{id}/sharing` to `MedFlow.Api/Controllers/AttachmentsController.cs` and `PUT api/medicalnotes/{id}/sharing` to `MedFlow.Api/Controllers/DomainControllers.cs`, both owner-checked by `DoctorId` (404 when not owned)
- [X] T037 [US3] Add `GET portal/attachments`, `GET portal/attachments/{id}/download` (404 identical for missing/unshared/not-theirs; logs `Download`) and `GET portal/notes` (logs `View`) to `MedFlow.Api/Controllers/PortalController.cs`
- [X] T038 [P] [US3] Add `setSharing` to `attachmentsApi` and `notesApi` and `portalApi.attachments/notes/downloadAttachment` (blob, header auth, no query-string token) in `medflow-client/src/api/services.ts`; matching hooks in `medflow-client/src/hooks/queries.ts`
- [X] T039 [P] [US3] Add a "Shared with patient" toggle to each attachment card in `medflow-client/src/components/attachments/AttachmentsTab.tsx`
- [X] T040 [P] [US3] Add a "Shared with patient" toggle to each note in the notes list in `medflow-client/src/pages/PatientsPage.tsx`
- [X] T041 [US3] Add "Documents" and "Notes from your doctor" sections to `medflow-client/src/pages/PortalPage.tsx` with download via blob and object URL
- [X] T042 [P] [US3] Test: unshared attachment/note is invisible and its download id returns 404; sharing makes it appear; unsharing makes the old id return 404; another patient's shared id returns 404; access log rows written, in `MedFlow.Api.Tests/SharingTests.cs`
- [X] T043 [US3] Run quickstart §3 and fix defects

**Checkpoint**: sharing works end to end.

---

## Phase 6: User Story 4 – Doctor invites a patient (P2)

**Goal**: a doctor invites a patient by email; the patient sets a password and lands in the portal.

**Independent Test**: quickstart §1.

- [X] T044 [P] [US4] Add `AcceptInvitationRequest` (token, email, password, confirmPassword), `InvitationResultDto` and `portalStatus` on the patient detail/summary DTOs in `MedFlow.Core/DTOs/Dtos.cs`
- [X] T045 [P] [US4] Add `IPortalInvitationRepository` to `MedFlow.Core/Interfaces/IRepositories.cs` (create superseding earlier pending invitations, find valid by token hash, mark used, revoke access)
- [X] T046 [US4] Implement `PortalInvitationRepository` in `MedFlow.Infrastructure/Repositories/Repositories.cs` (SHA-256 hashed token, 7-day expiry) and register it in `MedFlow.Infrastructure/DependencyInjection.cs`; compute `portalStatus` (`NotInvited`/`Invited`/`Active`) in the patient projections
- [X] T047 [US4] Create `MedFlow.Api/Controllers/PortalInvitationsController.cs` (`[Authorize(Roles = "Doctor")]`) with `POST api/patients/{patientId}/portal-invitation` (owner check, email required, active patient, not already linked, send link `{frontend}/accept-invite?token=…&email=…` via `IEmailSender`) and `DELETE api/patients/{patientId}/portal-access`
- [X] T048 [US4] Add `POST api/auth/accept-invitation` to `MedFlow.Api/Controllers/AuthController.cs`: identical generic error for unknown/expired/used/superseded/email-changed/inactive; on success create the user with the `Patient` role and password policy from Identity, set `Patient.PortalUserId`, mark the invitation used, return `AuthResponse`
- [X] T049 [US4] Add an `accept-invitation` rate-limit policy in `MedFlow.Api/Program.cs` (same shape as `forgot-password`) and apply it to the new endpoint
- [X] T050 [P] [US4] Add `authApi.acceptInvitation`, `patientsApi.invite` and `patientsApi.revokePortalAccess` to `medflow-client/src/api/services.ts` and matching mutations in `medflow-client/src/hooks/queries.ts`
- [X] T051 [P] [US4] Create the accept-invitation page (reuse the `AuthShell`/`Field`/`PasswordInput` patterns, zod validation) as `AcceptInvitePage` in `medflow-client/src/pages/AuthPages.tsx` and add the public route `/accept-invite` in `medflow-client/src/App.tsx`
- [X] T052 [US4] Add a portal status badge and "Invite to portal" / "Resend" / "Revoke access" actions to the patient detail header in `medflow-client/src/pages/PatientsPage.tsx`, with a clear message when the patient has no email
- [X] T053 [P] [US4] Test: invite requires owned, active patient with email; invitation is single-use; expired, superseded and email-changed invitations fail with the identical generic error; revoke blocks portal access; accepting creates a `Patient`-role user linked to the record, in `MedFlow.Api.Tests/InvitationTests.cs`
- [X] T054 [US4] Run quickstart §1 end to end and fix defects

**Checkpoint**: full flow works from invitation to viewing records.

---

## Phase 7: Polish & Cross-Cutting

- [X] T055 [P] Confirm password recovery works for a patient account (existing `forgot-password`/`reset-password`, no changes expected) and add the case to `MedFlow.Api.Tests/AuthRoleTests.cs`
- [ ] T056 [P] Verify layout on a phone-width viewport for `PortalPage.tsx` and `AcceptInvitePage`; fix overflow
- [X] T057 [P] Update `README.md` with the new roles, the invitation flow and the Development demo patient account
- [ ] T058 Run the full quickstart (§0–§5), `dotnet test`, and `npm run build` in `medflow-client`; then push the branch and open a PR to `dev` referencing issue #11

---

## Dependencies & Execution Order

- **Phase 1 → Phase 2** (T001–T002 only needed by tests; non-test work can start after T003)
- **Phase 2 blocks everything.** T003→T006→T007 sequential (same files/migration); T004, T005, T012, T014 parallel with each other.
- **US1 and US2** both depend on Phase 2. US2's guards (T026) need `PortalPage` from US1 (T024); US2 tests need `PortalController` (T019). Do US1 first, then US2.
- **US3** depends on US1 (`PortalController`, `PortalPage`).
- **US4** depends on Phase 2 only, so it can run in parallel with US3 by a second developer. For *manual* testing of US1–US3 without US4, use the T020 demo account.
- **Polish** last.

```text
Phase 1 ─▶ Phase 2 ─┬─▶ US1 ─▶ US2 ─▶ US3 ─┐
                    └──────────▶ US4 ───────┴─▶ Polish
```

## Parallel Examples

```text
Phase 2:  T004 ∥ T005 ∥ T014   (then T012 ∥ T013)
US1:      T016 ∥ T017 ∥ T020 ∥ T021 ∥ T022 ∥ T023
US2:      T027 ∥ T028 ∥ T029 ∥ T030 ∥ T031
US3:      T033 ∥ T034 ;  T038 ∥ T039 ∥ T040 ∥ T042
US4:      T044 ∥ T045 ;  T050 ∥ T051 ∥ T053
```

## Implementation Strategy

1. **Foundation first** (Phase 2). It is also the riskiest change: it removes access from any user without the `Doctor` role, so verify T015 before moving on.
2. **Testable core = Foundational + US1 + US2**, using the Development demo patient. Demo/stop here to review.
3. **Releasable MVP = + US4**, since real patients need invitations. US3 can follow, but without it patients see no attachments or notes (the spec's default is unshared), which is acceptable for a first release.
4. Commit per phase on `005-patient-portal-records`; open one PR to `dev` per constitution.

---

## Implementation notes

- Analysis findings folded in: **C1** (re-invite / existing-email handling in `accept-invitation`, with tests), **C2** (sessions saved before roles existed are dropped in `authStore.ts`), **C4** (frontend types).
- Manual UI walkthroughs **T025, T043, T054** were confirmed by the user in the browser. Still open: **T056** (phone-width check) and **T058** (push + PR). Backend behaviour is covered by `MedFlow.Api.Tests` (36 tests) and an end-to-end run against SQL Server on a throwaway database.
- Fixed unrelated `noUnusedLocals` errors that were breaking `npm run build` on this branch (`AttachmentsTab.tsx`, `hooks/queries.ts`).
