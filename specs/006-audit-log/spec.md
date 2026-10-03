# Feature Specification: Audit Log

**Feature Branch**: `006-audit-log`

**Created**: 2026-10-02

**Status**: Draft

**Input**: User description: "Audit log (GitHub issue #24). As a clinic owner, I want a log of who viewed or changed each patient record so that we meet HIPAA audit requirements. Acceptance: view and change events recorded with user and timestamp; filterable log per patient. Refs #24"

## Clarifications

### Session 2026-10-02

- Q: If an audit event cannot be saved, should the audited action still go ahead? → A: No. The action fails and nothing is returned or changed (fail closed), for views and changes alike.
- Q: Should patient list, search and dashboard screens create events? → A: No. Only opening a specific patient's record, one of its sections, or an attachment creates a view event; list screens show summary fields only.
- Q: Should repeated views of the same item within a short time be merged into one event? → A: No. Every request is recorded separately so the log is complete.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Access to patient records is recorded automatically (Priority: P1)

Whenever anyone opens or changes a patient's information, the system records who did it, what they did, which patient it concerned, and when. Staff do nothing extra; recording cannot be skipped or switched off by the person being recorded.

**Why this priority**: Without a trustworthy record there is nothing to review. This is the core compliance need.

**Independent Test**: As a doctor, open a patient, edit a field, add a note, and view an attachment; then confirm each action produced exactly one record with the correct user, patient, action and time.

**Acceptance Scenarios**:

