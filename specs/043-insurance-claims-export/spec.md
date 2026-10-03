# Feature Specification: Insurance Details and Claims Export

**Feature Branch**: `claude/issue-26-insurance-claims` (branch name fixed by the requester; the spec folder is `043-insurance-claims-export`)

**Created**: 2026-10-03

**Status**: Draft

**Input**: User description: "Insurance details and claims export (GitHub issue #26, Refs #26). As a biller I want to store each patient's insurance and export claims (CMS-1500 / 837) so that I can get reimbursed. Acceptance: insurance fields on the patient; claim export from an invoice."

## Scope Interpretation (read first)

The issue says "CMS-1500 / 837". MedFlow holds no procedure codes (CPT/HCPCS), diagnosis codes (ICD-10), provider NPI or tax ID, so a payer-ready claim cannot honestly be produced. The minimal defensible interpretation is a clearly labelled **DRAFT claim worksheet**: the data MedFlow does hold, arranged under the CMS-1500 (02/12) item numbers, as JSON or CSV, together with an explicit list of the required items that are missing.

**What the export is**: a draft of CMS-1500 box data, for a biller to complete and key into a clearinghouse or the paper form.

**What it is not**: it is NOT an official CMS-1500 form (no form image or PDF), NOT an X12 837 file (no ISA/GS/ST envelope, loops or segments; no 837 validation was performed), and NOT validated by any payer or clearinghouse. The issue's "837" wording is therefore **not met** and is out of scope; the export must never be described as 837-conformant.

## Clarifications

### Session 2026-10-03

- Q: Should the export be refused when insurance data is incomplete? → A: No. It returns the draft with a missing-items list, so the biller sees exactly what to complete (refusing would hide the gaps).
- Q: Which invoice statuses can be exported? → A: Any status (Pending, Paid, Overdue, Cancelled), because rebilling and corrections are normal; the invoice status is shown in the draft.
- Q: Are the new insurance fields exposed to the patient portal or to intake review responses? → A: No (stricter option). They are doctor-side only; portal DTOs are unchanged.
- Q: What does the downloaded file name contain? → A: Only the invoice number (for example `claim-draft-INV-0001.json`), never the patient's name.
- Q: What does the audit event record for a claim export? → A: Action View, item kind Invoice, the invoice id and patient id, no values (reuses existing audit vocabulary; no new enum value).
- Q: How is the draft status asserted in machine-readable form? → A: A top-level `status: "DRAFT"` plus `conformance` text stating not an official CMS-1500, not X12 837, not validated; the same lines appear as the first rows of the CSV.
- Q: Are the insurance fields validated for format (for example payer ID pattern)? → A: No format rules, only maximum lengths (group number 100, payer ID 50, subscriber name 200), subscriber date of birth not in the future, relationship from the fixed list. Payer formats vary and wrong rules would reject real data.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Record a patient's insurance details (Priority: P1)

A doctor or biller opens a patient's form and records the full insurance details: provider, policy number, group number, payer ID, the subscriber (policyholder) name and date of birth, and the subscriber's relationship to the patient (Self, Spouse, Child, Other). The details appear on the patient's detail view. The provider and policy number already exist and are already filled by the intake-form accept flow; they are reused, not duplicated.

**Why this priority**: First acceptance criterion and the data the export depends on.

**Independent Test**: Edit a patient, fill the new fields, save, reopen, and see them; accept an intake submission and see provider and policy number still land in the same fields.

**Acceptance Scenarios**:

1. **Given** a doctor's patient, **When** the doctor saves insurance details within the length limits, **Then** they are stored and shown on the patient detail view.
2. **Given** a value exceeding a field's maximum length, an invalid relationship, or a future subscriber date of birth, **When** saving, **Then** the request is rejected with a localized validation message and nothing changes.
3. **Given** a patient with only provider and policy number (existing data), **When** viewed, **Then** the new fields show as empty and nothing breaks.
4. **Given** insurance fields change, **When** saved, **Then** the existing patient audit entry records the changed field names (not values).
5. **Given** another doctor's patient, **When** a doctor tries to read or update it, **Then** the existing not-found behaviour applies.

---

### User Story 2 - Export a draft claim from an invoice (Priority: P1)

From the invoice list, a doctor chooses "Export claim draft" on an invoice and downloads a JSON (default) or CSV file. It is labelled DRAFT, states what it is not, shows patient, insured, payer, billing provider and one service line (from the invoice date, description and amount) under CMS-1500 item numbers, and lists every required item that is missing (for example, no payer, no policy number, and always CPT/HCPCS, ICD-10, NPI and tax ID, which MedFlow does not store).

**Why this priority**: Second acceptance criterion.

**Independent Test**: Export an invoice of a patient with full insurance details as JSON and CSV; check the disclaimer, populated fields and the missing list; export for a patient with no insurance and check the missing list grows.

**Acceptance Scenarios**:

