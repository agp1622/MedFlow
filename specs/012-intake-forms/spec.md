# Feature Specification: Digital Intake Forms

**Feature Branch**: `claude/issue-12-intake-forms` (spec folder `012-intake-forms`; branch name is intentionally not the Spec Kit default)

**Created**: 2026-10-03

**Status**: Draft

**Input**: User description: "Digital intake forms (GitHub issue #12). As a doctor, I want new patients to fill in demographics, history and consent online before their first visit so that check-in is faster. Acceptance: form link is sent with the booking confirmation; answers fill the patient record, but the doctor reviews before accepting; consent is stored with timestamp and e-signature."

## Clarifications

### Session 2026-10-03

- Q: Does the doctor accept or reject a submission as a whole, or per field? → A: As a whole (accept applies all intake fields; reject applies none).
- Q: How are current medications and past history stored, given the patient record has no dedicated fields? → A: Appended to the patient's Notes as a dated "Intake" block on accept (existing notes never overwritten); other fields overwrite their record counterparts.
- Q: May a patient hold more than one pending submission? → A: Each link accepts exactly one submission. Issuing a new link supersedes the old one, and a patient may end up with several submissions over time, each decided independently.
- Q: Rate limit on the public endpoints? → A: Conservative per-IP limit (10 requests per 15 minutes) on the public form routes.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Doctor sends an intake form link to a patient (Priority: P1)

A doctor, from a patient's record, sends the patient an email containing a personal, time-limited link to the online intake form. This is the delivery mechanism available today; when online booking confirmations exist, the same link is to be included in them (see Dependencies).

**Why this priority**: Without a delivered link there is no way for patients to start the form.

**Independent Test**: Send a link for a patient with an email on file and confirm an email containing a working link is dispatched; try a patient without an email and confirm it is refused.

**Acceptance Scenarios**:

1. **Given** an active patient with an email on file, **When** the doctor sends the intake form, **Then** the patient is emailed a link that expires after a fixed period.
2. **Given** a patient without a valid email, **When** the doctor tries to send the form, **Then** the doctor is told an email is required and nothing is sent.
3. **Given** a link was already sent, **When** the doctor sends a new one, **Then** the earlier link stops working.
4. **Given** a doctor who does not own the patient, **When** they try to send a form for that patient, **Then** the request is treated as not found.

---

### User Story 2 - Patient completes the form and signs consent (Priority: P1)

The patient opens the emailed link (no account or sign-in required), fills in demographics, contact details, insurance, medical history (conditions, allergies, current medications) and reads and agrees to the consent statement, signing it by typing their full name. They submit once.

**Why this priority**: This is the core value: data capture and consent before the visit.

**Independent Test**: Open a valid link, complete all required fields, tick consent, type a name, submit; confirm a confirmation message and that the link cannot be used to submit again.

**Acceptance Scenarios**:

1. **Given** a valid link, **When** the patient opens it, **Then** they see an empty form greeting them by first name; no other stored patient data is shown.
2. **Given** a completed form with consent ticked and a typed signature, **When** they submit, **Then** the submission is saved as pending doctor review and the patient sees a confirmation.
3. **Given** missing required fields, consent not ticked, or a signature that is blank, **When** they submit, **Then** they see field-level errors and nothing is saved.
4. **Given** an expired, used, revoked or unknown link, **When** it is opened or submitted, **Then** the patient sees one generic "link is not valid" message that does not reveal whether a patient or email exists.

---

### User Story 3 - Doctor reviews and accepts or rejects the answers (Priority: P1)

The doctor sees pending intake submissions, compares the answers with the current patient record, and either accepts (the answers update the patient record) or rejects (the record is untouched, with an optional reason).

**Why this priority**: The acceptance criteria require the doctor to review before data enters the record.

**Independent Test**: Submit a form, confirm the patient record is unchanged, accept it as the doctor, and confirm the record now reflects the answers.

**Acceptance Scenarios**:

1. **Given** a pending submission, **When** the doctor views it, **Then** they see each answer next to the current record value.
2. **Given** a pending submission, **When** the doctor accepts, **Then** the demographic, contact, insurance and medical-history fields of the patient record are updated and the submission is marked accepted with who and when.
3. **Given** a pending submission, **When** the doctor rejects, **Then** the patient record is unchanged and the submission is marked rejected.
4. **Given** a submission already accepted or rejected, **When** the doctor tries to decide again, **Then** the action is refused.
5. **Given** a submission for another doctor's patient, **When** a doctor tries to view or decide it, **Then** it is treated as not found.
6. **Given** any signed-in patient (portal) account, **When** they call doctor review actions, **Then** access is denied.

---

### User Story 4 - Consent is stored as an auditable record (Priority: P1)

Each submission stores the consent as evidence: the exact consent text version shown, the typed signature name, the date and time of signing, and that the patient affirmed agreement. The record is kept with the submission and is not editable.

**Why this priority**: An explicit acceptance criterion.

**Independent Test**: After a submission, the doctor's view shows consent text version, signature name and timestamp; no endpoint allows modifying them.

**Acceptance Scenarios**:

