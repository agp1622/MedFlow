# Implementation Plan: Multi-user Clinic with Roles

**Branch**: `claude/issue-23-clinic-roles` | **Date**: 2026-10-03 | **Spec**: [spec.md](spec.md)

## Summary

Introduce a Clinic layer. Every doctor-scoped record gains a `ClinicId`; every staff request is authorized by one central policy handler that resolves the caller's active clinic membership from the database, checks a single permission matrix, and hands controllers a `ClinicScope`. Repositories filter by `ClinicScope.ClinicId` instead of `DoctorId`. Owners manage staff through an invitation flow that reuses the hashed-token pattern of portal invitations. A hand-ordered migration backfills one clinic per existing doctor so no data is lost. The client gains a Staff page, an accept-invitation page and role-aware navigation.

## Technical Context

**Language/Version**: C# / .NET 8, React + TypeScript (Vite)
**Primary Dependencies**: ASP.NET Core authorization (`IAuthorizationHandler`, dynamic policies), EF Core (SQL Server; InMemory in tests), ASP.NET Core Identity, `IEmailSender`, TanStack Query, react-hook-form + zod, i18next
**Storage**: new tables `Clinics`, `ClinicMembers`, `StaffInvitations`; `ClinicId` on 15 record tables; migration `AddClinicsAndRoles`
**Testing**: xUnit with `TestApiFactory`; new `ClinicRolesTests`, `StaffManagementTests`, `ClinicIsolationTests`, `ClinicBackfillTests`
**Constraints**: role and clinic never from the token or request body; fail closed; cross-clinic equals missing (404); tokens 256-bit random, SHA-256 hex at rest; no account enumeration
**Scale/Scope**: a practice of a few to tens of staff; one clinic per user

## Constitution Check

- I Git workflow: feature branch off `dev`, local commits only. DEVIATION (requester-mandated): branch `claude/issue-23-clinic-roles`, spec folder `045-clinic-roles` (same pattern as earlier specs).
- II Layers: roles, permission matrix, scope, entities, DTOs and interfaces in Core (no ASP.NET/EF types); `AppDbContext`, `ClinicService`, repositories in Infrastructure; the authorization handler and attribute are HTTP concerns in Api; controllers hold no EF queries. Client only through `api/services.ts`. PASS
- III Contracts: DTO records, string enums, `PagedResult<T>` for staff and invitation lists, typed client methods. PASS
- IV Security & Least Privilege: authentication stays Identity + `GenerateToken`; no parallel auth scheme (staff invitation acceptance creates the account through `UserManager` and returns the standard `AuthResponse`). Uniform responses for invitation failures and for inviting an existing email. Password policy stays central. PASS
- V Simplicity: no multi-clinic, no custom roles, no global EF query filter, no feature flags. A central `ClinicId` stamp in the DbContext is justified as the single place that makes "ClinicId never 0 and never contradicts the patient" true for every writer. PASS
- Governance note: the constitution says the matrix must be enforced centrally; this plan does so with one attribute + handler + matrix and a reflection test that fails if a staff action lacks a permission.

## Project Structure

```text
specs/045-clinic-roles/{spec,plan,research,data-model,quickstart,tasks}.md, contracts/{api,permission-matrix}.md
MedFlow.Core/Roles.cs                      # ClinicRole, Permission, PermissionMatrix, ClinicScope
MedFlow.Core/Entities/ClinicEntities.cs    # Clinic, ClinicMember, StaffInvitation, IClinicScoped
MedFlow.Core/Entities/*                    # IClinicScoped + ClinicId on 15 entities
MedFlow.Core/DTOs/Dtos.cs                  # staff/clinic records, UserDto +clinic
MedFlow.Core/Interfaces/                   # IClinicService, repository signatures take ClinicScope, IAuditService
MedFlow.Infrastructure/Clinics/ClinicService.cs
MedFlow.Infrastructure/Data/AppDbContext.cs (+ClinicId stamping, FKs, indexes) + Migrations/*AddClinicsAndRoles
MedFlow.Infrastructure/Repositories/*      # filter by ClinicId
MedFlow.Api/Authorization/                 # HasPermissionAttribute, PermissionRequirement, PermissionAuthorizationHandler, ClinicScopeExtensions
MedFlow.Api/Controllers/*                  # permission attributes; StaffController, ClinicController; AuthController (+accept-staff-invitation)
MedFlow.Api/Program.cs                     # policies, rate limit, handler registration
MedFlow.Api/Localization/Messages.cs
MedFlow.Api.Tests/                         # new tests; small edits to 4 premise-changed assertions
medflow-client/src/{types,api/services.ts,hooks/queries.ts,i18n/resources,pages/StaffPage.tsx,pages/AuthPages.tsx,App.tsx,components/layout,store/authStore.ts}
```

