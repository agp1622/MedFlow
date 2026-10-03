# Intake API Contract

## Doctor (role Doctor, scoped by DoctorId)
- `POST /api/patients/{patientId}/intake-link` -> 200 `{ message, expiresAt }`; 400 no valid email / inactive patient; 404 not owner; 502 email failure. Supersedes earlier links.
- `GET /api/intake-submissions?status=Pending&page=1&pageSize=20` -> `PagedResult<IntakeSubmissionSummaryDto>` (id, patientId, patientName, status, submittedAt).
- `GET /api/intake-submissions/{id}` -> `IntakeSubmissionDetailDto` (answers, consent, current record values, decision); 404 if not owner.
- `POST /api/intake-submissions/{id}/accept` -> 204; 409 if not Pending.
- `POST /api/intake-submissions/{id}/reject` body `{ reason? }` -> 204; 409 if not Pending.

## Public (anonymous, rate limited "intake-public")
- `GET /api/intake/{token}` -> 200 `{ firstName, consentVersion, consentText }`; any invalid token -> 404 (uniform).
- `POST /api/intake/{token}` body `IntakeSubmitRequest` (answers + `consentAgreed`, `signatureName`) -> 201 `{ message }`; 400 validation `{ errors: [...] }`; invalid token -> 404 (uniform).

Patient role tokens are rejected on all doctor routes (403). Enums are strings.
