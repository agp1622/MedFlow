# Quickstart: validating Insurance Details and Claims Export

Prerequisites: .NET 8 SDK, Node 20+. See [contracts/api.md](contracts/api.md) and [data-model.md](data-model.md).

## Automated

```sh
dotnet build MedFlow.sln
dotnet test MedFlow.Api.Tests --filter InsuranceClaim
cd medflow-client && npm run build && npm run lint
```

## Manual (needs a running API and client)

1. Sign in as a doctor, open a patient, edit insurance: fill group number, payer ID, subscriber name/DOB, relationship. Save and reopen: values shown on the detail view. Try a 300-character subscriber name: rejected with a message in the app language.
2. Open Billing, choose the export action on an invoice of that patient. A JSON file `claim-draft-INV-xxxx.json` downloads and a toast says it is a draft only. Open it: status DRAFT, disclaimer, items under CMS-1500 numbers, `missing` lists at least 21, 24D, NPI, 25.
3. Repeat with CSV (client uses format=csv via the service; or call the endpoint directly with a doctor token).
4. Switch the language between Spanish and English and repeat: labels and messages follow.
5. Check the patient's audit log: a View of Invoice per export.
6. Sign in as another doctor or a patient and request the same invoice: 404 or 403.

## Not covered by tests (manual only)

Visual layout of the patient form fields and the invoice list button, and the browser download itself.
