# Feature Specification: Password Recovery Page and Functionality

**Feature Branch**: `001-password-recovery`

**Created**: 2026-09-19

**Status**: Draft

**Input**: User description: "Story: Password Recovery Page and Functionality"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Request a password reset (Priority: P1)

A doctor who has forgotten their MedFlow password goes to the login page, selects "Forgot password?", enters the email address associated with their account, and submits the request. Whether or not that email matches an account, they see the same confirmation message telling them to check their inbox. If the email does match an account with a local password, they receive an email with a link to reset it.

**Why this priority**: Without this, a locked-out doctor has no self-service way back into the system and must rely on manual/administrative intervention. This is the entry point for the whole recovery flow and delivers value on its own (even before the reset step, ops can confirm delivery works).

**Independent Test**: Submit the forgot-password form with a known account's email and verify a reset email arrives; submit it with an unregistered email and verify the same on-screen confirmation appears with no email sent.

**Acceptance Scenarios**:

1. **Given** a doctor is on the login page, **When** they select "Forgot password?", **Then** they are taken to a page asking only for their email address.
2. **Given** a doctor submits their registered email on the forgot-password page, **When** the request is processed, **Then** they see a confirmation message and an email containing a password reset link arrives at that address within a few minutes.
3. **Given** someone submits an email address that is not registered, **When** the request is processed, **Then** they see the identical confirmation message as a registered user, and no email is sent.
4. **Given** a doctor submits the forgot-password form repeatedly in a short period, **When** the requests exceed a reasonable threshold, **Then** further requests are throttled without revealing whether the email is registered.

---

### User Story 2 - Set a new password from the reset link (Priority: P1)

A doctor who requested a password reset opens the emailed link, arrives at a "Reset password" page, enters and confirms a new password meeting the account's password rules, and submits it. On success they are told their password was changed and can log in with the new password immediately. Their old password no longer works.

**Why this priority**: This is the step that actually restores account access; the request step alone delivers no value without it. Equal priority to Story 1 because the feature is incomplete without both.

**Independent Test**: Using a valid reset link, submit a new password meeting the policy and confirm login succeeds with the new password and fails with the old one.

**Acceptance Scenarios**:

1. **Given** a doctor opens a valid, unexpired reset link, **When** the reset page loads, **Then** they see fields to enter and confirm a new password.
2. **Given** a doctor enters a new password and matching confirmation that meet the account password policy, **When** they submit, **Then** the password is changed, they see a success message, and they can log in with the new password.
3. **Given** a doctor enters a new password that does not meet the password policy, **When** they submit, **Then** they see a clear validation error and the password is not changed.
4. **Given** a doctor enters a new password and a confirmation that do not match, **When** they submit, **Then** they see a validation error before any request is sent.
5. **Given** a doctor opens an expired or already-used reset link, **When** the reset page loads or they submit, **Then** they see a message explaining the link is no longer valid and are offered a way to request a new one.
6. **Given** a doctor successfully resets their password, **When** they attempt to log in with the old password afterward, **Then** the login is rejected.

---

### User Story 3 - Google Sign-In accounts are guided correctly (Priority: P2)

A doctor whose account was created via "Sign in with Google" (and therefore has no local password) requests a password reset using that account's email. Instead of receiving a password reset link that would not make sense for their account type, they are told (on-screen and/or by email) to use "Sign in with Google" instead.

**Why this priority**: Prevents a confusing dead end for a meaningful subset of users (Google Sign-In is already a supported login method), but the core recovery flow (Stories 1-2) can ship and deliver value without this refinement, since the generic confirmation message in Story 1 already avoids an error state.

**Independent Test**: Submit the forgot-password form using a Google Sign-In-only account's email and verify no password-reset link is issued, while the account owner still receives clear guidance to sign in with Google.

**Acceptance Scenarios**:

1. **Given** an account was created via Google Sign-In and has no local password, **When** its email is submitted on the forgot-password page, **Then** the on-screen confirmation is the same generic message as any other submission, and no password-reset link is generated for that account.
2. **Given** an account was created via Google Sign-In, **When** its owner submits the forgot-password form, **Then** they receive an email directing them to sign in with Google rather than a password reset link.

---

### Edge Cases

