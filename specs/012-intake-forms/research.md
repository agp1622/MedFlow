# Research: Digital Intake Forms

- **Credential for the public form**: Decision: single-use-per-submission, 14-day, random 32-byte token, only SHA-256 stored (same as `PortalInvitation`). Rationale: new patients have no account; matches existing pattern. Alternatives: portal account first (rejected: defeats "before first visit"); signed JWT (rejected: no revocation).
- **Link in URL**: token in the path/query of a SPA route `/intake/:token`; API takes it as a route/body parameter. Patient from token only.
- **Invalid link responses**: all failure causes return the same 404 body.
- **E-signature**: typed full name + checkbox + server UTC timestamp + consent text version string (`2026-10-v1`) + the consent text snapshot hash is unnecessary; the version identifies the text. Legal sufficiency not asserted; flagged for legal review. Alternatives: drawn signature canvas (rejected: scope, no added legal certainty); third-party e-sign (rejected: new dependency).
- **Mapping to the record**: Patient has Address/City/State/ZipCode/InsuranceProvider/InsurancePolicyNumber/PrimaryCondition/Allergies/Phone/Gender/DateOfBirth. Medications and past history have no fields: appended to Notes on accept as a dated block. Rationale: avoids a schema change to Patient and never overwrites. Note: `Notes` may be exposed elsewhere only if shared (notes are `MedicalNote`, separate); `Patient.Notes` is not in portal DTOs (verify at implement).
- **Rate limiting**: reuse the existing fixed-window limiter pattern in `Program.cs` with a new `intake-public` policy, 10/15min per IP, configurable.
- **Booking confirmation**: no booking code on `dev`; deferred.
- **Concurrency**: mark link used with a conditional update (`UsedAt IS NULL`) so only one submission succeeds.
