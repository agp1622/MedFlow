# Tasks: Lab Orders and Results

**Input**: [spec.md](spec.md), [plan.md](plan.md), [data-model.md](data-model.md), [contracts/labs-api.md](contracts/labs-api.md)
**Tests**: required by the build rules (access control, isolation, validation limits each get a test).
Refs #21

## Phase 1: Setup

- [ ] T001 Append `LabOrderStatus` (Ordered, Completed, Cancelled), `LabFlag` (None, Low, High) and `LabOrder` to `AuditItemKind` in MedFlow.Core/Enums/Enums.cs
- [ ] T002 [P] Create `LabOrder` and `LabResult` entities in MedFlow.Core/Entities/LabEntities.cs
- [ ] T003 [P] Add lab DTO records and requests (`LabSummaryDto`, `LabOrderDto`, `LabResultDto`, `SaveLabOrderRequest`, `SaveLabResultRequest`) in MedFlow.Core/DTOs/Dtos.cs
- [ ] T004 Add `ILabOrderRepository` in MedFlow.Core/Interfaces/IRepositories.cs

## Phase 2: Foundational

- [ ] T005 Add DbSets, soft-delete query filters, column limits, string enum conversion and indexes for `LabOrders`/`LabResults` in MedFlow.Infrastructure/Data/AppDbContext.cs
- [ ] T006 Implement `LabOrderRepository` (ownership checks, flag computation in DTO mapping, status derivation, counts) in MedFlow.Infrastructure/Repositories/LabOrderRepository.cs and register it in MedFlow.Infrastructure/DependencyInjection.cs
- [ ] T007 Generate the EF migration `AddLabOrders` (`dotnet ef migrations add AddLabOrders --project MedFlow.Infrastructure --startup-project MedFlow.Api`) and review it for unintended drops or renames
- [ ] T008 [P] Add `Lab.*` es/en messages to MedFlow.Api/Localization/Messages.cs

## Phase 3: User Story 1 - Order a lab (P1)

**Independent test**: create, edit, cancel, delete an order for an owned patient; 404 for another doctor's.

- [ ] T009 [US1] Implement list, create, update, cancel and delete order endpoints with audit and caps in MedFlow.Api/Controllers/LabOrdersController.cs
- [ ] T010 [US1] Tests for order CRUD, validation (empty/long name, long note, future date) and the 100-order cap in MedFlow.Api.Tests/LabOrderTests.cs

## Phase 4: User Story 2 - Record results with flags (P1)

**Independent test**: 12.0 in 4.0-10.0 is High, 7.0 none, no range none, bound inclusive.

- [ ] T011 [US2] Implement add/update/remove result endpoints with range validation, cancelled-order 409, status re-derivation, 50-result cap in MedFlow.Api/Controllers/LabOrdersController.cs
- [ ] T012 [US2] Tests for flags (High, Low, none, bounds inclusive, no range), low>high rejection, status transitions, cancelled-order conflict and result cap in MedFlow.Api.Tests/LabOrderTests.cs

## Phase 5: User Story 4 - Private and audited (P1)

**Independent test**: other doctor 404 on every route, portal token refused, audit events written without values.

- [ ] T013 [US4] Tests for cross-doctor isolation on every route, portal patient token refusal, anonymous 401, lab data absent from portal responses, and audit events (view and change, field names only) in MedFlow.Api.Tests/LabOrderTests.cs

## Phase 6: User Story 3 - Abnormal values on the patient page (P1)

**Independent test**: Labs tab shows High/Low labels and the count in the tab; Spanish and English.

- [ ] T014 [P] [US3] Add lab types to medflow-client/src/types/index.ts and `labsApi` to medflow-client/src/api/services.ts
- [ ] T015 [US3] Add query key and hooks (`usePatientLabs`, order/result mutations) to medflow-client/src/hooks/queries.ts
- [ ] T016 [P] [US3] Add `labs.*`, `patients.tabs.labs` and `enums` (LabOrder, Ordered, Low, High, ...) keys to medflow-client/src/i18n/resources/es.ts and en.ts
- [ ] T017 [US3] Build `LabsPanel` (orders, result rows, flag badge with text, forms with react-hook-form + zod) in medflow-client/src/components/labs/LabsPanel.tsx
- [ ] T018 [US3] Add the Labs tab with abnormal-count badge to medflow-client/src/pages/PatientsPage.tsx

## Phase 7: Polish

- [ ] T019 Run `dotnet build MedFlow.sln`, `dotnet test MedFlow.Api.Tests`, `npm run build` and `npm run lint` in medflow-client and fix failures
- [ ] T020 Walk through [quickstart.md](quickstart.md) items and note which need a manual UI pass

## Dependencies

T001-T004 -> T005-T008 -> T009 -> T011; tests T010/T012/T013 follow their endpoints. Client T014-T018 depend only on the contract and can start after T003.
