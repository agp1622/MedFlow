# Implementation Plan: Patient Portal – My Records

**Branch**: `005-patient-portal-records` | **Date**: 2026-10-01 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/005-patient-portal-records/spec.md`

## Summary

Add a read-only patient portal. Patients get a distinct `Patient` Identity role, are invited by their
doctor by email, and see only their own appointments, prescriptions, invoices, and the attachments and
notes the doctor has explicitly shared. Approach: introduce Identity roles (`Doctor`, `Patient`) and a
role claim in the JWT; lock every existing controller to `Doctor`; add a separate `PortalController`
that resolves the patient **only from the authenticated user** (never from client-supplied ids); add a
per-item `SharedWithPatient` flag; add a hashed, single-use, expiring invitation; and add a patient-side
React area with its own layout and route guard. See [research.md](research.md) for the decisions.

## Technical Context

**Language/Version**: C# / .NET 8 (`net8.0`); TypeScript + React (Vite)

**Primary Dependencies**: ASP.NET Core Web API, EF Core (SQL Server), ASP.NET Core Identity, JWT bearer, Serilog; React, TanStack Query, Zustand, react-hook-form + zod, react-router-dom v6

**Storage**: SQL Server via EF Core migrations; attachment files remain on disk under `wwwroot/uploads/attachments/{patientId}/`

**Testing**: No automated test projects exist today. Plan adds one small xUnit + `WebApplicationFactory` project covering access control only (SC-002); everything else verified via [quickstart.md](quickstart.md)

**Target Platform**: Web (desktop and mobile browsers); API in Docker/Azure per existing pipeline

**Project Type**: web-application (Api / Core / Infrastructure + `medflow-client`)

**Performance Goals**: Portal lists render within the spec's SC-001 (30 s from open, effectively one round trip per section); sharing changes visible on next request (SC-003)

**Constraints**: Read-only for patients; no PHI or tokens in URLs for portal downloads; no account-existence leaks; existing doctor flows unchanged (FR-011)

**Scale/Scope**: Single-doctor-per-patient data model; tens to low thousands of patients per doctor; ~8 new endpoints, 2 new entities, 2 new columns, ~3 new pages

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Git workflow | PASS | Branch `005-patient-portal-records` off `dev`; PR back to `dev`. Branch not yet pushed to `origin`. |
| II. Layered architecture | PASS | New entities/DTOs/`IPortalRepository` in Core; EF + repo impl in Infrastructure; controllers thin in Api. Client calls only through `api/services.ts`. |
| III. Consistent API contracts | PASS | Records for all DTOs, string enums, `/api/[controller]` routes, typed `portalApi`/extensions added to `services.ts` and `types`. |
| IV. Security & least privilege | PASS (with care) | Auth stays on Identity + existing `GenerateToken` (extended with role claims). Patient resolved server-side from the token. Uniform responses on invite accept and portal 404/403. No secrets added. |
| V. Simplicity | PASS | Read-only; no new auth system; no new DB engine. Access log is write-only (no UI). One new test project, justified below. |

**Post-design re-check (after Phase 1)**: unchanged — PASS. Roles are an extension of Identity already in use, not a new auth mechanism.

## Project Structure

### Documentation (this feature)

```text
specs/005-patient-portal-records/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── portal-api.md
└── tasks.md             # /speckit-tasks – not created here
```

### Source Code (repository root)

```text
MedFlow.Core/
├── Entities/        # PortalInvitation, PortalAccessLog (new); Patient, PatientAttachment, MedicalNote (edited)
├── DTOs/Dtos.cs     # Portal*Dto, invite/accept/sharing requests; UserDto gains Role
└── Interfaces/      # IPortalRepository, IPortalInvitationRepository

MedFlow.Infrastructure/
├── Data/            # AppDbContext sets + config; Migrations/<AddPatientPortal> (schema + role backfill)
├── Repositories/    # PortalRepository, PortalInvitationRepository
└── DependencyInjection.cs   # register repos; seed roles

MedFlow.Api/
├── Controllers/     # PortalController (new, Patient role); PortalInvitationsController (new, Doctor role);
│                    # existing controllers get [Authorize(Roles = "Doctor")] + sharing endpoints
├── Controllers/AuthController.cs  # roles on register/google/login; accept-invite; block patients from google-login
├── Extensions/JwtExtensions.cs    # role claims
└── Program.cs       # rate-limit policy for accept-invite

medflow-client/src/
├── api/services.ts  # portalApi, invitations + sharing calls
├── types/index.ts   # portal types, UserDto.role
├── store/authStore.ts
├── hooks/queries.ts
├── components/layout/PortalLayout.tsx          # new
├── components/attachments/AttachmentsTab.tsx   # share toggle
├── pages/PortalPage.tsx, AcceptInvitePage      # new
├── pages/PatientsPage.tsx                      # invite button, share toggle on notes
└── App.tsx          # role-based guards and redirects

MedFlow.Api.Tests/   # new: access-control tests only
```

**Structure Decision**: Existing three-project backend plus `medflow-client`; no new runtime projects. A single test project is added solely for SC-002.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| New `MedFlow.Api.Tests` project | SC-002 requires 100% denial of cross-patient/doctor-only access; this is a regression-prone security boundary and the repo has no tests | Manual checking alone cannot guard every existing and future controller against a missing `[Authorize(Roles)]` |
