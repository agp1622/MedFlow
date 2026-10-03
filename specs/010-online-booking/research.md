# Research

- **Double-booking prevention**: Decision: perform the overlap check and insert in one repository method guarded by a per-doctor in-process lock, plus a Serializable transaction when the provider is relational. Rationale: appointments can have arbitrary start/duration (doctor-created), so a unique index on start time cannot express overlap and would change existing behaviour; InMemory (tests) does not support transactions. Alternatives: unique filtered index (rejected: breaks existing data/behaviour), optimistic row version on a slot table (rejected: new slot table, extra scope).
- **"Scheduled" status**: maps to existing `Pending`; adding a status would touch enum, client badges, and reminders for no gain.
- **Time zone**: UTC, as existing `ScheduledAt` handling; availability stored as `TimeOnly` interpreted as UTC.
- **Doctor selection**: the patient's assigned doctor (`Patient.DoctorId`); avoids a doctor directory and cross-doctor leakage.
- **Slot generation**: computed on the fly from windows minus blocked dates minus overlapping non-cancelled appointments; nothing materialised.