1. **Given** the doctor's own invoice, **When** exporting with default format, **Then** a JSON document is returned containing a draft status, a disclaimer that it is not an official CMS-1500 form, not an X12 837 file and not validated, the item-numbered data, and a missing-fields list.
2. **Given** `format=csv`, **When** exporting, **Then** a CSV file with one row per item (item number, field key, value) and the same disclaimer and missing-fields information is returned.
3. **Given** incomplete insurance data, **When** exporting, **Then** the export still succeeds and the missing-fields list names each gap; CPT/HCPCS, ICD-10, provider NPI and tax ID are always listed as missing.
4. **Given** an unsupported `format`, **When** exporting, **Then** a localized 400 is returned.
5. **Given** the web app, **When** the doctor uses the export action, **Then** the file downloads and a notice tells them it is a draft only.

---

### User Story 3 - Access control, audit and localisation (Priority: P1)

Only doctors can export; only for invoices of their own patients. Every successful export is audited. All UI text and API error messages exist in Spanish and English.

**Independent Test**: Patient token, other doctor's token, anonymous, and unknown id all fail; the audit log shows a View of the invoice for a successful export.

**Acceptance Scenarios**:

1. **Given** a patient (portal) token or no token, **When** requesting the export, **Then** the request is rejected (403/401).
2. **Given** an invoice that does not exist or belongs to another doctor, **When** exporting, **Then** an identical empty 404 is returned and nothing is audited.
3. **Given** a successful export, **Then** an audit View event on the invoice is recorded against the patient, and the response is marked no-store.
4. **Given** the app language is Spanish or English, **Then** labels, the draft notice and API messages appear in that language.

### Edge Cases

- Patient with no insurance at all: export succeeds, missing list is long.
- Invoice with no appointment: service date is the invoice date.
- Subscriber relationship Self with empty subscriber name or date of birth: the insured name and date of birth default to the patient's. Relationship empty: nothing is defaulted and the relationship and insured items are reported as missing.
- Free text containing commas, quotes or newlines, or starting with `=`, `+`, `-`, `@`: CSV is correctly quoted and neutralised against spreadsheet formula injection.
- Soft-deleted patient or invoice: treated as not found.
- Audit store failure: the export fails rather than returning data unaudited.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The patient record MUST additionally store insurance group number, payer ID, subscriber name, subscriber date of birth and subscriber relationship (Self, Spouse, Child, Other), all optional, with maximum lengths; provider and policy number are reused as they are.
- **FR-002**: Patient create, update and detail MUST carry the new fields; the existing intake flow MUST keep working unchanged.
- **FR-003**: Invalid insurance input (over length, unknown relationship, subscriber date of birth in the future) MUST be rejected with a localized message.
- **FR-004**: Doctors MUST be able to export a draft claim for an invoice of their own patient as JSON (default) or CSV.
- **FR-005**: The export MUST state it is a DRAFT, not an official CMS-1500 form, not an X12 837 file and not validated by any payer or clearinghouse, and MUST NOT claim 837 conformance.
- **FR-006**: The export MUST contain the available data under CMS-1500 (02/12) item numbers for patient, insured, payer, billing provider and one service line (date, description, charge) from the invoice.
- **FR-007**: The export MUST list missing required items, and MUST always list CPT/HCPCS, ICD-10 diagnosis, provider NPI and federal tax ID as missing because they are not stored.
- **FR-008**: The export MUST be doctor-only, return an identical empty 404 for a missing or not-owned invoice, record an audit View of the invoice only on success, fail closed if auditing fails, and send `Cache-Control: no-store`.
- **FR-009**: CSV output MUST be safe against formula injection and correctly quoted.
- **FR-010**: UI strings and API error messages MUST exist in Spanish and English; the client MUST use a typed method in the shared API service layer and an export action on the invoice list.
- **FR-011**: Schema change MUST ship as a migration; existing rows remain valid.

### Key Entities

- **Patient (extended)**: gains group number, payer ID, subscriber name, subscriber date of birth, subscriber relationship.
- **Claim draft (computed, not stored)**: disclaimer, status DRAFT, item-numbered data, missing-items list, generated-at time.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A doctor can record complete insurance details for a patient in under 2 minutes.
- **SC-002**: A doctor can download a claim draft from an invoice in at most 2 actions and under 3 seconds.
- **SC-003**: 100% of exports carry the draft and non-conformance statement and a missing-items list.
- **SC-004**: 0 exports are returned for invoices outside the doctor's own patients, and 100% of successful exports appear in the audit log.
- **SC-005**: All new UI text and error messages are available in both Spanish and English.

## Assumptions

- The biller role is the doctor role; MedFlow has no separate biller role and none is added.
- Insurance values are free text; no payer directory or eligibility check.
- One service line per invoice; no CPT/ICD capture, so those items are always reported missing.
- CMS-1500 item numbers follow the 02/12 form; the mapping is a best-effort labelling, not a certified one.
- Insurance details are not exposed through the patient portal.
- Existing invoice update, delete and mark-paid behaviour is unchanged.
- Out of scope: X12 837 generation, PDF/form rendering, clearinghouse submission, CPT/ICD coding, claim status tracking, a biller role.
