# Research

- **Built-in SOAP template**: served by the API as a constant (`GET /api/notetemplates/builtin`, Id 0, `isBuiltIn: true`), not stored in DB. Rationale: no seeding/migration data, cannot be edited or deleted. Alternative (seeded row per doctor) rejected as needless.
- **Uniqueness**: enforced in the repository (case-insensitive compare on trimmed name per doctor, also against built-in name "SOAP") rather than a DB unique index, because `BaseEntity` uses soft delete and an index would block re-using a deleted name.
- **Cross-doctor access**: lookups filter by DoctorId from the token; miss returns 404 for GET/PUT/DELETE.
- **Copy-forward**: `GET /api/medicalnotes/patient/{patientId}/latest` returns the doctor's own latest note (by NoteDate) as `CopyForwardDto`; 404 when none, including unknown patient. Notes are not modified; sharing flag is not carried (new notes default to unshared).
- **Picker overwrite**: client confirms before replacing a non-empty body.
- **Existing issue noted, not fixed**: `MedicalNotesController.Delete` has no owner check (pre-existing, out of scope).
