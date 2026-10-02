# Implementation Plan: Secure Messaging

**Branch**: `006-secure-messaging` | **Date**: 2026-10-02 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/006-secure-messaging/spec.md` (GitHub issue #14)

## Summary

Add asynchronous patient ↔ doctor messaging on top of the patient portal (005) and the attachments
feature (003). Each patient has one thread with their doctor (the data model already ties a patient to
exactly one doctor). Patients use new `/api/portal/messages` routes (patient resolved from the token
only, like every other portal route); doctors use a new `/api/messages` controller scoped to their own
patients. Unread state is one `ReadAt` per message (the recipient is always the other party), exposed
as a count for the doctor dashboard and the portal. Message files reuse the existing attachment
storage, type allow-list and 50 MB limit by extracting that logic into a shared helper, and are stored
as `PatientAttachment` rows bound to a message so they never leak into the chart list or the doctor's
"shared with patient" list. See [research.md](research.md).

## Technical Context

**Language/Version**: C# / .NET 8 (`net8.0`); TypeScript + React (Vite)

**Primary Dependencies**: ASP.NET Core Web API, EF Core (SQL Server), Identity + JWT (roles `Doctor`/`Patient` from 005), Serilog; React, TanStack Query, Zustand, react-hook-form + zod, react-router-dom v6. No new packages.

**Storage**: SQL Server via one new EF Core migration (2 new tables, 2 new columns); files stay on disk under `wwwroot/uploads/attachments/{patientId}/` as today

**Testing**: Extend the existing xUnit + `WebApplicationFactory` project `MedFlow.Api.Tests` with messaging isolation/unread/attachment tests; remaining flows verified via [quickstart.md](quickstart.md)

**Target Platform**: Web (desktop and mobile browsers); API in Docker/Azure per existing pipeline

**Project Type**: web-application (Api / Core / Infrastructure + `medflow-client`)

**Performance Goals**: Thread loads in one round trip; unread count is a single indexed count query; client polls the count every 30 s to meet SC-002 (1 minute) without push infrastructure

**Constraints**: No account-existence or thread-existence leaks (404 for foreign threads/files); no message text in logs, URLs or any unauthenticated surface; no edit/delete of sent messages; body limit 4,000 characters

**Scale/Scope**: One thread per patient; low thousands of patients per doctor; ~9 endpoints, 2 new entities, 2 new columns, 2 new UI areas (portal messages, doctor messages) plus 2 dashboard indicators

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Git workflow | PASS | Branch `006-secure-messaging` off `dev`, named after `specs/006-secure-messaging/`; must be pushed to `origin` and merged by PR. Not yet pushed. |
| II. Layered architecture | PASS | Entities/DTOs/`IMessageRepository` in Core; EF + repo in Infrastructure; thin controllers in Api; client only through new `messagesApi`/`portalApi` methods in `api/services.ts`. |
| III. Consistent API contracts | PASS | Record DTOs, string enums, `/api/[controller]` routes, typed client methods and TS types added in the same change. |
| IV. Security & least privilege | PASS (with care) | Existing Identity/JWT only. Patient resolved from token; doctor access checked against `Patient.DoctorId`. Foreign/missing thread or file → identical 404. Access logged via existing `PortalAccessLog`. Secrets untouched. |
| V. Simplicity | PASS | One thread per patient, single `ReadAt`, no real-time channel, no edit/delete, no notifications. Attachment logic is extracted and reused rather than duplicated. |

**Post-design re-check (after Phase 1)**: unchanged — PASS. The only structural change to existing code is moving upload validation/storage into a shared helper (no behaviour change).

## Project Structure

### Documentation (this feature)

```text
specs/006-secure-messaging/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── messaging-api.md
└── tasks.md             # /speckit-tasks – not created here
```

### Source Code (repository root)

```text
MedFlow.Core/
├── Entities/        # MessageThread, Message (new); PatientAttachment (+MessageId); PortalAccessLog (+ActorUserId)
├── DTOs/Dtos.cs     # MessageDto, MessageThreadSummaryDto, MessageAttachmentDto, UnreadCountDto
└── Interfaces/      # IMessageRepository

MedFlow.Infrastructure/
├── Data/            # AppDbContext sets + config; Migrations/<AddSecureMessaging>
├── Repositories/    # MessageRepository; existing attachment queries exclude message-bound rows
└── DependencyInjection.cs   # register IMessageRepository

MedFlow.Api/
├── Controllers/     # MessagesController (Doctor, new); PortalController (+ message routes);
│                    # AttachmentsController uses the shared helper
├── Services/        # AttachmentStorage (extracted validation + disk write/read, shared by both)
└── Program.cs       # register AttachmentStorage

medflow-client/src/
├── api/services.ts  # messagesApi (doctor), portalApi message methods
├── types/index.ts   # message types
├── hooks/queries.ts # thread, threads, unread-count (30 s refetch) hooks
├── components/messages/   # MessageThreadView, MessageComposer (shared by both roles)
├── components/layout/     # AppLayout nav item + badge; PortalLayout nav item + badge
├── pages/MessagesPage.tsx # doctor: thread list + conversation
├── pages/PortalMessagesPage.tsx   # patient conversation
├── pages/DashboardPage.tsx        # unread indicator
└── App.tsx          # routes /messages and /portal/messages

MedFlow.Api.Tests/MessagingTests.cs   # isolation, unread, attachments, validation
```

**Structure Decision**: Existing three-project backend plus `medflow-client`; no new projects. Messaging UI components are shared between the two roles; pages and routes stay separate because the roles have separate layouts and guards (as in 005).

## Complexity Tracking

No constitution violations to justify.
