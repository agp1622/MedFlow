# Quickstart: Validating Secure Messaging

Prerequisites: the API and client run as for feature 005 (see [../005-patient-portal-records/quickstart.md](../005-patient-portal-records/quickstart.md)); a doctor account and a patient with a portal account (invited and accepted). Details: [data-model.md](data-model.md), [contracts/messaging-api.md](contracts/messaging-api.md).

## Setup

1. From the repo root, apply the new migration and start the API: `dotnet ef database update --project MedFlow.Infrastructure --startup-project MedFlow.Api`, then `dotnet run --project MedFlow.Api`.
2. Start the client: `cd medflow-client && npm run dev`.
3. Use two browser profiles: one signed in as the doctor, one as the patient.

## Automated checks

`dotnet test MedFlow.Api.Tests` — messaging tests must pass (cross-patient denial, unread counts, attachment access, validation).

## Manual scenarios

| # | Steps | Expected |
|---|---|---|
| 1 | Patient: Messages → send "Question about my dosage" | Appears in the thread with sender and time (US1) |
| 2 | Doctor: dashboard | Unread indicator shows 1; Messages lists the patient with an unread badge (US3) |
| 3 | Doctor: open thread, reply | Reply appears; unread count drops to 0 (US2, US3) |
| 4 | Patient: dashboard/portal nav (wait ≤ 30 s or refresh) | Unread shows 1; opening Messages clears it |
| 5 | Patient: send an empty message | Rejected with a clear message |
| 6 | Patient: send a message with a PDF and a JPG | Doctor can download both (US4) |
| 7 | Patient: attach a `.exe` or a file > 50 MB | Same rejection text as chart attachments; no message stored |
| 8 | Patient: send > 4,000 characters | Rejected with the length message |
| 9 | Doctor: Patients → patient → Attachments tab | Message files are **not** listed there |
| 10 | Patient B signed in: call patient A's message-attachment URL | `404`, no information revealed |
| 11 | Doctor of another practice: open this patient's thread by id | `404` |
| 12 | Doctor revokes portal access | Patient can no longer open or send messages; doctor still sees history |
| 13 | Check `PortalAccessLogs` | Entries for thread views and file downloads with the actor id |

## Done when

All scenarios behave as expected and `dotnet test` and `npm run build` succeed.
