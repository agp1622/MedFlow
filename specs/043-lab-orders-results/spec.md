# Feature Specification: Lab Orders and Results

**Feature Branch**: `claude/issue-21-lab-orders` (caller-specified; spec folder `043-lab-orders-results`)

**Created**: 2026-10-03

**Status**: Draft

**Input**: User description: "Lab orders and results (GitHub issue #21). As a doctor, I want to order labs and record or attach results with abnormal values flagged so that results don't get lost. Acceptance: manual result entry first (HL7/FHIR later); abnormal values flagged on the patient page. Refs #21"

## Clarifications

### Session 2026-10-03

- Q: Should lab data be shared with the patient through the portal? → A: No. Doctor-private only (stricter privacy); sharing is future work.
- Q: Can a doctor edit or delete results of a Completed order? → A: Yes, to correct mistakes, but each change is audited (field names only) and the flag is recomputed.
- Q: Does a Cancelled order still show on the patient page, and do its results count toward the abnormal count? → A: It is shown (greyed, with status), cannot receive new results, and any values it already holds are excluded from the abnormal count.
- Q: Is a result value without a reference range ever flagged? → A: No. Never infer ranges; such values show "no range" with no flag.
- Q: Does opening the Labs section on the patient page create an audit event? → A: Yes, one "view" event per lab list request, consistent with other patient sections.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Order a lab for a patient (Priority: P1)

A doctor opens one of their patients and records a lab order: the name of the test (for example "Hemoglobin A1c" or "Lipid panel"), an optional note, and the date ordered. The order appears in a "Labs" section of the patient page with status "Ordered", so a pending test is never forgotten.

**Why this priority**: Without orders there is nothing to attach results to; tracking pending orders is the "results don't get lost" value.

**Independent Test**: As a doctor, create an order for an owned patient and see it listed as Ordered; try the same for another doctor's patient and get "not found".

**Acceptance Scenarios**:

1. **Given** a doctor viewing their own patient, **When** they create a lab order with a test name, **Then** it is listed on the patient page with status Ordered, the ordered date and their note.
2. **Given** a doctor, **When** they submit an order with an empty or over-long test name or note, **Then** it is rejected with a validation message and nothing is saved.
3. **Given** a doctor, **When** they try to create, read, edit or delete an order for a patient who is not theirs (or an order id that is not theirs), **Then** the response is identical to "not found".
4. **Given** an existing order, **When** the doctor cancels it, **Then** its status becomes Cancelled and it can no longer receive results.

---

### User Story 2 - Record results manually with abnormal flagging (Priority: P1)

For an order, the doctor enters one or more result values. Each value has an analyte name, a numeric value, a unit, and optionally a reference range (low and/or high) typed by the doctor from the lab report. When a value is below the low bound or above the high bound it is flagged abnormal (Low / High). The order becomes Completed once results are recorded. The system never supplies or assumes reference ranges: with no range entered, the value is shown with no flag.

**Why this priority**: This is the core of the issue's acceptance criteria.

**Independent Test**: Record a result of 12.0 with range 4.0 to 10.0 and see it flagged High; record 7.0 in the same range and see no flag; record a value with no range and see no flag.

**Acceptance Scenarios**:

1. **Given** an Ordered lab order, **When** the doctor adds result values with analyte, value, unit and a reference range, **Then** the values are saved, the order shows Completed, and each value outside its range is marked abnormal (High or Low).
2. **Given** a value with no reference range entered, **When** it is saved, **Then** it is displayed without any abnormal flag.
3. **Given** a range where low is greater than high, **When** submitted, **Then** it is rejected with a validation message.
4. **Given** a saved result, **When** the doctor corrects it, **Then** the flag is recomputed from the corrected value and range.
5. **Given** a value exactly equal to a bound, **When** saved, **Then** it is not flagged (bounds are inclusive).

---

### User Story 3 - See abnormal values on the patient page (Priority: P1)

On the patient page, the Labs section lists orders newest first, with results beneath each order. Abnormal values are visually marked (colour plus a text label, not colour alone) and the Labs section header shows a count of abnormal values so the doctor notices them at a glance.

**Why this priority**: Second acceptance criterion of the issue.

**Independent Test**: With one abnormal and one normal result recorded, open the patient page and confirm only the abnormal one carries the marker and the count is 1.

**Acceptance Scenarios**:

1. **Given** a patient with abnormal results, **When** the doctor opens the patient page, **Then** abnormal values are marked High or Low and the section shows how many are abnormal.
2. **Given** a patient with no lab orders, **When** the page is opened, **Then** an empty-state message is shown.
3. **Given** the interface language is Spanish (default) or English, **When** the Labs section is shown, **Then** all labels, statuses and flags appear in that language.

---

### User Story 4 - Keep lab data private and audited (Priority: P1)

Lab orders and results are visible only to the doctor who owns the patient. Patients (portal) and other doctors cannot see or change them. Every view and every change of lab data is recorded in the patient's audit log using the existing audit mechanism.

