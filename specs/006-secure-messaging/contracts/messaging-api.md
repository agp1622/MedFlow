# API Contract: Secure Messaging

All routes under `/api`. Enums serialize as strings. Errors use `{ "error": "..." }` like existing controllers. Auth: **Doctor** / **Patient** = JWT with that role; no token → `401`; wrong role → `403`.

## DTOs

```text
MessageDto            { id, senderRole: "Patient"|"Doctor", body, sentAt, readAt|null, isMine: bool,
                        attachments: MessageAttachmentDto[] }
MessageAttachmentDto  { id, fileName, contentType, fileSize }
MessageThreadSummaryDto { patientId, patientName, lastMessageAt, lastMessagePreview, unreadCount }
UnreadCountDto        { unreadCount }
```

`isMine` is computed for the caller. Internal ids (`SenderUserId`, `DoctorId`, `StoredFileName`) are never returned.

## Patient  (role Patient; patient resolved from the token, portal 403 when unavailable)

| Method & route | Returns / behaviour |
|---|---|
| `GET /portal/messages` | `MessageDto[]` oldest first (empty array if no thread yet). Writes an audit entry |
| `POST /portal/messages` | multipart: `body` (text), `files` (0–5). `201 MessageDto` |
| `POST /portal/messages/read` | Marks the doctor's messages read. `204` |
| `GET /portal/messages/unread-count` | `UnreadCountDto` |
| `GET /portal/messages/attachments/{id}/download` | File stream; `404` if missing or not in the caller's thread |

## Doctor  (role Doctor; patient must belong to the caller, else `404`)

| Method & route | Returns / behaviour |
|---|---|
| `GET /messages/threads` | `MessageThreadSummaryDto[]`, most recent first (only patients with a thread) |
| `GET /messages/patient/{patientId}` | `MessageDto[]` oldest first. Writes an audit entry |
| `POST /messages/patient/{patientId}` | multipart as above. `201 MessageDto`; creates the thread if absent |
| `POST /messages/patient/{patientId}/read` | Marks the patient's messages read. `204` |
| `GET /messages/unread-count` | `UnreadCountDto` (all of the doctor's threads) |
| `GET /messages/attachments/{id}/download` | File stream; `404` if missing or not in one of the doctor's threads |

## Send errors (both roles)

| Status | Body | When |
|---|---|---|
| `400` | `{ "error": "Message cannot be empty." }` | No text and no files |
| `400` | `{ "error": "Message is too long (max 4000 characters)." }` | Body too long |
| `400` | `{ "error": "A message can include at most 5 files." }` | Too many files |
| `400` | `{ "error": "File exceeds the 50 MB size limit." }` / `{ "error": "File type '…' is not allowed." }` | Same wording as the existing attachments endpoint; nothing is stored |
| `403` | `{ "error": "Portal access is unavailable." }` | Patient inactive/unlinked |
| `404` | | Doctor: patient not theirs or missing |

## Changes to existing behaviour

- `GET /attachments/patient/{id}`, `PUT /attachments/{id}/sharing`, `GET /attachments/{id}/download|preview`, `DELETE /attachments/{id}`, and the portal attachment list/download ignore attachments that belong to a message (they return `404` / are omitted).
- The upload allow-list and size limit are unchanged; they now live in one shared helper.

## Client (`api/services.ts`)

- `messagesApi` (doctor): `threads`, `thread(patientId)`, `send(patientId, body, files)`, `markRead(patientId)`, `unreadCount`, `downloadAttachment(id, fileName)`.
- `portalApi` gains: `messages`, `sendMessage(body, files)`, `markMessagesRead`, `messagesUnreadCount`, `downloadMessageAttachment(id, fileName)`.
- Downloads use authenticated blob requests (no tokens in URLs), like the existing portal download.
