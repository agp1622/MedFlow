# Tasks: Appointment Reminders

Refs #9. Tests are included (repo rules require access control, isolation and validation tests).

## Phase 1: Foundational (blocks all stories)

- [x] T001 Add enums `ReminderStatus`, `ReminderOutcome`, `ReminderResponse` to MedFlow.Core/Enums/Enums.cs
- [x] T002 Add entities `AppointmentReminder` and `ReminderDelivery` to MedFlow.Core/Entities/DomainEntities.cs
- [x] T003 Add DTOs (`ReminderLookupDto`, `ReminderTokenRequest`, `ReminderRespondRequest`, `ReminderLogDto`, `ReminderDeliveryDto`) to MedFlow.Core/DTOs/Dtos.cs
- [x] T004 Add `IReminderRepository` and `IReminderProcessor` to MedFlow.Core/Interfaces/IRepositories.cs
- [x] T005 Add DbSets and EF config (indexes, max lengths) in MedFlow.Infrastructure/Data/AppDbContext.cs
- [x] T006 Generate and review EF migration `AddAppointmentReminders` in MedFlow.Infrastructure/Data/Migrations
- [x] T007 Add `ReminderSettings` (lead time 1-168 default 24, interval, max attempts) in MedFlow.Infrastructure/Reminders/ReminderSettings.cs and bind in DependencyInjection.cs

## Phase 2: US1 Automatic reminder (P1)

- [x] T008 [US1] Implement `ReminderProcessor` (eligibility, reschedule handling, skip without email, claim attempt, token generation, email send via IEmailSender, delivery log, bounded retry) in MedFlow.Infrastructure/Reminders/ReminderProcessor.cs and register in DependencyInjection.cs
- [x] T009 [US1] Add `ReminderBackgroundService` (PeriodicTimer, scope per pass, swallow and log errors) in MedFlow.Api/Services/ReminderBackgroundService.cs and register in Program.cs
- [x] T010 [US1] Tests in MedFlow.Api.Tests/ReminderTests.cs: sent once and idempotent, outside window, closed/past statuses, no email skipped, failure then retry with max attempts, reschedule re-sends; update TestApiFactory to remove hosted services

## Phase 3: US2 Confirm or cancel from link (P1)

- [x] T011 [US2] Implement `ReminderRepository` (lookup by token hash, respond rules) in MedFlow.Infrastructure/Repositories/Repositories.cs and register
- [x] T012 [US2] Add anonymous `AppointmentResponseController` (lookup, respond, uniform 404, 409 closed) and rate-limit policy in MedFlow.Api/Controllers/AppointmentResponseController.cs and Program.cs
- [x] T013 [US2] Tests in ReminderTests.cs: confirm, cancel, cancelled cannot be re-confirmed, invalid/expired/tampered identical response, link cannot touch other appointments, lookup does not change state, stale token after reschedule
- [x] T014 [US2] Client: types in medflow-client/src/types/index.ts, `appointmentResponseApi` in medflow-client/src/api/services.ts, public page medflow-client/src/pages/AppointmentResponsePage.tsx, route in medflow-client/src/App.tsx

## Phase 4: US3 Doctor delivery log (P2)

- [x] T015 [US3] Add `GET /api/appointments/{id}/reminders` (owner-only, 404 otherwise) in MedFlow.Api/Controllers/AppointmentsController.cs using `IReminderRepository`
- [x] T016 [US3] Tests in ReminderTests.cs: owner sees log, other doctor 404, patient token rejected, anonymous 401
- [x] T017 [US3] Client: `appointmentsApi.reminders`, query hook in medflow-client/src/hooks/queries.ts, display log and response in medflow-client/src/pages/AppointmentsPage.tsx

## Phase 5: Polish

- [x] T018 Document `Reminders` settings in MedFlow.Api/appsettings.json (non-secret defaults only) and README.md
- [ ] T019 Run dotnet build, dotnet test, npm run build, npm run lint

Dependencies: Phase 1 first; US1 and US2 share T011 only for tests; US3 after US1 entities. MVP: Phase 1 + US1 + US2.
