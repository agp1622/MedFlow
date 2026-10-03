# Data Model

**AppointmentReminder** (BaseEntity): AppointmentId, ScheduledAt (snapshot of appointment time), TokenHash (64), Status (Pending|Sent|Failed|Skipped), Attempts, SentAt?, Response (None|Confirmed|Cancelled), RespondedAt?. Index on TokenHash, unique-ish on (AppointmentId, ScheduledAt).
**ReminderDelivery** (BaseEntity): AppointmentId, ReminderId, AttemptedAt, Channel ("Email"), Outcome (Sent|Failed|Skipped), Reason (max 200, no addresses/tokens).

State: Pending -> Sent | Failed (retry until MaxAttempts) | Skipped (no valid email; re-evaluated on later runs so it can recover if an email is added, but the Skipped delivery entry is logged only once). Appointment status transitions via link: Pending/Confirmed -> Cancelled; Pending -> Confirmed; Confirmed -> Confirmed (idempotent); Cancelled/Completed/NoShow: none.
Neither entity has a navigation to Appointment/Patient that bypasses soft-delete filters (appointment looked up explicitly).
