# Quickstart: Practice Reports

1. `dotnet test MedFlow.Api.Tests --filter ReportsTests` covers totals, isolation, validation, A/R buckets and CSV injection.
2. `cd medflow-client && npm run build && npm run lint`.
3. Manual: run API + `npm run dev`, sign in as a doctor, open **Reportes / Reports** in the sidebar.
   - Pick a date range and period; charts and totals update.
   - Tab through Revenue, Visits, No-shows, A/R aging; A/R shows "as of today".
   - Click Export CSV; open in a spreadsheet; confirm a patient named `=1+1` shows as text.
   - Switch language en/es; labels and CSV headers change.
   - Sign in as a patient (portal): `/reports` is not reachable and the API returns 403.
