# Phase 1 Data Model: Password Recovery Page and Functionality

## Entities

### Account (existing — `ApplicationUser` / `MedFlow.Infrastructure.Identity.ApplicationUser`)

No schema changes. Relevant existing attributes for this feature:

| Attribute | Source | Relevance to this feature |
|---|---|---|
| `Email` | `IdentityUser.Email` | Target of forgot-password lookups (`UserManager.FindByEmailAsync`) |
| `PasswordHash` (indirect) | `IdentityUser` | Whether the account has a local password — read via `UserManager.HasPasswordAsync(user)`, never inspected directly |
| `SecurityStamp` | `IdentityUser` | Backs Identity's token validity; changes on `ResetPasswordAsync`, which is what makes reset tokens single-use and supersedes older tokens (see `research.md` §1) |
| Lockout fields (`LockoutEnd`, `AccessFailedCount`) | `IdentityUser` | Cleared as part of a successful reset per FR-013, via `UserManager.ResetAccessFailedCountAsync` / `SetLockoutEndDateAsync(user, null)` |

**No new table or migration is introduced by this feature** — see `research.md` §1 for why a
dedicated "password reset request" table is unnecessary given Identity's token provider.

### Password Reset Token (conceptual, not persisted)

Represents the spec's "Password Reset Request" key entity. It is **not** a database row; it is
the opaque, signed token string returned by `UserManager.GeneratePasswordResetTokenAsync(user)`
and consumed by `UserManager.ResetPasswordAsync(user, token, newPassword)`.

| Conceptual field | Backing mechanism |
|---|---|
| Associated account | Encoded inside the token via the user's ID + `SecurityStamp` (Identity internals) |
| Issued at / expiry | Enforced by `DataProtectionTokenProviderOptions.TokenLifespan` (default 1 day) at validation time — not stored explicitly |
| Consumed / single-use | Enforced implicitly: `ResetPasswordAsync` success rotates `SecurityStamp`, invalidating the token (and any sibling tokens) for future use |

This keeps the feature entirely within the existing Identity/EF Core schema — no migration is
required for this story.

## Request/response contracts (DTOs to add to `MedFlow.Core/DTOs/Dtos.cs`)

```csharp
// ── Auth (Password Recovery) ─────────────────────────────────────────────────
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Email, string Token, string NewPassword, string ConfirmPassword);
```

- `ForgotPasswordRequest.Email`: required, must be a syntactically valid email (validated
  client-side with `zod`, and server-side via `[EmailAddress]`/model validation) — but an invalid
  *format* MAY return a validation error (this is a client input error, not an enumeration risk);
  a well-formed but *unregistered* email MUST still receive the generic success response (FR-003).
- `ResetPasswordRequest.Token`: opaque string from the emailed link; passed through unmodified to
  `UserManager.ResetPasswordAsync`. Because reset links are typically emailed with the token
  URL-encoded, the controller must URL-decode it if ASP.NET model binding hasn't already done so.
- `ResetPasswordRequest.NewPassword` / `ConfirmPassword`: `ConfirmPassword` is validated for
  equality against `NewPassword` server-side as defense-in-depth (client already enforces this via
  the `zod` `refine` pattern used in `RegisterPage`'s schema); password policy is enforced by
  `UserManager.ResetPasswordAsync` itself (same `PasswordOptions` configured in
  `DependencyInjection.cs`, so no duplicated rules).

Both endpoints return a simple `{ message: string }` shape on success/generic-acknowledgement,
matching the existing controller convention of anonymous objects for non-entity responses (e.g.
`return BadRequest(new { error = "..." })` in `AuthController`).
