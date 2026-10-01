# Research: Patient Portal – My Records

Findings from reading the current code, and the decisions that follow. No open NEEDS CLARIFICATION.

## Current-state findings that drive the design

- **No roles exist.** `AuthController.Register` and Google login both create an `ApplicationUser` plus a `Doctor` row; `GenerateToken` emits no role claim. Every controller is plain `[Authorize]` and scopes data by `DoctorId == User.GetUserId()`.
- **Consequence:** a new patient account would pass `[Authorize]` everywhere. Reads would mostly return empty (their id matches no `DoctorId`), but writes such as `POST /api/patients` would **succeed**. Role gating on existing controllers is therefore mandatory, not optional.
- **Google login signs in any existing user by email** and would also mint a doctor for any unknown Google email.
- **Attachment preview** accepts the JWT in an `access_token` query string (`Program.cs`, `/api/attachments`).
- **Pre-existing gaps (out of scope, noted):** `MedicalNotesController.Delete` and `.Create` only check existence, not ownership. Role gating blocks patients from them but a doctor can still touch another doctor's note. Recommend a separate fix.
- `PatientStatus` is `Active | Inactive | Deceased`; `Patient` belongs to exactly one `DoctorId`.

## Decisions

### D1. Roles via ASP.NET Core Identity
- **Decision:** Roles `Doctor` and `Patient`; role claims added to the JWT in `GenerateToken`; `UserDto` gains `Role`.
- **Rationale:** Extends the Identity/JWT setup the constitution requires; no parallel auth.
- **Alternatives:** a boolean `IsPatient` on `ApplicationUser` (not enforceable with `[Authorize(Roles)]`); policy "not Patient" (fails open for any future role).

### D2. Fail-closed gating of existing controllers
- **Decision:** Add `[Authorize(Roles = "Doctor")]` to all existing controllers except `AuthController`. Backfill the `Doctor` role in the migration with SQL (`AspNetRoles` rows, plus `AspNetUserRoles` for every user that has a `Doctors` row), so it also works outside Development where the seeder does not run.
- **Rationale:** Missing role = no access. Without the backfill, existing doctors would be locked out after deploy.
- **Alternatives:** seeder-only backfill (does not run in production).

### D3. Link patient account to record with a nullable unique `Patient.PortalUserId`
- **Decision:** One nullable, filtered-unique column on `Patient`.
- **Rationale:** A patient belongs to one doctor, so the account↔record link is 1:1 (spec assumption); no join table needed (Simplicity).
- **Alternatives:** `PatientAccount` join entity (premature for multi-doctor, which is out of scope).

### D4. Custom hashed invitation instead of reusing the password-reset token
- **Decision:** `PortalInvitation` stores a SHA-256 hash of a random token, email, expiry (7 days) and used-at. Sending a new invitation invalidates earlier unused ones. Accepting creates the user with the `Patient` role, sets the password, links `PortalUserId`, marks the invitation used, and returns a normal `AuthResponse`.
- **Rationale:** Pre-creating the user and reusing reset tokens would make the invitation email take the wrong branch in `forgot-password` (no password → "sign in with Google" email) and would let Google login sign the pending account in.
- **Alternatives:** Identity reset tokens on a pre-created user (above); open self-registration (out of scope per spec).

### D5. Portal endpoints never take a patient id
- **Decision:** `PortalController` (`[Authorize(Roles = "Patient")]`) resolves the patient by `Patient.PortalUserId == User.GetUserId()` on every call. Resource ids (attachment id) are looked up *within* that patient's shared items.
- **Rationale:** Removes the whole class of "change the id in the URL" bugs (spec US2).
- **Alternatives:** reuse existing endpoints with ownership checks (they scope by `DoctorId`, wrong model for patients).

### D6. Sharing as a boolean on each item, default false
- **Decision:** `SharedWithPatient bool NOT NULL DEFAULT 0` on `PatientAttachment` and `MedicalNote`. Doctor toggles via `PUT .../sharing`. Existing rows stay unshared (spec assumption).
- **Alternatives:** separate share table (no need for per-recipient sharing).

### D7. Portal downloads use header auth, not query-string tokens
- **Decision:** Portal attachment download is a normal authenticated GET; the client fetches a blob and opens an object URL. The `access_token` query path stays scoped to the doctor attachments controller only.
- **Rationale:** Avoids putting patient-access tokens in URLs/logs/history.

### D8. Block patients from Google login; give Google-created users the Doctor role
- **Decision:** `google-login` returns the generic invalid-credentials response for users in the `Patient` role; newly created Google users get `Doctor`.
- **Rationale:** Spec assumes email/password only for patients; otherwise an email match would grant a session without the portal's invitation path.

### D9. Access logging
- **Decision:** `PortalAccessLog` rows (patient, resource type/id, action, time) written on shared-attachment download and note view. No UI in this feature.
- **Rationale:** FR-012 asks for recording only; keeps scope tight.

### D10. Patient-safe DTOs
- **Decision:** Dedicated portal DTOs omit internal fields (appointment `Notes`, invoice `Notes`, doctor ids, storage file names).

### D11. Testing
- **Decision:** xUnit + `WebApplicationFactory` limited to access control (patient vs doctor endpoints, cross-patient ids, unshared items, revoked sharing, invitation expiry/reuse). Everything else follows [quickstart.md](quickstart.md).
