# API Contracts

Doctor (role Doctor; scoped to caller; other doctors' patients/entries -> 404):
- `GET /api/waitlist?page&pageSize` -> `PagedResult<WaitlistEntryDto>` Waiting entries, oldest first. `WaitlistEntryDto { id, patientId, patientName, joinedAt }`
- `POST /api/waitlist` body `{ patientId }` -> 201 `WaitlistEntryDto`; 404 patient not own; 400 patient not Active; 409 already waiting
- `DELETE /api/waitlist/{id}` -> 204; 404 not own / not waiting

Portal (role Patient; patient from token; 403 `{error}` when unavailable):
- `GET /api/portal/waitlist` -> `{ onWaitlist, joinedAt? }`
- `POST /api/portal/waitlist` -> 201 same shape; 409 already waiting
- `DELETE /api/portal/waitlist` -> 204; 404 if not waiting

Public (anonymous, rate limited policy `waitlist-offer`, token in body):
- `POST /api/waitlist-offer/lookup` `{ token }` -> `{ slotStartsAt, durationMinutes, doctorName, expiresAt }`; 404 invalid
- `POST /api/waitlist-offer/claim` `{ token }` -> 200 `{ slotStartsAt, durationMinutes, doctorName }`; 404 invalid; 409 slot taken / limit / overlap
- `POST /api/waitlist-offer/leave` `{ token }` -> 204; 404 invalid