**Why this priority**: Patient-data privacy and HIPAA-style audit are binding constraints.

**Independent Test**: Another doctor and a portal patient are refused on every lab route; the owning doctor's views and changes appear in the audit log.

**Acceptance Scenarios**:

1. **Given** a patient portal token, **When** any lab route is called, **Then** access is refused.
2. **Given** a doctor who does not own the patient, **When** any lab route is called, **Then** "not found" is returned and nothing is audited against that patient.
3. **Given** the owning doctor lists, creates, edits, records results for, cancels or deletes lab data, **Then** one audit event per request is recorded (view for reads, change for writes) naming the item kind "lab order"; if the audit event cannot be stored the request fails.
4. **Given** lab values appear in the audit log, **Then** only the changed field names are shown, never the values.

---

### User Story 5 - Attach a result document (Priority: P3, deferred)

Attaching a scanned lab report to a specific order is not built in this feature. The existing attachments feature stays attached to the patient as today, so a doctor can already upload a lab report PDF there; linking it to a specific order is deferred.

**Independent Test**: None; documented scope decision.

### Edge Cases

- Value and range are decimals, possibly negative or zero; very large magnitudes are rejected.
- A qualitative result (for example "Positive") is out of scope; values are numeric only.
- Deleting an order removes its results from view (soft delete, consistent with other patient data) and is audited.
- Results cannot be added to a Cancelled order; adding to a Completed order is allowed (additional analytes) up to a cap.
- A deleted patient makes their orders unreachable like other patient data.
- Only a low bound or only a high bound is valid; each bound is checked on its own.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Doctors MUST be able to create a lab order for one of their own patients with a test name (required, up to 150 characters), an optional note (up to 1000 characters) and an ordered date (defaults to today, may not be in the future).
- **FR-002**: An order MUST have a status of Ordered, Completed or Cancelled; it starts as Ordered.
- **FR-003**: Doctors MUST be able to add, edit and remove result values on an order that is not Cancelled. Each value has an analyte name (required, up to 100 characters), a numeric value, a unit (optional, up to 30 characters) and optional reference range low and high bounds.
- **FR-004**: A result value MUST be flagged Low when below the entered low bound, High when above the entered high bound, and otherwise unflagged. The system MUST NOT supply, infer or default any reference range. Bounds are inclusive. Flags are computed on the server so every consumer sees the same answer.
- **FR-005**: Entering a low bound greater than the high bound MUST be rejected.
- **FR-006**: An order MUST become Completed when it has at least one result value and MUST return to Ordered if its last result value is removed. Doctors MUST be able to cancel an Ordered or Completed order (terminal: a Cancelled order cannot be edited, reopened or receive result changes, and attempts return a conflict) and delete an order in any status.
- **FR-007**: The patient page MUST show a Labs section listing the patient's orders newest first with their results, visibly marking abnormal values with a text label in addition to colour, and showing the count of abnormal values.
- **FR-008**: A patient MUST NOT have more than 100 lab orders, and an order MUST NOT have more than 50 result values.
- **FR-009**: All lab routes MUST be doctor-only, scoped to the doctor's own patients; a patient not owned, or an order not owned, MUST yield "not found" indistinguishable from a missing record. Patient (portal) tokens MUST be refused, and lab data MUST NOT appear in any portal response.
- **FR-010**: Every view and change of lab data MUST be recorded through the existing audit service with a new item kind for lab orders, never including result values, and a failure to record MUST fail the request.
- **FR-011**: The data model MUST be delivered with a database migration in the same change.
- **FR-012**: All new client text MUST exist in Spanish and English following the existing localization approach.
- **FR-013**: Attaching documents to an order is out of scope (see User Story 5); no HL7/FHIR import is built.

### Key Entities

- **Lab order**: belongs to one patient and one doctor; test name, note, ordered date, status; has zero or more result values.
- **Lab result value**: belongs to one lab order; analyte name, numeric value, unit, optional reference low and high; abnormal flag (None, Low, High) derived from value and the entered range.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A doctor can create an order and record a first result for it in under 2 minutes.
- **SC-002**: 100% of values outside an entered reference range are shown as abnormal on the patient page, and 0% of values with no range are flagged.
- **SC-003**: 0 lab records are reachable by another doctor or by a patient in any tested route.
- **SC-004**: 100% of lab views and changes by a doctor produce an audit event.
- **SC-005**: Every new on-screen label is available in both Spanish and English.

## Assumptions

- Doctor role only; there is no clinic-owner/staff sharing model for labs in this feature, matching other doctor-private patient data.
- Reference ranges are per result value because they vary by lab, age and sex; the doctor copies them from the lab report.
- Results are numeric only; qualitative results and HL7/FHIR integration are later work.
- Lab data is never exposed through the patient portal; sharing with patients is a later decision.
- Existing attachments are patient-level and stay unchanged; no order-specific attachment link is added (smallest safe choice).
- Spec folder is numbered 043 (next free number); the working branch name was set by the caller and is intentionally not the folder name.
