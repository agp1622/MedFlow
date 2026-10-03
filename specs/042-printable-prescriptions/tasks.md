# Tasks: Printable Prescriptions

**Input**: `/specs/042-printable-prescriptions/` (spec, plan, research, data-model, contracts/api.md). Tests are included: the build rules require tests for access control, isolation and audit.

## Phase 1: Setup

- [ ] T001 Add `PDFsharp` 6.2.4 package reference to `MedFlow.Infrastructure/MedFlow.Infrastructure.csproj`
- [ ] T002 [P] Copy DejaVu Sans Regular and Bold TTF plus the licence text into `MedFlow.Infrastructure/Documents/Fonts/` and embed them as resources in `MedFlow.Infrastructure/MedFlow.Infrastructure.csproj`

## Phase 2: Foundational (Core contracts)

- [ ] T003 Add `PrescriptionDocumentData` record to `MedFlow.Core/DTOs/Dtos.cs`
- [ ] T004 Add `GetDocumentDataAsync(int id, string doctorId)` to `IPrescriptionRepository` and a new `IPrescriptionDocumentRenderer` (`byte[] Render(PrescriptionDocumentData)`) in `MedFlow.Core/Interfaces/IRepositories.cs`

## Phase 3: User Story 1 - Download a printable prescription (P1)

**Goal**: doctor gets a one-page PDF with doctor, patient, medication and signature block.
**Independent test**: GET the endpoint as the owning doctor, receive a valid PDF containing the expected text.

- [ ] T005 [US1] Implement `GetDocumentDataAsync` (scoped to prescription.DoctorId and patient.DoctorId) in `MedFlow.Infrastructure/Repositories/Repositories.cs`
- [ ] T006 [P] [US1] Implement `EmbeddedFontResolver` in `MedFlow.Infrastructure/Documents/EmbeddedFontResolver.cs`
- [ ] T007 [US1] Implement `PrescriptionPdfRenderer` (header, doctor, patient, medication, NOT VALID banner, signature block, wrapping, single page) in `MedFlow.Infrastructure/Documents/PrescriptionPdfRenderer.cs`
- [ ] T008 [US1] Register the renderer in `MedFlow.Infrastructure/DependencyInjection.cs`
- [ ] T009 [US1] Add `GET {id}/pdf` to `PrescriptionsController` in `MedFlow.Api/Controllers/DomainControllers.cs`: scoped load, render, audit View with the prescription id, `no-store`, `Content-Disposition`
- [ ] T010 [P] [US1] Add `prescriptionsApi.downloadPdf` (blob) in `medflow-client/src/api/services.ts`
- [ ] T011 [US1] Add a print button per row (open PDF in a new tab, fall back to download, error message on failure) in `medflow-client/src/pages/BillingPrescriptionsPages.tsx`
- [ ] T012 [P] [US1] Add English and Spanish labels in `medflow-client/src/i18n/resources/en.ts` and `es.ts`
- [ ] T013 [US1] Tests in `MedFlow.Api.Tests/PrescriptionPdfTests.cs`: returns a PDF with expected headers and content, optional fields empty, NOT VALID for expired/cancelled, long values stay on one page

## Phase 4: User Story 2 - Access control and audit (P1)

**Independent test**: other doctor, patient token and anonymous callers are refused; audit view appears only on success.

- [ ] T014 [US2] Tests in `MedFlow.Api.Tests/PrescriptionPdfTests.cs`: other doctor gets 404 identical to a missing id with no audit event, patient token gets 403, anonymous gets 401, success creates exactly one View/Prescription audit event with the prescription id

## Phase 5: Polish

- [ ] T015 Run `dotnet build MedFlow.sln`, `dotnet test MedFlow.Api.Tests`, and `npm run build` and `npm run lint` in `medflow-client`

## Dependencies

T001, T002 then T003, T004 then T005 to T009 (T006 before T007, T007 before T008) then T013, T014. T010 to T012 can run in parallel with the backend once T009's route is fixed.
MVP: Phase 1 to 3 plus T014.
