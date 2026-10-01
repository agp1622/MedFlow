# Implementation Plan: Password Recovery Page and Functionality

**Branch**: `001-password-recovery` | **Date**: 2026-09-19 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-password-recovery/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Add self-service password recovery to MedFlow: a "Forgot password?" flow that emails a doctor a
single-use, time-limited reset link (via Gmail SMTP), and a reset-password page that applies a new
password using that link. Google Sign-In-only accounts are detected and redirected to "Sign in
with Google" instead of receiving a reset link. The approach reuses ASP.NET Core Identity's
existing token provider (no new database table), adds two anonymous endpoints to the existing
`AuthController`, a small SMTP-backed `IEmailSender` in `MedFlow.Infrastructure`, and two new pages
in the existing React auth flow (`AuthPages.tsx` pattern).

## Technical Context

**Language/Version**: C# / .NET 8 (`MedFlow.Api`, `MedFlow.Core`, `MedFlow.Infrastructure`); TypeScript (`medflow-client`, React 18 + Vite)

**Primary Dependencies**: ASP.NET Core Identity + EF Core 8 (existing), `System.Net.Mail.SmtpClient` (framework-provided, no new package) for Gmail SMTP, ASP.NET Core built-in rate limiting middleware (framework-provided); frontend: existing `react-hook-form` + `zod`, `@tanstack/react-query`, `zustand`, `react-router-dom`, `react-hot-toast`

**Storage**: SQL Server via existing `AppDbContext` / EF Core — **no schema changes**; password reset tokens are ephemeral, derived from Identity's `SecurityStamp` (see `research.md` §1), not persisted

**Testing**: No automated test project exists in the repo today (backend or frontend); this feature is verified via the manual scenarios in `quickstart.md` plus the acceptance scenarios in `spec.md`. Introducing a test framework is out of scope for this story (Constitution Principle V)

**Target Platform**: Linux server (existing Docker/Azure deployment) for the API; modern web browsers for the React client

**Project Type**: Web application (backend + frontend, matches the existing repo structure — Option 2 below)

**Performance Goals**: Standard web-app expectations; no feature-specific performance target beyond SC-004 (95% of reset emails arrive within 5 minutes)

**Constraints**: Reset links expire within Identity's default `TokenLifespan` (1 day, already configured); forgot-password requests are rate-limited per FR-012; no user-enumeration signal in `forgot-password` responses (FR-003/FR-006)

**Scale/Scope**: 2 new API endpoints, 1 new cross-cutting service (`IEmailSender`), 2 new frontend pages, 2 new frontend routes — no new persistent entities

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Check | Result |
|---|---|---|
| I. Git Workflow (Branch-per-Feature) | Work happens on `001-password-recovery`, branched off `dev`, matching `specs/001-password-recovery/` | PASS |
| II. Layered Architecture | New `IEmailSender` interface lives in `MedFlow.Core.Interfaces`; SMTP implementation lives in `MedFlow.Infrastructure`; new controller actions stay in the existing `AuthController` and delegate to `UserManager`/`IEmailSender`, no EF/Identity types leak into responses (DTOs only); frontend goes through `authApi` in `api/services.ts` | PASS |
| III. Consistent API Contracts | New `ForgotPasswordRequest`/`ResetPasswordRequest` DTOs as C# records in `MedFlow.Core.DTOs`; new routes follow existing `/api/[controller]` convention; matching `authApi.forgotPassword`/`resetPassword` + TS types added in the same change | PASS |
| IV. Security & Least Privilege | Reuses existing Identity auth (no parallel auth mechanism); SMTP credentials read from configuration, not hard-coded; `forgot-password` response is uniform regardless of account existence; password policy stays centrally defined in `DependencyInjection.cs`, not duplicated | PASS |
| V. Simplicity & No Half-Finished Work | Reuses Identity's built-in token/security-stamp mechanism instead of a new table; reuses `SmtpClient` instead of adding a new email package; reuses existing `AuthShell`/`Field` UI building blocks and `authApi` pattern; both user stories (request + reset) are implemented together so the slice is end-to-end usable | PASS |

No violations — Complexity Tracking table is not needed.

## Project Structure

### Documentation (this feature)

```text
specs/001-password-recovery/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md         # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
│   └── auth-password-recovery.md
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
MedFlow.Core/
├── DTOs/Dtos.cs                          # + ForgotPasswordRequest, ResetPasswordRequest
└── Interfaces/IRepositories.cs           # + IEmailSender (or new IEmailSender.cs alongside it)

MedFlow.Infrastructure/
├── DependencyInjection.cs                # + Email options binding + IEmailSender registration
└── Email/                                # (new) SmtpEmailSender, EmailSettings

MedFlow.Api/
├── Controllers/AuthController.cs         # + POST forgot-password, POST reset-password
├── appsettings.json                      # + "Email" config section (placeholders, no secrets)
└── Program.cs                            # + rate limiting middleware registration for forgot-password

medflow-client/src/
├── pages/AuthPages.tsx                   # + ForgotPasswordPage, ResetPasswordPage; "Forgot password?" link on LoginPage
├── api/services.ts                       # + authApi.forgotPassword, authApi.resetPassword
├── types/index.ts (or equivalent)        # + ForgotPasswordRequest, ResetPasswordRequest types
└── App.tsx                               # + /forgot-password, /reset-password routes
```

**Structure Decision**: This is the existing web application structure (ASP.NET Core API under
the `MedFlow.*` projects, React client under `medflow-client/`) — no new top-level projects or
directories are introduced. The feature is additive within the established
`Core → Infrastructure → Api` layering and the frontend's `pages/` + `api/services.ts` pattern.

## Complexity Tracking

> No Constitution Check violations — this section intentionally left empty.
