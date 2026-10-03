# Research: Client Linting

- Decision: Legacy `.eslintrc.cjs` config. Rationale: ESLint 8.57 is installed and the existing script uses `--ext`, which is a legacy-config flag; package is ESM so config must be `.cjs`. Alternatives: flat config (requires ESLint 9 upgrade and script change; rejected as scope creep).
- Decision: extends `eslint:recommended`, `plugin:@typescript-eslint/recommended`, `plugin:react-hooks/recommended`; parser `@typescript-eslint/parser`; `root: true`; env browser + es2020. Rationale: satisfies "TypeScript and React with react-hooks" with deps already installed. Alternatives: type-aware rules (slower, needs parserOptions.project; rejected), eslint-plugin-react (not installed; JSX runtime is automatic so not needed).
- Decision: ignorePatterns `dist`, `node_modules`, `.eslintrc.cjs`. Rationale: FR-006 and avoid linting config itself.
- Decision: findings fixed behaviour-neutrally, otherwise single-line `eslint-disable-next-line <rule> -- reason`. Alternatives: disabling rules globally (hides issues; only acceptable if a rule is pure noise, and then justified in the config).
- Decision: CI lint step placed between install and build in `BuildClient`. Rationale: fail fast. Branch triggers (`develop`) left untouched (clarified out of scope).
