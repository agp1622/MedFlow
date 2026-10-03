# Quickstart: Audit Log

Automated: `dotnet test MedFlow.Api.Tests --filter AuditLog` covers recording, filters, isolation and immutability.

Manual:
1. Sign in as a doctor, open a patient, edit the phone number, add a note, upload and download a file.
2. Open the patient's "Audit log" tab: each action appears newest first with your name and time; the edit lists `Phone` as a changed field and no values.
3. Filter by Change, by your name, and by today's date range.
4. Invite the patient, sign in as the patient, open My Records, then check the log shows the patient's views.
5. Sign in as another doctor and request the log URL: 404.
