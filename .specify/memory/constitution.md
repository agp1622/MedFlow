<!--
Sync Impact Report
- Version change: [UNSET TEMPLATE] → 1.0.0 (initial ratification)
- Modified principles: n/a (first fill of the template)
- Added sections:
  - I. Git Workflow (Branch-per-Feature)
  - II. Layered Architecture
  - III. Consistent API Contracts
  - IV. Security & Least Privilege
  - V. Simplicity & No Half-Finished Work
  - Technology Stack Constraints
  - Development Workflow
  - Governance
- Removed sections: none (template placeholders replaced)
- Deferred items: none — all placeholders resolved from repo context and user input
-->

# MedFlow Constitution

## Core Principles

### I. Git Workflow (Branch-per-Feature)

All feature work MUST happen on a branch created off `dev`; `dev` MUST NOT receive direct
commits for feature/story work. Feature branches MUST follow the Spec Kit feature-directory
naming convention (`NNN-feature-name`, matching the corresponding `specs/NNN-feature-name/`
directory, e.g. `001-password-recovery`). Every feature branch MUST be pushed to `origin` so
work is visible and recoverable outside the local checkout. Merging back into `dev` happens via
pull request, not by fast-forwarding or rewriting `dev` history directly.

**Rationale**: `dev` is the shared integration line for the team; committing directly to it makes
work impossible to review, bisect, or roll back independently, and collides with concurrent
stories. Branch-per-feature keeps each story's diff isolated and traceable to its spec.

### II. Layered Architecture

The solution MUST keep `MedFlow.Core` (domain entities, DTOs, enums, repository interfaces),
`MedFlow.Infrastructure` (EF Core `DbContext`, Identity, repository implementations, external
integrations), and `MedFlow.Api` (controllers, HTTP concerns, composition root) as separate
projects with dependencies flowing one direction only: `Api` → `Infrastructure` → `Core`.
Controllers MUST NOT contain EF Core query logic beyond what is already delegated to
repositories/services, and `Core` MUST NOT reference ASP.NET Core or EF Core types. The React
client (`medflow-client`) MUST talk to the API exclusively through the `api/services.ts` layer,
never with ad hoc `fetch`/`axios` calls scattered in components.

**Rationale**: The existing codebase already follows this separation (Core/Infrastructure/Api
projects, `authApi`/`patientsApi`/etc. service modules); preserving it keeps domain logic
testable and swappable independent of persistence or transport details.

### III. Consistent API Contracts

All request/response shapes crossing the API boundary MUST be explicit DTOs (C# `record` types
in `MedFlow.Core.DTOs`), never EF entities or Identity types returned directly. Enums MUST
serialize as strings (per the existing `JsonStringEnumConverter` configuration) rather than
numeric codes. New endpoints MUST follow the existing REST conventions already in use
(`/api/[controller]`, resource-oriented routes, `PagedResult<T>` for list endpoints) and MUST
have a corresponding typed method added to the matching `*Api` object in `medflow-client/src/api/services.ts`
plus matching TypeScript types.

**Rationale**: The frontend and backend are maintained together in this repo; contract drift
between the two is the most common source of runtime breakage, so the DTO/service-layer pattern
already established MUST be extended, not bypassed, by new work.

### IV. Security & Least Privilege

Authentication MUST continue to go through ASP.NET Core Identity (`UserManager`/`SignInManager`)
and JWT bearer tokens issued by the existing `GenerateToken` extension; new endpoints MUST NOT
implement parallel or ad hoc authentication. Secrets (JWT signing keys, SMTP/OAuth credentials,
connection strings) MUST be supplied via configuration/environment, never hard-coded or committed
in source. Endpoints and flows that deal with account recovery, credential changes, or any
patient-identifiable data MUST avoid leaking account existence or sensitive state through response
content, status codes, or timing (e.g. uniform responses for password-reset requests regardless of
whether the email is registered). Password and lockout policy MUST remain centrally defined in
Identity configuration (`AddInfrastructure`), not duplicated per endpoint.

**Rationale**: MedFlow handles medical practice and patient data; consistent, centrally-enforced
auth and information-disclosure discipline is a baseline requirement, not a per-feature choice.

### V. Simplicity & No Half-Finished Work

Implementations MUST match the scope of the approved spec/plan for the story being built — no
speculative abstractions, feature flags, or unused extension points added "for later." A story is
not done until its acceptance scenarios are met end-to-end (UI + API + persistence, where
applicable); partial slices MUST NOT be merged as if complete. Prefer extending existing patterns
already present in the codebase (e.g. the `*Api` service objects, the `AuthShell`/`Field` UI
building blocks, the repository interfaces in `MedFlow.Core.Interfaces`) over introducing new,
parallel patterns for the same concern.

**Rationale**: A small team maintaining a focused product benefits more from a consistent,
finished codebase than from premature generalization; this mirrors how the codebase is written
today.

## Technology Stack Constraints

- **Backend**: .NET 8 (`net8.0`), ASP.NET Core Web API, Entity Framework Core with SQL Server,
  ASP.NET Core Identity for auth, JWT bearer tokens, Serilog for logging.
- **Frontend**: React + TypeScript, built with Vite, Zustand for client state (`authStore`),
  TanStack Query for server state, `react-hook-form` + `zod` for form validation, Tailwind-style
  utility classes for styling.
- **Data**: SQL Server via EF Core migrations; schema changes MUST ship as EF Core migrations
  committed alongside the feature that needs them.
- Introducing a new language, runtime, database engine, or a replacement for any of the above
  requires an explicit constitution amendment before implementation begins.

## Development Workflow

- Every feature starts from a spec under `specs/NNN-feature-name/` (via `/speckit-specify`)
  before implementation begins, per the Git Workflow principle's branch naming.
- Pull requests are the only way changes land on `dev`; PRs SHOULD reference the spec/plan they
  implement.
- Backend changes that touch persistence MUST include the corresponding EF Core migration.
- Changes that add or modify an API endpoint MUST update the matching frontend service/types in
  the same change, per the Consistent API Contracts principle.
- UI changes SHOULD be manually verified in a running dev server before being reported as
  complete, per standard practice for this project.

## Governance

This constitution supersedes ad hoc conventions when the two conflict. Amendments are made via
`/speckit-constitution`, MUST update the Sync Impact Report at the top of this file, and MUST
follow semantic versioning for the constitution itself: MAJOR for backward-incompatible
principle removals/redefinitions, MINOR for new principles or materially expanded guidance,
PATCH for clarifications and wording fixes. Plans and tasks produced by `/speckit-plan` and
`/speckit-tasks` MUST be checked against these principles; any deviation MUST be called out
explicitly and justified rather than silently introduced. Complexity that isn't justified by a
principle above should be simplified before merging.

**Version**: 1.0.0 | **Ratified**: 2026-09-19 | **Last Amended**: 2026-09-19
