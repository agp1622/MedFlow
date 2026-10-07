# Feature Specification: Waitlist for Cancelled Slots

**Feature Branch**: `claude/issue-25-waitlist` (spec folder `044-waitlist`)
**Created**: 2026-10-03
**Status**: Draft
**Input**: GitHub issue #25 (Refs #25) - As a receptionist, I want a waitlist that offers cancelled slots to waiting patients so that the schedule stays full. Acceptance: patients can be added to a waitlist; a cancelled slot notifies waitlisted patients.

## Clarifications

### Session 2026-10-03

- Q: Is a freed slot offered to one patient at a time with expiry, or to several at once? -> A: To the earliest-joined waiting patients up to the cap at once; first valid claim wins through atomic booking (simplest safe option, no hold and no double booking possible). Join time decides who is emailed when more than the cap are waiting.
- Q: Which language are offer emails written in, given patients have no stored language preference? -> A: Bilingual body, Spanish first then English.
- Q: What happens to an entry when the patient books an unrelated appointment? -> A: Nothing automatic; entry stays Waiting until claim, leave or staff removal (conservative: no hidden data changes).
- Q: Are removed entries deleted? -> A: No, status becomes Removed (history kept); a removed patient can rejoin with a new entry.
- Q: Does the cap count patients without a valid email? -> A: No, only patients that can actually be emailed count.
- Q: May a patient with an existing overlapping appointment be emailed? -> A: Yes, the claim is rejected as a conflict by the booking rules (no extra pre-check).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Staff manage the doctor's waitlist (Priority: P1)

