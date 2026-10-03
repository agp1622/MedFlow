# Tasks: Insurance Details and Claims Export

**Input**: [spec.md](spec.md), [plan.md](plan.md), [data-model.md](data-model.md), [contracts/api.md](contracts/api.md)

**Tests**: required by the repo build rules (access control, isolation, validation limits each get a test).

## Phase 1: Setup

- [x] T001 Add `InsuranceRelationship { Self, Spouse, Child, Other }` to `MedFlow.Core/Enums/Enums.cs`

## Phase 2: Foundational (schema and DTO plumbing, blocks all stories)

- [x] T002 Add `InsuranceGroupNumber`, `InsurancePayerId`, `InsuranceSubscriberName`, `InsuranceSubscriberDateOfBirth` (DateOnly?), `InsuranceSubscriberRelationship` (enum?) to `MedFlow.Core/Entities/Patient.cs`
- [x] T003 Configure max lengths (100/50/200) and string conversion for the enum in `MedFlow.Infrastructure/Data/AppDbContext.cs`
- [x] T004 Generate EF migration `AddPatientInsuranceDetails` (`dotnet ef migrations add AddPatientInsuranceDetails --project MedFlow.Infrastructure --startup-project MedFlow.Api`), read it for unintended drops or renames
- [x] T005 Add the five optional fields to `PatientDto`, `CreatePatientRequest`, `UpdatePatientRequest` in `MedFlow.Core/DTOs/Dtos.cs` (new params at the end with defaults so existing constructors keep compiling) and fix all call sites

## Phase 3: User Story 1 - Record insurance details (P1)

**Goal**: store, validate, show the new fields. **Independent test**: edit patient, reload, see values; bad input rejected.

- [x] T006 [US1] Map and validate the new fields (localized messages, blank to null, DOB not future and not before 1900, relationship is a defined enum value) in `MedFlow.Api/Controllers/PatientsController.cs` Create/Update/MapToDto
- [x] T007 [US1] Add `Patient.Insurance.*` message keys (es/en) to `MedFlow.Api/Localization/Messages.cs`
- [x] T008 [P] [US1] Add fields to `PatientDto`, `CreatePatientRequest` and the `InsuranceRelationship` type in `medflow-client/src/types/index.ts`
- [x] T009 [US1] Add insurance inputs (group, payer ID, subscriber name, subscriber DOB, relationship select, zod limits) to the patient create form and edit form (if present) and show them on the detail view in `medflow-client/src/pages/PatientsPage.tsx`
- [x] T010 [P] [US1] Add en/es strings for the new patient fields and relationship options in `medflow-client/src/i18n/resources/en.ts` and `es.ts`
- [x] T011 [US1] Tests in `MedFlow.Api.Tests/InsuranceClaimTests.cs`: save and read back new fields; over-length, bad relationship and future subscriber DOB rejected with 400 (es and en); other doctor's patient update is 404; audit entry lists changed field names; intake accept still fills provider and policy number

## Phase 4: User Story 2 - Export a draft claim (P1)

**Goal**: `GET /api/invoices/{id}/claim-export`. **Independent test**: export JSON and CSV for a fully insured and an uninsured patient.

- [x] T012 [US2] Add `ClaimSourceData`, `ClaimItemDto`, `ClaimDraftDto` records to `MedFlow.Core/DTOs/Dtos.cs`
- [x] T013 [US2] Implement pure `ClaimDraftBuilder` (item map, Self defaults, sex mapping, missing rules, always-missing keys) in `MedFlow.Core/ClaimDraftBuilder.cs`
- [x] T014 [US2] Add `IInvoiceRepository.GetClaimSourceAsync(int id, string doctorId)` in `MedFlow.Core/Interfaces/IRepositories.cs` and implement scoped query (invoice and patient both the doctor's, appointment date, doctor) in `MedFlow.Infrastructure/Repositories/Repositories.cs`
- [x] T015 [P] [US2] Implement `ClaimCsvWriter` (RFC 4180 quoting, formula-injection prefix) in `MedFlow.Api/Services/ClaimCsvWriter.cs`
- [x] T016 [US2] Add `Claim.*` message keys (disclaimer, item labels, missing labels, unsupported format) es/en to `MedFlow.Api/Localization/Messages.cs`
- [x] T017 [US2] Add `ClaimExport` action to `InvoicesController` in `MedFlow.Api/Controllers/DomainControllers.cs`: validate format (400), load source (404), build, localize, audit View Invoice (404 if false), no-store, attachment named by invoice number only
- [x] T018 [P] [US2] Add `ClaimDraftDto` types in `medflow-client/src/types/index.ts` and `invoicesApi.downloadClaimDraft(id, format)` blob method in `medflow-client/src/api/services.ts`
- [x] T019 [US2] Add `useExportClaimDraft` hook in `medflow-client/src/hooks/queries.ts` (blob download, draft-only info toast, failure toast) and an export button per row in `medflow-client/src/pages/BillingPrescriptionsPages.tsx`
- [x] T020 [P] [US2] Add en/es strings (`billing.claim.*`, toasts) in `medflow-client/src/i18n/resources/en.ts` and `es.ts`
- [x] T021 [US2] Tests in `MedFlow.Api.Tests/InsuranceClaimTests.cs`: builder unit tests (full data, Self defaults, always-missing keys, sex mapping); JSON export content and disclaimer wording (contains "not an X12 837", never claims conformance); CSV export parses, quotes commas/quotes/newlines, neutralises `=cmd`; unsupported format 400; es and en labels; invoice with linked appointment uses its date

## Phase 5: User Story 3 - Access control, audit, localisation (P1)

- [x] T022 [US3] Tests in `MedFlow.Api.Tests/InsuranceClaimTests.cs`: anonymous 401; patient token 403; other doctor's invoice 404 with empty body identical to nonexistent id; nothing audited on 404; audit View/Invoice recorded on success with invoice id; `Cache-Control: no-store`; file name has no patient name
- [x] T023 [US3] Verify in code that a soft-deleted patient or invoice yields 404 (query filter) and add a test if the filter is not global

## Phase 6: Polish

- [x] T024 Add a short "Claim export draft" note stating exactly what is and is not conformant to `README.md`
- [x] T025 Run `dotnet build MedFlow.sln`, `dotnet test MedFlow.Api.Tests`, and `npm run build` and `npm run lint` in `medflow-client`; run the quickstart checks that can be automated

## Dependencies

T001 -> T002 -> T003 -> T004 -> T005 -> US1 and US2. US2 (T012-T014) needs T002/T005. T017 needs T013-T016. T019 needs T018. US3 tests need T017.

## Parallel opportunities

T008, T010 with T006/T007; T015, T018, T020 are independent files.

## Strategy

Both P1 stories are required for the issue; US1 first (data), then US2 (export), then US3 tests.
