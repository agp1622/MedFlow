# Feature Specification: Structured Note Templates

**Feature Branch**: `claude/issue-16-note-templates` (spec folder `016-note-templates`; branch name chosen by the requester, see Assumptions)
**Created**: 2026-10-03
**Status**: Draft
**Input**: GitHub issue #16 "Structured SOAP note templates": As a doctor, I want reusable note templates (SOAP, specialty-specific) so that charting is faster and consistent. Acceptance: create, edit and pick templates when writing a note; copy-forward from the last visit.

## Clarifications

### Session 2026-10-03

- Q: When a template is picked and the body already has text, what happens? -> A: The doctor is asked to confirm replacement; cancelling keeps the text.
- Q: Which prior notes may copy-forward draw from? -> A: Only the requesting doctor's own latest note for that patient (stricter option; other doctors' notes are never used).
- Q: Does a saved note remember which template it came from? -> A: No; notes are independent plain notes.
- Q: Are templates shared across a clinic? -> A: No; private per doctor.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Pick a template when writing a note (Priority: P1)

A doctor writing a medical note for a patient chooses a template (the built-in SOAP template or one of their own) and the note body is pre-filled with the template's section headings, ready to be completed and saved.

**Why this priority**: This is the core charting speed-up and consistency benefit.

**Independent Test**: Open the new-note form for a patient, pick "SOAP", confirm the body is filled with the Subjective / Objective / Assessment / Plan headings, complete and save; the saved note contains that text.

**Acceptance Scenarios**:

1. **Given** a doctor on the new-note form, **When** they open the template picker, **Then** they see the built-in SOAP template plus every template they have created.
2. **Given** a template is chosen, **When** the body is empty, **Then** it is filled with the template body.
3. **Given** the doctor has already typed text, **When** they choose a template, **Then** the existing text is not silently lost (they must confirm replacement, or the template is only inserted when the body is empty).
4. **Given** a note saved from a template, **When** it is viewed later, **Then** it is an ordinary note; later edits to the template do not change it.

---

### User Story 2 - Create and edit my own templates (Priority: P1)

A doctor creates reusable templates (for example a specialty-specific cardiology follow-up), edits their name or body, and deletes ones they no longer need.

**Why this priority**: Required by the issue ("create, edit ... templates"); makes specialty-specific templates possible.

**Independent Test**: Create a template with a name and body, see it in the list, edit it, delete it.

**Acceptance Scenarios**:

1. **Given** a doctor, **When** they save a template with a name and body, **Then** it appears in their template list and the picker.
2. **Given** an existing own template, **When** they edit name or body, **Then** the change is saved and used for future notes only.
3. **Given** an own template, **When** they delete it, **Then** it no longer appears; existing notes are unaffected.
4. **Given** the built-in SOAP template, **When** a doctor tries to edit or delete it, **Then** this is not allowed; they may create their own copy instead.
5. **Given** a doctor, **When** they request or modify another doctor's template, **Then** it behaves as if it does not exist.

---

### User Story 3 - Copy forward from the last visit (Priority: P2)

When writing a note for a patient who already has notes written by this doctor, the doctor can choose "Copy from last visit" to pre-fill the body with the content of that doctor's most recent note for that patient, then edit it.

**Why this priority**: Explicit acceptance criterion; speeds up follow-up charting. Depends on nothing else.

**Independent Test**: For a patient with a prior note, click copy-forward on a new note; the body contains the prior note's text; saving creates a new, separate note and leaves the prior one unchanged.

**Acceptance Scenarios**:

1. **Given** the doctor has a previous note for the patient, **When** they choose copy-forward, **Then** the body is filled with the most recent such note's content.
2. **Given** no previous note by this doctor for the patient, **When** the form loads, **Then** copy-forward is unavailable or reports that there is nothing to copy.
3. **Given** another doctor's notes or another patient's notes, **When** copy-forward is used, **Then** they are never returned.
4. **Given** a copied-forward note is saved, **Then** it is created unshared with the patient, regardless of the source note's sharing state.

---

### Edge Cases

- Template name empty or whitespace, or duplicate of another of the doctor's template names: rejected with a clear message (duplicates are rejected case-insensitively).
- Template name or body exceeding the limits: rejected.
- Template body empty: rejected.
- Patient token (portal user) calling any template or copy-forward endpoint: rejected as forbidden.
- Copy-forward for a patient ID the doctor has no notes for or that does not exist: same "nothing to copy" result, no information about other doctors' data.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide one built-in, read-only SOAP template (Subjective, Objective, Assessment, Plan headings) available to all doctors.
- **FR-002**: A doctor MUST be able to create, list, edit and delete their own templates, each with a name (max 100 characters) and a body (max 5000 characters).
- **FR-003**: Templates MUST be private to the doctor who created them; other doctors and patients cannot see, edit or delete them.
- **FR-004**: Template names MUST be unique per doctor, case-insensitive, and cannot match the built-in template's name.
- **FR-005**: The new-note form MUST let the doctor pick a template and pre-fill the body, without silently overwriting existing text.
- **FR-006**: The system MUST let the doctor retrieve the content of their own most recent note for a given patient for copy-forward; the result MUST be limited to notes authored by that doctor.
- **FR-007**: Notes created from a template or by copy-forward MUST be ordinary independent notes, not shared with the patient by default.
- **FR-008**: Template and copy-forward operations MUST be restricted to doctor accounts; patient accounts are rejected.
- **FR-009**: Template list responses MUST be paged consistent with other list endpoints.

### Key Entities

- **Note Template**: owned by one doctor; name, body, created/updated timestamps.
- **Medical Note** (existing): unchanged; templates only pre-fill its content.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A doctor can start a structured note from a template in two interactions (open picker, choose).
- **SC-002**: A doctor can create a new template in under one minute.
- **SC-003**: 100% of attempts by another doctor or a patient to read or change a doctor's template are refused or indistinguishable from "not found".
- **SC-004**: Copy-forward never returns content from another doctor's or another patient's notes.

## Assumptions

- The issue's "specialty-specific" templates are satisfied by doctor-authored templates plus the built-in SOAP one; no further built-in specialty library or admin/shared clinic templates are included.
- Templates are plain text with headings; no structured fields, placeholders or variables.
- Copy-forward copies the text of the doctor's own latest note for the patient (the existing note model has no link to appointments, so "last visit" means the latest note). Notes are not versioned and no link to the source is stored.
- Existing note storage and note sharing behaviour are unchanged.
- The requester specified the branch name `claude/issue-16-note-templates` instead of the usual `NNN-feature-name` branch; the spec folder uses `016-note-templates`.
