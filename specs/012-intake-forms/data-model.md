# Data Model

## IntakeLink
Id, PatientId, Email, TokenHash (SHA-256, indexed unique), ExpiresAt, UsedAt (null until submitted or superseded). No navigation to Patient (soft-delete filter), same as `PortalInvitation`.

## IntakeSubmission
Id, PatientId, IntakeLinkId, Status (Pending/Accepted/Rejected, stored as string), SubmittedAt (UTC).
Answers: FirstName, LastName, DateOfBirth, Gender, Phone, Address, City, State, ZipCode, InsuranceProvider, InsurancePolicyNumber, PrimaryCondition, Allergies, CurrentMedications, PastHistory, AdditionalNotes (each with max length).
Consent (immutable): ConsentVersion, ConsentAgreed (always true), SignatureName, SignedAt (UTC).
Decision: DecidedByDoctorId, DecidedAt, RejectionReason (max 500).
DoctorId copied from patient at submission for scoping.

State: Pending -> Accepted | Rejected (terminal).

Validation: required DOB, gender, phone, signature name (<=200), consent true; max lengths 100-2000 per field; DOB not in the future.
