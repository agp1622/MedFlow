# Tasks: Client Linting

**Input**: Design documents from `/specs/041-add-eslint-client/`
**Tests**: None requested; validation is by running lint/build (see quickstart.md).

## Phase 1: Setup

- [ ] T001 Run `npm ci` in medflow-client and record the baseline `npm run build` and `npm run lint` results (lint expected to fail: no config)

## Phase 2: Foundational

- [ ] T002 Create ESLint 8 legacy config medflow-client/.eslintrc.cjs (root, parser @typescript-eslint/parser, extends eslint:recommended, plugin:@typescript-eslint/recommended, plugin:react-hooks/recommended, browser env, ignorePatterns dist/node_modules/.eslintrc.cjs)
- [ ] T003 Add any missing devDependency required by the config to medflow-client/package.json and package-lock.json (none expected)

## Phase 3: User Story 1 - Run lint locally (P1)

**Goal**: `npm run lint` runs and reports. **Independent test**: quickstart steps 2-3.

- [ ] T004 [US1] Run `npm run lint` in medflow-client and list all findings by rule/file
- [ ] T005 [US1] Verify a deliberate hooks violation is reported, then revert (quickstart step 3)

## Phase 4: User Story 3 - Existing code clean (P2)

**Goal**: zero errors and warnings, no behaviour change. **Independent test**: lint + build pass.

- [ ] T006 [US3] Fix findings in medflow-client/src/** with behaviour-neutral edits (e.g. unused imports, empty types)
- [ ] T007 [US3] For findings that cannot be fixed without behaviour change, add narrow `eslint-disable-next-line <rule> -- <reason>` comments in the affected files under medflow-client/src/**
- [ ] T008 [US3] Confirm `npm run lint` exits 0 and `npm run build` still passes in medflow-client

## Phase 5: User Story 2 - Lint gate in CI (P2)

**Independent test**: quickstart step 5.

- [ ] T009 [US2] Add a "Lint React app" script step (`cd medflow-client && npm run lint`) after "Install dependencies" and before "Build React app" in azure-pipelines.yml

## Phase 6: Polish

- [ ] T010 Run `dotnet build MedFlow.sln` and `dotnet test MedFlow.sln` to confirm nothing else changed; verify `git diff --stat` touches no API code

## Dependencies

T001 -> T002/T003 -> T004/T005 -> T006/T007 -> T008. T009 is independent of the code fixes (parallel with T006-T008). T010 last.
MVP: User Story 1 (T001-T005).
