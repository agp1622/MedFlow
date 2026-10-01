# API Contract: Patient Portal

All routes under `/api`. Enums serialize as strings. Errors use `{ "error": "..." }` or `{ "errors": [...] }` like existing controllers.

Auth legend: **Anon** = no token; **Doctor** / **Patient** = JWT with that role. Any other role/no role → `403`; no token → `401`.

## Changes to existing behaviour

- All existing controllers (`Patients`, `Appointments`, `Prescriptions`, `Invoices`, `VitalSigns`, `MedicalNotes`, `Attachments`, `Dashboard`) require role **Doctor**. A Patient token gets `403`.
- `AuthResponse.User` / `UserDto` gains `role: "Doctor" | "Patient"`.
- `POST /auth/register` and new Google users receive the `Doctor` role.
- `POST /auth/google-login` for a user in the `Patient` role → `401 { "error": "Invalid Google token." }` (no signal).

## Doctor – invitations

### `POST /patients/{patientId}/portal-invitation`  (Doctor)
Sends (or re-sends) a portal invitation to the patient's email on file. Supersedes earlier pending invitations.

| Status | Body | When |
|---|---|---|
| `200` | `{ "message": "Invitation sent.", "expiresAt": "..." }` | Sent |
| `400` | `{ "errors": ["Patient has no email on file."] }` | Missing/invalid email |
| `400` | `{ "errors": ["Patient already has portal access."] }` | Already linked |
| `404` | | Patient not owned by this doctor |

### `DELETE /patients/{patientId}/portal-access`  (Doctor)
Revokes portal access (unlinks account, invalidates pending invitations). `204` / `404`.

## Doctor – sharing

### `PUT /attachments/{id}/sharing`  (Doctor)
### `PUT /medicalnotes/{id}/sharing`  (Doctor)
Body: `{ "shared": true }` → `200` with the updated DTO (`sharedWithPatient` included). `404` if not owned.

Existing list DTOs (`PatientAttachmentDto`, `MedicalNoteDto`) gain `sharedWithPatient: boolean`; `PatientSummaryDto`/patient detail gains `portalStatus`.

## Public – accept invitation

### `POST /auth/accept-invitation`  (Anon, rate-limited)
```json
{ "token": "…", "email": "pat@example.com", "password": "…", "confirmPassword": "…" }
```
| Status | Body | When |
|---|---|---|
| `200` | `AuthResponse` (role = Patient) | Success; user signed in |
| `400` | `{ "errors": ["Passwords do not match."] }` / password policy errors | Validation |
| `400` | `{ "error": "This invitation is invalid or has expired." }` | Unknown, expired, used, superseded, email changed, or patient inactive — all identical |
| `429` | `{ "error": "Too many requests. Please try again later." }` | Rate limit |

## Patient portal  (all require role Patient; patient resolved from the token only)

| Method & route | Returns |
|---|---|
| `GET /portal/me` | `PortalProfileDto { firstName, lastName, doctorName }` |
| `GET /portal/appointments` | `PortalAppointmentDto[]` – upcoming (not cancelled), soonest first |
| `GET /portal/prescriptions` | `PortalPrescriptionDto[]` |
| `GET /portal/invoices` | `PortalInvoiceDto[]` |
| `GET /portal/attachments` | `PortalAttachmentDto[]` – shared only |
| `GET /portal/attachments/{id}/download` | File stream; `404` if not found **or** not shared **or** not theirs (identical) |
| `GET /portal/notes` | `PortalNoteDto[]` – shared only |

If the linked patient is not `Active`: every route returns `403 { "error": "Portal access is unavailable." }`.

### DTO shapes (records in `MedFlow.Core.DTOs`)

```text
PortalAppointmentDto  { id, scheduledAt, durationMinutes, type, status, reason?, location? }
PortalPrescriptionDto { id, drugName, dosage, frequency, instructions?, issuedDate, expiryDate, refillsRemaining, status }
PortalInvoiceDto      { id, invoiceNumber, serviceDescription, amount, paidAmount?, status, invoiceDate, dueDate?, paidDate? }
PortalAttachmentDto   { id, fileName, contentType, fileSize, category?, description?, createdAt }
PortalNoteDto         { id, doctorName, visitType?, content, noteDate }
```
Deliberately excluded: appointment/invoice internal `notes`, `doctorId`, `storedFileName`.

## Client service additions (`medflow-client/src/api/services.ts`)

`portalApi` (me, appointments, prescriptions, invoices, attachments, notes, `downloadAttachment` as blob), `authApi.acceptInvitation`, `patientsApi.invite` / `.revokePortalAccess`, `attachmentsApi.setSharing`, `notesApi.setSharing`.
