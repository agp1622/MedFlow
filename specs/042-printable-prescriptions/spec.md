# Feature Specification: Printable Prescriptions

**Feature Branch**: `claude/issue-22-printable-prescriptions` (branch name fixed by the requester; the spec folder is `042-printable-prescriptions`)

**Created**: 2026-10-03

**Status**: Draft

**Input**: User description: "Printable prescriptions (GitHub issue #22). As a doctor, I want to print a signed prescription PDF so that patients can take it to any pharmacy. Acceptance: the PDF includes doctor and patient details and a signature block. Real e-prescribing (Surescripts) is a later phase and out of scope."

## Clarifications

### Session 2026-10-03

- Q: Should prescriptions that are expired or not active be printable? → A: Yes (stricter variant of "block" not chosen, to allow records/reprints), but the page carries a prominent "NOT VALID" banner with the status, so it cannot pass as a valid prescription at a pharmacy.
- Q: Which identifier is recorded in the audit log for a print? → A: The prescription's own id and its patient id, action View, item kind Prescription (not a list-level event with no item id).
- Q: Does the response reveal why a request was refused for another doctor's prescription? → A: No. Missing prescription and other doctor's prescription both return the same empty "not found" response.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Download a printable prescription (Priority: P1)

A doctor viewing a prescription for one of their own patients chooses to print it. They receive a single-page PDF document containing the doctor's details, the patient's details, the medication details and a blank signature block. They can sign it by hand and give it to the patient, who can take it to any pharmacy.

**Why this priority**: This is the whole feature and the issue's primary acceptance criterion.

**Independent Test**: Sign in as a doctor with a prescription for one of their patients, use the print action, and confirm a PDF opens/downloads containing doctor name, specialty, licence number and phone, patient name, date of birth and contact details, drug, dosage, frequency, instructions, issue and expiry dates, refills, and a signature line with the doctor's printed name and a date line.

**Acceptance Scenarios**:

1. **Given** a doctor with an active prescription for their patient, **When** they request the printable prescription, **Then** they receive a PDF that includes the doctor details, patient details, medication details and a signature block.
2. **Given** a prescription whose optional fields (instructions, doctor licence number or phone, patient address) are empty, **When** the PDF is generated, **Then** it still renders correctly and omits the missing fields without errors.
3. **Given** a prescription that is expired, cancelled or otherwise not active, **When** the PDF is generated, **Then** the document clearly shows its status so it is not mistaken for a valid prescription.
4. **Given** the doctor uses the print action in the web app, **When** the PDF is ready, **Then** the browser downloads or opens it so it can be printed.

---

### User Story 2 - Access control and audit trail (Priority: P1)

Prescriptions contain patient health information. Only prescriptions of the doctor's own patients can be printed, and every print is recorded in the existing audit log as a view of that prescription.

**Why this priority**: Patient-data protection is binding under the project constitution.

**Independent Test**: Request the PDF as a different doctor, as a patient, and unauthenticated; verify refusal and that the audit log records a view only for successful generations.

**Acceptance Scenarios**:

1. **Given** a prescription belonging to another doctor's patient, **When** a doctor requests its PDF, **Then** the response is "not found", identical to a prescription that does not exist, and no document is produced.
2. **Given** a patient-role or unauthenticated caller, **When** they request the PDF, **Then** access is refused.
3. **Given** a successful PDF generation, **When** the audit log is reviewed, **Then** a View event for that prescription and patient by that doctor is present.
4. **Given** a refused or not-found request, **When** the audit log is reviewed, **Then** no view event exists for it.

---

### Edge Cases

- Very long drug names, instructions or addresses must wrap and not overflow the page; the content must stay within one page for any value the system already accepts.
- Names with non-English or accented characters (e.g. Spanish, which the app supports) must render correctly.
- A prescription id that does not exist returns "not found".
- Printing the same prescription repeatedly produces a new audit view event each time.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST generate a printable PDF for a single prescription on request.
- **FR-002**: The PDF MUST include the prescribing doctor's details: full name, specialty, licence number (when on file) and phone (when on file).
- **FR-003**: The PDF MUST include the patient's details: full name, date of birth, contact phone and address (when on file).
- **FR-004**: The PDF MUST include the prescription details: drug, dosage, frequency, instructions (when present), issue date, expiry date, refills remaining and status.
- **FR-004a**: A prescription that is expired (expiry date before today) or whose status is neither Active nor Expiring Soon (i.e. Expired or Cancelled) MUST be printable but MUST display a prominent "NOT VALID" banner stating the reason (expired or its status).
- **FR-005**: The PDF MUST include a signature block: a blank line for the doctor's handwritten signature, the doctor's printed name and a date line. The system MUST NOT claim to be, or embed, an electronic or digital signature.
- **FR-006**: Only authenticated doctors MAY request the PDF; patient-role and unauthenticated callers MUST be refused.
- **FR-007**: A doctor MUST only be able to generate PDFs for prescriptions belonging to their own patients; any other request MUST return "not found", indistinguishable from a missing prescription.
- **FR-008**: Each successful generation MUST be recorded in the existing audit log as a View of the prescription (carrying the prescription id) for that patient; refused or not-found requests MUST NOT create such events.
- **FR-009**: The doctor-facing prescriptions screen MUST offer a print/download action per prescription, with English and Spanish labels, and a visible error if generation fails.
- **FR-010**: The PDF MUST NOT contain data beyond what is listed above (no insurance, notes, allergies or attachments).
- **FR-011**: Responses MUST be marked non-cacheable so the document is not stored by shared caches.

### Key Entities

- **Prescription**: existing record of a drug prescribed to a patient by a doctor; the source of the document.
- **Doctor**: existing prescriber profile (name, specialty, licence number, phone).
- **Patient**: existing patient record (name, date of birth, contact, address).
- **Audit event**: existing log entry; a View is added for each print.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A doctor can go from the prescriptions list to a downloaded PDF in a single action.
- **SC-002**: 100% of generated documents contain doctor, patient, medication and signature-block sections.
- **SC-003**: 0 documents are produced for prescriptions outside the requesting doctor's patients, and every refused request is indistinguishable from "not found".
- **SC-004**: 100% of successful prints appear in the audit log as a view.
- **SC-005**: The document is generated and delivered in under 3 seconds under normal load.

## Assumptions

- Out of scope: real e-prescribing (Surescripts), electronic/digital signatures, emailing or faxing the PDF, patient-portal access to the PDF, controlled-substance rules and pharmacy selection.
- The signature is a wet signature applied by hand to the printed page.
- The document language is English; labels are fixed, user data is rendered as stored.
- Printing is allowed for any prescription status; the status is shown on the document.
- Existing prescription, doctor and patient data are reused; no new stored data is required.
- Content is laid out to print acceptably on either A4 or Letter.
