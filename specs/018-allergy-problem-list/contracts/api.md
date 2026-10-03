# API Contract: Clinical lists

Base: `/api/patients/{patientId}/clinical`. Auth: JWT with role `Doctor` (patient role: 403, anonymous: 401).
Non-owning doctor, unknown or deleted patient/entry, or entry of another patient: 404 (empty body).
Validation failure: 400 problem details (`errors`). Duplicate allergy substance: 409. List cap exceeded: 400.

| Method | Path | Body | Success |
|---|---|---|---|
| GET | `` | - | 200 `ClinicalSummaryDto {allergies[], problems[], medications[]}` (newest first by id desc) |
| POST | `/allergies` | `{substance, reaction?, severity}` | 201 `AllergyDto` |
| PUT | `/allergies/{id}` | same | 200 `AllergyDto` |
| DELETE | `/allergies/{id}` | - | 204 |
| POST | `/problems` | `{description, icd10Code, status, onsetDate?}` | 201 `ProblemDto` |
| PUT | `/problems/{id}` | same | 200 `ProblemDto` |
| DELETE | `/problems/{id}` | - | 204 |
| POST | `/medications` | `{name, dosage?, frequency?, notes?}` | 201 `MedicationDto` |
| PUT | `/medications/{id}` | same | 200 `MedicationDto` |
| DELETE | `/medications/{id}` | - | 204 |

DTOs: `AllergyDto {id, substance, reaction, severity, createdAt, updatedAt}`,
`ProblemDto {id, description, icd10Code, status, onsetDate, createdAt, updatedAt}`,
`MedicationDto {id, name, dosage, frequency, notes, createdAt, updatedAt}`. Enums serialize as strings.