1. **Given** a signed-in doctor, **When** they open a patient's record or any of its sections (appointments, prescriptions, invoices, vital signs, notes, attachments), **Then** a "view" event is recorded with the doctor, the patient, the section and the time.
2. **Given** a signed-in doctor, **When** they create, edit, delete, share or unshare anything belonging to a patient (including the patient's demographic details), **Then** a "change" event is recorded with the doctor, the patient, what kind of item was affected and the time.
3. **Given** a signed-in patient using the portal, **When** they view their own records or download a shared attachment, **Then** a "view" event is recorded naming the patient as the actor.
4. **Given** a request that is denied (for example a doctor asking for another doctor's patient), **When** it is rejected, **Then** no event is recorded against that patient and nothing about the patient is revealed.
5. **Given** a recorded event, **When** anyone uses the application, **Then** there is no way to edit or delete that event.

---

### User Story 2 - Review the log for one patient (Priority: P1)

A clinic owner opens a patient and sees a chronological log (newest first) of every recorded view and change for that patient, showing who, what, and when. They can narrow it by action type (view or change), by user, and by date range.

**Why this priority**: This is the second acceptance criterion and the way the owner actually demonstrates compliance.

**Independent Test**: Generate a mix of view and change events by two users on two patients; open patient A's log and confirm it lists only patient A's events and that each filter narrows the list correctly.

**Acceptance Scenarios**:

1. **Given** a patient with recorded events, **When** the owner opens that patient's audit log, **Then** events are listed newest first with user, action (view or change), item affected, and timestamp, in pages.
2. **Given** the log is open, **When** the owner filters by action type, user, or date range (alone or combined), **Then** only matching events appear.
3. **Given** a patient with no events, **When** the owner opens the log, **Then** a clear empty-state message appears.
4. **Given** the log is open, **When** the owner views it, **Then** the act of viewing the log is itself recorded.
5. **Given** a change event, **When** it is displayed, **Then** it identifies which kind of item and which fields or item changed, but does not display the sensitive field values themselves.

---

### User Story 3 - Audit data is protected (Priority: P2)

Only the doctor who owns a patient's record can read that patient's audit log. Patients and other doctors cannot read it. The log never exposes clinical content.

**Why this priority**: The audit log itself identifies sensitive activity; it must be at least as protected as the records it describes.

**Independent Test**: As a patient and as a different doctor, request patient A's audit log by every available route; all attempts are denied without revealing whether the patient exists.

**Acceptance Scenarios**:

1. **Given** a signed-in patient, **When** they request any audit log, **Then** access is denied.
2. **Given** a doctor who does not own the patient, **When** they request that patient's audit log, **Then** the response is identical to the one for a patient who does not exist.
3. **Given** an unauthenticated visitor, **When** they request an audit log, **Then** access is denied.

---

### Edge Cases

- A user is deleted or renamed: past events keep showing the name or identifier as it was at the time of the event.
- A patient is deleted: that patient's events remain retained but are not reachable through the per-patient view.
- Many events are generated at once (for example opening a patient screen that loads several sections): each section view is recorded, and the log stays usable through paging and filters.
- Recording an event fails: the audited action fails too (for views and changes alike), so no access or change goes unrecorded. The one exception is creating a brand-new patient, which is recorded immediately after creation because the patient does not exist beforehand.
- A date filter whose start is after its end returns a clear validation error, not an empty list.
- Page size and filter values outside allowed limits are rejected.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST record an audit event every time a doctor or patient views a patient's record or any section of it, including downloading or previewing attachments.
- **FR-002**: System MUST record an audit event every time a patient's demographic details or any related item (appointments, prescriptions, invoices, vital signs, notes, attachments) is created, edited, deleted, shared or unshared.
- **FR-003**: Each event MUST capture the acting user, the acting user's role, the patient concerned, the action type (view or change), the kind of item affected, the item identifier where applicable, and a timestamp in UTC.
- **FR-004**: Events MUST NOT contain clinical content or field values (for example notes text, allergies, diagnoses); only the kind of item and, for changes, the names of the fields changed.
- **FR-005**: Events MUST be append-only: the application MUST offer no way to edit or delete them.
- **FR-006**: Rejected or unauthorized requests MUST NOT create events against a patient and MUST NOT reveal whether the patient exists.
- **FR-007**: A doctor MUST be able to retrieve the audit log for each of their own patients, newest first, in pages.
- **FR-008**: The log MUST be filterable by action type, by acting user, and by date range, with filters combinable.
- **FR-009**: Only the doctor who owns the patient MAY read that patient's audit log; patients, other doctors, and unauthenticated callers MUST be denied.
- **FR-010**: Reading an audit log MUST itself be recorded as a view event.
- **FR-011**: The audit log MUST be presented in the doctor's patient screen as its own section.
- **FR-012**: Events MUST be retained indefinitely, as HIPAA requires audit records to be kept for six years, and MUST survive deletion of the user or patient they refer to.
- **FR-013**: Filter and paging inputs MUST be validated, with sensible upper limits on page size.

### Key Entities

- **Audit Event**: One immutable record of an access. Attributes: acting user (identifier and display name as of the event), role, patient concerned, action type (view or change), item kind, item identifier, changed field names (for changes), timestamp.
- **Patient**: Existing record the events are about; events are scoped to the owning doctor.
- **User**: Existing doctor or patient account that performs the action.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of in-scope view and change actions on a patient produce a matching audit event, verified by automated checks across every patient-related area.
- **SC-002**: An owner can locate all activity by a given user on a given patient within a given week in under 1 minute.
- **SC-003**: The per-patient log shows the first page of results in under 2 seconds for patients with up to 10,000 events.
- **SC-004**: 0 audit events contain clinical field values, and 0 events can be altered or removed through the application.
- **SC-005**: 100% of attempts by patients or non-owning doctors to read an audit log are denied with no information leak.

## Assumptions

- There is no separate "clinic owner" role in the product today; the owner is a doctor account, and each doctor sees the audit log only for their own patients. Introducing an admin or owner role is out of scope.
- Date filters use UTC calendar days, inclusive of both ends; the acting-user filter matches on part of the user's name. Creating an item records no item identifier because the event is written before the item exists.
- Events are recorded for actions by authenticated doctors and patients; system or background activity is out of scope.
- A single log view per patient is the scope; a cross-patient, clinic-wide log and export (CSV/PDF) are out of scope.
- Patient list, search and dashboard screens are not recorded; each repeated view is recorded separately.
- Login and logout events and failed sign-ins are not part of this feature.
- Retention is indefinite (no purge job), which satisfies the six-year requirement.
- Backfilling events for actions that happened before this feature ships is not possible and not attempted.
