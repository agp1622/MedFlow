# Data Model

**WaitlistEntry** (BaseEntity): `PatientId`, `DoctorId` (string, copied from the patient at join), `Status` (Waiting | Booked | Removed, string, max 20), `ClosedAt?`. CreatedAt is the join time. Indexes: (DoctorId, Status, CreatedAt), (PatientId, Status). Rule: at most one Waiting entry per patient (checked in the repository; ordering by CreatedAt then Id).

**WaitlistOffer** (BaseEntity): `EntryId`, `SlotStartsAt` (UTC), `TokenHash` (64), `ExpiresAt`, `SentAt?`, `ClaimedAt?`. Unique (EntryId, SlotStartsAt); index on TokenHash. Created before sending (claimed attempt), `SentAt` set after a successful send; an offer whose send failed has no SentAt and its token is not usable.

No navigations to Appointment (soft-delete filter); Patient navigation on the entry is explicit-loaded.

State: Waiting -> Booked (claim) | Removed (staff, portal, or token leave). Booked/Removed are terminal; rejoining creates a new entry.
