# Implementation Plan: Patient Portal Online Booking

**Branch**: `claude/issue-10-online-booking` | **Date**: 2026-10-03 | **Spec**: [spec.md](spec.md)

## Summary

Doctors maintain weekly availability windows and blocked dates. The portal exposes open 30-minute slots of the patient's own doctor and a booking endpoint that creates a `Pending` appointment (the "Scheduled" state) atomically, preventing double-booking.

## Technical Context

**Language/Version**: C# / .NET 8, React + TypeScript (Vite)
**Primary Dependencies**: ASP.NET Core, EF Core (SQL Server; InMemory in tests), TanStack Query, react-hook-form + zod
**Storage**: two new tables (`DoctorAvailabilities`, `DoctorBlockedDates`) via an EF migration; appointments reuse the existing table
**Testing**: xUnit with `TestApiFactory` in `MedFlow.Api.Tests`
**Project Type**: web-service + SPA
**Constraints**: patient identity and doctor from the token/patient record only; UTC times; 30-minute slots
**Scale/Scope**: single practice, small data volumes

## Constitution Check

- I Git workflow: work on a feature branch off `dev`, local commits only. PASS (branch `claude/issue-10-online-booking`, spec folder `010-online-booking`, as instructed by the requester).
- II Layers: entities/DTOs/interface in Core; EF logic in Infrastructure repository; controllers delegate. Client uses `services.ts`. PASS
- III Contracts: DTO records, string enums, typed client methods. PASS
- IV Security: existing JWT/roles; patient from token; no account-existence leakage (doctor never a parameter). PASS
- V Simplicity: no new status, no cancel/reschedule, no notifications. PASS

## Project Structure

```text
specs/010-online-booking/{spec,plan,research,data-model,quickstart,tasks}.md, contracts/api.md
MedFlow.Core/Entities/DomainEntities.cs      # + DoctorAvailability, DoctorBlockedDate
MedFlow.Core/DTOs/Dtos.cs                    # + availability/slot/booking records
MedFlow.Core/Interfaces/IRepositories.cs     # + IBookingRepository
MedFlow.Infrastructure/Data/AppDbContext.cs  # DbSets, indexes; migration
MedFlow.Infrastructure/Repositories/Repositories.cs # BookingRepository
MedFlow.Api/Controllers/AvailabilityController.cs   # doctor-only
MedFlow.Api/Controllers/PortalController.cs         # + slots, book
MedFlow.Api.Tests/BookingTests.cs
medflow-client/src/{types,api/services.ts,hooks/queries.ts,pages}   # availability settings + portal booking UI
```

**Structure Decision**: extend the existing three-project layout and client service layer; no new projects.
