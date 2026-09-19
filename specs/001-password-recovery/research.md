# Phase 0 Research: Password Recovery Page and Functionality

## 1. Token issuance, expiry, and single-use enforcement

**Decision**: Use ASP.NET Core Identity's built-in `UserManager.GeneratePasswordResetTokenAsync` /
`UserManager.ResetPasswordAsync`, backed by the default `DataProtectorTokenProvider` already
registered via `AddDefaultTokenProviders()` in `DependencyInjection.cs`. Do not add a new
database table to track reset tokens.

**Rationale**: The default token provider derives each token from the user's current
`SecurityStamp` plus a time window (`DataProtectionTokenProviderOptions.TokenLifespan`, default
1 day — already within the spec's "24 hours or less" assumption). `ResetPasswordAsync` changes
the password, which updates the `SecurityStamp` as part of `UpdatePasswordHash`. Because every
outstanding token for that user was derived from the *previous* stamp, this single stamp change:
- invalidates the token that was just used (single-use — satisfies FR-007), and
- invalidates any other previously issued, unused token for the same account (satisfies FR-008),
without any custom persistence, cleanup job, or new table. This matches Constitution Principle V
(Simplicity — prefer existing patterns) and Principle II (Layered Architecture — stays inside the
Identity abstraction already used for login/register).

**Alternatives considered**:
- *Custom `PasswordResetToken` table with explicit expiry/used columns*: rejected — duplicates
  what Identity's token provider already guarantees, adds a migration and cleanup concern for no
  additional behavior the spec requires.
- *Shortening `TokenLifespan` to e.g. 1 hour*: left as a configuration knob (`AddIdentity` options
  in `DependencyInjection.cs`), not a code-structural decision; default (1 day) satisfies the spec
  as written. Documented here so it can be tuned later without a design change.

## 2. Email delivery (Google SMTP)

**Decision**: Send the reset email (and the Google-Sign-In-guidance email) through SMTP against
Gmail (`smtp.gmail.com:587`, STARTTLS) using an `IEmailSender` abstraction defined in
`MedFlow.Core.Interfaces` and implemented in `MedFlow.Infrastructure` using the .NET built-in
`System.Net.Mail.SmtpClient`. Credentials (sender address + Gmail App Password) are read from
configuration (`Email:SenderEmail`, `Email:AppPassword`, etc.), never hard-coded, per Constitution
Principle IV.

**Rationale**: Google SMTP was explicitly chosen by the user over other providers. `SmtpClient` is
part of the framework already referenced (`Microsoft.AspNetCore.App` in `MedFlow.Infrastructure`),
so it adds no new package dependency — consistent with Principle V. Gmail SMTP requires an
App Password (2FA-enabled account) rather than the account's normal password; this is an
operational/config concern documented in `quickstart.md`, not a code concern.

**Alternatives considered**:
- *MailKit*: more actively maintained SMTP client with better async support, but adds a new
  NuGet dependency for a low-volume transactional-email use case that `SmtpClient` already covers
  adequately. Not chosen to keep the dependency surface minimal; can be revisited later behind the
  same `IEmailSender` interface without touching callers.
- *Third-party transactional email API (SendGrid, etc.)*: explicitly not chosen — user specified
  Google SMTP.

## 3. Preventing account-existence disclosure (timing/response)

**Decision**: The `forgot-password` endpoint always returns the same `200 OK` with the same body,
and always performs a comparable amount of work on the request path — look up the user, and only
conditionally send an email — before returning, rather than short-circuiting early for unknown
emails. Email sending itself is fire-and-forget from the perspective of the response (awaited, but
the response shape doesn't change based on outcome).

**Rationale**: Satisfies FR-003/FR-006 and SC-002. This is standard practice for forgot-password
endpoints (OWASP guidance on user enumeration).

**Alternatives considered**:
- *Distinguishable error for unknown email*: rejected — directly contradicts FR-003/FR-006 and
  the spec's explicit non-enumeration requirement.

## 4. Rate limiting

**Decision**: Use ASP.NET Core's built-in rate limiting middleware (`Microsoft.AspNetCore.RateLimiting`,
part of the .NET 8 shared framework — no new package) with a fixed-window limiter applied to the
`forgot-password` endpoint, keyed by client IP (and optionally the submitted email, hashed, as a
secondary partition key).

**Rationale**: Satisfies FR-012 without a new dependency or persistent store, consistent with
Principle V. In-memory limiting is acceptable for this app's current single-instance deployment
profile (see `docker-compose.yml`); revisit with a distributed store only if the app is scaled
horizontally.

**Alternatives considered**:
- *Third-party rate-limiting library / Redis-backed limiter*: rejected as over-scoped for current
  deployment (single API instance); would introduce new infrastructure not otherwise needed by
  this feature.

## 5. Google-only accounts

**Decision**: Detect a "Google Sign-In only" account by checking whether the user has a usable
password via `UserManager.HasPasswordAsync(user)` (Identity API already available). If `false`,
skip `GeneratePasswordResetTokenAsync` entirely and send the "use Google Sign-In" guidance email
instead.

**Rationale**: `AuthController.GoogleLogin` already creates users via `CreateAsync(user)` with no
password argument, so `HasPasswordAsync` reliably returns `false` for those accounts — no new
field needed on `ApplicationUser`. Satisfies FR-005 and SC-005.

**Alternatives considered**:
- *New `IsGoogleAccount` flag on `ApplicationUser`*: rejected — redundant with the already-reliable
  `HasPasswordAsync` check; would need a migration for information Identity already exposes.

## 6. Testing approach

**Decision**: No automated test project currently exists in the repository (backend or frontend).
This feature's primary verification is the manual `quickstart.md` walkthrough plus the acceptance
scenarios already defined in `spec.md`. Introducing a test framework (xUnit for backend,
Vitest/RTL for frontend) is out of scope for this story per Constitution Principle V (matching
story scope, not speculative infrastructure); it should be proposed as its own story if desired.

**Rationale**: Avoids scope creep beyond what the spec asks for, while still leaving the door open
for the endpoints/components to be covered later since they're built as ordinary, testable
services (interfaces, DTOs) rather than static/hard-to-isolate code.
