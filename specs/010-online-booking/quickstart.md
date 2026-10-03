# Quickstart

1. `dotnet test MedFlow.Api.Tests --filter BookingTests` - covers availability CRUD/validation, slot listing, booking, conflict, isolation.
2. Manual: run API + client, sign in as a doctor, open Availability, add Mon-Fri 09:00-12:00 and a blocked date. Sign in as an invited patient, open Book appointment, pick a slot, confirm; check the doctor's Appointments list shows it as Pending (Scheduled) and the slot is gone.
