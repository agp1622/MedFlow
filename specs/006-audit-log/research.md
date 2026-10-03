# Research: Audit Log

## Where to record events
- **Decision**: Explicit `IAuditService.RecordAsync(...)` calls in controllers.
- **Rationale**: Patient id is not uniform across routes (some routes carry an item id only); resolving it needs the loaded entity, which the controller already has. Explicit calls keep order control (record before returning data or applying a write) and are easy to test.
- **Alternatives**: MVC action filter (cannot know the patient for item-id routes without extra lookups); EF `SaveChanges` interceptor (covers writes only, not reads, and has no user context).

## Fail-closed behavior
- **Decision**: Record before the response data leaves / before the write is applied; exceptions propagate to the existing error middleware (500). For updates the entity is mutated in memory first, then the audit save persists event and change in one `SaveChanges`.
- **Rationale**: Real DB transactions are unavailable under the EF InMemory test provider and add retry-strategy complexity.
- **Limitation**: Creates record before insert (no item id); patient creation records after insert because the patient id does not exist earlier.

## Ownership
- **Decision**: `RecordAsync` returns false when the patient is not owned by the acting doctor (or, for portal, not linked to the acting patient); controllers return the same 404 as for a missing patient.
- **Rationale**: Prevents events against foreign patients and closes existing id-only lookups (appointments, invoices, prescriptions) that did not check the doctor.

## Changed field names
- **Decision**: Reflect over scalar properties of the entity before/after mutation, store the names only.
- **Alternatives**: Storing before/after values (rejected: PHI in the log).

## Immutability
- **Decision**: `AppDbContext.SaveChangesAsync` throws if an `AuditEvent` entry is Modified or Deleted; no update/delete API; no FK to patient (soft-deleted patients keep events).

## Filters
- **Decision**: action (View/Change), actor name substring, from/to dates (UTC, inclusive), page/pageSize (1-100). `from > to` is a 400.
