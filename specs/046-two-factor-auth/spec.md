# Feature Specification: Two-Factor Authentication (TOTP)

**Feature Branch**: `claude/issue-28-two-factor` (spec folder `046-two-factor-auth`)
**Created**: 2026-10-04
**Status**: Draft
**Input**: GitHub issue #28 (Refs #28) - As a user, I want to enable TOTP 2FA so that patient data is protected if my password leaks. Acceptance: enable/disable in settings with QR setup; recovery codes provided; enforced at login when enabled.

## Clarifications

### Session 2026-10-04

- Q: Must a 2FA challenge be single-use (consumed once a session is issued)? -> A: No stored state. The challenge is stateless, expires in 5 minutes, and every exchange needs a fresh valid code (TOTP step replay and recovery single-use are enforced), so a replayed challenge alone grants nothing. (Stricter stored one-time challenges were rejected: extra table for no added protection.)
- Q: Does using a recovery code as the "current code" for disable or regenerate consume it? -> A: Yes, it is spent exactly as at login and audited as "recovery code used".
- Q: Should audit events store the client IP or device? -> A: No. Only user, kind and time (least data; IP is a personal data field and no patient context exists).
- Q: Is Google sign-in gated even though Google may have its own 2FA? -> A: Yes, gated; MedFlow cannot know Google's assurance level, so a user who enabled MedFlow 2FA is never bypassed.
- Q: How many recovery codes remain visible after enabling? -> A: Never the codes themselves; the Security page shows only the remaining count, and warns when 3 or fewer remain.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Enable 2FA from security settings (Priority: P1)

A staff member (any clinic role) opens a Security settings page, starts setup, scans a QR code with an authenticator app (or types the shown key), and confirms with a current 6-digit code. Only after a valid code does 2FA become active. On activation they receive 10 one-time recovery codes, shown once, with copy and download.

**Why this priority**: Without enrollment nothing else exists.

**Independent Test**: Start setup, compute the current code from the returned key, confirm; status shows enabled and 10 codes were returned.

**Acceptance Scenarios**:

1. **Given** a signed-in staff member with 2FA off, **When** they start setup, **Then** they receive a one-time setup key and an otpauth URI, the QR is drawn in the browser, and 2FA is still off.
2. **Given** a started setup, **When** they submit a wrong or malformed code, **Then** 2FA stays off and a generic error is returned.
3. **Given** a started setup, **When** they submit a valid current code, **Then** 2FA turns on, 10 recovery codes are returned once, and an audit event "enabled" is recorded.
4. **Given** 2FA already on, **When** setup or enable is requested again, **Then** it is refused (the existing secret is never replaced or revealed).
5. **Given** a patient (portal) token, **When** any 2FA management endpoint is called, **Then** access is denied.

---

### User Story 2 - Login is gated by the second factor (Priority: P1)

When 2FA is on, a correct password does not sign the user in. The API returns a short-lived challenge instead of a session; the session is issued only after a valid authenticator code or an unused recovery code. Google sign-in is gated the same way.

**Why this priority**: It is the protection the issue asks for.

**Independent Test**: Login with password returns a challenge and no session; the challenge cannot call any API; a valid code exchanges it for a session.

**Acceptance Scenarios**:

1. **Given** a user with 2FA on, **When** they log in with the right password, **Then** the response contains a challenge (and no session token) and the client shows a code step.
2. **Given** a challenge, **When** a valid current code is submitted, **Then** the normal session response is returned.
3. **Given** a challenge, **When** an unused recovery code is submitted to the recovery endpoint, **Then** a session is returned and that code can never be used again; an audit event "recovery code used" is recorded.
4. **Given** a challenge token, **When** it is used as a bearer token on any API, **Then** it is rejected; **Given** a normal session token, **When** it is submitted as a challenge, **Then** it is rejected.
5. **Given** an expired, tampered or unknown challenge, a wrong code, a reused code, or a used recovery code, **When** submitted, **Then** the same generic invalid error is returned.
6. **Given** a user with 2FA on who signs in with Google, **When** the Google token is valid, **Then** the same challenge is returned instead of a session.
7. **Given** a user with 2FA off, **When** they log in (password or Google), **Then** behavior is unchanged.
8. **Given** a patient portal account, **When** they log in, **Then** behavior is unchanged (portal accounts cannot enable 2FA).

---

### User Story 3 - Brute-force and replay protection (Priority: P1)

Guessing codes is throttled and a code cannot be replayed.

**Acceptance Scenarios**:

1. **Given** repeated wrong codes on a challenge, **When** the account failure limit is reached, **Then** the account is locked out under the central lockout policy and further attempts (even with a right code) are refused until it expires.
2. **Given** a 2FA account with failed code attempts, **When** the attacker logs in with the correct password, **Then** the failure counter is not reset by that password success.
3. **Given** a code accepted once, **When** the same code (same 30-second step) is submitted again, **Then** it is rejected.
4. **Given** more than the allowed requests per window from one address on the verify/recovery endpoints, **When** another arrives, **Then** it is answered 429.
5. **Given** a recovery code, **When** two requests use it concurrently, **Then** at most one succeeds.

---

### User Story 4 - Manage recovery codes and disable (Priority: P2)

A user with 2FA on can regenerate recovery codes (old ones stop working) and can disable 2FA. Both require the current password (if the account has one) plus a current authenticator or recovery code.

**Acceptance Scenarios**:

1. **Given** 2FA on, **When** the user supplies password and a valid code to regenerate, **Then** 10 new codes are returned once, all old codes stop working, and an audit event is recorded.
2. **Given** 2FA on, **When** the user supplies password and a valid code to disable, **Then** 2FA is off, the secret and recovery codes are deleted, and an audit event "disabled" is recorded.
3. **Given** a wrong password or a wrong code, **When** disabling or regenerating, **Then** the same generic error is returned, nothing changes, and the failure counts toward lockout.
4. **Given** a Google-only account (no password), **When** disabling, **Then** only the current code is required.

---

### User Story 5 - Client experience (Priority: P2)

Spanish-primary and English UI: a Security page (status, QR setup, code confirmation, recovery codes with copy/download, regenerate, disable) and a login code step with a "use a recovery code" alternative, for password and Google login.

**Acceptance Scenarios**:

1. **Given** a staff user, **When** they open the app, **Then** a Security entry is reachable from navigation for every staff role.
2. **Given** a challenge response at login, **When** the user enters a code, **Then** they are signed in; a link switches to recovery-code entry.
3. **Given** newly generated recovery codes, **When** shown, **Then** the user can copy and download them and is warned they will not be shown again.

### Edge Cases

- Existing JWTs issued before 2FA was enabled stay valid until expiry (sessions are not revoked).
- Password reset does not disable 2FA; the next login still requires the second factor.
- Invitation-accept flows create new accounts, so they never involve 2FA.
- Clock drift: a code from the adjacent 30-second step is accepted; further away is not.
- Recovery codes are case-insensitive and tolerate dashes and spaces.
- A locked-out account returns the existing "account locked" message, as password login already does.
- All 10 recovery codes used: login still works with TOTP; the status shows 0 remaining.
- Setup started but never confirmed: the pending secret grants nothing; starting setup again replaces it.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Staff users of any clinic role MUST be able to start 2FA setup and receive a fresh authenticator secret and an otpauth URI; the response MUST be `Cache-Control: no-store`. The QR MUST be rendered in the browser; the secret MUST NOT be sent to any third-party service.
- **FR-002**: 2FA MUST NOT become active until a valid current code is submitted; the code used to confirm MUST NOT be reusable.
- **FR-003**: Enabling MUST return exactly 10 recovery codes once (no-store); the system MUST store only salted hashes, each single-use; regeneration MUST invalidate all previous codes.
- **FR-004**: When 2FA is on, password login MUST return a challenge instead of a session, after the password (and lockout state) are checked; Google sign-in MUST do the same.
- **FR-005**: The challenge MUST be a short-lived (5 minutes by default), single-purpose token with a different audience from access tokens; it MUST be rejected as a bearer token, and access tokens MUST be rejected as challenges.
- **FR-006**: A session MUST be issued only after a valid TOTP code (30-second step, 6 digits, plus or minus one step) or an unused recovery code.
- **FR-007**: Code comparison MUST be constant time; a TOTP time step MUST be accepted at most once per user (replay rejected); a recovery code MUST be accepted at most once, including under concurrency.
- **FR-008**: Wrong codes MUST count as failed access attempts under the central Identity lockout policy; a correct password MUST NOT reset the counter of a 2FA account (only a completed second factor does); locked accounts MUST be refused before any code is checked.
- **FR-009**: Verify and recovery endpoints MUST be rate limited per client address.
- **FR-010**: Disabling and regenerating recovery codes MUST require the current password (when the account has one) and a current TOTP or recovery code, evaluated so the error never reveals which part was wrong.
- **FR-011**: Disabling MUST delete the authenticator secret and all recovery codes.
- **FR-012**: Security events (2FA enabled, disabled, recovery code used, recovery codes regenerated) MUST be recorded through the audit service with the acting user and time, not tied to a patient, and MUST NOT contain secrets or codes.
- **FR-013**: Secrets, codes, challenge tokens and OTP URIs MUST NEVER be logged. Responses MUST contain no patient data.
- **FR-014**: Patient (portal) accounts MUST be refused on all 2FA management endpoints; patient login behavior is unchanged.
- **FR-015**: Failure responses MUST use uniform messages (Spanish primary, English), identical for expired challenge, wrong code, replayed code and used recovery code.
- **FR-016**: The client MUST provide the Security page, the login code step (with recovery alternative, for password and Google login), recovery code copy and download, regenerate and disable flows, in Spanish and English, through typed methods in the API service layer.

### Key Entities

- **Authenticator secret**: per-user TOTP key kept by the identity system; exists while setup is pending or 2FA is on.
- **Recovery code**: user, salted hash, created time, used time.
- **Security event**: user, kind, time (no patient).
- **Challenge**: short-lived signed token naming the user; not stored.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can enable 2FA, including saving recovery codes, in under 3 minutes.
- **SC-002**: With 2FA on, 0 sign-in paths (password, Google) return a session without a valid second factor, verified by automated tests.
- **SC-003**: A replayed code, a reused recovery code and a challenge used as a session are each rejected in 100% of tested cases.
- **SC-004**: After the configured number of failures the account is locked and a correct code is refused during lockout.
- **SC-005**: All pre-existing automated tests stay green except any whose premise changed, each listed explicitly.

## Assumptions

- Staff only in this release; no SMS/email codes, WebAuthn, remembered devices, admin-enforced 2FA, admin reset of another user's 2FA, or UI to browse security events.
- The authenticator secret is stored by the identity system's standard token store (no extra application-level encryption); operators are expected to use database encryption at rest.
- Existing sessions are not revoked on enabling.
- Account lockout thresholds remain those centrally defined for Identity.
- A user who loses both device and recovery codes needs operator intervention (out of scope).