## Design notes

- **Scope resolution**: `PermissionAuthorizationHandler` runs for every `[HasPermission(p)]` endpoint. It rejects unauthenticated, Patient-role tokens and users without an active membership of an existing clinic, then checks `PermissionMatrix.Has(role, p)`. On success it stores the `ClinicScope` in `HttpContext.Items`; controllers call `this.Scope()` (throws `UnauthorizedAccessException` -> 403 if absent, so a forgotten attribute fails closed).
- **Repositories**: replace `string doctorId` with `ClinicScope scope`. Patient-owned rows are filtered by their own `ClinicId`. Gate for single-record access remains the audit gate (patient must be in the scope's clinic) plus the repository filter.
- **ClinicId stamping**: `AppDbContext.SaveChangesAsync` fills `ClinicId` on Added `IClinicScoped` entities whose value is 0 from the patient (`PatientId`) or, for `Patient`, from the treating doctor's membership; it throws if it cannot resolve, and throws if a non-zero value differs from the patient's clinic.
- **Receptionist redaction**: `PatientClinicalFields` permission gates primary condition, allergies, notes and blood type in `PatientDto`/`PatientSummaryDto`, patient search (no matching on primary condition), and update (preserve stored values). Create ignores them.
- **Treating doctor for staff-created rows**: patient create and appointment create accept optional `doctorId` (must be an active Owner/Doctor of the clinic with a Doctor profile, otherwise 404/400); default is the caller when Owner/Doctor, else the clinic's earliest active Owner. Invoice doctor = appointment's doctor, else caller when Owner/Doctor, else the patient's doctor.
- **Notes by non-doctors**: `MedicalNote.DoctorId` keeps meaning "author user id"; the FK to `Doctors` is removed; author display name comes from `Doctors` when present, else `Users`.
- **Roles and Identity**: staff do not get Identity roles; `Patient` remains an Identity role. `BuildAuthResponse` emits the clinic role (or `Patient`) as the token role claim and `UserDto.Role`; authorization ignores it for staff.
- **Staff service**: `ClinicService` serializes membership mutations per clinic with a `SemaphoreSlim` and an `UpdatedStamp` concurrency token on `Clinic`; last-owner rule counts active Owners excluding the target.
- **Invitations**: random 32 bytes hex token, SHA-256 hash at rest, 7-day lifetime, superseding earlier pending invitations for the same clinic+email, single use; accept endpoint rate limited, uniform `Auth.InvalidInvitation` error; account created through `UserManager` with `EmailConfirmed = true` (the link proved the address), password policy from Identity; a Doctor invitee also gets a `Doctors` profile. Role changes to Owner/Doctor ensure a `Doctors` profile exists.
- **Audit**: `IAuditService.RecordAsync(ClinicScope, ...)` for staff, `RecordPortalAsync(userId, ...)` for patients; events store `ClinicId`, actor role = clinic role. `GetLogAsync(scope, patientId, query)`: Owner any patient of clinic; Doctor only if `Patient.DoctorId == scope.UserId`; others denied by permission.
- **Reports**: repository takes the scope; non-Owners are limited to `DoctorId == scope.UserId`.
- **Migration**: see data-model.md; reviewed by hand for unintended drops.
- **Out of scope**: multi-clinic users, receptionist-managed availability, custom roles, SSO, EF global query filters, clinic deletion, email change of members.