1. **Given** a submitted form, **When** the doctor views it, **Then** the signature name, signing timestamp and consent text version are shown.
2. **Given** an accepted or rejected submission, **When** anyone attempts to change the consent details, **Then** no such action exists.

---

### Edge Cases

- A patient opens the link twice before submitting: the form works until submitted or expired.
- Two near-simultaneous submissions with the same link: only one succeeds.
- Over-long answers or too many medications are rejected with validation errors.
- Typed signature differs from the patient's name: allowed; doctor sees both for review.
- Patient is archived/inactive after the link was sent: submission is refused with the generic invalid-link response.
- Email delivery fails: doctor is told it failed and may retry.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Doctors MUST be able to send an intake form link by email to an active patient of theirs who has a valid email on file.
- **FR-002**: Links MUST be single-purpose, unguessable, expire after 14 days, and be superseded when a new one is sent; only a hash of the link secret is stored.
- **FR-003**: The patient identity for a submission MUST come from the link only, never from a request field.
- **FR-004**: The public form MUST NOT reveal any stored patient data other than the first name, and invalid, expired, used and revoked links MUST be indistinguishable to the caller.
- **FR-005**: The form MUST collect: date of birth, gender, phone, address, city, state, ZIP, insurance provider and policy number, primary condition, allergies, current medications, past history, and free-text notes, with defined maximum lengths; first and last name are confirmed.
- **FR-006**: Submission MUST require consent affirmation and a typed signature name; the system MUST store the consent text version, signature name, the affirmation, and the server-side UTC timestamp.
- **FR-007**: A submission MUST be stored as Pending and MUST NOT change the patient record until a doctor accepts it.
- **FR-008**: Doctors MUST be able to list pending (and past) submissions for their own patients with paging, view one beside current record values, accept it, or reject it with an optional reason.
- **FR-009**: Accepting is whole-submission: it MUST update only the intake-form fields of the patient record (current medications and past history are appended to Notes as a dated Intake block, never overwriting existing notes); rejecting MUST leave the record unchanged. Decisions are final and record the deciding doctor and time.
- **FR-010**: Consent details MUST be immutable after submission.
- **FR-011**: A link MUST be usable for one successful submission only.
- **FR-012**: Doctors MUST only access their own patients' submissions; patient-role accounts MUST be denied all doctor intake routes.
- **FR-013**: The public submission endpoint MUST be rate limited per link/IP to resist abuse (10 requests per 15 minutes per IP).
- **FR-014**: Doctors MUST see a way to send the form and review submissions in the client.

### Key Entities

- **Intake Link**: Hashed secret, patient, email, expiry, used/superseded marker.
- **Intake Submission**: Patient, status (Pending, Accepted, Rejected), answers, consent (text version, signature name, signed-at), submitted-at, decision (doctor, time, reason).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A patient can complete and submit the form in under 10 minutes on a phone.
- **SC-002**: 100% of submissions carry a consent timestamp, signature name and consent version.
- **SC-003**: 0 patient record changes occur without a doctor acceptance.
- **SC-004**: Doctors can review and decide a submission in under 2 minutes.
- **SC-005**: Invalid, expired and used links give identical responses.

## Assumptions

- The patient record already exists (created by the doctor or the booking flow); the form updates it, rather than creating patients.
- No patient account is required; the emailed link is the credential.
- E-signature is a typed full name plus an explicit agreement checkbox with a server timestamp and consent text version. This is a minimal approach; it is not asserted to meet any jurisdiction's legal e-signature standard (e.g. ESIGN/eIDAS advanced signatures). Legal review of the consent wording and signature method is required before production use.
- The consent text is a fixed, versioned text stored in configuration/code; per-practice editing is out of scope.
- Link validity is 14 days.
- File uploads, form builders and per-practice custom fields are out of scope.

## Dependencies

- Issue #10 (online booking confirmation email) is not on `dev`. Automatic inclusion of the link in booking confirmations is deferred to a small follow-up once that lands; the doctor-triggered send is delivered here. The acceptance criterion "sent with the booking confirmation" is therefore only partially satisfied.

## Addendum: intake link in the booking confirmation (issue #12, follow-up to #10)

Issue #10 is now merged, so the deferred acceptance criterion is delivered here and the Dependencies note above is superseded.

- **FR-015**: When a patient books online (`POST /api/portal/booking` returns Created), the system MUST email a booking confirmation containing a freshly created intake link, reusing the existing link creation (`IIntakeRepository.CreateLinkAsync`) and `IEmailSender`, but only if the patient has a valid email on file AND has no Pending/Accepted intake submission AND has no still-valid unused link.
- **FR-016**: Sending is best effort and happens after the appointment is saved: any link or email failure is logged and MUST NOT fail the booking. Rejected bookings (slot taken, limit, not available) send nothing, so a client retry never produces a second link or email; a later booking while a valid link is outstanding reuses that link and sends nothing.
- The confirmation email is only sent when a link is needed; patients who already completed or submitted intake get no new email from this change.
- Out of scope: a general booking confirmation email for patients who do not need an intake link; email localization (matches the existing intake email, English).
