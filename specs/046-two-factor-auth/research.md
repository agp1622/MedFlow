# Research: Two-Factor Authentication

## R1 - TOTP verification
- Decision: own verifier (HMAC-SHA1, 30 s, 6 digits, window -1..+1) over the key from `UserManager.GetAuthenticatorKeyAsync` (base32). Returns the matched step; compare with `CryptographicOperations.FixedTimeEquals`; the matched step must be greater than `LastTotpStep`, then it is saved.
- Rationale: Identity's `AuthenticatorTokenProvider` compares with `==` (not constant time), accepts a +-2 step window, and hides the matched step, so replay cannot be rejected.
- Alternatives: Identity provider alone (no replay protection); an OTP NuGet package (new dependency for ~40 lines).

## R2 - Replay race
- Decision: `LastTotpStep` lives on `ApplicationUser`; saved through `UserManager.UpdateAsync`, whose `ConcurrencyStamp` check makes a concurrent double-accept fail.

## R3 - Recovery codes
- Decision: 10 codes of 10 characters from an unambiguous 32-letter alphabet (50 bits), shown as `XXXXX-XXXXX`; hashed with `IPasswordHasher<ApplicationUser>` (PBKDF2, salted, constant-time verify); `UsedAt` is a concurrency token so a double spend fails. Identity's own `GenerateNewTwoFactorRecoveryCodesAsync` stores plain text, so it is not used.
- Verification checks every unused code (max 10 hashes) so timing does not depend on which code matched; protected by lockout and rate limit.

## R4 - Challenge token
- Decision: JWT signed with the existing key, audience `<Jwt:Audience>:2fa`, claim `purpose=2fa`, `sub` = user id, no role claims, 5 minutes (`Jwt:TwoFactorChallengeMinutes`). Validated manually with `JwtSecurityTokenHandler`. The bearer middleware (audience = `Jwt:Audience`) rejects it; the validator rejects access tokens by audience and purpose.
- Alternatives: stored one-time challenges (extra table; no added protection given per-exchange code requirements).

## R5 - Lockout
- Decision: reuse Identity lockout (`AccessFailedAsync`, `IsLockedOutAsync`). For 2FA accounts the password check uses `CheckPasswordAsync` (not `CheckPasswordSignInAsync`, which resets the counter on success); the counter resets only after a completed second factor. Wrong password, wrong code, failed disable/regenerate all count. Locked accounts get the existing "account locked" message before any code is checked.

## R6 - Secret at rest
- Decision: Identity token store (`AspNetUserTokens`) as is. Identity encrypts it only with `ProtectPersonalData` (requires key ring and lookup protector infrastructure not present). Reported as a deployment recommendation (database encryption at rest), not built here.

## R7 - Audit
- Decision: `IAuditService.RecordSecurityAsync(userId, SecurityEventKind)` writes to a new append-only `SecurityEvents` table (kind, user, time). The existing `AuditEvents` table requires a patient and shows up in patient logs, so it is not reused.

## R8 - QR
- Decision: `qrcode` npm package (MIT) draws the otpauth URI in the browser as an SVG/canvas data URL; no network request.

## R9 - Status/management auth
- Decision: `[Authorize]` plus an explicit refusal of the Patient role (403); staff of any clinic role use it. Deactivated staff are already denied on staff endpoints, but 2FA management does not need clinic data, so it only needs a valid staff identity.
