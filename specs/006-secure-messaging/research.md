# Research: Secure Messaging

## R1. Thread model — one thread per patient
**Decision**: `MessageThread` has a unique `PatientId`; `DoctorId` is copied from `Patient.DoctorId` when the thread is created.
**Rationale**: The domain gives every patient exactly one doctor (`Patient.DoctorId`), so the spec's "per patient–doctor pair" is one thread per patient today. Keeping `DoctorId` on the thread makes the doctor-side filter a simple column match.
**Alternatives**: Thread per (patient, doctor) with a composite key — needless until multi-doctor patients exist (005 explicitly defers that). Subject-based multiple threads — out of scope ("threaded per patient").

## R2. Read state — single `ReadAt` per message
**Decision**: `Message.ReadAt` (nullable). The recipient is always the other party of `SenderRole`, so "unread for user U" = messages in U's threads where sender ≠ U's role and `ReadAt IS NULL`.
**Rationale**: Only two participants exist; a per-recipient read table would add a join for no benefit.
**Alternatives**: Per-recipient `MessageRead` table — only needed for group chat (out of scope). "Last read message id" on the thread — cheaper but loses per-message precision and fights concurrent sends.

## R3. Marking read — explicit call, not a GET side effect
**Decision**: `POST …/read` marks the other party's messages read; the client calls it when a thread is opened/visible.
**Rationale**: GET requests with side effects get triggered by prefetch, retries and polling, which would silently zero the unread count.
**Alternatives**: Mark on GET — simpler but wrong under polling/refetch.

## R4. Message attachments — reuse the pipeline, bind to the message
**Decision**: Extract the allow-list, 50 MB limit and disk write/read from `AttachmentsController` into an `AttachmentStorage` helper used by both it and messaging. Message files are `PatientAttachment` rows with a new nullable `MessageId`, stored in the same `uploads/attachments/{patientId}/` folder. All chart and portal attachment queries exclude rows with a `MessageId`; message files are only downloadable through the message download routes.
**Rationale**: Satisfies "existing attachments pipeline" with identical type/size rules and one place to change them. Excluding them from the chart list avoids message files appearing in the doctor's sharing UI where "shared with patient" would be meaningless or, worse, make a patient's own photo appear shared/unshared inconsistently.
**Alternatives**: A separate `MessageAttachment` table + second storage path — duplicates validation and storage. Surfacing patient uploads in the chart automatically — arguably useful, but changes 003/005 semantics; can be a later feature.

## R5. Sending is one multipart request
**Decision**: `POST` with `multipart/form-data` (`body` + zero or more `files`). All files are validated before anything is written; if any write fails, already-written files are removed and no message is stored.
**Rationale**: Meets the "no half-complete send" edge case with no draft/orphan state. Upload-then-attach would need orphan cleanup.
**Alternatives**: Upload first, send ids — more moving parts, orphaned files.

## R6. Unread delivery — polling
**Decision**: TanStack Query `refetchInterval` of 30 s on the unread-count query, plus invalidation after sends/reads.
**Rationale**: Meets SC-002 (1 minute) with zero new infrastructure; spec puts real-time out of scope.
**Alternatives**: SignalR/WebSockets — new moving part, rejected by Principle V for this scope.

## R7. Audit trail
**Decision**: Reuse `PortalAccessLog` with `ResourceType` `Message` / `MessageAttachment` and a new nullable `ActorUserId` so doctor and patient reads are distinguishable. Logged once per thread open and per file download.
**Rationale**: FR-012 is SHOULD; the table and write path already exist from 005.
**Alternatives**: New audit table — duplicates the existing one.

## R8. Access control rules
**Decision**: Patient routes live in `PortalController` and resolve the patient from the token (portal 403 when inactive/unlinked, per 005). Doctor routes check `Patient.DoctorId == caller` and return 404 otherwise. Foreign/missing threads and files return an identical 404.
**Rationale**: Same non-leaking pattern as 005; satisfies FR-004/FR-008 and SC-003.

## R9. Revoked or deactivated patients
**Decision**: Portal routes already return 403 for inactive/unlinked patients, which blocks reading and sending. Doctors keep read access to history; doctors can still reply (reply sits unread until access returns).
**Rationale**: Matches the edge case "history kept for the doctor" without extra state.

## R10. Limits, protection and retention
**Decision**: Body max 4,000 chars, trimmed; empty body allowed only with ≥1 file; max 5 files per message. Messages are never edited or deleted through the API. Protection in transit = existing HTTPS/JWT; at rest = the same database and disk as other patient data (encryption at rest is a deployment concern, consistent with existing attachments). Retention follows the practice's existing policy; no purge job added.
**Rationale**: Simple defaults; FR-009/010/011. The 5-file cap is an added guard to keep a single request bounded alongside the existing 50 MB per-file limit.
