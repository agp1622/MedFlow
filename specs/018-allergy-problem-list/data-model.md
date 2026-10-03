# Data Model: Allergy and Problem List

All three extend `BaseEntity` (Id, CreatedAt, UpdatedAt, IsDeleted soft delete + global query filter) via abstract `ClinicalEntry` with `PatientId` (FK Patient, Restrict) and `DoctorId` (string, owning doctor's user id; FK to Doctor.UserId, Restrict). Index `(PatientId, DoctorId)`.

| Entity | Fields | Rules |
|---|---|---|
| PatientAllergy | Substance (req, 1-200), Reaction (opt, <=500), Severity (req enum: Mild, Moderate, Severe, LifeThreatening, stored string) | Substance unique per patient, case-insensitive, among non-deleted |
| PatientProblem | Description (req, 1-200), Icd10Code (req, <=8, normalised upper case), Status (req enum: Active, Resolved), OnsetDate (opt DateOnly, not in future) | |
| PatientMedication | Name (req, 1-200), Dosage (opt, <=100), Frequency (opt, <=100), Notes (opt, <=500) | |

Cap: 100 non-deleted entries per type per patient. `Patient` gains no columns and no navigation changes.
