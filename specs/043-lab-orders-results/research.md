# Research: Lab Orders and Results

- **Abnormal flag storage**: Decision: compute `LabFlag` (None/Low/High) in the DTO mapping from value and the stored bounds; never persist it. Rationale: a corrected value or range can never leave a stale flag. Alternative: stored column (rejected, drift risk).
- **Reference ranges**: Decision: per-result optional low/high decimals typed by the doctor; no built-in table. Rationale: requirement not to invent clinical ranges; ranges depend on lab, age and sex.
- **Numeric type**: Decision: `decimal(18,4)` for value and bounds, API range limited to +/-999,999,999. Rationale: exact comparison with inclusive bounds.
- **Attachments**: Decision: not linked. Existing `PatientAttachment` (patient-level, category "Test result") already stores lab reports; linking it would need a new FK, new authorization paths and UI. Documented as deferred (spec US5).
- **Status**: Decision: stored on the order; Completed/Ordered is re-derived on result add/remove unless Cancelled. Cancel is explicit and terminal for new results.
- **Audit**: Decision: `AuditItemKind.LabOrder` appended to the enum (stored as string, so no renumbering risk); reads record `View`, writes record `Change` with field names only; result add/remove records changed field `Results`. Audit is called through `AuditAsync`, which also acts as the ownership check (false -> 404).
- **Ownership pattern**: Decision: follow `PatientClinicalRepository` (`OwnsPatientAsync`, `GetOwned...` filtered by patient and doctor) so non-owned patient or order yields the same 404.
- **Localization**: Decision: new `Lab.*` entries in the API `Messages` catalog (es/en) for custom errors; client uses i18n keys under `labs.*`, `enums.*`.
- **Client placement**: Decision: a "Labs" tab on the patient page whose tab button carries the abnormal count, so abnormal values are visible without opening the tab.
