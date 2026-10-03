# Feature Specification: Allergy and Problem List

**Feature Branch**: `claude/next-medflow-issue-slda0y` (spec folder `018-allergy-problem-list`; branch fixed by the requester)

**Created**: 2026-10-03

**Status**: Draft

**Input**: GitHub issue #18 (Refs #18): "As a doctor, I want structured allergies, active problems (ICD-10) and current medications on the patient summary so that I see critical information at a glance." Acceptance: allergies, problems and meds editable on the patient page; shown prominently on the patient summary.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See critical clinical info at a glance (Priority: P1)

A doctor opens a patient's page and immediately sees, in a prominent panel at the top of the Overview, the patient's allergies (with severity), active problems (with ICD-10 code) and current medications, without opening any tab.

**Why this priority**: This is the core value of the issue: allergy and problem information is safety-critical.

**Independent Test**: Seed one allergy, one problem and one medication for a patient, open the patient page, and confirm all three appear in the summary panel; for a patient with none, confirm an explicit "none recorded" state.

**Acceptance Scenarios**:

1. **Given** a patient with recorded allergies, active problems and medications, **When** the owning doctor opens the patient page, **Then** all three lists are shown prominently, severe allergies visually emphasised.
2. **Given** a patient with nothing recorded, **When** the doctor opens the page, **Then** each section shows an explicit "none recorded" state (not blank).
3. **Given** a problem marked resolved, **When** the summary is shown, **Then** it is not listed among active problems.
4. **Given** the patient already has legacy free-text allergies, **When** the summary is shown, **Then** that text is displayed read-only as "Legacy allergy notes" next to the structured list and is never altered by this feature.

---

### User Story 2 - Maintain allergies, problems and medications (Priority: P1)

The doctor adds, edits and removes allergies (substance, reaction, severity), problems (description, ICD-10 code, status active/resolved) and medications (name, dosage, frequency) from the patient page.

**Why this priority**: The issue requires these to be editable on the patient page.

**Independent Test**: Add, edit and remove one entry of each kind through the page and confirm the summary reflects each change.

**Acceptance Scenarios**:

1. **Given** the patient page, **When** the doctor adds an allergy, problem or medication with valid values, **Then** it is saved and appears in the summary.
2. **Given** an existing entry, **When** the doctor edits it, **Then** the changes persist.
3. **Given** an existing entry, **When** the doctor removes it after confirming, **Then** it no longer appears.
4. **Given** a problem with an invalid ICD-10 code (e.g. "ZZZ"), **When** saved, **Then** it is rejected with a clear message and nothing is stored.
5. **Given** over-long or empty required text, **When** saved, **Then** it is rejected with a clear message.

---

### User Story 3 - Privacy of clinical lists (Priority: P1)

These lists are doctor-facing and private to the owning doctor.

**Why this priority**: Patient health data; constitution principle IV.

**Independent Test**: With two doctors and a portal patient, verify other doctors get 404 and patient tokens are rejected.

**Acceptance Scenarios**:

1. **Given** a patient owned by doctor A, **When** doctor B reads or changes that patient's lists (or any entry by id), **Then** the response is "not found" and nothing changes.
2. **Given** a patient with portal access, **When** the patient's token calls these endpoints, **Then** access is refused, and none of this data appears in any portal response.

### Edge Cases

- Entry id belongs to a different patient than the one in the URL: not found.
- Deleted (removed) entries never reappear; deleting a patient hides its lists.
- ICD-10 entered in lower case or with surrounding whitespace is normalised to upper case before validation.
- Duplicate allergy substances for one patient are allowed to be rejected as duplicates (case-insensitive) to avoid conflicting entries.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST let the owning doctor list, add, edit and remove structured allergies (substance, reaction, severity: Mild, Moderate, Severe, LifeThreatening) for a patient.
- **FR-002**: The system MUST let the owning doctor list, add, edit and remove problems (description, ICD-10 code, status Active or Resolved, optional onset date).
- **FR-003**: The system MUST let the owning doctor list, add, edit and remove current medications (name, dosage, frequency, optional notes).
- **FR-004**: ICD-10 codes MUST be validated for format (letter, two alphanumerics, optional dot and 1 to 4 alphanumerics) and normalised to upper case.
- **FR-005**: Text fields MUST have maximum lengths; required fields MUST be non-blank; invalid input MUST be rejected with a validation error.
- **FR-006**: The patient page MUST show a prominent summary of allergies, active problems and medications, with explicit empty states and emphasis on Severe/LifeThreatening allergies.
- **FR-007**: The system MUST NOT modify or overwrite `Patient.Allergies`, `Patient.Notes`, `Patient.PrimaryCondition` or any other existing user-entered text as a result of this feature. Legacy allergy text is shown read-only alongside the structured list. Removing an entry requires explicit confirmation in the UI.
- **FR-008**: All data MUST be scoped to the owning doctor; non-owners MUST receive "not found" for patients and entries.
- **FR-009**: Patient-role (portal) tokens MUST be refused, and this data MUST NOT appear in any portal response.
- **FR-010**: Removed entries MUST be soft-deleted, consistent with other clinical data.
- **FR-011**: The schema change MUST be additive only (new tables, no drops or alterations of existing data).

### Key Entities

- **Allergy**: substance, reaction, severity; belongs to one patient and doctor.
- **Problem**: description, ICD-10 code, status (Active/Resolved), optional onset date; belongs to one patient and doctor.
- **Medication**: name, dosage, frequency, notes; a doctor-maintained current medication list, distinct from issued prescriptions.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A doctor sees all allergies, active problems and medications on opening the patient page, with no extra clicks.
- **SC-002**: A doctor can add a new entry of any kind in under 30 seconds.
- **SC-003**: 100% of attempts by non-owning doctors or patient accounts to read or change these lists are refused.
- **SC-004**: 0 existing free-text patient fields are changed by using this feature.

## Assumptions

- Existing prescriptions are a separate issued-Rx record; "current medications" is a separate doctor-maintained list. No automatic sync, to avoid silent data changes.
- ICD-10 validation is format-only; no code catalogue lookup is included.
- Patient.Allergies legacy text stays as is; migration of legacy text into structured entries is manual and out of scope.
- Portal exposure of these lists is out of scope (doctor-facing only).
- Existing doctor-scoping and soft-delete patterns are reused.
