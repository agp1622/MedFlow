# Contracts

Public (anonymous, rate limited), body `{ token }`:
- `POST /api/appointment-response/lookup` -> 200 `{ appointmentAt, durationMinutes, doctorName, location, status, canRespond }`; invalid/expired/unknown -> 404 `{ error: "This link is not valid." }`.
- `POST /api/appointment-response/respond` body `{ token, action: "Confirm"|"Cancel" }` -> 200 same shape as lookup with the new status; closed appointment with valid token -> 409 `{ error: "This appointment can no longer be changed." }`; invalid token -> 404 same as above.

Doctor (role Doctor, own appointments only, else 404):
- `GET /api/appointments/{id}/reminders` -> `{ response: "None|Confirmed|Cancelled", respondedAt, deliveries: [{ attemptedAt, channel, outcome, reason }] }`.

Existing endpoints unchanged.
