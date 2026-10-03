# Feature Specification: Multi-user Clinic with Roles

**Feature Branch**: `claude/issue-23-clinic-roles` (spec folder `045-clinic-roles`)
**Created**: 2026-10-03
**Status**: Draft
**Input**: GitHub issue #23 (Refs #23) - As a clinic owner, I want to add staff (receptionist, nurse, doctor) with role-based permissions so that the team can share one practice. Acceptance: a Clinic/Organization layer is added (records are currently scoped to one doctor); role-based permissions are enforced in the API; it is a prerequisite for portal and staff features.

## Clarifications

### Session 2026-10-03

- Q: Who may read the audit log? -> A: Owners for any patient of the clinic; Doctors only for patients they are the treating doctor of (stricter than all clinic doctors); Nurse and Receptionist never.
- Q: May an invitation grant the Owner role? -> A: No. Invitations grant Doctor, Nurse or Receptionist; only an existing Owner can promote a member to Owner (keeps privilege escalation behind an authenticated Owner action).
- Q: Is blood type clinical data hidden from Receptionists? -> A: Yes (stricter option).
- Q: What does deactivation do to a member's history? -> A: Membership row is kept (inactive) so audit and authorship stay valid; access is denied immediately and the account can be reactivated by an Owner.
- Q: Can a user belong to several clinics? -> A: No, one clinic per user in this release (unique membership per user); an invitation to an email that already has any account is refused generically.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Existing and new doctors own a clinic, nothing is lost (Priority: P1)

Every doctor who exists today, and every doctor who registers from now on, becomes the Owner of their own clinic. All of the doctor's existing records (patients, appointments, prescriptions, invoices, notes, vitals, attachments, labs, clinical lists, waitlist, intake, availability, audit history) belong to that clinic, and the doctor sees exactly what they saw before.

**Why this priority**: Everything else depends on the clinic layer, and existing data must survive the change.

**Independent Test**: Migrate a database that has two doctors with data; each doctor signs in and sees only their own data, unchanged; the other doctor's records answer 404.

**Acceptance Scenarios**:

1. **Given** a registered doctor, **When** they sign in, **Then** their account has an active clinic membership with the Owner role and their clinic name is shown.
2. **Given** a database from before this feature, **When** the upgrade is applied, **Then** each doctor has one clinic with an Owner membership and every record has the clinic of its doctor; no row is lost.
3. **Given** a signed-in user with no active clinic membership (never joined, or deactivated), **When** they call any staff endpoint, **Then** access is denied and no clinic data is returned (fail closed).
4. **Given** a record of another clinic, **When** a staff member asks for it by id, **Then** the answer is the same 404 as for a record that does not exist.

---

### User Story 2 - Roles are enforced by one permission matrix (Priority: P1)

Staff have one of four roles (Owner, Doctor, Nurse, Receptionist). What each role may do is defined in one documented matrix and enforced centrally; a role that lacks a permission receives 403, while data outside the clinic is 404.

**Why this priority**: This is the access-control core of the issue.

**Independent Test**: For each role and each area, call a representative read and write endpoint and compare with the matrix.

**Acceptance Scenarios**:

1. **Given** a Receptionist, **When** they read notes, labs, prescriptions, allergies/problems/medications, vitals or attachments, **Then** they are denied; **When** they manage appointments, waitlist, patient demographics, intake links and invoices/payments, **Then** it succeeds.
2. **Given** a Receptionist reading or editing a patient, **When** the patient has clinical free text (primary condition, allergies, notes), **Then** those fields are not returned and are never overwritten by the receptionist's edits.
3. **Given** a Nurse, **When** they read clinical data and record vitals and notes, **Then** it succeeds; **When** they prescribe, create or change invoices, or manage staff, **Then** they are denied.
4. **Given** a Doctor, **When** they work on any patient of the clinic (not only patients they created), **Then** clinical access succeeds; staff management is denied.
5. **Given** an Owner, **When** they use any clinical, billing or staff feature, **Then** it succeeds.
6. **Given** a patient (portal) token, **When** any staff endpoint is called, **Then** access is denied; portal endpoints behave exactly as before.

---

