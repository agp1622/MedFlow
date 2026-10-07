# Feature Specification: Remove Committed Secrets and Load Them From Configuration

**Feature Branch**: `047-remove-committed-secrets`
**Created**: 2026-10-06
**Status**: Draft
**Input**: GitHub issue #56 - "Remove committed secrets and load them from configuration" (Refs #56)

## Clarifications

### Session 2026-10-06

- Q: Should seeding fall back to any built-in email or password when none is configured? → A: No; seeding is skipped with a warning when the seed email or password is missing (stricter option).
- Q: How is a placeholder signing key recognised? → A: Case-insensitive match on common placeholder markers (e.g. "change", "your", "placeholder", "replace", "example", "secret") or empty/whitespace; any key under 32 characters also fails.
- Q: Which environments skip the strong-key check? → A: Only Development and Testing; every other environment name is enforced.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A repository free of real credentials (Priority: P1)

An operator or reviewer opens the committed configuration files and finds no real passwords, keys or
account credentials: email service password, seed account password and signing key are all empty or
clearly marked placeholders, and the values are supplied from the hosting environment (environment
variables, a secret store, or per-developer local secrets).

**Why this priority**: Credentials committed to source control are treated as leaked and block commercial rollout.

**Independent Test**: Search tracked configuration files for password/key values; none are real. Starting the
API in Development with locally supplied values still works.

**Acceptance Scenarios**:

1. **Given** the repository, **When** the committed configuration files are inspected, **Then** the email app password, seed passwords and signing key contain no real value.
2. **Given** a developer with locally supplied secrets, **When** they run the stack locally, **Then** sign-in, seeding and email behave as before.
3. **Given** the seeder runs with no seed password configured, **When** the database is empty, **Then** no account is created and a warning is logged (no built-in fallback password exists).

---

### User Story 2 - Production refuses to start with an unsafe signing key (Priority: P1)

When the API starts in any environment other than Development or Testing, it verifies that the token signing
key is configured, is not a placeholder, and is at least 32 characters. If not, startup fails with a clear
message that does not reveal the key.

**Why this priority**: A guessable signing key lets anyone forge staff or patient tokens.

**Independent Test**: Start the API in Production with a missing, placeholder, or short key and observe failure; with a strong key observe success.

**Acceptance Scenarios**:

1. **Given** Production, **When** the key is missing, a placeholder, or under 32 characters, **Then** the API fails to start with a descriptive error.
2. **Given** Production with a strong key, **When** the API starts, **Then** it starts normally.
3. **Given** Development or Testing, **When** the key is a short local value, **Then** the check is not enforced.

---

### User Story 3 - Seed accounts only in Development (Priority: P2)

Demo doctor and demo patient accounts are created only in Development, and only from explicitly configured credentials.

**Why this priority**: Known accounts must never exist in a live environment.

**Independent Test**: Start in Production against an empty database: no accounts are seeded. Start in Development with configured seed values: accounts are created.

**Acceptance Scenarios**:

1. **Given** a non-Development environment, **When** the API starts, **Then** the seeder is never invoked.
2. **Given** Development with seed email and password configured, **When** the database has no users, **Then** the accounts are created.

---

### User Story 4 - Documented setup and leak response (Priority: P2)

README documents how to supply each secret in each environment, and states that the previously committed
credentials are leaked and must be rotated, and that git history must be scanned and purged as needed.

**Independent Test**: Follow the README on a clean checkout to run locally and to deploy.

**Acceptance Scenarios**:

1. **Given** a new developer, **When** they follow the README, **Then** they can supply all required secrets locally.
2. **Given** an operator, **When** they read the README, **Then** they find rotation and history-scan instructions.

### Edge Cases

- Key exactly 32 characters passes; 31 fails.
- Key with only whitespace, or equal to a known placeholder text, fails.
- Staging or any custom environment name is treated as non-Development.
- Seed email configured but password missing: seeding skipped with a warning, startup continues.
- Email service password empty: outgoing email fails gracefully as it does for any unconfigured mail setup; the API still starts.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Committed configuration MUST NOT contain real passwords, keys or private account credentials (email password, seed passwords, signing key).
- **FR-002**: Secrets MUST be readable from environment variables and compatible with a secret store; Development MUST support per-developer local secrets.
- **FR-003**: Outside Development and Testing, startup MUST fail if the signing key is missing, a placeholder, or shorter than 32 characters; the error MUST NOT print the key.
- **FR-004**: The seeder MUST run only in Development and MUST NOT contain hardcoded fallback passwords or personal fallback emails; it MUST skip with a warning when required seed values are absent.
- **FR-005**: Local container and cloud deployment definitions MUST pass secrets by reference or parameter, not committed real values.
- **FR-006**: README MUST document required secrets per environment, local setup, the leaked-credential rotation requirement, and git history scanning.
- **FR-007**: Automated tests MUST cover the startup validation (failure cases and success case) and the Development-only seeding.

### Key Entities

- **Secret setting**: a configuration value (signing key, email password, seed passwords, connection password) that must be supplied externally.
- **Environment**: Development, Testing, or any other (treated as production-like).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A scan of tracked configuration files finds zero real credential values.
- **SC-002**: 100% of production-like startups with a missing, placeholder or short signing key are refused.
- **SC-003**: Zero accounts are seeded in any non-Development environment.
- **SC-004**: A new developer can run the app locally using only README instructions.

## Assumptions

- Rotation of the leaked email and seed credentials and purging git history are manual operator actions outside this change; the change documents them.
- The Google sign-in client id is a public identifier, not a secret, and is left in configuration.
- Local database container password is a throwaway dev value and may stay in local-only compose files supplied via environment defaults.
- The existing Development-only migrate-and-seed block is the single seeding entry point.
