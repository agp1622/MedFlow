# Tasks: Waitlist for Cancelled Slots

**Input**: specs/044-waitlist/ (spec, plan, data-model, contracts). Tests are included (constitution/build rules require them).

## Phase 1: Foundational (blocking)

- [ ] T001 Add `WaitlistStatus` enum and `AuditItemKind.Waitlist` in MedFlow.Core/Enums/Enums.cs
- [ ] T002 Add `WaitlistEntry` and `WaitlistOffer` entities in MedFlow.Core/Entities/DomainEntities.cs
- [ ] T003 Add waitlist DTO records in MedFlow.Core/DTOs/Dtos.cs
- [ ] T004 Add `IWaitlistRepository`, `IWaitlistService`, and `IBookingRepository.IsSlotOpenAsync` in MedFlow.Core/Interfaces/IRepositories.cs
- [ ] T005 Add DbSets, EF configuration (string enums, indexes) in MedFlow.Infrastructure/Data/AppDbContext.cs and generate migration `AddWaitlist` in MedFlow.Infrastructure/Data/Migrations
- [ ] T006 Add `WaitlistSettings`, register services and settings in MedFlow.Infrastructure/DependencyInjection.cs; implement `IsSlotOpenAsync` in MedFlow.Infrastructure/Repositories/Repositories.cs
- [ ] T007 Add es/en message keys in MedFlow.Api/Localization/Messages.cs and the `waitlist-offer` rate-limit policy in MedFlow.Api/Program.cs

## Phase 2: US1 Staff manage the waitlist (P1)

- [ ] T008 [US1] Implement `WaitlistRepository` list/add/remove (doctor scoped) in MedFlow.Infrastructure/Waitlist/WaitlistRepository.cs
- [ ] T009 [US1] Add doctor `WaitlistController` with `IAuditService` Change events (FR-012) in MedFlow.Api/Controllers/WaitlistController.cs
- [ ] T010 [US1] Tests: add/list/remove, duplicate 409, inactive 400, cross-doctor 404, patient token 403 in MedFlow.Api.Tests/WaitlistTests.cs

## Phase 3: US2 Offers on cancellation (P1)

- [ ] T011 [US2] Implement `WaitlistService.OfferFreedSlotAsync` (eligibility, cap, token, bilingual email) in MedFlow.Infrastructure/Waitlist/WaitlistService.cs
- [ ] T012 [US2] Hook Update/UpdateStatus/Delete in MedFlow.Api/Controllers/AppointmentsController.cs and Cancel in MedFlow.Api/Controllers/AppointmentResponseController.cs
- [ ] T013 [US2] Tests: offers sent in join order up to cap, none for non-bookable/past slot, no duplicate offer, failing email does not fail cancel, other-doctor patients not emailed, reminder-link cancel triggers in MedFlow.Api.Tests/WaitlistTests.cs

## Phase 4: US3 Claim or leave via link (P1)

- [ ] T014 [US3] Implement lookup/claim/leave in `WaitlistRepository` (token hash, expiry, atomic `BookAsync`; FR-012 audit of the booking as the patient's portal user when one exists; inactive patient -> uniform 404) in MedFlow.Infrastructure/Waitlist/WaitlistRepository.cs
- [ ] T015 [US3] Add public `WaitlistOfferController` in MedFlow.Api/Controllers/WaitlistOfferController.cs
- [ ] T016 [US3] Tests: claim books, second claim 409 and single appointment, concurrent claims, bad/expired/used token uniform 404, leave, rate limit in MedFlow.Api.Tests/WaitlistTests.cs

## Phase 5: US4 Portal join/leave (P2)

- [ ] T017 [US4] Add portal waitlist endpoints (FR-004, FR-012 audit via `IAuditService`, fail closed) in MedFlow.Api/Controllers/PortalController.cs
- [ ] T018 [US4] Tests: join/leave/status, token-derived patient, isolation in MedFlow.Api.Tests/WaitlistTests.cs

## Phase 6: Client

- [ ] T019 Add types in medflow-client/src/types/index.ts, `waitlistApi`/`waitlistOfferApi`/portal methods in medflow-client/src/api/services.ts, hooks in medflow-client/src/hooks/queries.ts
- [ ] T020 [P] Doctor WaitlistPage with route and nav in medflow-client/src/pages/WaitlistPage.tsx, App.tsx, layout
- [ ] T021 [P] Public WaitlistOfferPage and route in medflow-client/src/pages/WaitlistOfferPage.tsx, App.tsx
- [ ] T022 [P] Portal waitlist card in medflow-client/src/pages/PortalPage.tsx
- [ ] T023 es/en strings in medflow-client/src/i18n/resources

## Phase 7: Polish

- [ ] T024 Run build, tests, lint, `dotnet ef migrations has-pending-model-changes`; update quickstart notes
