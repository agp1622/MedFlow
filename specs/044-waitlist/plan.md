# Implementation Plan: Waitlist for Cancelled Slots

**Branch**: `claude/issue-25-waitlist` | **Date**: 2026-10-03 | **Spec**: [spec.md](spec.md)

## Summary

Doctors (and staff using the doctor account) keep a waitlist of their own patients; portal patients can join and leave. When a future active appointment is cancelled or deleted and its start is a currently bookable open slot, a service emails the earliest-joined waiting patients (cap, default 5) an offer with a hashed single-purpose token. A public page backed by token-only POST endpoints lets the patient claim (via the existing atomic `BookAsync`) or leave. No slot hold: the first successful atomic booking wins; others get 409.

## Technical Context

**Language/Version**: C# / .NET 8, React + TypeScript (Vite)
**Primary Dependencies**: ASP.NET Core, EF Core (SQL Server; InMemory in tests), existing `IEmailSender`, `IBookingRepository.BookAsync`, `IAuditService`, TanStack Query, react-hook-form + zod, i18next
**Storage**: two new tables (`WaitlistEntries`, `WaitlistOffers`) via migration `AddWaitlist`
**Testing**: xUnit with `TestApiFactory` (`FakeEmailSender`) in `MedFlow.Api.Tests/WaitlistTests.cs`
**Constraints**: patient from token or doctor ownership only; UTC; tokens 256-bit random, SHA-256 hex at rest; POST bodies for tokens
**Scale/Scope**: single practice; tens of waiting patients

## Constitution Check

- I Git workflow: feature branch off `dev`, local commits only. DEVIATION (requester-mandated): branch `claude/issue-25-waitlist`, spec folder `044-waitlist` (as in 009/010).
- II Layers: entities/DTOs/enums/interfaces in Core; repository, offer service and EF config in Infrastructure; controllers delegate. Client uses `services.ts`. PASS
- III Contracts: DTO records, string enums, `PagedResult<T>` for the doctor list, typed client methods. PASS
- IV Security: doctor routes `[Authorize(Roles = Doctor)]` with ownership scoping (404); portal patient from token; public token-only, uniform 404, rate limited; no PHI in emails; no new auth scheme. PASS
- V Simplicity: no slot hold, no SMS, no new background job (offers are sent inline on cancellation, best effort). PASS

## Project Structure

```text
specs/044-waitlist/{spec,plan,research,data-model,quickstart,tasks}.md, contracts/api.md
MedFlow.Core/Entities/DomainEntities.cs   # + WaitlistEntry, WaitlistOffer
MedFlow.Core/Enums/Enums.cs               # + WaitlistStatus, AuditItemKind.Waitlist
MedFlow.Core/DTOs/Dtos.cs                 # + waitlist records
MedFlow.Core/Interfaces/IRepositories.cs  # + IWaitlistRepository, IWaitlistService, IBookingRepository.IsSlotOpenAsync
MedFlow.Infrastructure/Waitlist/          # WaitlistRepository, WaitlistService, WaitlistSettings
MedFlow.Infrastructure/Data/AppDbContext.cs + Migrations/*AddWaitlist
MedFlow.Api/Controllers/WaitlistController.cs (doctor), WaitlistOfferController.cs (public), PortalController.cs (+waitlist), AppointmentsController.cs and AppointmentResponseController.cs (hooks)
MedFlow.Api/Localization/Messages.cs, Program.cs (rate-limit policy, settings)
MedFlow.Api.Tests/WaitlistTests.cs
medflow-client/src/{types,api/services.ts,hooks/queries.ts,i18n/resources,pages/WaitlistPage.tsx,pages/WaitlistOfferPage.tsx,pages/PortalPage.tsx,App.tsx,layout nav}
```

## Design notes

- Settings `Waitlist:OfferHours` (24, valid 1-168), `Waitlist:MaxOffersPerSlot` (5, valid 1-20); invalid values fall back to defaults.
- Hooks: capture "was active and future" before the mutation, then call `IWaitlistService.OfferFreedSlotAsync(doctorId, start)` after the save, wrapped so exceptions are logged and never fail the cancellation. Triggers: `AppointmentsController` Update (status becomes Cancelled), UpdateStatus, Delete; `AppointmentResponseController.Respond` when action is Cancel and result Ok (appointment id/doctor looked up via the repository).
- Offer eligibility: slot open for the doctor (`IsSlotOpenAsync`: candidate slot per availability, blocked dates, lead time, horizon, and no active doctor appointment overlap); entries Waiting for that doctor, patient Active with valid email, no existing offer for (entry, slot), ordered by join time then id, Take(cap).
- Claim: token hash lookup -> offer unexpired, unclaimed, entry Waiting, patient Active; `BookAsync`; on Booked mark offer claimed + entry Booked; audit as the patient's portal user when one exists. NotAvailable/Conflict/LimitReached -> 409.
- Leave via token: entry Removed.
- Out of scope: rescheduling (moving) an appointment does not offer its old slot.
