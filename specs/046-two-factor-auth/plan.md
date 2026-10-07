# Implementation Plan: Two-Factor Authentication (TOTP)

**Branch**: `claude/issue-28-two-factor` | **Date**: 2026-10-04 | **Spec**: [spec.md](spec.md)

## Summary

TOTP 2FA for staff. Identity keeps the authenticator key and the `TwoFactorEnabled` flag; a small own RFC 6238 verifier replaces Identity's authenticator provider because that provider compares codes in non-constant time and does not tell which time step matched (needed to reject replay). Recovery codes live in a new hashed table. When 2FA is on, `login` and `google-login` return a challenge token (separate audience) and `2fa/verify` or `2fa/recovery` exchange it for the normal `AuthResponse`. Account endpoints manage setup, enable, regenerate and disable. Security events are stored through `IAuditService`.

## Technical Context

**Language/Version**: C# / .NET 8, React + TypeScript (Vite)
**Primary Dependencies**: ASP.NET Core Identity (`UserManager`, `IPasswordHasher`), `System.IdentityModel.Tokens.Jwt`, rate limiter, EF Core; client adds `qrcode` (MIT) for local QR rendering
**Storage**: new tables `TwoFactorRecoveryCodes`, `SecurityEvents`; column `LastTotpStep` on `AspNetUsers`; migration `AddTwoFactorAuth`
**Testing**: xUnit with `TestApiFactory`; new `TwoFactorTests`
**Constraints**: no secrets or codes in logs; constant-time comparisons; fail closed; uniform errors; no-store on secret-bearing responses
**Scale/Scope**: staff users only

## Constitution Check

- I Git workflow: feature branch off `dev`, local commits only. DEVIATION (requester-mandated): branch `claude/issue-28-two-factor`, spec folder `046-two-factor-auth`. PASS otherwise.
- II Layers: DTOs, `ITwoFactorService`, `SecurityEventKind` and `IAuditService` addition in Core (no ASP.NET/EF); `TwoFactorService`, TOTP verifier, entities config in Infrastructure; controller and challenge-token helpers in Api; controller holds no EF queries. Client only via `api/services.ts`. PASS
- III Contracts: DTO records, string enums, typed client methods. No list endpoints. PASS
- IV Security: authentication stays Identity + `GenerateToken`; the challenge is not a parallel auth scheme, it only gates issuance of the standard token. Uniform errors; central lockout policy reused. PASS
- V Simplicity: no remember-device, no stored challenges, no admin reset. The own TOTP verifier is justified in research.md R1. PASS

## Project Structure

```text
specs/046-two-factor-auth/{spec,plan,research,data-model,quickstart,tasks}.md, contracts/api.md
MedFlow.Core/Entities/TwoFactorEntities.cs        # TwoFactorRecoveryCode, SecurityEvent
MedFlow.Core/Enums/Enums.cs                       # SecurityEventKind
MedFlow.Core/DTOs/Dtos.cs                         # 2FA records
MedFlow.Core/Interfaces/ITwoFactorService.cs      # + IAuditService.RecordSecurityAsync
MedFlow.Infrastructure/Identity/{TotpVerifier,TwoFactorService}.cs, ApplicationUser (+LastTotpStep)
MedFlow.Infrastructure/Data/AppDbContext.cs (+sets, config) + Migrations/*AddTwoFactorAuth
MedFlow.Infrastructure/Repositories/AuditService.cs (+RecordSecurityAsync)
MedFlow.Api/Extensions/JwtExtensions.cs           # challenge issue/validate
MedFlow.Api/Controllers/{AuthController,TwoFactorController}.cs
MedFlow.Api/Program.cs                            # "two-factor" rate limit policy
MedFlow.Api/Localization/Messages.cs              # TwoFactor.* messages
MedFlow.Api.Tests/TwoFactorTests.cs (+ TotpTestHelper)
medflow-client/src/{api/services.ts,types/index.ts,pages/SecurityPage.tsx,pages/AuthPages.tsx,App.tsx,components/layout/AppLayout.tsx,i18n/resources/*}
```

## Google sign-in and portal interaction (decision)

- Google sign-in is gated: with 2FA on it returns the same challenge; the user completes it with TOTP or a recovery code. It is NOT bypassed.
- Portal patients cannot enrol (management endpoints reject patient tokens), so their login is unchanged. Login still challenges any account whose `TwoFactorEnabled` is true, whatever its role (fail closed).
- Invitation accept flows only create new accounts (or reset a patient password) and cannot carry 2FA. Password reset leaves 2FA on.
