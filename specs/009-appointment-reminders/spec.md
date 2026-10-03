# Feature Specification: Appointment Reminders

**Feature Branch**: `claude/issue-9-appointment-reminders` (spec folder `009-appointment-reminders`; branch name set by the requester, deviating from the constitution's `NNN-name` convention)

**Created**: 2026-10-03

**Status**: Draft

**Input**: GitHub issue #9 "Appointment reminders": As a doctor, I want patients to get automatic email/SMS reminders so that fewer of them miss appointments. Reminder sent 24h before the visit with configurable timing; patient can confirm or cancel from a link and the appointment status updates; delivery is logged on the appointment; reuse the existing Email service. SMS is out of scope (no SMS provider exists in the system).

## Clarifications

### Session 2026-10-03

- Q: Should the doctor be emailed when a patient cancels? → A: No; the doctor sees the status and response on the appointment (smallest scope).
- Q: How many send attempts before giving up? → A: 3 attempts in total; later runs retry only Failed deliveries until the limit.
- Q: Can a link be used more than once? → A: Yes until the appointment starts, but a Cancelled appointment can never be re-confirmed through a link (stricter option); Confirmed may later be cancelled.
- Q: Where does the link point? → A: A public client page that shows minimal details and POSTs the explicit choice to a public token-based API; the token is a query parameter on the client page only, is sent to the API in a POST body, and is never logged.
- Q: How often does the reminder job run? → A: Every 15 minutes, configurable.
- Q: Public endpoints abuse protection? → A: Uniform responses, 256-bit random tokens, and a per-IP rate limit on the public endpoints (same mechanism as existing invitation acceptance).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Patient receives an automatic reminder (Priority: P1)

A patient with an upcoming appointment receives an email reminder ahead of the visit, without the doctor doing anything. The email states the date, time, doctor, and location, and contains a personal link to confirm or cancel.

**Why this priority**: This is the core value: reducing missed appointments.

**Independent Test**: Create an appointment for a patient with an email address scheduled inside the reminder window, let the reminder process run, and verify exactly one email is sent and a delivery record exists on the appointment.

**Acceptance Scenarios**:

1. **Given** a Pending or Confirmed appointment whose start time is within the configured lead time (default 24 hours) and no reminder has been sent, **When** the reminder process runs, **Then** the patient gets one reminder email and a delivery record is stored on the appointment.
2. **Given** a reminder has already been sent for an appointment, **When** the reminder process runs again, **Then** no second reminder is sent.
3. **Given** the lead time is configured to a different value (e.g. 48 hours), **When** the process runs, **Then** reminders go out according to that value.
4. **Given** an appointment that is Cancelled, Completed, NoShow, or already in the past, **When** the process runs, **Then** no reminder is sent.
5. **Given** a patient with no valid email address, **When** the process runs, **Then** no email is attempted and the appointment records a skipped delivery with the reason.
6. **Given** the email service fails, **When** the process runs, **Then** the failure is recorded on the appointment and the next run retries, up to a bounded number of attempts.
7. **Given** an appointment is rescheduled to a new time, **Then** a new reminder is sent for the new time.

---

### User Story 2 - Patient confirms or cancels from the link (Priority: P1)

The patient opens the link in the reminder, sees the appointment details, and chooses Confirm or Cancel, with no login. The appointment status updates accordingly.

**Why this priority**: Explicit acceptance criterion; lets the doctor free slots and know who is coming.

**Independent Test**: Use a reminder link, confirm, and verify the appointment status is Confirmed; repeat with a fresh link and cancel, verify status Cancelled.

**Acceptance Scenarios**:

1. **Given** a valid link for a Pending appointment, **When** the patient chooses Confirm, **Then** status becomes Confirmed.
2. **Given** a valid link for a Pending or Confirmed appointment, **When** the patient chooses Cancel, **Then** status becomes Cancelled.
3. **Given** a link that is invalid, tampered with, unknown, or expired (appointment start has passed), **When** it is opened or acted on, **Then** the response is the same generic "link not valid" outcome in all cases and nothing changes.
4. **Given** the appointment is already Cancelled, Completed, or NoShow, **When** the link is used, **Then** no status change occurs and the patient is told the appointment can no longer be changed.
5. **Given** a link for appointment A, **Then** it can never read or change any other appointment or patient data.
6. **Given** a link is merely opened (viewed), **Then** no status change happens until the patient explicitly chooses Confirm or Cancel.

---

### User Story 3 - Doctor sees delivery log on the appointment (Priority: P2)

The doctor opens an appointment and sees the history of reminder delivery attempts (time, outcome, failure reason) and how the patient responded.

**Why this priority**: Acceptance criterion; gives the doctor confidence and a way to diagnose failures.

**Independent Test**: After a reminder run, fetch the appointment as the doctor and see the delivery entries.

**Acceptance Scenarios**:

1. **Given** reminders were attempted for an appointment, **When** the doctor views it, **Then** each attempt is listed with timestamp, outcome (Sent, Failed, Skipped) and a non-sensitive reason.
2. **Given** another doctor, **When** they request that appointment's delivery log, **Then** they get no access.
3. **Given** a patient portal user, **When** they call the doctor-only log, **Then** it is rejected.

---

### Edge Cases

- Appointment created already inside the lead window: the reminder goes out on the next run.
- Multiple application instances or overlapping runs must not double-send for the same appointment time.
- A lead time of zero or negative, or larger than a sane maximum, is rejected at startup/falls back to the default.
- Appointment time changes after a reminder: old links must not be usable to act on a different time without the patient seeing the current details.
- Email addresses and tokens must never appear in logs or delivery records.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST automatically send one email reminder per appointment time to the patient when the appointment is Pending or Confirmed and starts within the configured lead time.
- **FR-002**: The lead time MUST be configurable (default 24 hours, allowed range 1 to 168 hours) without code changes.
- **FR-003**: The system MUST NOT send reminders for Cancelled, Completed, NoShow, or past appointments, nor send a second reminder for the same scheduled time.
- **FR-004**: The system MUST send reminders through the existing email sender abstraction.
- **FR-005**: Each reminder MUST contain a personal link that lets the patient confirm or cancel without logging in.
- **FR-006**: The link token MUST be unguessable, bound to a single appointment, stored only as a hash, and stop working once the appointment has started or is closed.
- **FR-007**: Opening the link MUST NOT change state; confirm and cancel MUST each be an explicit patient action.
- **FR-008**: Confirm MUST set status to Confirmed; Cancel MUST set status to Cancelled; closed appointments MUST NOT change.
- **FR-009**: Invalid, expired, and unknown tokens MUST yield identical responses so no appointment existence or state leaks.
- **FR-010**: Every reminder attempt (Sent, Failed, Skipped) MUST be logged on the appointment with timestamp and a non-sensitive reason, and patient responses MUST be recorded.
- **FR-011**: Failed sends MUST be retried on later runs up to a bounded number of attempts.
- **FR-012**: Doctors MUST be able to view the delivery log only for their own appointments; patient tokens MUST be rejected for that route.
- **FR-013**: The patient-facing confirm/cancel page MUST show only minimal details (date, time, doctor name, location) and no clinical data.
- **FR-014**: The doctor's appointment view in the client MUST display the delivery log and the patient response.

### Key Entities

- **Reminder Delivery**: one record per attempt on an appointment: appointment, attempted time, outcome (Sent, Failed, Skipped), reason, channel (Email).
- **Appointment Response Link**: per appointment and scheduled time, a hashed token with an expiry equal to the appointment start; records the patient's response and when.
- **Appointment**: existing entity; status is updated by the patient response; gains the reminder log.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of eligible appointments get exactly one reminder within the configured window plus one process interval.
- **SC-002**: A patient can confirm or cancel in under 30 seconds from opening the email, with no login.
- **SC-003**: The doctor sees the status change and delivery history on the appointment immediately after the patient responds.
- **SC-004**: Zero cases of a link affecting an appointment other than its own, or revealing whether an appointment exists.

## Assumptions

- Email only. SMS is out of scope because no SMS provider exists; it can be a later feature.
- Reminders are sent by an in-process background job that checks periodically (default every 15 minutes).
- Appointment times are stored and compared in UTC as they are today.
- The patient email on file is used; patients without a valid email are skipped and logged.
- "Cancel" by the patient sets status Cancelled; no doctor notification email is included in this feature.
- Rescheduling is detected by a changed scheduled time on the appointment.
- Outbound email configuration is reused unchanged; secrets stay in configuration.
