# Research: Multi-user Clinic with Roles

## Decisions

1. **Authorization mechanism**
   - Decision: dynamic permission policies (`HasPermissionAttribute` -> policy per `Permission` value) with one `AuthorizationHandler<PermissionRequirement>` that loads membership from the database.
   - Rationale: ASP.NET Core native, one place for deny rules, 401 for anonymous and 403 for forbidden behave as today, scope is computed once per request.
   - Alternatives: `[Authorize(Roles=...)]` from token claims (stale after deactivation, role changes need re-login; rejected); a service call at the top of each action (scattered, forgettable; rejected); middleware (cannot know the endpoint's permission).

2. **Scope carrier**
   - Decision: `ClinicScope(ClinicId, UserId, Role)` record in Core placed in `HttpContext.Items` by the handler; repositories receive it as a parameter.
   - Rationale: explicit data flow, easy to unit test, no ambient state in repositories.
   - Alternatives: scoped `ICurrentClinic` service injected into DbContext with global query filters (defence in depth, but anonymous/public/portal/background paths run without scope and would either fail open or need many bypasses; deferred as follow-up).

3. **ClinicId on records**
   - Decision: denormalized `ClinicId` on every doctor-scoped record table (patient-owned and Patient), stamped centrally in `SaveChangesAsync`; indexed; FK to `Clinics`.
   - Rationale: direct indexed filter, defence in depth, matches the "every record has a clinic" requirement; stamping removes the risk of a writer forgetting it and keeps tests that seed rows directly valid.
   - Alternatives: derive scope only by joining Patient (no schema change on children, but every query joins and audit rows of deleted patients lose scope).

4. **One clinic per user**
   - Decision: unique index on `ClinicMembers.UserId`.
   - Rationale: removes cross-clinic switching from tokens and UI; documented follow-up.

5. **Roles source of truth**
   - Decision: `ClinicMembers.Role` (database), read per request. Token role claim is display only.
   - Rationale: immediate deactivation and role change; patient detection stays the `Patient` Identity role.

6. **Last-owner protection under concurrency**
   - Decision: per-clinic in-process `SemaphoreSlim` + `Clinic.UpdatedStamp` concurrency token + re-count inside the lock.
   - Rationale: same style as `BookingRepository` locks; token covers multiple instances on SQL Server.

7. **Invitation token**
   - Decision: reuse portal-invitation pattern (random 32 bytes hex, SHA-256 at rest, lifetime 7 days, superseded, UsedAt). Accept is public POST with email + token, rate limited by a new `staff-invitation` policy, one generic error.
   - Rationale: proven pattern in this codebase.

8. **Inviting an existing email**
   - Decision: identical success response, no row, no email.
   - Rationale: constitution IV (no account enumeration). Cost: the Owner gets no feedback; documented.

9. **Non-doctor note authors**
   - Decision: drop FK `MedicalNotes.DoctorId -> Doctors.UserId`; resolve name via Doctors then Users.
   - Rationale: SQL Server would reject a nurse-authored note with the FK; InMemory tests would not catch this, so it is handled explicitly. Down reassigns such notes to the patient's doctor before restoring the FK.

10. **Migration backfill approach**
    - Decision: temporary `Clinics.LegacyDoctorUserId` to map doctors to clinics in set-based SQL; child rows take `ClinicId` from their patient; guard with `THROW` if any `ClinicId = 0` remains; column dropped at the end; indexes/FKs created after backfill.
    - Alternatives: row-by-row cursor (slower, no benefit); EF data code in migration (needs services).

11. **Report scoping**: Owner clinic-wide; Doctor own (`DoctorId == caller`); others denied. Rationale: reports include revenue; own-scope for doctors matches prior behaviour.

12. **Audit log readers**: Owner (clinic patients) and Doctor (only treating patients). Rationale: least privilege; the log lists who looked at a patient.

13. **Searching**: receptionist patient search excludes `PrimaryCondition` matching to avoid inferring withheld data.

14. **Premise-changed existing tests**: a registered doctor is now Owner of their clinic, so assertions of role label `Doctor` for that user change to `Owner` (4 tests, one literal each).

All NEEDS CLARIFICATION items were resolved in the spec's clarification session.
