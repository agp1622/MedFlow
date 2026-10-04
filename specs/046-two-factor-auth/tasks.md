# Tasks: Two-Factor Authentication (TOTP)

**Input**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/api.md](contracts/api.md)
Format: `- [ ] ID [P] [Story] description with path`

## Phase 1: Foundational (blocks all stories)

- [ ] T001 Add `SecurityEventKind` enum to `MedFlow.Core/Enums/Enums.cs`; add `TwoFactorRecoveryCode` and `SecurityEvent` entities in `MedFlow.Core/Entities/TwoFactorEntities.cs`
- [ ] T002 [P] Add 2FA DTO records (status, setup, enable/disable/regenerate requests, recovery codes, challenge request/response, verify/recovery requests) to `MedFlow.Core/DTOs/Dtos.cs`
- [ ] T003 Add `ITwoFactorService` in `MedFlow.Core/Interfaces/ITwoFactorService.cs` and `IAuditService.RecordSecurityAsync` in `MedFlow.Core/Interfaces/IAuditService.cs`
- [ ] T004 Add `LastTotpStep` to `MedFlow.Infrastructure/Identity/ApplicationUser.cs`; DbSets, entity config, append-only guard for `SecurityEvent` in `MedFlow.Infrastructure/Data/AppDbContext.cs`; implement `RecordSecurityAsync` in `MedFlow.Infrastructure/Repositories/AuditService.cs`
- [ ] T005 [P] Implement `TotpVerifier` (RFC 6238, base32 decode, constant-time, returns matched step) in `MedFlow.Infrastructure/Identity/TotpVerifier.cs`
- [ ] T006 Implement `TwoFactorService` (status, setup, enable, verify TOTP, verify recovery, regenerate, disable, lockout + audit) in `MedFlow.Infrastructure/Identity/TwoFactorService.cs`; register in `MedFlow.Infrastructure/DependencyInjection.cs`
- [ ] T007 Generate EF migration `AddTwoFactorAuth` (`dotnet ef migrations add`), review for unintended drops, confirm `has-pending-model-changes` is clean

## Phase 2: US1 Enable 2FA (P1)

- [ ] T008 [US1] Create `MedFlow.Api/Controllers/TwoFactorController.cs` account endpoints `GET/POST api/account/2fa`, `setup`, `enable` (staff only, no-store); add `TwoFactor.*` messages in `MedFlow.Api/Localization/Messages.cs`

## Phase 3: US2 + US3 Gated login, brute force (P1)

- [ ] T009 [US2] Challenge token issue/validate helpers in `MedFlow.Api/Extensions/JwtExtensions.cs`
- [ ] T010 [US2] Gate `login` and `google-login` in `MedFlow.Api/Controllers/AuthController.cs` (challenge response, lockout-safe password check for 2FA accounts)
- [ ] T011 [US2] Add `POST api/auth/2fa/verify` and `/recovery` to `TwoFactorController.cs`; add `two-factor` rate limit policy in `MedFlow.Api/Program.cs`
- [ ] T012 [US2] [US3] Tests in `MedFlow.Api.Tests/TwoFactorTests.cs` (+ TOTP test helper): enable flow, gating, google gating, challenge vs bearer, replay, recovery single use, lockout, password-success-no-reset, rate limit, uniform errors, patient refusal, no-store

## Phase 4: US4 Regenerate and disable (P2)

- [ ] T013 [US4] Add `recovery-codes` and `disable` endpoints to `TwoFactorController.cs`
- [ ] T014 [US4] Tests for regenerate/disable/audit events/Google-only disable in `TwoFactorTests.cs`

## Phase 5: US5 Client (P2)

- [ ] T015 [P] [US5] Add `qrcode` + `@types/qrcode` to `medflow-client/package.json`; types in `medflow-client/src/types/index.ts`; `twoFactorApi` and `authApi.verifyTwoFactor/recoverTwoFactor` in `medflow-client/src/api/services.ts`
- [ ] T016 [US5] `medflow-client/src/pages/SecurityPage.tsx` (status, QR setup, confirm, recovery codes copy/download, regenerate, disable); route `/security` in `App.tsx`; nav entry in `components/layout/AppLayout.tsx`
- [ ] T017 [US5] Login 2FA step (code, recovery alternative, password and Google) in `medflow-client/src/pages/AuthPages.tsx`
- [ ] T018 [P] [US5] Spanish and English strings in `medflow-client/src/i18n/resources/es.ts` and `en.ts`

## Phase 6: Polish

- [ ] T020 Verify FR-013: grep the new code for any logger call that includes secrets, codes, challenge tokens or otpauth URIs (log only user id and event kind); verify the Serilog request logging does not capture bodies
- [ ] T019 Run `dotnet build`, `dotnet test`, `npm run build`, `npm run lint`; fix any caused failures; note any test whose premise changed