- What happens when a doctor requests a password reset for the same email multiple times before using the first link? (Prior unused reset links become invalid once a newer one is issued or once one is successfully used.)
- What happens when a doctor's account is locked out (e.g., due to repeated failed logins) and they request a password reset? (The reset flow still succeeds; completing a reset also clears any existing lockout.)
- What happens if the reset link is opened on a different device/browser than the one the request was made from? (It still works; the link itself is the credential, not the session.)
- What happens if the reset email bounces or the doctor's inbox provider delays delivery? (No error is shown to the requester beyond the generic confirmation; support/admin escalation is out of scope for this feature.)
- What happens when someone tampers with the reset link (altered token or email parameter)? (The system treats it as invalid/expired and rejects it with the same generic invalid-link message.)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The login page MUST provide a visible "Forgot password?" entry point that leads to a dedicated password recovery page.
- **FR-002**: The password recovery page MUST accept an email address and let the user submit a reset request.
- **FR-003**: The system MUST show the same confirmation message after a forgot-password submission regardless of whether the submitted email belongs to a registered account, so that account existence cannot be inferred from the response.
- **FR-004**: When the submitted email belongs to an account that has a local password, the system MUST send that address an email containing a unique, single-use password reset link.
- **FR-005**: When the submitted email belongs to an account created via Google Sign-In with no local password, the system MUST NOT issue a password reset link for that account, and MUST instead inform the account owner (by email) that they should sign in with Google.
- **FR-006**: When the submitted email does not belong to any account, the system MUST NOT send any email.
- **FR-007**: Each password reset link MUST expire after a limited time window and MUST become unusable after a single successful use.
- **FR-008**: Requesting a new password reset for an account MUST invalidate any previously issued, unused reset link for that same account.
- **FR-009**: The reset password page MUST require entry of a new password and a matching confirmation before allowing submission, and MUST validate the new password against the account's existing password policy (minimum length and complexity rules used elsewhere in the product).
- **FR-010**: On a valid, unexpired reset link, submitting a new password that meets the policy MUST update the account's password and MUST invalidate the account's previous password immediately.
- **FR-011**: On an invalid, expired, or already-used reset link, the system MUST reject the reset attempt and show the user a clear, non-technical explanation with an option to request a new reset link.
- **FR-012**: The system MUST rate-limit password reset requests per email address and/or per requester to prevent abuse (e.g., spamming an inbox or probing for registered emails).
- **FR-013**: Successfully resetting a password MUST clear any existing account lockout state for that account.
- **FR-014**: All password reset emails MUST be sent from the product's own sender identity and MUST clearly identify the product so recipients can distinguish legitimate reset emails from phishing attempts.
- **FR-015**: The system MUST log password reset requests and completions (without logging the plaintext password or the reset token itself) for security auditing.

### Key Entities

- **Password Reset Request**: Represents a single attempt to recover access to an account — associated account/email, the time it was issued, its expiration time, and whether it has been consumed. Not user-facing data, but governs what the reset link is allowed to do.
- **Account (existing)**: The doctor's MedFlow login identity; relevant attributes here are the email address and whether the account has a local password (as opposed to Google Sign-In only), since that determines which recovery path applies.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A doctor who has forgotten their password can regain access to their account without any manual/administrative help, from request to successful login, in under 5 minutes under normal email delivery conditions.
- **SC-002**: 100% of forgot-password submissions, whether or not the email is registered, receive an identical on-screen confirmation, with no observable difference in response content or timing that would reveal account existence.
- **SC-003**: 100% of password reset links become unusable after either their expiration window elapses or a single successful use, verified by attempting reuse.
- **SC-004**: At least 95% of password reset emails for valid, registered, non-Google accounts arrive within 5 minutes of the request under normal conditions.
- **SC-005**: 0% of Google Sign-In-only accounts receive a password-reset link; all such requests instead result in guidance to use Google Sign-In.
- **SC-006**: After a successful password reset, 100% of subsequent login attempts using the old password are rejected.

## Assumptions

- The product already has a working account/password system (registration, login, password policy) and Google Sign-In as an alternate login method; this feature extends that existing system rather than replacing it.
- Password reset links expire on a short, fixed window (industry-standard default: 24 hours or less) unless a shorter window is later specified; this is treated as a configurable security parameter, not a user-facing decision.
- Outbound recovery emails are sent via the product's existing/designated email-sending capability; the specific email service provider is an implementation detail outside the scope of this specification.
- Rate limiting thresholds (e.g., max requests per email per hour) follow standard anti-abuse defaults and can be tuned operationally without changing this specification.
- Invalidating a user's active login sessions on password reset is treated as standard security best practice and is in scope as part of "the account's previous password is invalidated immediately," though the mechanism is an implementation detail.
- Mobile app support is out of scope; this feature covers the web application only, consistent with the rest of the current product.
