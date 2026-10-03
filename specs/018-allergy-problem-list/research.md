# Research: Allergy and Problem List

- **Reuse of Patient.Allergies**: Decision: keep untouched, display read-only. Rationale: free-text user data; spec forbids silent overwrite. Alternative: auto-migrate text into entries (rejected: unsafe parsing, overwrite risk).
- **Medications vs Prescription**: Decision: new `PatientMedication`. Rationale: Prescription carries expiry/refill/issue semantics and is billed/issued data; "current meds" includes non-prescribed items. Alternative: derive from active prescriptions (rejected: can't edit, conflates concepts).
- **One table vs three**: Decision: three entities with a shared abstract base class. Rationale: different fields; shared ownership/scoping logic.
- **Validation**: Decision: DataAnnotations on request records (auto 400 via `[ApiController]`), plus controller checks for ICD-10 normalisation, future onset date, duplicate allergy and per-list cap. Alternative: FluentValidation (new dependency, rejected).
- **ICD-10 format**: `^[A-Z][0-9][A-Z0-9](\.[A-Z0-9]{1,4})?$` after trim and upper-casing. Format only.
- **Ownership**: Decision: check patient ownership first (404), then load entry by (id, patientId, doctorId) (404). Same status for missing and foreign.
- **Enums**: Nullable required enums in requests so a missing value is a 400 rather than silently the first enum member; stored as strings.
- **Migration**: generated with `dotnet ef`, reviewed to contain only CreateTable/CreateIndex in `Up`.
