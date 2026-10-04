# API contract: clinic and staff

All routes below require a staff token with the stated permission (see permission-matrix.md). Enums are strings. Lists are `PagedResult<T>` (`items`, `totalCount`, `page`, `pageSize`).

## Clinic

- `GET /api/clinic` (ClinicRead) -> `ClinicDto { id, name, role, userId }`.
- `PUT /api/clinic` (StaffManage) body `{ name }` (1-200 chars) -> `ClinicDto`.
- `GET /api/clinic/doctors` (ClinicRead) -> `ClinicDoctorDto[] { userId, fullName, role }` active Owners/Doctors that have a doctor profile.

## Staff (all StaffManage)

- `GET /api/staff?page&pageSize` -> `PagedResult<StaffMemberDto { id, userId, email, firstName, lastName, role, isActive, joinedAt }>`.
- `GET /api/staff/invitations` -> `PagedResult<StaffInvitationDto { id, email, role, expiresAt, createdAt }>` (pending only).
- `POST /api/staff/invitations` body `{ email, role }`, role in Doctor|Nurse|Receptionist -> 200 `InvitationResultDto { message, expiresAt }`. Same response when the email already has an account (nothing is created or sent). 400 on invalid email/role. 502 when the email fails to send (the new invitation row is kept; inviting again supersedes it and resends). Supersedes earlier pending invitations to the same email in the clinic.
- `DELETE /api/staff/invitations/{id}` -> 204 (404 if not a pending invitation of the clinic).
- `PUT /api/staff/{id}/role` body `{ role }` (any ClinicRole) -> `StaffMemberDto`; 404 other clinic/missing; 409 `{ error }` last active Owner.
- `POST /api/staff/{id}/deactivate` / `POST /api/staff/{id}/reactivate` -> `StaffMemberDto`; 409 on last active Owner deactivate.

## Auth

- `POST /api/auth/accept-staff-invitation` (anonymous, rate limit `staff-invitation`) body `{ token, email, password, confirmPassword, firstName, lastName, specialty? }` -> `AuthResponse` (staff user, role = invited role). Any invalid/expired/used/superseded/revoked token or email mismatch -> 400 `{ error: "Auth.InvalidInvitation" text }` (identical). Password mismatch -> 400 `{ errors }`.
- `register`, `login`, `google-login` -> `AuthResponse.user` gains `clinicId`, `clinicName`; `role` is the clinic role (`Owner` for self-registered doctors).

## Behaviour changes in existing endpoints

- Patient create/update: staff without `PatientClinicalFields` never receive primaryCondition/allergies/notes/bloodType values (empty / `Unknown`) and their updates keep stored values. Create accepts optional `doctorId`.
- Appointment create accepts optional `doctorId`.
- `GET /api/audit-log` path unchanged: `/api/patients/{id}/audit-log`; 404 for a Doctor who is not the treating doctor.
- Reports: Owner clinic-wide; Doctor own data; others 403.
