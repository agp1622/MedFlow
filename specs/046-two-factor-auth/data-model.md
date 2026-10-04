# Data Model

- **ApplicationUser** (existing): `+ LastTotpStep long?` (last accepted TOTP step, replay guard). Identity already holds `TwoFactorEnabled`, lockout fields, and the authenticator key (`AspNetUserTokens`).
- **TwoFactorRecoveryCode**: `Id`, `UserId` (FK to AspNetUsers, cascade delete, indexed), `CodeHash` (max 256), `CreatedAt`, `UsedAt?` (concurrency token).
- **SecurityEvent**: `Id`, `UserId` (max 450, no FK so events outlive users), `Kind` (string enum: TwoFactorEnabled, TwoFactorDisabled, RecoveryCodeUsed, RecoveryCodesRegenerated), `OccurredAt`. Append-only (modify or delete throws in `SaveChangesAsync`). Index (UserId, OccurredAt).

State: Off (no key or key without flag) -> Pending (key set, flag false) -> On (flag true, 10 codes) -> Off (key and codes removed).
