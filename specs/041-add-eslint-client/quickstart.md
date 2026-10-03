# Quickstart: Validate Client Linting

1. `cd medflow-client && npm ci`
2. `npm run lint` -> exits 0, no output of findings.
3. Add `if (x) { useState(0) }` inside a component temporarily -> `npm run lint` exits non-zero citing `react-hooks/rules-of-hooks`; revert.
4. `npm run build` -> succeeds.
5. Open `azure-pipelines.yml`: `BuildClient` job has a lint script step after `npm ci` and before the build.
