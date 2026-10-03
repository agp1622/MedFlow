# Implementation Plan: Insurance Details and Claims Export

**Branch**: `claude/issue-26-insurance-claims` (spec `043-insurance-claims-export`) | **Date**: 2026-10-03 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/043-insurance-claims-export/spec.md` (Refs #26)

## Summary

1. Extend `Patient` with five optional insurance columns (group number, payer ID, subscriber name, subscriber date of birth, subscriber relationship enum) via one EF migration. Provider and policy number are reused as-is (the intake accept flow already maps onto them, so nothing is migrated or duplicated). Carry the fields through patient DTOs, create/update, and the patient form and detail view. Validate with localized messages.
2. Add `GET /api/invoices/{id}/claim-export?format=json|csv` (doctor-only). The repository returns a scoped `ClaimSourceData` (invoice, appointment date, patient, doctor) only when the invoice and its patient both belong to the caller; otherwise 404. A pure Core builder (`ClaimDraftBuilder`) arranges the data under CMS-1500 (02/12) item numbers and computes missing items. The API localizes labels and the disclaimer, serialises JSON or CSV, audits a View of the invoice, and returns an attachment with `Cache-Control: no-store`.
3. Client: typed `invoicesApi.downloadClaimDraft`, an export action on the invoice list, new insurance inputs in the patient form, en/es strings.

No X12 837, no PDF, no new NuGet or npm dependency.

## Technical Context

**Language/Version**: C# / .NET 8; TypeScript + React (Vite)

**Primary Dependencies**: existing only (ASP.NET Core, EF Core, TanStack Query, react-hook-form + zod, i18next)

**Storage**: SQL Server; 5 nullable columns on `Patients`. No claim storage.

**Testing**: xUnit + `TestApiFactory` in `MedFlow.Api.Tests`; builder unit tests are plain xUnit

**Performance Goals**: single-row queries; export well under 1 s

**Constraints**: doctor-only, uniform 404, audit View on success only (fail closed), no-store, no patient name in file name, CSV formula-injection safe, never claim 837 conformance

**Scale/Scope**: 1 endpoint, 1 builder, 1 CSV writer, 5 columns, 1 migration, 1 client method, 1 button, form fields

## Constitution Check

| Principle | Status | Notes |
|---|---|---|
| I. Git workflow | PASS (noted) | Requester-fixed branch `claude/issue-26-insurance-claims` off `origin/dev`; not named `NNN-name`; not pushed by this run. |
| II. Layered architecture | PASS | DTOs, enum, builder, repo interface in Core (no ASP.NET/EF); EF query in Infrastructure; controller delegates; client via `services.ts`. |
| III. Consistent API contracts | PASS | Returns DTO-shaped JSON (not entities); enums serialise as strings; typed client method. Not a list endpoint. |
| IV. Security | PASS | `[Authorize(Roles = Doctor)]`, scoped query, uniform 404, audit via `IAuditService`, no-store, JWT header blob download (no token in URL), no secrets, portal DTOs unchanged. |
| V. Simplicity | PASS | One format-neutral builder, two serialisers, no storage, no templates, no 837. Reuses existing audit vocabulary (View/Invoice). |

Post-design re-check: unchanged, PASS.

## Project Structure

```text
specs/043-insurance-claims-export/{spec,plan,research,data-model,quickstart,tasks}.md, contracts/api.md

MedFlow.Core/Enums/Enums.cs                      # InsuranceRelationship
MedFlow.Core/Entities/Patient.cs                 # 5 new properties
MedFlow.Core/DTOs/Dtos.cs                        # PatientDto/Create/Update fields, ClaimSourceData, ClaimDraftDto
MedFlow.Core/ClaimDraftBuilder.cs                # pure arrangement + missing items
MedFlow.Core/Interfaces/IRepositories.cs         # IInvoiceRepository.GetClaimSourceAsync
MedFlow.Infrastructure/Data/AppDbContext.cs      # lengths + enum-as-string
MedFlow.Infrastructure/Data/Migrations/*AddPatientInsuranceDetails*
MedFlow.Infrastructure/Repositories/Repositories.cs
MedFlow.Api/Controllers/PatientsController.cs    # map + validate new fields
MedFlow.Api/Controllers/DomainControllers.cs     # InvoicesController.ClaimExport
MedFlow.Api/Services/ClaimCsvWriter.cs
MedFlow.Api/Localization/Messages.cs             # es/en
MedFlow.Api.Tests/InsuranceClaimTests.cs
medflow-client/src/{types/index.ts,api/services.ts,hooks/queries.ts,pages/PatientsPage.tsx,pages/BillingPrescriptionsPages.tsx,i18n/resources/{en,es}.ts}
```

## Conformance statement (carried into UI, JSON, CSV, README note)

The export is a DRAFT data worksheet using CMS-1500 (02/12) item numbers. It is not the CMS-1500 form, not an X12 837 file, and not validated by a payer or clearinghouse. Item numbering is a best-effort mapping. CPT/HCPCS (24D), ICD-10 (21), billing/rendering NPI (24J, 33a) and federal tax ID (25) are not stored and are always reported missing.
