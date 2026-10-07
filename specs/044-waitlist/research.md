# Research

- **Offer model**: notify up to N earliest-joined waiting patients at once; first atomic booking wins. One-at-a-time with expiry needs a hold or a cascading job (expiry sweeper) and delays filling the slot; rejected for simplicity. Join order still decides who is emailed when more than N wait.
- **Trigger**: inline call after the save in the controllers (event is a user action; no polling). Alternative: a periodic scan of cancelled appointments was rejected (needs "already processed" tracking and delays offers).
- **No-hold**: reuse `BookAsync` (per-doctor semaphore + serializable transaction) so there is no second booking path to keep consistent.
- **Token**: same scheme as `ReminderTokens` (32 random bytes URL-safe base64, SHA-256 hex stored); reuse the helper. One token per offer; actions claim/leave via POST bodies so tokens stay out of logs.
- **Email**: reuse `IEmailSender`; link base from `AllowedOrigins[0]` like reminders. Body bilingual (Spanish then English); contains first name, doctor name, date/time UTC, links only.
- **Uniform 404**: all token failures return the same body; a valid token whose slot was taken returns 409 (the holder already knows the offer).
- **Privacy**: entries contain no reasons or clinical data. Audit uses new `AuditItemKind.Waitlist`.
