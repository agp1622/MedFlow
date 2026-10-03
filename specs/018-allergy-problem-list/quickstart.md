# Quickstart: validating Allergy and Problem List

Prereqs: .NET 8 SDK, Node 18+.

1. Automated: `dotnet test MedFlow.Api.Tests --filter ClinicalListTests` (CRUD, validation, isolation, portal rejection).
2. `dotnet build MedFlow.sln` and `npm run build` in `medflow-client`.
3. Manual (needs running API + client, not covered by tests):
   - Open a patient: the Clinical Summary panel shows above the tabs with "none recorded" states.
   - Add an allergy (Severe), a problem (`E11.9`, Active) and a medication; each appears immediately; Severe allergy is emphasised.
   - Edit each; remove each (confirm dialog appears).
   - Enter an invalid ICD-10 (`ZZZ`): inline error, nothing saved.
   - Patient with legacy free-text allergies: text shows read-only as "Legacy allergy notes"; Notes/Allergies fields unchanged on the edit patient data.
   - Mark a problem Resolved: it leaves the active list (visible under "Show resolved").
