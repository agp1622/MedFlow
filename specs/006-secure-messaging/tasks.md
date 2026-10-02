---

description: "Task list for Secure Messaging"
---

# Tasks: Secure Messaging

**Input**: Design documents from `/specs/006-secure-messaging/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/messaging-api.md](contracts/messaging-api.md), [quickstart.md](quickstart.md)

**Tests**: Backend tests extend the existing `MedFlow.Api.Tests` project, as called for in plan.md (access control, unread counts, attachment access, validation). UI behaviour is verified via quickstart.md.

**Organization**: Grouped by user story. US1, US2 and US3 are P1; US4 is P2. US1 and US2 are two halves of one conversation and should ship together.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an unfinished task)
- **[Story]**: US1–US4 from spec.md
- Paths are relative to the repo root

## Path Conventions

Backend: `MedFlow.Core/`, `MedFlow.Infrastructure/`, `MedFlow.Api/`. Frontend: `medflow-client/src/`. Tests: `MedFlow.Api.Tests/`.

---

## Phase 1: Setup

- [X] T001 Confirm the base is healthy before changing anything: from the repo root run `dotnet build MedFlow.sln` and `dotnet test MedFlow.Api.Tests`, and `npm run build` in `medflow-client`; note any pre-existing failures in the PR description so they are not attributed to this feature

---

## Phase 2: Foundational (blocks all user stories)

**Purpose**: schema, DTOs and the repository that every story uses.

- [X] T002 [P] Add `MessageThread` (PatientId, DoctorId, LastMessageAt) and `Message` (ThreadId, SenderRole, SenderUserId, Body, SentAt, ReadAt) entities per data-model.md in `MedFlow.Core/Entities/DomainEntities.cs`
- [X] T003 [P] Add a `MessageSenderRole` enum (`Patient`, `Doctor`) in `MedFlow.Core/Enums/Enums.cs`
- [X] T004 [P] Add nullable `MessageId` to `PatientAttachment` and nullable `ActorUserId` to `PortalAccessLog` in `MedFlow.Core/Entities/DomainEntities.cs`
- [X] T005 [P] Add `MessageDto`, `MessageAttachmentDto`, `MessageThreadSummaryDto` and `UnreadCountDto` records per contracts/messaging-api.md in `MedFlow.Core/DTOs/Dtos.cs`
- [X] T006 Configure the new sets and columns in `MedFlow.Infrastructure/Data/AppDbContext.cs`: `MessageThreads`/`Messages` DbSets; unique index on `MessageThread.PatientId`; index `(DoctorId, LastMessageAt)`; `Message.Body` max length 4000 (required, may be empty string); `SenderRole` stored as string; indexes `(ThreadId, SentAt)` and `(ThreadId, SenderRole, ReadAt)`; FK `Message.ThreadId` with `Restrict`; soft-delete query filters for both new entities; `PatientAttachment.MessageId` index; `PortalAccessLog.ActorUserId` max length 450
- [X] T007 Generate EF migration `AddSecureMessaging` in `MedFlow.Infrastructure/Data/Migrations/` (`dotnet ef migrations add AddSecureMessaging --project MedFlow.Infrastructure --startup-project MedFlow.Api`) and review the generated `Up`/`Down` and the updated `AppDbContextModelSnapshot.cs`
- [X] T008 Declare `IMessageRepository` in `MedFlow.Core/Interfaces/IRepositories.cs` with: `GetMessagesAsync(int patientId, MessageSenderRole viewerRole)`, `SendAsync(int patientId, string doctorId, MessageSenderRole role, string senderUserId, string body)` (creates the thread when absent and updates `LastMessageAt`), `MarkReadAsync(int patientId, MessageSenderRole viewerRole)`, `GetUnreadCountForPatientAsync(int patientId)`, `GetUnreadCountForDoctorAsync(string doctorId)`, `GetThreadsForDoctorAsync(string doctorId)`
- [X] T009 Implement `MessageRepository` in `MedFlow.Infrastructure/Repositories/Repositories.cs` per data-model.md: messages ordered by `SentAt` then `Id`; `isMine` computed from the viewer role; unread = other party's messages with `ReadAt` null; `MarkReadAsync` sets `ReadAt` only on the other party's unread messages; thread summaries ordered by `LastMessageAt` desc with a 100-character preview and unread count; never expose `SenderUserId`/`DoctorId`
- [X] T010 Register `IMessageRepository` → `MessageRepository` in `MedFlow.Infrastructure/DependencyInjection.cs`
- [X] T011 Extend `IPortalRepository.LogAccessAsync` in `MedFlow.Core/Interfaces/IRepositories.cs` and its implementation in `MedFlow.Infrastructure/Repositories/Repositories.cs` with an optional `actorUserId` parameter that is stored in `PortalAccessLog.ActorUserId`; update the existing call sites in `MedFlow.Api/Controllers/PortalController.cs` to pass the caller id
- [X] T012 [P] Add TypeScript types `MessageDto`, `MessageAttachmentDto`, `MessageThreadSummaryDto`, `UnreadCountDto` in `medflow-client/src/types/index.ts`
- [X] T013 Verify `dotnet build` and `dotnet ef database update` succeed and the existing `dotnet test MedFlow.Api.Tests` suite still passes

**Checkpoint**: schema, DTOs and repository exist; no endpoints or UI yet.

---

## Phase 3: User Story 1 – Patient sends a message and reads replies (P1)

**Goal**: a signed-in patient sends a text message to their doctor and sees the whole thread.

**Independent Test**: sign in as a patient, send a message, see it in the thread; add a doctor reply directly in the database (or after US2) and see it appear.

- [X] T014 [US1] Add patient message routes to `MedFlow.Api/Controllers/PortalController.cs` using the existing `ResolvePatientAsync`/`Unavailable` pattern: `GET messages` (returns the thread, empty array if none, logs a `Message`/`View` access entry with the caller id), `POST messages` (`[FromForm] string? body`; trims; rejects empty with `{ "error": "Message cannot be empty." }` and over 4,000 characters with `{ "error": "Message is too long (max 4000 characters)." }`; stores via `IMessageRepository.SendAsync` using the patient's `DoctorId`; `201 MessageDto`). Inject `IMessageRepository`
- [X] T015 [P] [US1] Add `messages`, `sendMessage(body)` (multipart `FormData`) to `portalApi` in `medflow-client/src/api/services.ts`
- [X] T016 [P] [US1] Add `usePortalMessages` and `useSendPortalMessage` (invalidates the messages query) hooks in `medflow-client/src/hooks/queries.ts`
- [X] T017 [P] [US1] Create `MessageThreadView` (chronological list, sender label, timestamp, "mine" vs "theirs" alignment, empty state) in `medflow-client/src/components/messages/MessageThreadView.tsx`, reusing the existing UI primitives in `medflow-client/src/components/ui/index.tsx`
- [X] T018 [P] [US1] Create `MessageComposer` (react-hook-form + zod: required text up to 4,000 characters with a counter, disabled while sending, shows server errors, "not for urgent matters" notice) in `medflow-client/src/components/messages/MessageComposer.tsx`
- [X] T019 [US1] Create `medflow-client/src/pages/PortalMessagesPage.tsx` composing the thread view and composer, and add the `messages` route under the `PatientRoute` in `medflow-client/src/App.tsx`
- [X] T020 [US1] Add a "Messages" link to `medflow-client/src/components/layout/PortalLayout.tsx`
- [X] T021 [US1] Add `MedFlow.Api.Tests/MessagingTests.cs` with US1 tests: patient send creates a thread and a second send appends to the same thread; empty and over-length bodies return 400 and store nothing; a doctor token calling `/api/portal/messages` gets 403; an unauthenticated call gets 401; an unlinked/inactive patient gets 403. Add any needed helpers (e.g. send a message as a patient) to `MedFlow.Api.Tests/TestApiFactory.cs`

**Checkpoint**: patients can send and read text messages.

---

## Phase 4: User Story 2 – Doctor reads and replies (P1)

**Goal**: a doctor sees threads from their own patients, reads one and replies.

**Independent Test**: after a patient has sent a message, the doctor opens the thread, replies, and the patient sees the reply.

- [X] T022 [US2] Create `MedFlow.Api/Controllers/MessagesController.cs` (`[Authorize(Roles = Roles.Doctor)]`, route `api/[controller]`) with `GET threads`, `GET patient/{patientId}` (404 when the patient is not owned by the caller; logs a `Message`/`View` entry with the caller id), and `POST patient/{patientId}` (same validation and errors as T014; sender role `Doctor`; 404 for foreign or missing patients). Verify ownership through the existing patient repository/`DoctorId` check used by `PatientsController`
- [X] T023 [P] [US2] Add `messagesApi` (`threads`, `thread(patientId)`, `send(patientId, body)`) to `medflow-client/src/api/services.ts`
- [X] T024 [P] [US2] Add `useMessageThreads`, `useMessageThread(patientId)` and `useSendMessage` hooks in `medflow-client/src/hooks/queries.ts`
- [X] T025 [US2] Create `medflow-client/src/pages/MessagesPage.tsx`: thread list on the left (patient name, preview, last activity, unread badge placeholder), selected conversation on the right using `MessageThreadView` and `MessageComposer`; on narrow screens show list and conversation as separate views
- [X] T026 [US2] Add the `messages` route inside the doctor-only area in `medflow-client/src/App.tsx` and a "Messages" item in `medflow-client/src/components/layout/AppLayout.tsx`
- [X] T027 [US2] Add US2 tests to `MedFlow.Api.Tests/MessagingTests.cs`: doctor sees the patient's message and can reply and the patient sees the reply; doctor B gets 404 on doctor A's patient thread (read and send); a patient token on `/api/messages/*` gets 403; thread list is ordered by last activity and contains only the doctor's own patients

**Checkpoint**: full text conversation works end to end between patient and doctor.

---

## Phase 5: User Story 3 – Unread count on the dashboard (P1)

**Goal**: both parties see an unread count that drops when they open the thread.

**Independent Test**: send a message from one side; the other side's count increases by one within 30 seconds; opening the thread clears it.

- [X] T028 [US3] Add `POST messages/read` and `GET messages/unread-count` to `MedFlow.Api/Controllers/PortalController.cs` (patient viewer role; `204` and `UnreadCountDto`)
- [X] T029 [US3] Add `POST patient/{patientId}/read` and `GET unread-count` to `MedFlow.Api/Controllers/MessagesController.cs` (404 for foreign patients on the read route; count covers all of the doctor's threads); include `unreadCount` per thread in `GET threads`
- [X] T030 [P] [US3] Add `markMessagesRead`, `messagesUnreadCount` to `portalApi` and `markRead(patientId)`, `unreadCount` to `messagesApi` in `medflow-client/src/api/services.ts`
- [X] T031 [P] [US3] Add `useUnreadCount` hooks for each role (query refetch interval 30 s, refetch on window focus) and `useMarkRead` mutations that invalidate the unread-count and thread queries, in `medflow-client/src/hooks/queries.ts`
- [X] T032 [US3] Call mark-read when a thread is opened and when new messages arrive while it is visible, in `medflow-client/src/pages/PortalMessagesPage.tsx` and `medflow-client/src/pages/MessagesPage.tsx`
- [X] T033 [US3] Show the unread indicator on the doctor dashboard in `medflow-client/src/pages/DashboardPage.tsx` (hidden when zero, links to Messages), add a badge to the Messages item in `medflow-client/src/components/layout/AppLayout.tsx`, and show per-thread badges in `medflow-client/src/pages/MessagesPage.tsx`
- [X] T034 [US3] Show the unread indicator on the patient landing page `medflow-client/src/pages/PortalPage.tsx` (hidden when zero, links to Messages) and a badge on the Messages link in `medflow-client/src/components/layout/PortalLayout.tsx`
- [X] T035 [US3] Add US3 tests to `MedFlow.Api.Tests/MessagingTests.cs`: a doctor message raises the patient's count and the patient's message raises the doctor's; own messages never count; mark-read zeroes only the caller's side; a GET of the thread does not change the count; doctor B cannot mark doctor A's thread read (404); counts are isolated between patients

**Checkpoint**: unread counts work for both roles.

---

## Phase 6: User Story 4 – Attach files to a message (P2)

**Goal**: either party can attach files using the existing attachment rules; only thread participants can download them.

**Independent Test**: send a message with a PDF and a JPG as the patient; the doctor downloads both; a different patient and a different doctor get 404.

- [X] T036 [US4] Extract the content-type allow-list, 50 MB limit, stored-file-name generation, and disk write/read/delete logic from `MedFlow.Api/Controllers/AttachmentsController.cs` into a new `MedFlow.Api/Services/AttachmentStorage.cs` (no behaviour or message-text change), register it in `MedFlow.Api/Program.cs`, and switch `AttachmentsController` and `PortalController` download code to it
- [X] T037 [US4] Extend `IMessageRepository.SendAsync` and its implementation to take stored-file descriptors and create `PatientAttachment` rows with `MessageId`, `PatientId` and the patient's `DoctorId`; add `GetMessageAttachmentForPatientAsync(id, patientId)` and `GetMessageAttachmentForDoctorAsync(id, doctorId)` returning null when the file is not in an allowed thread; include `attachments` in `MessageDto`, in `MedFlow.Core/Interfaces/IRepositories.cs` and `MedFlow.Infrastructure/Repositories/Repositories.cs`
- [X] T038 [US4] Exclude rows with a `MessageId` from every existing attachment query in `MedFlow.Infrastructure/Repositories/Repositories.cs` (`GetByPatientAsync`, `GetWithOwnerCheckAsync`, the portal shared list and `GetSharedAttachmentAsync`) so message files never appear in the chart, can't be shared, previewed, or deleted from there
- [X] T039 [US4] Accept `[FromForm] IFormFileCollection? files` in the send actions of `MedFlow.Api/Controllers/PortalController.cs` and `MedFlow.Api/Controllers/MessagesController.cs`: allow empty body when at least one file is present; reject more than 5 files (`A message can include at most 5 files.`); validate every file with `AttachmentStorage` before writing any; use the existing wording `File exceeds the 50 MB size limit.` / `File type '…' is not allowed.`; if any write or database save fails, delete the files already written and store nothing; set `[RequestSizeLimit]` for the multi-file request
- [X] T040 [US4] Add `GET messages/attachments/{id}/download` to `PortalController` and `GET attachments/{id}/download` to `MessagesController`: identical `404` for missing, foreign or non-message files; write a `MessageAttachment`/`Download` audit entry with the caller id; return the file with its original name and content type
- [X] T041 [P] [US4] Extend `sendMessage`/`send` to include `files` and add `downloadMessageAttachment(id, fileName)` / `downloadAttachment(id, fileName)` (authenticated blob request, no token in the URL) to `portalApi` and `messagesApi` in `medflow-client/src/api/services.ts`
- [X] T042 [US4] Add a file picker with a removable list of chosen files (max 5, client-side type and size hints matching the server) to `medflow-client/src/components/messages/MessageComposer.tsx`; allow sending with files and no text; show server rejection messages
- [X] T043 [US4] Render attachment chips (file name, size, download action) in `medflow-client/src/components/messages/MessageThreadView.tsx`, wired to the role-appropriate download function passed in as a prop from `PortalMessagesPage.tsx` and `MessagesPage.tsx`
- [X] T044 [US4] Add US4 tests to `MedFlow.Api.Tests/MessagingTests.cs`: send with PDF + JPG and the other party downloads both with the original name; disallowed type and oversize file return 400 with nothing stored (no rows, no files on disk); six files return 400; empty text with a file succeeds; another patient and another doctor get 404 on the download; message files are absent from `GET /api/attachments/patient/{id}` and the portal attachment list; `PUT /api/attachments/{id}/sharing` on a message file returns 404

**Checkpoint**: all four stories work independently.

---

## Phase 7: Polish & Cross-Cutting

- [X] T045 [P] Confirm no message text appears in logs or URLs: search `MedFlow.Api` for logging of request bodies/`Body` values and for query-string use in the new client calls; fix any hits
- [X] T046 [P] Add the "Secure messaging" section to `README.md` listing the new routes and the 4,000-character / 5-file limits, if the README documents portal features
- [ ] T047 Run every scenario in [quickstart.md](quickstart.md) in a running dev server (doctor and patient in separate browser profiles), including the mobile-width layout of both Messages pages, and fix any gaps found
- [X] T048 Run `dotnet build MedFlow.sln`, `dotnet test MedFlow.Api.Tests` and `npm run build` in `medflow-client`; all must succeed
- [ ] T049 Push `006-secure-messaging` to `origin` and open a PR into `dev` that references this spec and plan (constitution Principle I)

---

## Dependencies & Execution Order

- **Phase 1 → Phase 2** then user stories. Phase 2 blocks everything.
- **US1 → US2**: US2 reuses the shared `MessageThreadView`/`MessageComposer` from US1 (T017, T018); backend work for US2 (T022) can start right after Phase 2 in parallel with US1.
- **US3** depends on the endpoints and pages of US1 and US2 (it adds to the same controllers and pages).
- **US4** depends on US1 and US2 (send actions, composer, thread view); T036 can start any time after Phase 2.
- **Polish** is last.

```text
Phase 1 → Phase 2 ─┬─ US1 ─┬─ US3 ─┐
                   └─ US2 ─┤       ├─ Polish
                           └─ US4 ─┘
```

## Parallel Opportunities

- Phase 2: T002, T003, T004, T005 and T012 touch different files and can run together.
- US1: T015, T016, T017, T018 in parallel after T014's contract is fixed.
- US2: T022 (backend) alongside T023/T024 (client); T025 follows.
- US3: T030 and T031 in parallel; T033 and T034 touch different files.
- US4: T036 can run alongside any US1–US3 work; T041 in parallel with T039/T040.
- Polish: T045 and T046.

## Implementation Strategy

- **MVP**: Phases 1–4 (US1 + US2) — a working text conversation between patient and doctor. Not releasable on its own because the issue's acceptance criteria also require unread counts, so add **US3** before merging.
- **Increment 2**: US3 (unread counts), then US4 (attachments). Each can be demonstrated on its own using the Independent Test above.
- Constitution Principle V: do not merge partial slices as complete — the PR to `dev` should include all four stories, since both acceptance criteria in issue #14 (unread count, attachments) are required.