The doctor (or staff acting under the doctor's account) adds one of their own patients to their waitlist, sees the list in join order, and removes entries.

**Why this priority**: Without entries there is nothing to offer; this satisfies "patients can be added to a waitlist".

**Independent Test**: A doctor adds a patient, lists the waitlist, removes the entry. Another doctor cannot see, add to, or remove from it.

**Acceptance Scenarios**:

1. **Given** a signed-in doctor and their active patient, **When** they add the patient, **Then** an entry is created with the join time and listed.
2. **Given** a patient already waiting, **When** they are added again, **Then** the request is rejected as a conflict and no duplicate exists.
3. **Given** a patient of another doctor (or a missing id), **When** a doctor adds them or removes an entry that is not theirs, **Then** the response is 404.
4. **Given** a patient token, **When** the doctor waitlist routes are called, **Then** access is denied.
5. **Given** an Inactive or Deceased patient, **When** added, **Then** the request is rejected.

### User Story 2 - Cancelled slot is offered by email (Priority: P1)

When an appointment of the doctor is cancelled (doctor edit, doctor status change, doctor delete, or the patient cancelling through the reminder link) and the freed 30-minute slot is still open for online booking, the earliest-joined waiting patients with a valid email are emailed an offer.

**Why this priority**: The core value and second acceptance criterion.

**Independent Test**: With two waiting patients, cancelling a future appointment sends one email to each (up to the cap) and records the offers; nothing is sent when the slot is not bookable.

**Acceptance Scenarios**:

1. **Given** waiting patients and a cancelled future appointment on an open bookable slot, **When** the cancellation is saved, **Then** at most the cap (default 5) earliest-joined waiting patients with a valid email each receive one offer email.
2. **Given** a cancelled appointment in the past, or whose slot is outside availability, on a blocked date, inside the booking lead time, or already retaken, **When** it is cancelled, **Then** no offer is sent.
3. **Given** a patient who already received an offer for that slot, **When** the same slot is freed again, **Then** that patient is not emailed twice for it.
4. **Given** an email failure for one patient, **When** offers are sent, **Then** the cancellation still succeeds and other patients are still emailed.
5. **Given** a patient who is not on this doctor's waitlist, **When** a slot is freed, **Then** they are never emailed.

### User Story 3 - Patient claims an offered slot (Priority: P1)

The patient opens the emailed link, sees the date, time and doctor, and may book the slot or leave the waitlist. The first valid claim books the slot; others learn it is gone.

**Independent Test**: Two patients hold offers for one slot; the first claim creates a Pending appointment, the second gets a conflict and no second appointment exists.

**Acceptance Scenarios**:

1. **Given** a valid unexpired offer link, **When** the patient claims it, **Then** a Pending appointment is created through the same atomic booking rules as online booking and the entry is marked Booked.
2. **Given** a slot already taken, **When** another patient claims, **Then** they get a conflict response and no double booking exists.
3. **Given** an unknown, tampered, expired, or already-used token, **When** looked up or used, **Then** the response is the same 404 for all.
4. **Given** a valid token, **When** the patient chooses to leave, **Then** the entry is removed and no further offers are sent.
5. **Given** public offer routes, **When** called repeatedly from one client, **Then** they are rate limited.

### User Story 4 - Patient joins and leaves from the portal (Priority: P2)

A signed-in portal patient sees whether they are on their own doctor's waitlist, joins, and leaves.

**Independent Test**: A patient joins, sees the status, leaves. Another patient's entries are never reachable.

**Acceptance Scenarios**:

1. **Given** a portal patient, **When** they join, **Then** an entry for them (patient from the token only) is created.
2. **Given** a waiting patient, **When** they leave, **Then** the entry is removed.

### Edge Cases

- Two patients claiming concurrently: exactly one appointment exists.
- Patient already has an overlapping active appointment or has reached the upcoming-appointment limit: claim is rejected as a conflict.
- Patient later becomes Inactive: entry produces no offers and claims fail with the generic 404.
- Offer expiry is capped before the slot's own start; an expired offer is a 404.
- Soft-deleted appointments (doctor delete) free their slot the same as cancelling.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Doctors MUST be able to list (oldest first, paged), add and remove entries for their own patients only; other doctors' data MUST yield 404.
- **FR-002**: A patient MUST have at most one active waitlist entry.
- **FR-003**: Only Active patients MUST be addable.
- **FR-004**: Portal patients MUST be able to view, join and leave their own entry, the patient always taken from the token.
- **FR-005**: Doctor-only routes MUST reject patient tokens.
- **FR-006**: When an active future appointment becomes Cancelled or deleted and its start is a currently bookable open slot, the system MUST email offers to the earliest-joined waiting patients, in join order, up to a configurable cap (default 5), skipping patients without a valid email.
- **FR-007**: Each offer MUST carry a random single-purpose token stored only as a hash, expiring after a configurable period (default 24 hours) and never later than the slot start; a patient MUST NOT receive two offers for the same slot.
- **FR-008**: Claiming MUST use the existing atomic online-booking path so that availability, lead time, clashes and per-patient limits are enforced and double booking is impossible.
- **FR-009**: Unknown, expired, used or otherwise invalid tokens MUST produce one uniform 404; a valid token for a taken slot MUST produce a conflict.
- **FR-010**: Public routes MUST be rate limited and take the token in a POST body.
- **FR-011**: Offer emails MUST contain no clinical or reason data: only the patient first name, doctor name, date and time, and the links; sent in Spanish and English.
- **FR-012**: Staff and portal waitlist changes, and claim bookings, MUST be recorded via the audit log.
- **FR-013**: All user-facing messages and pages MUST exist in Spanish (primary) and English.
- **FR-014**: Failure to send an offer MUST NOT fail or roll back the cancellation.

### Key Entities

- **WaitlistEntry**: a patient waiting for their doctor; status Waiting, Booked or Removed; join time.
- **WaitlistOffer**: one emailed offer of a specific slot to an entry, with hashed token, expiry and outcome.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A cancelled bookable slot reaches every eligible waiting patient up to the cap within one request of the cancellation.
- **SC-002**: Under concurrent claims of one slot, exactly one appointment is ever created.
- **SC-003**: 100% of invalid, expired or used tokens are indistinguishable to the caller.
- **SC-004**: A staff member can add a patient to the waitlist in under 30 seconds.

## Assumptions

- Each patient belongs to one doctor, so the waitlist is per doctor with no clinic layer.
- Slots are the existing 30-minute online-booking slots; only the slot starting at the cancelled appointment's start time is offered.
- Offers are advisory: no slot is held; the atomic booking decides the winner.
- Offers are email-only; a patient who also uses the portal can leave there.
- The reminder and booking emails link to the client origin from configuration, as existing emails do.
