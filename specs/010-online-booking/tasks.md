# Tasks: Patient Portal Online Booking

Tests are required by the build rules (access control, isolation, validation limits).

## Phase 1: Foundational (Core + Infrastructure)

- [X] T001 Add `DoctorAvailability` and `DoctorBlockedDate` entities in MedFlow.Core/Entities/DomainEntities.cs
- [X] T002 Add availability/slot/booking DTO records in MedFlow.Core/DTOs/Dtos.cs
- [X] T003 Add `IBookingRepository` in MedFlow.Core/Interfaces/IRepositories.cs
- [X] T004 Add DbSets and model config (indexes, string enums, unique doctor+date) in MedFlow.Infrastructure/Data/AppDbContext.cs
- [X] T005 Implement `BookingRepository` (availability replace/read, blocked dates, slot generation, atomic booking with per-doctor lock and serializable transaction on relational providers) in MedFlow.Infrastructure/Repositories/Repositories.cs and register in MedFlow.Infrastructure/DependencyInjection.cs
- [X] T006 Generate EF migration `AddOnlineBooking` and review it for unintended drops, in MedFlow.Infrastructure/Data/Migrations

## Phase 2: User Story 1 - Doctor availability (P1)

- [X] T007 [US1] Add doctor-only `AvailabilityController` (GET, PUT weekly, blocked-dates POST/DELETE) with validation in MedFlow.Api/Controllers/AvailabilityController.cs
- [X] T008 [US1] Tests: CRUD, validation (end<=start, overlap, non-30-min, past/duplicate blocked date), patient token denied, cross-doctor isolation in MedFlow.Api.Tests/BookingTests.cs
- [X] T009 [P] [US1] Add types and `availabilityApi` in medflow-client/src/types/index.ts and medflow-client/src/api/services.ts, hooks in medflow-client/src/hooks/queries.ts
- [X] T010 [US1] Availability settings page (weekly windows, blocked dates) with route and nav entry in medflow-client/src/pages/AvailabilityPage.tsx, App routes and sidebar

## Phase 3: User Story 2 - Patient views slots and books (P1)

- [X] T011 [US2] Add `GET /api/portal/booking/slots` and `POST /api/portal/booking` to MedFlow.Api/Controllers/PortalController.cs (patient and doctor from token/record)
- [X] T012 [US2] Tests: only open slots (past, lead time, blocked, taken excluded), booking success, double-booking conflict (concurrent), outside availability rejected, range > 31 days rejected, reason limit, 3-upcoming limit, patient overlap, inactive patient denied, doctor token denied, cross-patient isolation, no doctor id/notes leakage in MedFlow.Api.Tests/BookingTests.cs
- [X] T013 [P] [US2] Add portal booking types and `portalApi.slots/book` in medflow-client/src/types/index.ts and medflow-client/src/api/services.ts, hooks in medflow-client/src/hooks/queries.ts
- [X] T014 [US2] Booking UI (date range, slot picker, reason, confirm) in medflow-client/src/pages/PortalPage.tsx or a new component under medflow-client/src/components

## Phase 4: User Story 3 - Calendar visibility (P2)

- [X] T015 [US3] Tests: booking appears in doctor `/api/appointments` as Pending and in portal appointments; cancelled booking frees slot in MedFlow.Api.Tests/BookingTests.cs
- [X] T016 [US3] Ensure query invalidation on booking so portal appointments refresh in medflow-client/src/hooks/queries.ts

## Phase 5: Polish

- [X] T017 Run dotnet build, dotnet test, npm run build and npm run lint; walk quickstart.md scenarios

Dependencies: Phase 1 -> US1 and US2 (US2 slot tests use US1 endpoints) -> US3.
