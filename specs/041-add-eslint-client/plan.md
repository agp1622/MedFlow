# Implementation Plan: Client Linting

**Branch**: `041-add-eslint-client` | **Date**: 2026-10-03 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/041-add-eslint-client/spec.md` (GitHub issue #41)

## Summary

`npm run lint` already exists in `medflow-client/package.json` and ESLint 8, `@typescript-eslint/*` 7 and `eslint-plugin-react-hooks` are already devDependencies; only the config file is missing. Add a legacy-format `.eslintrc.cjs` (ESLint 8, package is `"type": "module"` so `.cjs`), fix or narrowly justify findings on existing code without behaviour change, and add a lint step after `npm ci` and next to the build in the `BuildClient` job of `azure-pipelines.yml`.

## Technical Context

**Language/Version**: TypeScript 5.4, React 18, Node 20

**Primary Dependencies**: eslint 8.57, @typescript-eslint/parser + plugin 7.x, eslint-plugin-react-hooks 4.6 (all already declared)

**Storage**: N/A

**Testing**: `npm run lint`, `npm run build`; `dotnet test` unaffected

**Target Platform**: Developer machines and Azure Pipelines ubuntu-latest

**Project Type**: web (client only)

**Performance Goals**: N/A

**Constraints**: zero warnings (existing script uses `--max-warnings 0`); no runtime behaviour changes; no API code changes

**Scale/Scope**: medflow-client/src

## Constitution Check

- I Git workflow: branch `041-add-eslint-client` off `dev`, matches spec folder. PASS
- II Layered architecture: no API/Core/Infrastructure change; client services layer untouched. PASS
- III API contracts: no endpoints changed. PASS
- IV Security: no secrets; no auth change. PASS
- V Simplicity: minimal config, no extra tooling (no Prettier), no new deps unless required. PASS

Post-design re-check: PASS.

## Project Structure

### Documentation (this feature)

```text
specs/041-add-eslint-client/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── tasks.md
```

### Source Code (repository root)

```text
medflow-client/.eslintrc.cjs     # new
medflow-client/src/**            # behaviour-neutral lint fixes only
azure-pipelines.yml              # lint step in BuildClient
```

**Structure Decision**: client-only change plus one pipeline step.
