# Data Model: Multi-user Clinic with Roles

## New entities (all inherit `BaseEntity`: Id, CreatedAt, UpdatedAt, IsDeleted)

### Clinic (`Clinics`)
| Field | Type | Notes |
|---|---|---|
| Name | string(200) required | default "Clinic of {first} {last}" at registration |
| Stamp | Guid, concurrency token | rotated on every membership mutation (last-owner rule) |
| LegacyDoctorUserId | string(450)? | **migration-only**, dropped at the end of `Up`; not in the model |

### ClinicMember (`ClinicMembers`)
| Field | Type | Notes |
|---|---|---|
| ClinicId | int FK Clinics (Restrict) | |
| UserId | string(450) | Identity user id; **unique index** (one clinic per user) |
| Role | string(20) | `Owner`, `Doctor`, `Nurse`, `Receptionist` |
| IsActive | bool | deactivated members keep the row (history, authorship) |

Index `(ClinicId, Role, IsActive)`. Invariant: every clinic has at least one active Owner.

### StaffInvitation (`StaffInvitations`)
ClinicId (FK), Email(256), Role (Doctor/Nurse/Receptionist), TokenHash(64, indexed), ExpiresAt, UsedAt (set on accept, supersede or revoke), InvitedByUserId(450).

## Changed entities

`IClinicScoped { int ClinicId }` and a `ClinicId` column (int, required, indexed, FK to Clinics, Restrict) on: Patient, Appointment, Prescription, Invoice, VitalSign, MedicalNote, PatientAttachment, PatientAllergy, PatientProblem, PatientMedication, LabOrder, IntakeSubmission, WaitlistEntry, AuditEvent.

Not given a `ClinicId`: DoctorAvailability, DoctorBlockedDate, NoteTemplate (user-owned preferences, scoped by user id and requiring an active membership); PortalInvitation, IntakeLink, AppointmentReminder, ReminderDelivery, WaitlistOffer, PortalAccessLog, LabResult (reached only through a scoped parent or by token).

`MedicalNote`: navigation `Doctor` and FK to `Doctors.UserId` removed; `DoctorId` = author user id.

`UserDto`: adds `ClinicId?`, `ClinicName?`; `Role` = clinic role for staff, `Patient` for portal users.

## Rules

- `ClinicId` is set at creation (central stamp), never changed, never taken from request bodies.
- Child `ClinicId` always equals its patient's `ClinicId` (stamp throws otherwise).
- Role transitions: any role -> any role by an Owner; the last active Owner cannot leave Owner or become inactive.
- Invitation: Pending (UsedAt null, not expired) -> Used (accepted) | Superseded | Revoked | Expired.

## Migration `AddClinicsAndRoles` (hand-ordered)

Up:
1. Create `Clinics` (with temporary `LegacyDoctorUserId`), `ClinicMembers`, `StaffInvitations` (no FKs/indexes on child tables yet where they depend on data).
2. `AddColumn ClinicId int NOT NULL DEFAULT 0` on the 14 record tables.
3. SQL backfill: one clinic per `Doctors` row; `ClinicMembers` Owner row per doctor; `Patients.ClinicId` from the doctor's clinic; every other table's `ClinicId` from its patient (`VitalSigns`, `Appointments`, ...), with a fallback by `DoctorId` for rows whose patient is missing; `THROW` if any row is still 0.
4. Drop `LegacyDoctorUserId`.
5. Create indexes and foreign keys; drop FK `MedicalNotes -> Doctors`.

Down: drop the new FKs/indexes, `ClinicId` columns and the three tables; before restoring FK `MedicalNotes.DoctorId -> Doctors.UserId`, reassign notes whose author has no `Doctors` row to the patient's treating doctor (documented authorship loss on rollback). Original doctor-keyed data is untouched.
