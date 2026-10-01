# Quickstart: Validating Patient Portal – My Records

Prerequisites: SQL Server running; API on `http://localhost:5080`, client on `http://localhost:5173`; SMTP configured, or read the invitation link from the API log when email is not configured. Migrations apply on startup in Development.

Contracts: [contracts/portal-api.md](contracts/portal-api.md). Data: [data-model.md](data-model.md).

## 0. Existing doctor still works (FR-011)
1. Sign in as the seeded doctor (email/password and Google). Dashboard, patients, billing load as before.
2. Check `AspNetUserRoles`: the doctor has the `Doctor` role.

## 1. Invite a patient (US4)
1. In the doctor UI open a patient **with** an email → **Invite to portal**. Expect a success toast; patient shows "Invited".
2. Open a patient **without** an email → expect "email required".
3. Open the link from the email: set a password → you land in the portal as that patient.
4. Re-open the same link → generic "invalid or expired" message. Re-invite and open an old link → same message.

## 2. Patient sees own records (US1)
1. Seed the patient with an upcoming appointment, a prescription and an invoice.
2. Portal shows all three; a patient with none shows empty states.
3. No edit/create controls appear.

## 3. Sharing (US3)
1. As doctor, share one attachment and one note; leave another of each unshared.
2. Patient refreshes: only the shared ones appear; the shared attachment downloads.
3. Doctor unshares: the item disappears; hitting its previous download URL returns 404.
4. `PortalAccessLog` has a row per download/view.

## 4. Access control (US2, SC-002)
With the patient's token (copy from browser storage), call and expect `403`:
- `GET /api/patients`, `POST /api/patients`, `GET /api/dashboard`, `GET /api/invoices`
With a *second* patient's token: `GET /api/portal/attachments/{id of patient A's shared file}/download` → `404`.
Patient UI: visiting `/patients` or `/billing` redirects to the portal.
Google sign-in with a patient's email → generic failure.

## 5. Automated
`dotnet test MedFlow.Api.Tests` – access-control suite (patient vs doctor endpoints, cross-patient ids, unshared/revoked items, invitation expiry and reuse).
