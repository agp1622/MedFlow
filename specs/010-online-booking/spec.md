# Feature Specification: Patient Portal Online Booking

**Feature Branch**: `claude/issue-10-online-booking` (spec folder `010-online-booking`)
**Created**: 2026-10-03
**Status**: Draft
**Input**: GitHub issue #10 - As a patient, I want to book an appointment from the doctor's available slots so that I don't have to phone the office. Doctor sets weekly availability and blocked dates; patients only see open slots and double-booking is prevented; new bookings appear on the calendar as Scheduled.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Doctor sets availability (Priority: P1)

A doctor defines the recurring weekly hours during which they accept appointments (per weekday, one or more time windows) and a list of blocked dates (holidays, leave) on which no slots are offered.

**Why this priority**: Without availability there are no slots to book.

**Independent Test**: A doctor saves weekly hours and a blocked date, reads them back, and edits/removes them. Another doctor cannot see or change them.

**Acceptance Scenarios**:

1. **Given** a signed-in doctor, **When** they save Monday 09:00-12:00 and 13:00-17:00, **Then** those windows are stored and returned on the next read.
2. **Given** a doctor with weekly hours, **When** they add a blocked date, **Then** no slots are offered on that date.
3. **Given** a window whose end is not after its start, or overlapping windows on one day, **When** saved, **Then** it is rejected with a validation error.
4. **Given** a patient token, **When** it calls the availability management routes, **Then** access is denied.

### User Story 2 - Patient views open slots and books (Priority: P1)

A signed-in portal patient sees only the open slots of their own doctor for a date range, and books one.

**Why this priority**: The core value of the issue.

**Independent Test**: A patient lists slots, books one, and the slot disappears from the list for everyone.

**Acceptance Scenarios**:

1. **Given** a doctor with availability, **When** a patient requests slots for a date range, **Then** only future slots inside availability, not on blocked dates, and not overlapping an existing active appointment are returned.
2. **Given** an open slot, **When** the patient books it, **Then** an appointment is created for that patient (taken from the token) with that doctor and time.
3. **Given** a slot already taken (including a concurrent attempt), **When** another patient books it, **Then** the booking is rejected with a conflict and no second appointment exists.
4. **Given** a time that is not an offered slot (outside availability, blocked, in the past, beyond the booking horizon), **When** booking is attempted, **Then** it is rejected.

### User Story 3 - Booking appears on the calendar (Priority: P2)

A new online booking shows on the doctor's appointment calendar/list as Scheduled and on the patient's portal appointments.

**Independent Test**: After a booking, the doctor's appointment list contains it with the Scheduled (initial) status; the patient sees it in portal appointments.

**Acceptance Scenarios**:

1. **Given** a successful booking, **When** the doctor opens appointments, **Then** it is listed with the initial Scheduled status.
2. **Given** a cancelled appointment, **When** slots are listed, **Then** that slot is open again.

### Edge Cases

- Two patients booking the same slot simultaneously: exactly one succeeds.
- Patient with Inactive/Deceased status or unlinked account: booking and slot listing denied like other portal routes.
- The doctor is always the patient's assigned doctor and never a request parameter; a doctor with no availability yields an empty slot list.
- Date range too large: rejected / capped.
- A patient already holding an active appointment overlapping the slot: rejected.

## Clarifications

### Session 2026-10-03

- Q: Which doctor can a patient book with? -> A: Only the doctor the patient is assigned to (the patient's own doctor); no doctor directory is exposed to patients.
- Q: Minimum lead time for online booking? -> A: 60 minutes from now (stricter than "any future time").
- Q: Limit on a patient's upcoming online bookings? -> A: At most 3 upcoming non-cancelled appointments per patient; further bookings are rejected.
- Q: Does a patient's own overlapping appointment block booking? -> A: Yes, rejected (stricter option).
- Q: Timezone handling? -> A: UTC, consistent with existing `ScheduledAt`; availability times are interpreted as UTC.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Doctors MUST be able to view and replace their own weekly availability (weekday, start time, end time; multiple windows per day).
- **FR-002**: Doctors MUST be able to list, add, and remove their own blocked dates.
- **FR-003**: Availability windows MUST be validated (end after start, no overlaps within a day, aligned to the slot length).
- **FR-004**: Availability and blocked-date management MUST be restricted to doctors, each scoped to their own data.
- **FR-005**: Slots are fixed 30-minute units generated from availability; a slot is open only if it is at least 60 minutes in the future, within a 60-day booking horizon, not on a blocked date, and has no non-cancelled appointment overlapping it for that doctor.
- **FR-006**: Portal patients MUST be able to list open slots of their own doctor over a date range (max 31 days).
- **FR-007**: Portal patients MUST be able to book an open slot; the patient identity comes only from the token; an optional reason (max 500 chars) is accepted.
- **FR-008**: Double-booking MUST be prevented, including under concurrent requests; a taken slot returns a conflict.
- **FR-009**: A booked appointment is created with the initial status (shown as Scheduled) and type Consultation, and appears in the doctor's appointment list and the patient's portal appointments.
- **FR-010**: Responses MUST not expose other patients' data; slot listings contain only times, never who holds a slot.
- **FR-011**: Inactive/unlinked patients receive the same unavailable response as other portal routes.
- **FR-013**: A patient MUST NOT hold more than 3 upcoming non-cancelled appointments; booking beyond that is rejected.
- **FR-012**: Patients booking MUST NOT see or set doctor notes, location, or status.

### Key Entities

- **Availability window**: doctor, weekday, start time, end time.
- **Blocked date**: doctor, date, optional label.
- **Appointment** (existing): gains no new fields; a booking creates one.

## Success Criteria *(mandatory)*

- **SC-001**: A patient can book an open slot in under 1 minute without phoning the office.
- **SC-002**: 0 double-booked slots, including under simultaneous requests.
- **SC-003**: 100% of online bookings appear on the doctor's appointment list immediately.
- **SC-004**: 100% of slot responses exclude past, blocked, and taken times.

## Assumptions

- "Scheduled" in the issue maps to the existing initial status `Pending` (the app already says "Appointment scheduled" for it); no new status is added. Doctors confirm as today.
- Slot length is a fixed 30 minutes, matching the appointment default; times are treated in UTC consistent with existing `ScheduledAt` handling.
- Booking horizon 60 days; minimum lead time 60 minutes.
- Patients cannot cancel/reschedule online in this feature (out of scope), nor does it send notifications.
- Staff/admin roles can manage availability only if they are doctors; the single-practice model means an active patient may book only with their assigned doctor.
