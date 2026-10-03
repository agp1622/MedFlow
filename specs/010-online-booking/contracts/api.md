# API Contracts

Doctor (role Doctor; scoped to caller):
- `GET /api/availability` -> `{ windows: AvailabilityWindowDto[], blockedDates: BlockedDateDto[] }`
- `PUT /api/availability/weekly` body `{ windows: [{ dayOfWeek, startTime "HH:mm", endTime "HH:mm" }] }` -> 200 `AvailabilityWindowDto[]`; 400 on validation errors
- `POST /api/availability/blocked-dates` body `{ date "yyyy-MM-dd", label? }` -> 201 `BlockedDateDto`; 400 invalid/duplicate
- `DELETE /api/availability/blocked-dates/{id}` -> 204 (404 if not own)

Portal (role Patient; patient from token; 403 `{error}` like other portal routes if unavailable):
- `GET /api/portal/booking/slots?from=yyyy-MM-dd&to=yyyy-MM-dd` -> `BookingSlotDto[]` (`{ startsAt, durationMinutes }`); 400 if range invalid or > 31 days
- `POST /api/portal/booking` body `{ startsAt, reason? }` -> 201 `PortalAppointmentDto`; 400 not an open slot / reason too long; 409 slot taken or patient limit/overlap
