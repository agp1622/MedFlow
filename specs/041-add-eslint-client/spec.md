# Feature Specification: Client Linting

**Feature Branch**: `041-add-eslint-client`

**Created**: 2026-10-03

**Status**: Draft

**Input**: User description: "Add ESLint to the client (GitHub issue #41). `npm run lint` in medflow-client fails because there is no ESLint configuration file. Add an ESLint config for TypeScript and React (react-hooks rules included) plus any dev dependencies needed; make lint pass on existing code (fix or justify findings, no behaviour change); run lint in the CI pipeline next to the client build. Client-only; no API changes."

## Clarifications

### Session 2026-10-03

- Q: Should lint warnings be tolerated or fail the run? -> A: Zero warnings allowed (matches the existing lint script's max-warnings 0); recommended option.
- Q: Should the pipeline's branch triggers (which list `develop`, not `dev`) be changed as part of this work? -> A: No; out of scope, only a lint step is added to the existing client job.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run lint locally (Priority: P1)

A developer working on the web client runs the project's lint command and gets a real result (pass or a list of findings) instead of a "configuration not found" error.

**Why this priority**: Without a working lint command nothing else in this feature is possible.

**Independent Test**: From a fresh install of the client, run the lint command; it completes and exits successfully on the current code.

**Acceptance Scenarios**:

1. **Given** a fresh install of the client, **When** the developer runs the lint command, **Then** it runs to completion and exits successfully with no findings.
2. **Given** a change that violates a hooks rule (e.g. a hook called conditionally), **When** the developer runs lint, **Then** the violation is reported with file and line and the command exits with failure.

---

### User Story 2 - Lint gate in CI (Priority: P2)

A reviewer relies on the automated pipeline to reject client changes that fail lint, alongside the existing client build.

**Why this priority**: Local lint is easy to forget; the pipeline makes it enforceable.

**Independent Test**: Inspect the pipeline definition: the client job runs lint after dependency install and a lint failure fails the job.

**Acceptance Scenarios**:

1. **Given** a client change with a lint finding, **When** the pipeline runs, **Then** the client job fails at the lint step.
2. **Given** a clean client change, **When** the pipeline runs, **Then** the lint step passes and the build continues.

---

### User Story 3 - Existing code is clean without behaviour change (Priority: P2)

The existing client code passes lint. Findings are fixed in ways that do not change runtime behaviour, or are individually justified where a rule is intentionally not followed.

**Why this priority**: A lint check that fails on day one cannot be enforced.

**Independent Test**: Client build still succeeds and lint passes; diff of source files contains only behaviour-neutral edits.

**Acceptance Scenarios**:

1. **Given** the existing client source, **When** lint runs, **Then** zero errors and zero warnings are reported.
2. **Given** any suppression of a rule in existing code, **When** reviewed, **Then** it is narrowly scoped and carries a written justification.

### Edge Cases

- Generated or build output folders (e.g. the production build output) must not be linted.
- Lint configuration files themselves must not cause parsing failures.
- Unused suppression comments are reported rather than silently kept.
- Existing build must remain passing.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The client project MUST include a lint configuration covering TypeScript and React source, including the rules-of-hooks and exhaustive-dependencies rules.
- **FR-002**: Every tool the lint configuration needs MUST be declared as a development dependency so a clean install can run lint.
- **FR-003**: The lint command MUST pass with zero errors and zero warnings on the existing client code.
- **FR-004**: Fixes to existing code MUST NOT change runtime behaviour; any rule deliberately not followed MUST be suppressed narrowly with a written justification.
- **FR-005**: The CI pipeline MUST run the lint command in the client job, after dependency install and next to the client build, and MUST fail the job on findings.
- **FR-006**: Build output and dependency folders MUST be excluded from linting.
- **FR-007**: The change MUST be limited to the client and pipeline definition; no API or backend code changes.

### Key Entities

Not applicable (no data involved).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The lint command exits successfully with 0 errors and 0 warnings on the current codebase.
- **SC-002**: A deliberately introduced hooks-rule violation causes the lint command to fail 100% of the time.
- **SC-003**: The pipeline's client job includes a lint step that gates the build on a clean result.
- **SC-004**: The client production build and the existing backend test suite still pass unchanged.

## Assumptions

- The lint command and base dev dependencies are already declared in the client's package manifest; only the configuration (and any missing dependency) is absent.
- Developers use the existing package manager workflow with Node 20.
- Stylistic formatting rules (e.g. Prettier) are out of scope.
- Rule severity: findings are errors/zero-warning, matching the existing lint script.
