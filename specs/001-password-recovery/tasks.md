---
description: "Task list for Password Recovery Page and Functionality"
---

# Tasks: Password Recovery Page and Functionality

**Input**: Design documents from `/specs/001-password-recovery/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/auth-password-recovery.md, quickstart.md
**Tests**: Not requested (no test project exists; see plan.md). Verification is via the manual scenarios in quickstart.md.
**Organization**: Grouped by user story. Paths are relative to the repo root.

## Format: `- [ ] [ID] [P?] [Story?] Description with file path`

---

## Phase 1: Setup

- [X] T001 Add an `Email` config section (SmtpHost `smtp.gmail.com`, SmtpPort 587, SenderEmail, SenderName `MedFlow`, AppPassword — placeholders only, no real secrets) to `MedFlow.Api/appsettings.json`
- [X] T002 [P] Add `ForgotPasswordRequest(string Email)` and `ResetPasswordRequest(string Email, string Token, string NewPassword, string ConfirmPassword)` records under an "Auth (Password Recovery)" section in `MedFlow.Core/DTOs/Dtos.cs`
- [X] T003 [P] Add `ForgotPasswordRequest` and `ResetPasswordRequest` TypeScript types next to `LoginRequest`/`RegisterRequest` in `medflow-client/src/types/index.ts` (locate the actual types file first)

---

## Phase 2: Foundational (blocks all user stories)

- [X] T004 Define `IEmailSender` (`Task SendAsync(string toEmail, string subject, string htmlBody)`) in `MedFlow.Core/Interfaces/IEmailSender.cs`
- [X] T005 [P] Create `EmailSettings` options class (bound to the `Email` section) in `MedFlow.Infrastructure/Email/EmailSettings.cs`
- [X] T006 Implement `SmtpEmailSender : IEmailSender` using `System.Net.Mail.SmtpClient` (STARTTLS, App Password credentials from `EmailSettings`, sender name/address) in `MedFlow.Infrastructure/Email/SmtpEmailSender.cs`
- [X] T007 Bind `EmailSettings` and register `IEmailSender` → `SmtpEmailSender` in `MedFlow.Infrastructure/DependencyInjection.cs`
- [X] T008 [P] Add `authApi.forgotPassword` and `authApi.resetPassword` (POST `/auth/forgot-password`, `/auth/reset-password`, returning `{ message: string }`) in `medflow-client/src/api/services.ts`

**Checkpoint**: Email infrastructure and client API layer ready.

---

## Phase 3: User Story 1 - Request a password reset (P1) 🎯 MVP part 1

**Goal**: Doctor submits their email on a Forgot password page and gets a uniform confirmation; registered local-password accounts receive a reset-link email.
**Independent Test**: Submit a known email → reset email arrives; submit an unknown email → identical confirmation, no email; spam submissions → 429.

- [X] T009 [US1] Add `POST forgot-password` ([AllowAnonymous]) to `MedFlow.Api/Controllers/AuthController.cs`: validate email format (400), look up user via `UserManager.FindByEmailAsync`, if found with a password call `GeneratePasswordResetTokenAsync`, build link `{AllowedOrigins[0]}/reset-password?token={urlencoded}&email={urlencoded}`, send a MedFlow-branded email via `IEmailSender` (FR-014); always return 200 with the uniform message; do equivalent work on the request path for unknown emails; log outcome without the token or plaintext (FR-015)
- [X] T010 [US1] Register ASP.NET Core rate limiting (fixed-window limiter policy for forgot-password keyed by client IP, rejection returns 429 `{ "error": "Too many requests. Please try again later." }`), call `UseRateLimiter()` in the pipeline, and apply `[EnableRateLimiting]` to the forgot-password action in `MedFlow.Api/Program.cs` and `AuthController.cs`
- [X] T011 [P] [US1] Add `ForgotPasswordPage` (email field, react-hook-form + zod, uniform success message, `AuthShell`/`Field` reuse, handle 429 with a toast) in `medflow-client/src/pages/AuthPages.tsx`
- [X] T012 [US1] Add a "Forgot password?" link to `LoginPage` in `medflow-client/src/pages/AuthPages.tsx` and the `/forgot-password` route (wrapped in `PublicRoute`) in `medflow-client/src/App.tsx`

**Checkpoint**: Quickstart Scenario 1 passes.

---

## Phase 4: User Story 2 - Set a new password from the reset link (P1) 🎯 MVP part 2

**Goal**: Doctor opens the emailed link, sets a new password, and can log in with it; old password and used/expired links stop working.
**Independent Test**: Valid link + policy-compliant password → success and login works; reuse/expired/tampered link → invalid-link message.

- [X] T013 [US2] Add `POST reset-password` ([AllowAnonymous]) to `MedFlow.Api/Controllers/AuthController.cs`: 400 on password/confirm mismatch; find user by email (unknown → generic invalid-link 400); call `ResetPasswordAsync` (decode token if needed); token errors → 400 `{ error: "This reset link is invalid or has expired. Please request a new one." }`; policy errors → 400 `{ errors: [...] }`; on success clear lockout (`SetLockoutEndDateAsync(user, null)`, `ResetAccessFailedCountAsync`), log completion without secrets, return 200 with the success message (FR-009–FR-013, FR-015)
- [X] T014 [P] [US2] Add `ResetPasswordPage` in `medflow-client/src/pages/AuthPages.tsx`: read `token` and `email` from `useSearchParams`, zod schema with password policy (min 8, uppercase) and confirm-match `refine` as in `RegisterPage`, success state linking to `/login`, invalid/expired state with a link to `/forgot-password` (FR-011)
- [X] T015 [US2] Add the `/reset-password` route (wrapped in `PublicRoute`) in `medflow-client/src/App.tsx`

**Checkpoint**: Quickstart Scenario 2 passes.

---

## Phase 5: User Story 3 - Google Sign-In accounts are guided correctly (P2)

**Goal**: Google-only accounts get no reset link, but an email telling them to sign in with Google; on-screen response stays generic.
**Independent Test**: Submit a Google-only account's email → generic confirmation; inbox has a "Sign in with Google" email with no reset link.

- [X] T016 [US3] In the forgot-password action in `MedFlow.Api/Controllers/AuthController.cs`, branch on `UserManager.HasPasswordAsync(user)`: when false, skip token generation and send a MedFlow-branded "Sign in with Google instead" email linking to `{AllowedOrigins[0]}/login` via `IEmailSender`; response unchanged (FR-005, SC-005)

**Checkpoint**: Quickstart Scenario 3 passes.

---

## Phase 6: Polish & Cross-Cutting

- [X] T017 [P] Verify no secrets are committed (`appsettings.json` placeholders only) and ensure `.gitignore` covers `appsettings.Development.json` if it holds credentials
- [X] T018 Build backend (`dotnet build`) and frontend (`npm run build` in `medflow-client`) and fix any errors
- [ ] T019 Run the quickstart.md scenarios 1–3 end to end and record results

---

## Dependencies & Execution Order

- Phase 1 → Phase 2 → user stories. T004 → T006 → T007; T005 → T006.
- US1 and US2 are independent after Phase 2 (both edit `AuthController.cs`, `AuthPages.tsx`, `App.tsx`, so their tasks touching the same file run sequentially).
- US3 (T016) depends on T009.
- Polish depends on all stories.

## Parallel Examples

- Setup: T002 and T003 together.
- Foundational: T005 and T008 in parallel with T004.
- US1: T011 (frontend page) in parallel with T009/T010 (backend).

## Implementation Strategy

- **MVP**: Phases 1–4 (US1 + US2) — the flow is only usable end to end with both.
- Then add US3 (T016), then Polish.
