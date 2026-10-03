# Quickstart

1. `dotnet test MedFlow.Api.Tests --filter WaitlistTests` - add/list/remove, isolation, offers on cancellation, claim/conflict, token failures, leave.
2. `dotnet ef migrations has-pending-model-changes -p MedFlow.Infrastructure -s MedFlow.Api` - must report none.
3. Manual: doctor sets availability (010), a patient books a slot; add a second patient with an email to Waitlist (doctor page or portal); cancel the first appointment; the second patient's inbox (dev SMTP) receives the Spanish/English offer; open the link, claim; the doctor's Appointments shows the Pending appointment and the entry leaves the list.
