# Contract: Audit log API

`GET /api/patients/{patientId}/audit-log` (role Doctor; owner of the patient only)

Query: `action` (View|Change), `actor` (name substring), `from`, `to` (ISO dates, UTC, inclusive), `page` (default 1), `pageSize` (default 20, max 100).

200: `PagedResult<AuditEventDto>` ordered newest first.
`AuditEventDto { id, occurredAt, actorUserId, actorName, actorRole, action, itemKind, itemId, changedFields[] }`

Errors: 400 invalid paging or `from > to`; 401/403 unauthenticated or non-doctor; 404 patient missing or not owned (identical bodies).
Side effect: records a View event of kind AuditLog.
