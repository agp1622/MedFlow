# Quickstart: Vitals Trends

Prerequisites: API and client running; a doctor account with a patient.

1. Record 3+ vitals for a patient via the existing API (POST /api/vitalsigns) with blood pressure, heart rate, weight, height, temperature and oxygen saturation.
2. Open the patient, choose the "Vitals" tab: one chart per vital; BP has two lines.
3. Choose "30 days": records older than 30 days disappear. Choose a custom range with end before start: error message shown, charts unchanged.
4. Pick a range with no records: empty-state message.
5. Switch language to Spanish: labels translate.

Automated: `dotnet test MedFlow.Api.Tests` (VitalsAccessTests), `npm run build`, `npm run lint` in `medflow-client`.