### User Story 3 - Owner invites and manages staff (Priority: P1)

An Owner invites a person by email with a role. The invitee follows the emailed link, sets a password and joins the clinic. The Owner can list staff, change a role, deactivate and reactivate a member. The last active Owner can never be demoted, deactivated or removed.

**Why this priority**: Without it a clinic cannot have a team.

**Independent Test**: Owner invites a nurse, nurse accepts and signs in with Nurse permissions; Owner demotes the nurse to receptionist, then deactivates; deactivated user is denied immediately.

**Acceptance Scenarios**:

1. **Given** an Owner, **When** they invite an email with a role, **Then** an email with a personal expiring link is sent and the pending invitation is listed.
2. **Given** a valid link, **When** the invitee submits a password, **Then** an account is created, joined to the clinic with the invited role, and signed in.
3. **Given** an expired, used, superseded, revoked, tampered or unknown token, or an email that differs from the invited one, **When** it is submitted, **Then** the same generic invalid-invitation error is returned.
4. **Given** the only active Owner, **When** anyone tries to demote or deactivate them, **Then** the change is refused.
5. **Given** a deactivated member holding a still-valid token, **When** they call any staff endpoint, **Then** access is denied at once.
6. **Given** an email that already belongs to any existing account, **When** an Owner invites it, **Then** the response is identical to a successful invitation and nothing is created or sent, so nothing about the other account is revealed.
7. **Given** a non-Owner, **When** they call staff management endpoints, **Then** access is denied.

---

### User Story 4 - Audit log records the acting staff role and is clinic-scoped (Priority: P2)

Every audited view or change records which staff user acted and with which role. The audit-log endpoint returns only the clinic's events for a patient of that clinic.

**Independent Test**: A nurse views a patient; an Owner reads the log and sees the nurse's name and role; a Receptionist and Nurse cannot read the log; another clinic's owner gets 404.

**Acceptance Scenarios**:

1. **Given** any staff action on a patient, **When** the event is stored, **Then** it carries the actor user, name, role at that time and the clinic.
2. **Given** a permitted role of the clinic, **When** they read a patient's audit log, **Then** all events for that patient in the clinic are returned.
3. **Given** a Nurse or Receptionist, **When** they read the log, **Then** access is denied.

---

### User Story 5 - Reports stay valid (Priority: P2)

Reports remain available: clinic-wide for Owners and limited to the caller's own appointments and invoices for Doctors. Nurses and Receptionists have no reports.

**Acceptance Scenarios**:

1. **Given** two doctors in one clinic with invoices each, **When** the Owner views revenue, **Then** it includes both; **When** a Doctor views it, **Then** only their own.
2. **Given** a Nurse or Receptionist, **When** they open reports, **Then** access is denied.

---

### User Story 6 - Role-aware client (Priority: P2)

The client shows a Staff page to Owners and hides navigation entries a role cannot use, in Spanish (primary) and English.

**Acceptance Scenarios**:

1. **Given** a Receptionist, **When** they open the app, **Then** navigation lacks clinical-only entries and patient screens do not show clinical sections.
2. **Given** an Owner, **When** they open Staff, **Then** they can invite, change role, deactivate and reactivate.

### Edge Cases

