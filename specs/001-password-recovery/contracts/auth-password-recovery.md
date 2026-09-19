# API Contract: Password Recovery Endpoints

Base route: `/api/auth` (existing `AuthController`, `[ApiController] [Route("api/[controller]")]`).
Both endpoints are anonymous (no JWT required), matching `login`/`register`/`google-login`.

---

## `POST /api/auth/forgot-password`

Initiates a password reset for the given email, if eligible. Always responds the same way
regardless of whether the email is registered, per FR-003/FR-006 and SC-002.

**Request body** (`ForgotPasswordRequest`):

```json
{ "email": "doctor@clinic.com" }
```

**Responses**:

| Status | Body | When |
|---|---|---|
| `200 OK` | `{ "message": "If that email is registered, a password reset link has been sent." }` | Always, for any syntactically valid email — whether or not the account exists, and whether or not it's a Google-only account (FR-003, FR-005, FR-006) |
| `400 Bad Request` | `{ "errors": [...] }` | `email` missing or not a valid email format (client input error, not an enumeration signal) |
| `429 Too Many Requests` | `{ "error": "Too many requests. Please try again later." }` | Rate limit exceeded for this IP/email (FR-012) |

**Side effects** (not observable in the response, per non-enumeration requirement):
- Registered account with a local password → `UserManager.GeneratePasswordResetTokenAsync`, then
  send a reset-link email via `IEmailSender` (FR-004).
- Registered account, Google Sign-In only (`HasPasswordAsync` is `false`) → no token generated;
  send a "sign in with Google instead" email via `IEmailSender` (FR-005).
- Unregistered email → no token, no email (FR-006).
- Log the request outcome (email hash/account id if found, no PII beyond what's already logged
  elsewhere, no token value) per FR-015.

---

## `POST /api/auth/reset-password`

Completes a password reset using a token issued by `forgot-password`.

**Request body** (`ResetPasswordRequest`):

```json
{
  "email": "doctor@clinic.com",
  "token": "<url-decoded token from the emailed link>",
  "newPassword": "NewPassw0rd",
  "confirmPassword": "NewPassw0rd"
}
```

**Responses**:

| Status | Body | When |
|---|---|---|
| `200 OK` | `{ "message": "Your password has been reset. You can now sign in." }` | `UserManager.ResetPasswordAsync` succeeds (FR-010) — also clears lockout state (FR-013) |
| `400 Bad Request` | `{ "errors": [...] }` | `newPassword`/`confirmPassword` mismatch, or password fails policy (`IdentityResult.Errors`) (FR-009) |
| `400 Bad Request` | `{ "error": "This reset link is invalid or has expired. Please request a new one." }` | Unknown email, invalid token, or expired token (`ResetPasswordAsync` fails with a token error) (FR-011) |

Note: unlike `forgot-password`, this endpoint's failure responses do not need to be
enumeration-safe in the same way — the user already possesses a token proving they received the
email, so distinguishing "bad token" from "bad password" is useful feedback and doesn't leak new
information about account existence beyond what the reset-link email already implied.

---

## Frontend service additions (`medflow-client/src/api/services.ts`, `authApi`)

```ts
forgotPassword: (data: ForgotPasswordRequest) => api.post<{ message: string }>('/auth/forgot-password', data).then(r => r.data),
resetPassword:  (data: ResetPasswordRequest)  => api.post<{ message: string }>('/auth/reset-password', data).then(r => r.data),
```

with matching `ForgotPasswordRequest` / `ResetPasswordRequest` TypeScript types added alongside
the existing `LoginRequest`/`RegisterRequest` in `medflow-client/src/types`.

## Routing (frontend)

Two new public routes in `App.tsx`, alongside `/login` and `/register`:

- `/forgot-password` → `ForgotPasswordPage`
- `/reset-password` → `ResetPasswordPage` (reads `token` and `email` from `useSearchParams`)

Both wrapped in the existing `PublicRoute` guard (redirect away if already authenticated).
