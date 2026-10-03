# Tasks: Vitals Trends

**Input**: specs/042-vitals-trends/ (spec.md, plan.md, research.md)
**Tests**: Backend access-control tests are required by the build rules; client has no test runner.

## Phase 1: Foundational

- [ ] T001 [P] Add pure helpers `parseBloodPressure`, `buildSeries`, `filterByRange`, `rangeFromPreset`, `isValidRange` in medflow-client/src/components/vitals/trends.ts (UTC-safe timestamp parsing, inclusive local-day bounds, skip unparseable BP)
- [ ] T002 [P] Add en and es i18n keys under `patients.tabs.vitals` and `patients.trends.*` (title, presets, from, to, invalidRange, noneInRange, units, series names) in medflow-client/src/i18n/resources/en.ts and es.ts

## Phase 2: User Story 1 - Line chart per vital (P1)

**Independent test**: patient with several records shows one chart per recorded vital, BP with two lines.

- [ ] T003 [US1] Create medflow-client/src/components/vitals/VitalsTrends.tsx using `usePatientVitals`, Recharts `LineChart` per vital with tooltip including unit, omit vitals with no values, empty state when no vitals
- [ ] T004 [US1] Add a `vitals` tab to `TABS` and render `<VitalsTrends patientId={patientId} />` in medflow-client/src/pages/PatientsPage.tsx

## Phase 3: User Story 2 - Date range filter (P1)

**Independent test**: ranges narrow all charts; inverted range rejected; empty range message.

- [ ] T005 [US2] Add range presets (30 days, 6 months, 1 year, all) and custom from/to date inputs with inverted-range validation message to medflow-client/src/components/vitals/VitalsTrends.tsx, applied to all charts, plus "no vitals in this range" state

## Phase 4: Tests and Polish

- [ ] T006 [P] Add MedFlow.Api.Tests/VitalsAccessTests.cs: unauthenticated rejected; patient token rejected; other doctor gets 404 on patient's vitals (list and latest); owner gets own records in newest-first order
- [ ] T007 Run `dotnet build MedFlow.sln`, `dotnet test MedFlow.Api.Tests`, `npm run build`, `npm run lint`; walk through quickstart.md steps as far as possible