- A staff member is deactivated or demoted while holding a valid token: the next request reflects the change (membership is read per request, not trusted from the token).
- Two Owners demote each other concurrently: the last-owner rule must still hold.
- An Owner invites the same email twice: the earlier pending invitation is superseded.
- A nurse authors a note: authorship shows the nurse's name, not a doctor.
- A receptionist creates a patient or invoice: a treating doctor of the clinic is assigned so doctor-linked data stays valid.
- Cross-clinic ids passed in request bodies (patient, doctor) are treated as missing.
- Patient tokens and public token links (intake, reminder responses, waitlist offers, online booking) are unaffected.
- A legacy user with a Doctor identity role but no membership gets nothing.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST have a Clinic entity; each registered doctor (email or Google) MUST get a clinic and an active Owner membership.
- **FR-002**: Every record previously scoped to a doctor MUST belong to a clinic, set at creation, and every query MUST be clinic-scoped.
- **FR-003**: A user with no active membership MUST be denied on every staff endpoint; patient tokens MUST never satisfy a staff permission.
- **FR-004**: A record outside the caller's clinic MUST return the same 404 as a missing record.
- **FR-005**: Roles Owner, Doctor, Nurse and Receptionist MUST exist with the permission matrix documented in `contracts/permission-matrix.md`, enforced through one central authorization mechanism; a missing permission returns 403.
- **FR-006**: A Receptionist MUST NOT read or write notes, labs, prescriptions, allergies/problems/medications, vitals or attachments; the patient's primary condition, allergies and notes fields MUST be withheld from them and preserved on their edits.
- **FR-007**: A Nurse MUST be able to read clinical data and create vitals and notes, and MUST NOT prescribe, access invoices or manage staff.
- **FR-008**: An Owner MUST be able to invite staff by email; the invitation token MUST be random, stored only as a hash, single-purpose, expiring (7 days), single-use, and superseded by a newer invitation to the same email.
- **FR-009**: Accepting an invitation MUST create the account, attach it to the clinic with the invited role and sign the user in; every failure reason MUST return the same generic error; the endpoint MUST be rate limited.
- **FR-010**: An Owner MUST be able to list members and pending invitations, change a role, deactivate and reactivate a member, and revoke an invitation.
- **FR-011**: The last active Owner MUST NOT be demoted or deactivated, including under concurrent requests.
- **FR-012**: Audit events MUST record acting user, name, role and clinic; the audit-log endpoint MUST be clinic-scoped and readable by Owners (any patient of the clinic) and by a Doctor only for patients whose treating doctor they are; Nurses and Receptionists are denied.
- **FR-013**: Reports MUST be clinic-wide for Owners and own-scoped for Doctors; other roles MUST be denied.
- **FR-014**: Notes authored by non-doctor staff MUST show the author's name and the schema MUST allow non-doctor authors.
- **FR-015**: Staff-created patients, appointments and invoices MUST be linked to a valid treating doctor of the clinic.
- **FR-016**: Existing data MUST be preserved by a migration that backfills one clinic per doctor, an Owner membership, and the clinic id on every record, with a safe reversal.
- **FR-017**: The client MUST provide an Owner Staff page, role-aware navigation, and Spanish-primary plus English strings.
- **FR-018**: An invitation may grant Doctor, Nurse or Receptionist only. An Owner may later promote a member to Owner (only Owners change roles), and may demote or deactivate themselves only while another active Owner exists.
- **FR-019**: Patient fields withheld from Receptionists are primary condition, allergies, notes and blood type (returned empty/Unknown and preserved on their edits). Insurance and contact data stay visible.

### Key Entities

- **Clinic**: the practice; has a name; owns all records.
- **Clinic membership**: links one user to one clinic with a role and an active flag.
- **Staff invitation**: clinic, email, role, hashed token, expiry, used time, inviter.
- **Clinic-scoped records**: all existing doctor-scoped records plus audit events.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After upgrade, 100% of pre-existing records stay accessible to their original doctor with identical content and none to any other clinic.
- **SC-002**: For every role and area in the matrix, allowed actions succeed and denied actions are refused, verified by an automated role-by-area test table.
- **SC-003**: No request from one clinic returns or changes another clinic's data; cross-clinic and missing ids are indistinguishable.
- **SC-004**: An Owner can invite a colleague and the colleague can be working in the clinic in under 3 minutes.
- **SC-005**: The last active Owner can never be removed in any tested sequence.
- **SC-006**: All pre-existing automated tests remain green except those whose premise changed, each listed explicitly.

## Assumptions

- A user belongs to exactly one clinic (multi-clinic membership is a follow-up).
- Membership is read from the database on each request; the token is not trusted for role or clinic.
- Availability management stays per doctor (receptionist-managed availability is a follow-up); custom roles and SSO are out of scope.
- Existing doctor-keyed fields (patient's treating doctor, appointment doctor) keep their meaning; they are no longer an access boundary.
- The self-registered doctor is Owner, so the role label for them changes from Doctor to Owner.
- Role and clinic are never taken from request bodies.
