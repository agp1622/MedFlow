# Implementation Plan: Printable Prescriptions

**Branch**: `claude/issue-22-printable-prescriptions` (spec `042-printable-prescriptions`) | **Date**: 2026-10-03 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/042-printable-prescriptions/spec.md` (Refs #22)

## Summary

Add `GET /api/prescriptions/{id}/pdf` (doctor-only). The repository loads a scoped
`PrescriptionDocumentData` (prescription + doctor + patient) only if the prescription belongs to the
caller and the patient is the caller's patient; otherwise 404. A renderer behind a Core interface
(`IPrescriptionDocumentRenderer`, implemented in Infrastructure with PDFsharp, MIT) produces a one-page
PDF with a blank signature block. After rendering, the controller records an audit View with the
prescription id (`this.AuditAsync`), then returns `application/pdf` with `Cache-Control: no-store`.
The client adds `prescriptionsApi.downloadPdf` (blob, header auth) and a per-row print button.
No schema change, no migration.

## Technical Context

**Language/Version**: C# / .NET 8; TypeScript + React (Vite)

**Primary Dependencies**: ASP.NET Core Web API, EF Core, PDFsharp 6.2.4 (MIT, new); React, TanStack Query, axios via `services.ts`

**Storage**: none new

**Testing**: xUnit + `TestApiFactory` in `MedFlow.Api.Tests`

**Target Platform**: Web, Linux container (Dockerfile) and Windows dev

**Project Type**: web-application

**Performance Goals**: Single-page document in well under 1 s (SC-005 is 3 s)

**Constraints**: Doctor-only, uniform 404, audit View on success only, no-store caching, no secrets, no e-signature

**Scale/Scope**: 1 endpoint, 1 renderer, 2 bundled fonts, 1 client method, 1 button

## Constitution Check

| Principle | Status | Notes |
|---|---|---|
| I. Git workflow | PASS (noted) | Requester-fixed branch `claude/issue-22-printable-prescriptions` off `dev`; not named `NNN-name`; not pushed by this run. |
| II. Layered architecture | PASS | Interface and data record in Core (no ASP.NET/EF); PDFsharp, fonts, EF query in Infrastructure; controller delegates; client via `services.ts`. |
| III. Consistent API contracts | PASS | Returns a binary file, not an entity; typed `prescriptionsApi.downloadPdf`. Not a list endpoint so no `PagedResult`. |
| IV. Security | PASS | `[Authorize(Roles = Doctor)]` on controller, scoped query, uniform 404, JWT header via blob request (no token in URL), `no-store`. No secrets. |
| V. Simplicity | PASS | One endpoint, one renderer, no templates/config/localisation. New dependency is a library addition, not a stack replacement. |

Post-design re-check: unchanged, PASS. Technology Stack Constraints: adding a NuGet library is not a new language, runtime or DB.

## Project Structure

```text
specs/042-printable-prescriptions/{spec,plan,research,data-model,quickstart,tasks}.md, contracts/api.md

MedFlow.Core/DTOs/Dtos.cs                          # PrescriptionDocumentData record
MedFlow.Core/Interfaces/IRepositories.cs           # IPrescriptionRepository.GetDocumentDataAsync, IPrescriptionDocumentRenderer
MedFlow.Infrastructure/Repositories/Repositories.cs# GetDocumentDataAsync
MedFlow.Infrastructure/Documents/PrescriptionPdfRenderer.cs, EmbeddedFontResolver.cs
MedFlow.Infrastructure/Documents/Fonts/DejaVuSans*.ttf + LICENSE-DejaVu.txt (embedded resources)
MedFlow.Infrastructure/MedFlow.Infrastructure.csproj, DependencyInjection.cs
MedFlow.Api/Controllers/DomainControllers.cs       # PrescriptionsController.GetPdf
MedFlow.Api.Tests/PrescriptionPdfTests.cs
medflow-client/src/api/services.ts, src/pages/BillingPrescriptionsPages.tsx, src/i18n/resources/{en,es}.ts
```
