# Quickstart: Validating Password Recovery

## Prerequisites

- Local dev stack running per the repo's existing setup (`docker-compose.yml` for SQL Server,
  `dotnet run` for `MedFlow.Api`, `npm run dev` in `medflow-client`).
- A Gmail account with 2-Step Verification enabled and an **App Password** generated for it
  (Google Account → Security → App passwords). This is required because Gmail SMTP rejects the
  account's normal password for third-party SMTP clients.
- Configuration (e.g. `MedFlow.Api/appsettings.Development.json`, or environment variables —
  never commit real credentials) populated under an `Email` section:
  ```json
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SenderEmail": "your-sending-account@gmail.com",
    "SenderName": "MedFlow",
    "AppPassword": "<16-character Gmail App Password>"
  }
  ```
- A frontend base URL configured (reuse existing `AllowedOrigins[0]`, e.g.
  `http://localhost:5173`) so emailed reset links point at the running frontend.
- At least one seeded account with a local password (e.g. the existing `SeedUser` in
  `appsettings.json`) and, to validate User Story 3, one account created via Google Sign-In.

## Scenario 1 — Request a reset (User Story 1)

1. Open `http://localhost:5173/login`, click "Forgot password?".
2. Enter the seeded account's email and submit.
3. **Expect**: on-screen generic confirmation message; an email arrives at that inbox within a
   few minutes containing a link to `http://localhost:5173/reset-password?...`.
4. Repeat with an email that is *not* registered.
5. **Expect**: identical on-screen confirmation message; no email is sent (check the inbox after a
   reasonable wait to confirm absence).
6. Submit the form several times rapidly for the same email.
7. **Expect**: after a few attempts, further submissions are rejected (`429`) rather than queuing
   more emails.

## Scenario 2 — Complete a reset (User Story 2)

1. Open the link from the email received in Scenario 1, step 3.
2. Enter a new password meeting the policy (min 8 chars, at least one uppercase) and a matching
   confirmation; submit.
3. **Expect**: success message; log in at `/login` with the new password succeeds.
4. Attempt to log in with the *old* password.
5. **Expect**: login is rejected.
6. Request a second reset link for the same account, then try to use the *first* (already-used)
   link again.
7. **Expect**: rejected with an "invalid or expired" message, not a silent success.
8. Try submitting the reset form with a new password that doesn't meet policy, and separately with
   mismatched password/confirmation.
9. **Expect**: clear validation errors in both cases; no password change occurs.

## Scenario 3 — Google Sign-In accounts (User Story 3)

1. Submit the forgot-password form using a Google-Sign-In-only account's email.
2. **Expect**: same generic on-screen confirmation as any other submission (no difference from
   Scenario 1).
3. Check that inbox.
4. **Expect**: an email arrives directing the user to "Sign in with Google" — **not** a password
   reset link. Confirm following the email's guidance (clicking "Sign in with Google" on
   `/login`) works as already supported today.

## Notes

- This project currently has no automated test suite (backend or frontend); the scenarios above
  are the primary acceptance verification for this feature, matching `spec.md`'s acceptance
  scenarios and success criteria (SC-001 through SC-006).
- See `contracts/auth-password-recovery.md` for exact request/response shapes if verifying with a
  tool like the existing Swagger UI (`/swagger`) instead of the UI.
