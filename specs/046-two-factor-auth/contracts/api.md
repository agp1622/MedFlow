# API contract

All errors are `{ "error": "<localized>" }`. Codes: 6 digits (spaces tolerated); recovery codes `XXXXX-XXXXX`.

## Login (changed)
- `POST /api/auth/login` and `POST /api/auth/google-login`: unchanged for accounts without 2FA. With 2FA on: `200 { "twoFactorRequired": true, "challengeToken": "...", "expires": "..." }` (no `token`). Wrong password / locked: as before.

## Challenge exchange (anonymous, rate limit policy `two-factor`, 10 per 15 min per IP, 429 beyond)
- `POST /api/auth/2fa/verify` `{ challengeToken, code }` -> `200 AuthResponse`
- `POST /api/auth/2fa/recovery` `{ challengeToken, recoveryCode }` -> `200 AuthResponse`
- Failures: `401 { error: "Invalid code" }` for every cause (bad/expired challenge, wrong, replayed or used code); `401 Account locked` when locked out.

## Account (authenticated staff, patient tokens get 403; secret-bearing responses `Cache-Control: no-store`)
- `GET /api/account/2fa` -> `{ enabled, recoveryCodesRemaining }`
- `POST /api/account/2fa/setup` -> `{ sharedKey, otpAuthUri }`; `409` if already enabled
- `POST /api/account/2fa/enable` `{ code }` -> `{ recoveryCodes: string[10] }`; `400` invalid code or no setup started; `409` if already enabled
- `POST /api/account/2fa/recovery-codes` `{ password?, code }` -> `{ recoveryCodes }`
- `POST /api/account/2fa/disable` `{ password?, code }` -> `204`
- `code` here may be a TOTP code or a recovery code. `password` is required when the account has one. Failure: `400 { error: "Invalid credentials or code" }` uniformly; locked: `423`.
