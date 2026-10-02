# Data Model: Secure Messaging

New tables and columns ship as one EF Core migration, `AddSecureMessaging`. All new entities derive from `BaseEntity` (`Id`, `CreatedAt`, `UpdatedAt`, `IsDeleted`).

## MessageThread (new)

| Field | Type | Notes |
|---|---|---|
| PatientId | int | Unique index; one thread per patient |
| DoctorId | string(450) | `Patient.DoctorId` at creation (doctor's `UserId`) |
| LastMessageAt | DateTime | Updated on every send; used to order the doctor's list |

Relationships: has many `Message`. No navigation to `Patient` (it has a soft-delete query filter; same approach as `PortalInvitation`). Index on `(DoctorId, LastMessageAt)`.

## Message (new)

| Field | Type | Notes |
|---|---|---|
| ThreadId | int | FK → MessageThread, `Restrict` |
| SenderRole | string | `Patient` \| `Doctor` (stored as string, as other enums) |
| SenderUserId | string(450) | Identity user id of the sender |
| Body | string(4000) | Trimmed; may be empty only when the message has an attachment |
| SentAt | DateTime | UTC; ordering key with `Id` as tie-break |
| ReadAt | DateTime? | Null = unread by the recipient (the other party) |

Indexes: `(ThreadId, SentAt)`; `(ThreadId, SenderRole, ReadAt)` for the unread count.

Validation: body ≤ 4,000 chars; (body non-empty) OR (≥ 1 attachment); at most 5 attachments.
Immutability: no update/delete paths except setting `ReadAt` once.

## PatientAttachment (changed)

| Field | Type | Notes |
|---|---|---|
| MessageId | int? | New. Non-null ⇒ the file belongs to a message |

Rules: rows with a `MessageId` are excluded from the chart attachment list, the sharing endpoint, and the portal shared-attachment list/download. `DoctorId` is the patient's doctor; `SharedWithPatient` is unused for these rows.

## PortalAccessLog (changed)

| Field | Type | Notes |
|---|---|---|
| ActorUserId | string(450)? | New; who performed the action. Existing rows stay null |

New `ResourceType` values: `Message` (action `View`, ResourceId = message id) and `MessageAttachment` (action `Download`).

## Derived: Unread count

- For a **doctor**: count of messages where `Thread.DoctorId = caller`, `SenderRole = Patient`, `ReadAt IS NULL`.
- For a **patient**: count of messages in their thread where `SenderRole = Doctor`, `ReadAt IS NULL`.
- Own messages never count.

## State transitions

`Message`: created (ReadAt = null) → read (ReadAt set when the recipient opens the thread). No other transitions.
