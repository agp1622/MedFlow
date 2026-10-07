# Implementation Plan: Spanish as the Primary Language

**Branch**: `040-spanish-primary-language` | **Date**: 2026-10-03 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/040-spanish-primary-language/spec.md` (GitHub issue #40)

## Summary

Move every piece of client interface text into i18next translation resources (`es` default, `en` second), add a persisted language switcher reachable from every layout and public page, make date/time/number/currency formatting follow the selected language, and make the API return its user-facing messages in the language the client states with each request (`Accept-Language`, Spanish fallback). Doctor- and patient-entered content is only ever rendered, never passed through translation. No database change.

## Technical Context

**Language/Version**: C# / .NET 8 (API); TypeScript 5 + React 18 (client)

**Primary Dependencies**: existing stack plus `i18next` and `react-i18next` (client). `date-fns` (already present) supplies the `es` and `enUS` locales; `Intl.NumberFormat` handles numbers and currency. No new server packages (message catalog is plain C#).

**Storage**: Browser `localStorage` key `medflow_lang` (value `es` | `en`). No DB / schema change.

**Testing**: xUnit + `TestApiFactory` in `MedFlow.Api.Tests` for server messages and isolation; client verification by `tsc` (typed resources enforce es/en key parity) and `npm run build`, plus a manual pass from `quickstart.md`.

**Target Platform**: Web (desktop and mobile browsers), ASP.NET Core API.

**Project Type**: Web application (React client + .NET API).

**Performance Goals**: Language switch visible in under 1 second; no extra network call for translations (bundled resources).

**Constraints**: No ESLint config or eslint package entries touched (issue #41 runs concurrently); response shape and status codes unchanged; Spec 017 mobile layouts must still fit longer Spanish strings.

**Scale/Scope**: About 3,400 lines of client pages/hooks plus layouts and shared components; about 100 user-facing server message sites in 13 controllers, the error middleware and rate limiter.

## Constitution Check

| Principle | Assessment |
|-----------|------------|
| I. Branch-per-feature | Pass: branch `040-spanish-primary-language` from `dev`, matches spec folder. |
| II. Layered architecture | Pass: message catalog lives in `MedFlow.Api` (HTTP concern, needs request language); `Core` untouched. Client keeps all HTTP in `api/client.ts` and `api/services.ts`; only a header is added in the shared axios instance. |
| III. Consistent API contracts | Pass: no endpoint added or reshaped; DTOs, status codes, field names unchanged. Only message text varies. No new `*Api` methods needed. |
| IV. Security and least privilege | Pass: language never affects auth or account-existence behavior; uniform responses stay uniform in both languages; preference holds no patient data. |
| V. Simplicity | Pass: i18next is the library the issue names; a plain dictionary catalog on the server avoids resx tooling and new packages. Tech-stack note: adding i18next is a library, not a replacement of any listed technology, so no amendment is required. |

Post-design re-check: still passes; no Complexity Tracking entries.

## Design Decisions (see research.md)

1. Client: `src/i18n/` with `index.ts` (init, persisted language, `<html lang>` sync), `resources/es.ts`, `resources/en.ts`. `en` is typed against the shape of `es`, and `t` keys are typed via i18next `CustomTypeOptions`, so a missing key or typo fails `tsc`.
2. Switcher: `LanguageSwitcher` component (ES / EN toggle) placed in `AppLayout`, `PortalLayout`, `AuthShell` and the public intake and appointment-response pages.
3. Formatting: `utils/format.ts` `fmt.*` reads the active language and uses date-fns locale and `Intl` (USD kept). Raw `toLocaleString()` calls replaced.
4. Enum labels (`displayEnum`, blood types, statuses, roles) translated through resources with the stored value untouched.
5. Server: `Localization/Messages` catalog (key, es, en) plus `HttpContext` helper `T(key, args)` resolving language from `Accept-Language` (first supported of es, en; default es). Controllers, `ErrorHandlingMiddleware`, rate limiter and Identity error descriptions use it. Unknown Identity codes map to a generic localized message.
6. Client error handling: shared `apiError` helper uses the server message when present, else a localized generic message. Axios request interceptor sends `Accept-Language` from the current language.
7. Content safety: no translation of data values; templates, notes and patient data are rendered as-is. Seed data is unchanged.

## Project Structure

### Documentation (this feature)

```text
specs/040-spanish-primary-language/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── language-negotiation.md
└── tasks.md
```

### Source Code (repository root)

```text
MedFlow.Api/
├── Localization/
│   ├── Messages.cs            # es/en catalog keyed by message id
│   └── LocalizationExtensions.cs  # language resolution + T() helpers
├── Controllers/*              # user-facing strings replaced with T(...)
├── Middleware/ErrorHandlingMiddleware.cs
└── Program.cs                 # rate-limit message

MedFlow.Api.Tests/
└── LocalizationTests.cs       # language negotiation, fallback, isolation unchanged

medflow-client/
├── package.json               # + i18next, react-i18next only
└── src/
    ├── i18n/{index.ts, resources/es.ts, resources/en.ts, i18next.d.ts}
    ├── components/ui/LanguageSwitcher.tsx
    ├── api/client.ts          # Accept-Language header, localized generic errors
    ├── utils/format.ts        # locale-aware formatting + enum labels
    ├── hooks/queries.ts       # toasts and error messages via t()
    ├── components/**, pages/**# all UI text via useTranslation
    └── main.tsx, index.html   # i18n bootstrap, lang attribute, title
```

**Structure Decision**: Existing web-application layout; additions are one `i18n` folder in the client and one `Localization` folder in the API.

## Complexity Tracking

No constitution violations.
