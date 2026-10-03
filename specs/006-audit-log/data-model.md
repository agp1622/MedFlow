# Data Model: Audit Log

## AuditEvent (new, table `AuditEvents`, append-only)

| Field | Type | Notes |
|---|---|---|
| Id | int | PK |
| PatientId | int | indexed with OccurredAt; no FK (patient is soft-deleted) |
| DoctorId | string(450) | owning doctor of the patient at event time |
| ActorUserId | string(450) | Identity user id |
| ActorName | string(200) | display name at event time |
| ActorRole | string(20) | Doctor or Patient |
| Action | enum string | View, Change |
| ItemKind | enum string | Patient, Appointment, Prescription, Invoice, VitalSign, Note, Attachment, PortalAccess, AuditLog |
| ItemId | int? | null for patient-level, creates, and section lists |
| ChangedFields | string(500)? | comma-separated property names, changes only |
| OccurredAt | DateTime (UTC) | |

Rules: never updated or deleted; no clinical values stored.
