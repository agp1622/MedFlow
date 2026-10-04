# Quickstart: validating clinic roles

Prerequisites: .NET 8 SDK, Node 20+, `~/.dotnet/tools` on `PATH` (for `dotnet ef`).

1. Backend: `dotnet build MedFlow.sln` then `dotnet test MedFlow.Api.Tests` (role matrix, isolation, staff, invitation, backfill-guard tests included).
2. Model/migration sync: `dotnet ef migrations has-pending-model-changes -p MedFlow.Infrastructure -s MedFlow.Api` must report no pending changes.
3. Client: in `medflow-client`, `npm run build` and `npm run lint`.

Manual pass (needs SQL Server and `dotnet run`, plus `npm run dev`):

1. Register a doctor: the header shows the clinic name and role Owner.
2. Open Staff, invite a Nurse (check the SMTP/log output for the link), open the link, set name and password: you land signed in as Nurse; Prescriptions and Invoices are absent from navigation; patient page shows vitals and notes forms but no prescribe action.
3. As Owner change the nurse to Receptionist: after their next request the clinical sections vanish and patient screens hide primary condition/allergies/notes/blood type.
4. Try to demote or deactivate yourself while you are the only Owner: refused with a message.
5. Deactivate the receptionist: their open session gets 403 on the next request.
6. Upgrade check: restore a pre-feature database backup, run the migration, sign in as an existing doctor and confirm all patients, appointments and invoices are present and the role is Owner.
7. Not verifiable by automated tests here: the migration SQL against a real SQL Server and the full UI click-through.
