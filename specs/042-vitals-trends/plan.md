# Implementation Plan: Vitals Trends

**Branch**: `claude/issue-20-vitals-trends` | **Date**: 2026-10-03 | **Spec**: [spec.md](./spec.md)

## Summary

Add a "Vitals" tab to the patient detail page showing one Recharts line chart per recorded vital with a date range filter. Client-only: the existing `GET /api/vitalsigns/patient/{id}` (doctor-only, ownership-checked and audited via `AuditAsync`) already returns every record; parsing and range filtering happen in the browser. No backend, DTO or migration change. Backend access-control tests are added for the vitals endpoints because the feature depends on them and none exist beyond a status-code smoke check.

## Technical Context

**Language/Version**: C# / .NET (existing), TypeScript + React (existing)
**Primary Dependencies**: recharts ^2.12.7 (already in `medflow-client/package.json`, currently unused), TanStack Query, react-i18next, date-fns. No new dependency.
**Storage**: N/A (read-only on existing `VitalSigns`)
**Testing**: xUnit `MedFlow.Api.Tests` with `TestApiFactory`. The client has no test runner and adding one would be a new dependency; client logic is kept in a small pure module and verified by `tsc`, lint, build and the manual quickstart.
**Target Platform**: Web (existing)
**Project Type**: web-service + SPA
**Performance Goals**: re-filter 500 records instantly (memoized in-memory)
**Constraints**: no schema/API change; en/es parity (LocalizationTests)
**Scale/Scope**: one new tab, one component, one helper module, i18n keys, one test class

## Constitution Check

- I Git workflow: feature branch off `dev`. Branch name `claude/issue-20-vitals-trends` was mandated by the requester and differs from the `NNN-name` rule; flagged in the report. Pushing is left to the requester.
- II Layered architecture: no backend change; Core/Infrastructure untouched. PASS.
- III API contracts: no new endpoint; existing `vitalsApi.getByPatient` and `usePatientVitals` reused; types already exist. PASS.
- IV Security: access stays doctor-only via existing controller; tests lock it in. No patient-portal exposure. PASS.
- V Simplicity: no flags, no new abstractions beyond one helper module. PASS.

Post-design re-check: PASS.

## Project Structure

```text
specs/042-vitals-trends/{spec,plan,research,data-model,quickstart,tasks}.md, contracts/
medflow-client/src/
├── components/vitals/VitalsTrends.tsx     # range filter + charts (new)
├── components/vitals/trends.ts            # pure helpers: parseBloodPressure, buildSeries, filterByRange (new)
├── pages/PatientsPage.tsx                 # add 'vitals' tab
└── i18n/resources/{en,es}.ts              # new keys
MedFlow.Api.Tests/VitalsAccessTests.cs     # new
```

**Structure Decision**: Follow the existing `components/<area>/` pattern (clinical, attachments, audit).
