# Data Model

**NoteTemplate** (extends BaseEntity: Id, CreatedAt, UpdatedAt, IsDeleted)
- DoctorId (string, required, from token)
- Name (string, required, trimmed, max 100, unique per doctor case-insensitive, not "SOAP")
- Body (string, required, max 5000)

Global query filter `!IsDeleted`; index on DoctorId. No relation to MedicalNote.

Built-in (not stored): Name "SOAP", body with Subjective / Objective / Assessment / Plan headings.
