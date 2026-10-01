# Feature Specification: Patient Portal – My Records

**Feature Branch**: `005-patient-portal-records`

**Created**: 2026-10-01

**Status**: Draft

**Input**: User description: "Patient portal: my records (GitHub issue #11). As a patient, I want to log in and see my upcoming appointments, prescriptions, invoices and shared attachments so that I have my information without calling. Acceptance: separate patient role; patients can only see their own data; doctor chooses which attachments and notes are shared."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Patient views their own records (Priority: P1)

A patient signs in with their own account and lands on a "My Records" area showing their upcoming appointments, current and past prescriptions, and invoices with payment status. Everything shown belongs to that patient only.

**Why this priority**: This is the core value of the feature: patients can self-serve basic information instead of phoning the office. Without it nothing else matters.

**Independent Test**: Sign in as a patient who has appointments, prescriptions and invoices; confirm all three lists appear and contain only that patient's data.

**Acceptance Scenarios**:

1. **Given** a signed-in patient with future appointments, **When** they open My Records, **Then** they see those appointments with date, time, type, location and status, soonest first.
2. **Given** a signed-in patient with prescriptions, **When** they open the prescriptions section, **Then** they see medication, dosage, status and dates for each.
3. **Given** a signed-in patient with invoices, **When** they open the invoices section, **Then** they see amount, due date and status (pending, paid, overdue) for each.
4. **Given** a signed-in patient with no records in a section, **When** they open it, **Then** they see a clear empty-state message.

---

### User Story 2 - Patient access is strictly limited to their own data (Priority: P1)

Patient accounts are a separate kind of account from doctor accounts. A patient can never see another patient's information and cannot reach doctor-only screens or actions.

**Why this priority**: Patient data is sensitive; this protection is a prerequisite for releasing the portal at all.

**Independent Test**: Sign in as patient A and attempt to open patient B's records and doctor-only pages by every available route; all attempts are denied.

**Acceptance Scenarios**:

1. **Given** a signed-in patient, **When** they try to view any record belonging to another patient, **Then** access is denied and nothing about the other patient is revealed.
2. **Given** a signed-in patient, **When** they try to open doctor-only areas (patient list, dashboard, billing management), **Then** access is denied.
3. **Given** a signed-in doctor, **When** they sign in, **Then** their experience is unchanged.

---

### User Story 3 - Doctor chooses what to share (Priority: P2)

A doctor decides, per attachment and per note, whether it is visible to the patient in the portal. Nothing is shared by default.

**Why this priority**: Gives doctors control over sensitive content, but the portal is already useful with appointments, prescriptions and invoices alone.

**Independent Test**: As a doctor, mark one attachment and one note as shared and leave another of each unshared; sign in as the patient and confirm only the shared ones appear, then unshare one and confirm it disappears.

**Acceptance Scenarios**:

1. **Given** a patient attachment that is not shared, **When** the patient opens My Records, **Then** it does not appear and cannot be downloaded.
2. **Given** a doctor marks an attachment as shared, **When** the patient refreshes, **Then** they can view and download it.
3. **Given** a doctor marks a note as shared, **When** the patient refreshes, **Then** they can read it.
4. **Given** a doctor unshares an item, **When** the patient refreshes, **Then** the item is no longer visible or downloadable, including via a previously known link.

---

### User Story 4 - Doctor gives a patient portal access (Priority: P2)

A doctor invites an existing patient to the portal. The patient receives an invitation, sets up their own credentials, and their account is linked to their patient record.

**Why this priority**: Patients need accounts before anything can be viewed; kept P2 because accounts could be provisioned manually for an initial pilot.

**Independent Test**: Invite a patient who has an email on file, complete the invitation as that patient, and confirm sign-in leads to their own records.

**Acceptance Scenarios**:

1. **Given** a patient with an email address on file, **When** the doctor sends a portal invitation, **Then** the patient receives it and can create their credentials.
2. **Given** an expired or already-used invitation, **When** the patient opens it, **Then** they see a clear message and are not signed in.
3. **Given** a patient without an email on file, **When** the doctor tries to invite them, **Then** the doctor is told an email is required.

---

### Edge Cases

- A patient's record is archived or deactivated: portal access is blocked with a neutral message.
- A doctor changes a patient's email after invitation: the old invitation stops working.
- An item is unshared while the patient is viewing or downloading it: later requests are denied.
- A patient's session expires: they are asked to sign in again without losing their place where possible.
- Failed sign-in attempts must not reveal whether an account exists.
- A patient is linked to more than one doctor: only records from doctors who have invited them are shown (see Assumptions).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a patient account type distinct from doctor accounts, with its own permissions.
- **FR-002**: Patients MUST be able to sign in and see only their own upcoming appointments, prescriptions and invoices.
- **FR-003**: System MUST deny patients access to any other patient's data and to all doctor-only functionality, with responses that do not reveal whether the other data exists.
- **FR-004**: Doctors MUST be able to mark each attachment and each note as shared with the patient or not shared; the default MUST be not shared.
- **FR-005**: Patients MUST be able to view and download only attachments marked as shared, and read only notes marked as shared.
- **FR-006**: Revoking sharing MUST take effect immediately for all future views and downloads.
- **FR-007**: Doctors MUST be able to invite an existing patient to the portal by email; invitations MUST be single-use and expire.
- **FR-008**: Each patient account MUST be linked to exactly one patient record per inviting doctor, established at invitation time.
- **FR-009**: The portal MUST show a clear empty state for any section with no data.
- **FR-010**: Patients MUST have read-only access in this feature; no editing of clinical or billing data.
- **FR-011**: Portal sign-in, password recovery and Google sign-in behaviour for existing doctor accounts MUST remain unchanged.
- **FR-012**: System MUST record when a shared item is viewed or downloaded by a patient, so doctors can later audit access.

### Key Entities

- **Patient account**: A portal login belonging to one patient, linked to a patient record; read-only role.
- **Portal invitation**: A single-use, expiring invite tied to a patient record and email address.
- **Sharing flag**: Per attachment and per note, whether it is visible to the patient (default: not shared).
- **Patient record, appointment, prescription, invoice, attachment, note**: Existing entities; the portal presents a patient-safe, read-only view of them.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A patient can sign in and see their upcoming appointments within 30 seconds of opening the portal.
- **SC-002**: In access-control testing, 100% of attempts by a patient to view another patient's data or doctor-only areas are denied.
- **SC-003**: 100% of unshared attachments and notes are invisible to patients; shared ones appear within 5 seconds of sharing.
- **SC-004**: At least 90% of invited patients complete account setup on their first attempt in usability testing.
- **SC-005**: Phone/message requests to the office for "when is my appointment / what do I owe" drop by at least 30% among portal users within two months of launch.

## Assumptions

- Patients receive accounts only by doctor invitation to the email on their record; open self-registration is out of scope.
- The portal is read-only in this feature; online booking, messaging, intake forms and payments are separate stories (#10, #14, #12, #15).
- A patient record belongs to a single doctor today, so one patient account maps to one patient record; multi-doctor clinics are out of scope.
- Patients sign in with email and password using the existing recovery flow; Google sign-in for patients is out of scope for v1.
- Existing attachment and note data is reused; sharing is opt-in per item, so all existing items start as not shared.
- The portal works on phone and desktop browsers.
