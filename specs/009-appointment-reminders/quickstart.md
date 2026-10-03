# Quickstart

1. `dotnet build MedFlow.sln && dotnet test MedFlow.Api.Tests` (reminder tests cover sending, idempotency, retry, link actions, isolation).
2. Manual: run API (dev DB migrates automatically) and client; create an appointment ~2h ahead for a patient with email; set `Reminders:IntervalMinutes` low; confirm the email arrives, open the link, click Confirm; open the appointment in the doctor UI and see Confirmed plus delivery log.
3. Config: `Reminders:LeadTimeHours`, `Reminders:IntervalMinutes`, `Reminders:MaxAttempts`.
