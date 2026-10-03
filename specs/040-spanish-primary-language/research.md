# Research: Spanish as the Primary Language

## Client i18n library
- **Decision**: `i18next` + `react-i18next`, resources bundled in TypeScript modules, `lng` from `localStorage` else `es`, `fallbackLng: 'es'`, no browser-language detection plugin.
- **Rationale**: Named in the issue; bundling avoids a network fetch and flash of untranslated text; no detector keeps "Spanish regardless of browser language" trivially true.
- **Alternatives**: `react-intl` (not requested, heavier message syntax); `i18next-browser-languagedetector` (would honor browser English, contradicts spec FR-001).

## Key parity enforcement
- **Decision**: `en.ts` is typed as the shape of `es.ts` and i18next `CustomTypeOptions` types `t` keys, so `tsc` (part of `npm run build`) fails on any missing key or typo.
- **Rationale**: Satisfies FR-013 / SC-007 without adding a test runner (the client has none) or touching lint config.
- **Alternatives**: vitest key-diff test (new tooling); ESLint plugin (forbidden, issue #41).

## Persistence
- **Decision**: `localStorage` `medflow_lang`, validated against supported list, wrapped in try/catch; falls back to Spanish and still works in-session if storage is blocked.
- **Alternatives**: account-level preference (needs schema change, does not help public pages).

## Formatting
- **Decision**: date-fns `es`/`enUS` locale for dates, `Intl.NumberFormat(lang, {style:'currency', currency:'USD'})` for money; formatting functions read `i18n.language` at call time, components re-render on language change through `useTranslation`.
- **Rationale**: No new dependency; USD retained per clarification.

## Server messages
- **Decision**: Client sends `Accept-Language`; API resolves `es` or `en` (first match by quality order, default `es`) and looks up messages in a C# catalog keyed by id with positional arguments. Identity password errors map by `IdentityError.Code`; unmapped codes use a generic localized message. The `ErrorHandlingMiddleware` messages (including `ArgumentException`/`KeyNotFoundException` text which is internal English) are replaced with localized generic messages for 400/404 to avoid exposing raw English.
- **Rationale**: Keeps response shape (FR-011) and needs no client-side code table; unsupported language falls back to Spanish.
- **Alternatives**: stable error codes translated by the client (changes response contract, rejected in clarify); ASP.NET `IStringLocalizer`/resx (needs resource tooling, more moving parts for ~100 strings).

## Out of scope (clarified)
Outgoing emails, reminders, audit log text. Existing seed/default data.
