# Feature Specification: Secure Messaging

**Feature Branch**: `006-secure-messaging`

**Created**: 2026-10-02

**Status**: Draft

**Input**: User description: "I want to work in #issue 14" — GitHub issue #14 "Secure messaging": As a patient, I want to message my doctor securely so that I can ask follow-up questions. Acceptance: messages are threaded per patient with an unread count on the dashboard; patients can attach files via the existing attachments pipeline.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Patient sends a message to their doctor and reads replies (Priority: P1)

A signed-in patient opens the portal's messages area, writes a follow-up question to their doctor, and sends it. Their conversation with that doctor is shown as a single thread in chronological order, so they can later read the doctor's reply in context.

**Why this priority**: This is the core value of the issue: patients can ask follow-up questions without phoning the office.

**Independent Test**: Sign in as a patient, send a message, and confirm it appears in the thread with the correct time and sender; then have the doctor reply and confirm the reply appears in the same thread.

**Acceptance Scenarios**:

1. **Given** a signed-in patient with no previous messages, **When** they send a message to their doctor, **Then** a new thread is created and the message is shown in it.
2. **Given** a patient with an existing thread, **When** they send another message, **Then** it is appended to the same thread, not a new one.
3. **Given** a doctor has replied, **When** the patient opens the messages area, **Then** they see the reply in the thread with sender and timestamp.
4. **Given** a patient submits an empty message with no attachment, **When** they try to send, **Then** it is rejected with a clear message.

---

### User Story 2 - Doctor reads and replies to patient messages (Priority: P1)

A doctor sees the conversation threads from their patients, opens one, reads the full history and replies. Each thread belongs to exactly one patient.

**Why this priority**: Messaging is useless to patients unless doctors can read and answer.

**Independent Test**: As a doctor, open a patient's thread containing a patient message, send a reply, and confirm the patient sees it.

**Acceptance Scenarios**:

1. **Given** a patient has sent a message, **When** the doctor opens that patient's thread, **Then** they see the whole conversation, oldest first.
2. **Given** a doctor viewing a thread, **When** they send a reply, **Then** it is added to the thread and visible to the patient.
3. **Given** a doctor, **When** they view their message list, **Then** threads are shown per patient, most recently active first.

---

### User Story 3 - Unread count on the dashboard (Priority: P1)

Both patients and doctors see a count of unread messages on their dashboard. Opening a thread marks its messages as read and the count goes down.

**Why this priority**: Explicit acceptance criterion; without it, replies and questions are easily missed.

**Independent Test**: Send a message from one party; confirm the other party's dashboard shows an unread count increased by one; open the thread and confirm the count decreases.

**Acceptance Scenarios**:

1. **Given** a patient with two unread doctor messages, **When** they open the dashboard, **Then** the unread count shows 2.
2. **Given** an unread count above zero, **When** the user opens the thread, **Then** its messages are marked read and the count drops accordingly.
3. **Given** a user's own sent messages, **When** counts are computed, **Then** they are never counted as unread for that user.
4. **Given** no unread messages, **When** the dashboard loads, **Then** no unread indicator is shown.

---

### User Story 4 - Attach files to a message (Priority: P2)

When writing a message, a patient (or doctor) can attach one or more files, such as a photo of a symptom or a lab result, using the same attachment capability already available elsewhere in the product. Recipients can view and download them from the thread.

**Why this priority**: Explicit acceptance criterion, but text-only messaging already delivers value.

**Independent Test**: Send a message with an attachment, then open it as the recipient and download the file.

**Acceptance Scenarios**:

1. **Given** a patient composing a message, **When** they attach an allowed file and send, **Then** the message shows the attachment and the doctor can download it.
2. **Given** a file that violates existing attachment rules (type or size), **When** the user tries to attach it, **Then** the same rejection message as elsewhere in the product is shown and the message is not sent with it.
3. **Given** a message with an attachment, **When** anyone other than the thread's patient and their doctors tries to download it, **Then** access is denied.

---

### Edge Cases

- A patient's portal access is revoked or the patient is deactivated: they can no longer read or send messages; existing history is kept for the doctor.
- A patient tries to open another patient's thread by any route: access is denied and nothing about the thread is revealed.
- Two messages are sent at nearly the same time: both are kept, in a consistent order.
- A very long message: rejected with a clear length limit message rather than silently truncated.
- Attachment upload fails mid-send: the message is not sent in a half-complete state, and the user can retry.
- A patient has been invited by more than one doctor: each patient–doctor pair has its own thread.
- Message content must not appear in notification text or any place visible without signing in.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Patients MUST be able to send a text message to their doctor from the patient portal.
- **FR-002**: Doctors MUST be able to read and reply to messages from their patients.
- **FR-003**: Messages MUST be grouped into one thread per patient–doctor pair, displayed in chronological order with sender and timestamp.
- **FR-004**: A thread MUST be visible only to its patient and the doctor(s) responsible for that patient; no other user may read, send to, or learn of its existence.
- **FR-005**: The system MUST track read/unread state per message per recipient and show each user the total unread count on their dashboard.
- **FR-006**: Opening a thread MUST mark the other party's messages in it as read for the viewer, and the dashboard count MUST update accordingly.
- **FR-007**: Users MUST be able to attach files to a message using the existing attachments capability, subject to its existing type, size and access rules.
- **FR-008**: Attachments on a message MUST be downloadable only by participants of that thread.
- **FR-009**: The system MUST reject empty messages (no text and no attachment) and messages exceeding a maximum length, with a clear explanation.
- **FR-010**: Messages MUST be retained and not editable or deletable by patients after sending, so the record of communication stays intact.
- **FR-011**: Message content MUST be protected in transit and at rest to the same standard as other patient-identifiable data, and access MUST require authentication.
- **FR-012**: Sending, and reading threads of, patient messages SHOULD be recorded in an audit trail identifying who accessed which thread and when.

### Key Entities

- **Message Thread**: The conversation between one patient and one doctor. Has a last-activity time and belongs to exactly one patient.
- **Message**: A single entry in a thread. Has sender (patient or doctor), body text, sent time, optional attachments, and read state per recipient.
- **Message Attachment**: A file linked to a message through the existing attachments capability.
- **Unread Count**: Derived number of messages in a user's threads sent by the other party and not yet opened by that user.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A patient can send a message to their doctor in under 1 minute from the dashboard.
- **SC-002**: A new message is reflected in the recipient's unread count within 1 minute of being sent, without manual page refresh being required beyond normal navigation.
- **SC-003**: 100% of attempts by a user to read or download another patient's thread or attachments are denied in testing.
- **SC-004**: 95% of patients can send a message with an attachment on their first attempt without help.
- **SC-005**: Reduce follow-up phone calls to the office for routine questions by 30% within three months of release.

## Assumptions

- Messaging is between a patient and their doctor only; patient-to-patient, group, and staff-to-staff chat are out of scope.
- Messaging is asynchronous (like email); real-time chat, typing indicators and presence are out of scope.
- The patient portal (feature 005: patient role, patient–account linking, own-data-only access) and the existing attachments capability are available and reused as-is.
- A patient has one thread per doctor who has invited them to the portal.
- Messages are for non-urgent questions; the interface shows a notice that urgent matters require contacting the office or emergency services. Emergency handling is out of scope.
- Email or push notifications of new messages are out of scope for this version; the dashboard unread count is the notification mechanism.
- Message retention follows the practice's existing retention policy for patient records.
- Searching within messages and message templates are out of scope.
