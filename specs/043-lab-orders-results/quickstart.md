# Quickstart: validate Lab Orders and Results

Prerequisites: `dotnet build MedFlow.sln`; API and client running (`dotnet run --project MedFlow.Api`, `npm run dev` in `medflow-client`); a doctor account with a patient.

Automated: `dotnet test MedFlow.Api.Tests --filter LabOrderTests` covers CRUD, flags, isolation, portal refusal, audit, limits and validation.

Manual (UI):
1. Open a patient, choose the Labs tab. Empty state is shown.
2. Add an order "Lipid panel". It shows status Ordered.
3. Add a result: LDL, 160, mg/dL, range 0 to 100. It is marked High, the order becomes Completed and the tab shows an abnormal count of 1.
4. Add a result with no range: no flag. Add a value equal to a bound: no flag.
5. Switch the language (ES/EN): all Labs text follows.
6. Open the Audit log tab: view and change events for Lab order appear, without values.
7. Cancel an order: results can no longer be added.
8. Sign in as another doctor and request the same patient's labs (API): 404.
