# Feature Specification: Vitals Trends

**Feature Branch**: `claude/issue-20-vitals-trends` (caller-mandated name; spec folder `042-vitals-trends`)

**Created**: 2026-10-03

**Status**: Draft

**Input**: User description: "GitHub issue #20 - As a doctor, I want charts of BP, weight, glucose and similar vitals over time so that I can spot trends. Acceptance: line charts per vital with date range filter."

## Clarifications

### Session 2026-10-03

- Q: Where does the trends view live? → A: In the patient record's existing vitals area (no new page or route), keeping the existing doctor-only patient access.
- Q: Should charts show normal ranges or abnormal highlighting? → A: No; out of scope (no clinical thresholds are defined; avoids implied clinical advice).
- Q: Which day boundaries does the date range use? → A: The doctor's local calendar days, inclusive.
- Q: Add glucose (new field and migration)? → A: No; out of scope, flagged as unmet part of the issue's wording.
- Q: Export/print of charts? → A: Out of scope.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See each vital as a line chart over time (Priority: P1)

A doctor opens a patient's record and sees, alongside the latest vitals, a trends view with one line chart per vital the patient has recorded: blood pressure (systolic and diastolic lines), heart rate, weight, BMI, temperature and oxygen saturation. Each chart plots the recorded values against the date and time they were recorded.

**Why this priority**: This is the core of the issue; without it nothing is delivered.

**Independent Test**: Open a patient with several vitals recorded on different dates and confirm one chart per vital appears with points matching the recorded values in chronological order.

**Acceptance Scenarios**:

1. **Given** a patient with three or more vitals records, **When** the doctor opens the trends view, **Then** a line chart is shown for each vital that has at least one value, with points ordered oldest to newest.
2. **Given** blood pressure values recorded as "120/80", **When** the doctor views the blood pressure chart, **Then** systolic and diastolic are drawn as two distinct, labelled lines.
3. **Given** a vital that has no recorded values for the patient, **When** the trends view loads, **Then** no chart is shown for that vital (no empty or misleading chart).
4. **Given** a patient with no vitals at all, **When** the doctor opens the trends view, **Then** a clear "no vitals recorded" message is shown.

---

### User Story 2 - Filter trends by date range (Priority: P1)

The doctor narrows all charts to a date range, using quick presets (last 30 days, 6 months, 1 year, all time) or a custom start and end date.

**Why this priority**: Explicit acceptance criterion in the issue.

**Independent Test**: Select a range that includes only some of the patient's records and confirm every chart shows only those points.

**Acceptance Scenarios**:

1. **Given** vitals spread over a year, **When** the doctor selects "last 30 days", **Then** all charts show only records from that window.
2. **Given** a custom start and end date, **When** applied, **Then** records on both the start and end dates are included.
3. **Given** a range containing no records, **When** applied, **Then** a "no vitals in this range" message is shown and the range can easily be changed.
4. **Given** an end date earlier than the start date, **When** the doctor enters it, **Then** the range is rejected with a clear message and the previous charts remain.

---

### Edge Cases

- A single data point is still displayed (as a visible point) rather than a blank chart.
- Blood pressure text that cannot be read as "systolic/diastolic" is skipped for the chart and does not break the others.
- Records with only some vitals filled in appear only on the relevant charts.
- Dates are shown in the doctor's local time and the active interface language (English/Spanish).
- Many records (hundreds) remain readable and responsive.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST show doctors a trends view for a patient with one line chart per recorded vital: blood pressure (systolic and diastolic), heart rate, weight, BMI, temperature, oxygen saturation.
- **FR-002**: Each chart MUST plot values chronologically against the recording date and show the value and date on hover or focus, with the unit.
- **FR-003**: The system MUST provide a date range filter with presets (30 days, 6 months, 1 year, all) and a custom start/end date, applied to all charts together; default is all time.
- **FR-004**: The date range MUST be inclusive of both boundary days and MUST reject an end date earlier than the start date.
- **FR-005**: The system MUST omit charts for vitals with no values in the selected range and show an empty-state message when nothing is available.
- **FR-006**: Trend data MUST come from the existing vital signs records; no new data collection is introduced.
- **FR-007**: Access MUST remain doctor-only and limited to the doctor's own patients, with viewing audited exactly as the existing vitals view is; patient and other doctors' access MUST be denied.
- **FR-008**: All new text MUST be available in English and Spanish.
- **FR-009**: Glucose is NOT included, because glucose is not currently recorded; adding it is a separate change.

### Key Entities

- **Vital sign record**: An existing dated measurement set for a patient (blood pressure, heart rate, weight, height, BMI, temperature, oxygen saturation). Only read by this feature.
- **Trend series**: A derived, per-vital ordered list of (date, value) points within the selected date range.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A doctor can go from a patient's record to seeing all of that patient's vital trends in one step.
- **SC-002**: Changing the date range updates every chart in under 1 second for patients with up to 500 records.
- **SC-003**: 100% of recorded vitals values within the selected range appear on the matching chart, and none outside it.
- **SC-004**: No change to who can see vitals: 100% of unauthorized access attempts remain denied.

## Assumptions

- The existing vitals data source returns all of a patient's records and is sufficient; filtering happens in the browser, so no backend change is needed.
- Glucose is out of scope (no glucose field exists); flagged to the requester as an unmet part of the issue's wording.
- "Similar vitals" means the vitals already recorded.
- Units: mmHg, bpm, kg, kg/m2, degrees Celsius, percent.
- The spec folder follows the `NNN-name` convention; the branch name was dictated by the requester and differs from the constitution's naming rule.
