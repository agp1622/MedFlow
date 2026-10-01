# Data Model: Patient Portal – My Records

Changes are delivered as one EF Core migration (name suggestion: `AddPatientPortal`) that also seeds roles and backfills `Doctor` role assignments.

## Identity

| Item | Change |
|---|---|
| `AspNetRoles` | Add `Doctor`, `Patient` |
| `AspNetUserRoles` | Backfill: every user with a row in `Doctors` gets `Doctor` |
| JWT | Adds `role` claim(s); `UserDto.Role` exposed to the client |

## Edited entities

### Patient
- `PortalUserId` (string, nullable, FK-by-convention to `AspNetUsers.Id`, **unique filtered index** where not null) – the linked patient account.
- Derived in DTOs: `PortalStatus` = `NotInvited | Invited | Active` (computed from `PortalUserId` and an unexpired, unused invitation).

### PatientAttachment
- `SharedWithPatient` (bool, not null, default `false`)

### MedicalNote
- `SharedWithPatient` (bool, not null, default `false`)

## New entities

### PortalInvitation
| Field | Type | Notes |
|---|---|---|
| Id | int | PK |
| PatientId | int | FK → Patient, indexed |
| Email | string | Email at invite time (a later email change invalidates it) |
| TokenHash | string | SHA-256 of the random token; the raw token exists only in the email link |
| ExpiresAt | DateTime (UTC) | Created + 7 days |
| UsedAt | DateTime? | Set on acceptance |
| CreatedAt | DateTime | From `BaseEntity` |

Rules: single-use; creating a new invitation for a patient marks earlier unused ones as superseded (set `UsedAt`/treat as invalid); invalid if `Patient.Email` ≠ `Email`, patient not `Active`, or patient already has `PortalUserId`.

**States:** `Pending` → `Accepted` (UsedAt set) | `Expired` (time) | `Superseded` (replaced).

### PortalAccessLog
| Field | Type | Notes |
|---|---|---|
| Id | int | PK |
| PatientId | int | FK → Patient, indexed |
| ResourceType | string | `Attachment` or `Note` |
| ResourceId | int | |
| Action | string | `View` or `Download` |
| OccurredAt | DateTime (UTC) | |

## Relationships

```text
Doctor 1─* Patient 1─0..1 AspNetUser (Patient role)   via Patient.PortalUserId
Patient 1─* PortalInvitation
Patient 1─* PortalAccessLog
Patient 1─* PatientAttachment / MedicalNote (SharedWithPatient)
```

## Validation rules (from the spec)

- Invite requires a valid, non-empty `Patient.Email` and `Status == Active` (FR-007, edge cases).
- Only items with `SharedWithPatient == true` are returned or downloadable via the portal (FR-005, FR-006).
- Portal queries always filter by the patient resolved from the token (FR-002, FR-003).
- Archived (`Inactive`/`Deceased`) patients get a neutral denial on portal access.
