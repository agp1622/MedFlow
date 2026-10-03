# Tasks: Spanish as the Primary Language

**Input**: Design documents in `/specs/040-spanish-primary-language/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/language-negotiation.md
**Tests**: Server tests are required by the project rules (xUnit with `TestApiFactory`). The client has no test runner; parity and key typos are enforced by `tsc` (via `npm run build`), the rest by quickstart.md.
**Constraint**: Do not touch ESLint config or eslint entries in `medflow-client/package.json` (issue #41 is concurrent).

Format: `- [ ] ID [P?] [Story] Description with path`

## Phase 1: Setup

- [ ] T001 Add `i18next` and `react-i18next` to dependencies in `medflow-client/package.json` and update `medflow-client/package-lock.json` (no eslint-related changes)
- [ ] T002 Create i18n bootstrap in `medflow-client/src/i18n/index.ts`: init i18next with `es` default and fallback, `en` second, language read from validated `localStorage` key `medflow_lang` inside try/catch, `changeLanguage` wrapper that persists and sets `document.documentElement.lang`, exported `SUPPORTED_LANGUAGES`
- [ ] T003 [P] Create empty-to-start typed resource modules `medflow-client/src/i18n/resources/es.ts` and `medflow-client/src/i18n/resources/en.ts` (en typed as the shape of es) and `medflow-client/src/i18n/i18next.d.ts` typing `t` keys from `es`
- [ ] T004 Import `./i18n` in `medflow-client/src/main.tsx` before rendering; set `<html lang="es">` and a Spanish `<title>` in `medflow-client/index.html`

## Phase 2: Foundational (blocks all stories)

- [ ] T005 Create `LanguageSwitcher` component (ES / EN toggle with accessible labels, shows current language) in `medflow-client/src/components/ui/LanguageSwitcher.tsx` and export from `medflow-client/src/components/ui/index.tsx` if that file re-exports components
- [ ] T006 Add `Accept-Language` header from the current language in the request interceptor of `medflow-client/src/api/client.ts` and localize its 5xx toast
- [ ] T007 [P] Server catalog: create `MedFlow.Api/Localization/Messages.cs` (id to es/en text with `{0}` args) and `MedFlow.Api/Localization/LocalizationExtensions.cs` (resolve language from `Accept-Language`, first supported by quality order, default `es`; `T(this ControllerBase/HttpContext, id, args)`)
- [ ] T008 Server tests first: create `MedFlow.Api.Tests/LocalizationTests.cs` covering es, en, missing, unsupported (`fr`) and quality-ordered headers on a public endpoint, identical status codes across languages, uniform forgot-password response in both languages, patient-token isolation unchanged (fails until T009+)

**Checkpoint**: infrastructure exists; story work can proceed.

## Phase 3: User Story 1 - Whole app in Spanish by default (P1)

**Goal**: No English interface text with no saved preference.
**Independent Test**: Clear storage, browser in English, walk every route as doctor and patient.

- [ ] T009 [P] [US1] Translate `medflow-client/src/components/layout/AppLayout.tsx` and `PortalLayout.tsx` (nav, headers, sign out, menu labels) into `es`/`en` resources
- [ ] T010 [P] [US1] Translate `medflow-client/src/pages/AuthPages.tsx` (login, register, forgot/reset password, accept invite) including zod validation messages and toasts
- [ ] T011 [P] [US1] Translate `medflow-client/src/pages/DashboardPage.tsx` and `medflow-client/src/pages/AvailabilityPage.tsx`
- [ ] T012 [P] [US1] Translate `medflow-client/src/pages/PatientsPage.tsx` (list, detail tabs, forms, dialogs, empty states)
- [ ] T013 [P] [US1] Translate `medflow-client/src/pages/AppointmentsPage.tsx` and `medflow-client/src/pages/NoteTemplatesPage.tsx` (UI text only, never template content)
- [ ] T014 [P] [US1] Translate `medflow-client/src/pages/BillingPrescriptionsPages.tsx`
- [ ] T015 [P] [US1] Translate `medflow-client/src/pages/IntakePages.tsx`, `medflow-client/src/pages/AppointmentResponsePage.tsx` and `medflow-client/src/pages/PortalPage.tsx`
- [ ] T016 [P] [US1] Translate `medflow-client/src/components/attachments/AttachmentsTab.tsx`, `components/audit/AuditLogTab.tsx`, `components/sharing/ShareToggle.tsx`, `components/ui/PasswordInput.tsx`, `components/ui/ThemeToggle.tsx` and shared UI in `components/ui/index.tsx`
- [ ] T017 [US1] Translate toasts, fallback messages and `apiError`/`errMsg` helpers in `medflow-client/src/hooks/queries.ts` and enum/status labels, blood types and `displayEnum` in `medflow-client/src/utils/format.ts` (stored values unchanged)

## Phase 4: User Story 2 - Switch to English, remembered (P1)

**Goal**: Visible switcher everywhere, instant change, persisted.
**Independent Test**: Switch, reload, re-open; switch with a half-filled form.

- [ ] T018 [US2] Place `LanguageSwitcher` in `AppLayout`, `PortalLayout`, the auth shell in `AuthPages.tsx`, `IntakePages.tsx` public form and `AppointmentResponsePage.tsx`
- [ ] T019 [US2] Ensure language change re-renders without remounting routes (no `key` on routes) and that invalid or blocked storage falls back to Spanish and still switches in-session, in `medflow-client/src/i18n/index.ts`

## Phase 5: User Story 3 - Locale-aware formatting (P2)

**Goal**: Dates, times, numbers, currency follow the language.

- [ ] T020 [US3] Rework `fmt.*` in `medflow-client/src/utils/format.ts` to use date-fns `es`/`enUS` locales and `Intl.NumberFormat` (USD kept) based on the active language, including "Today/Tomorrow" labels via resources
- [ ] T021 [US3] Replace raw `toLocaleString()` in `components/audit/AuditLogTab.tsx` and `pages/IntakePages.tsx`, and any chart tick or tooltip formats in `pages/DashboardPage.tsx`, with `fmt` helpers

## Phase 6: User Story 4 - Server messages, portal, public pages (P1)

**Goal**: API messages follow the requested language; public pages and portal fully translated.

- [ ] T022 [P] [US4] Localize `MedFlow.Api/Controllers/AuthController.cs` messages including Identity error mapping by code with generic localized fallback
- [ ] T023 [P] [US4] Localize `PortalController.cs`, `PortalInvitationsController.cs`, `IntakeController.cs`, `IntakeReviewController.cs`, `AppointmentResponseController.cs`
- [ ] T024 [P] [US4] Localize `AvailabilityController.cs`, `AuditLogController.cs`, `NoteTemplatesController.cs`, `AttachmentsController.cs`, `AppointmentsController.cs`, `PatientsController.cs`, `DomainControllers.cs`
- [ ] T025 [US4] Localize `MedFlow.Api/Middleware/ErrorHandlingMiddleware.cs` (generic localized 400/403/404/500 text, no raw English exception text) and the rate-limit rejection message in `MedFlow.Api/Program.cs`
- [ ] T026 [US4] Update existing tests in `MedFlow.Api.Tests/*.cs` that assert English message text to send `Accept-Language: en` or assert status only; confirm T008 tests pass
- [ ] T027 [US4] Verify the client shows server `error`/`errors`/`message` text as-is and falls back to localized generic text in `hooks/queries.ts`, `pages/AuthPages.tsx` and public pages

## Phase 7: User Story 5 - Doctor-entered content untouched (P1)

- [ ] T028 [US5] Audit all rendering of notes, templates, patient fields, intake answers, prescription text and attachment names to confirm they bypass `t()` and `fmt` text transforms; confirm no save path substitutes interface text; add a server test in `MedFlow.Api.Tests/LocalizationTests.cs` that a template and note created under `es` and `en` round-trip byte-identical

## Phase 8: User Story 6 - Add a language without touching screens (P3)

- [ ] T029 [US6] Make the switcher and `SUPPORTED_LANGUAGES` data-driven from the registered resources (labels from resource key `language.name`), and document the "add a language" steps in `specs/040-spanish-primary-language/quickstart.md`

## Phase 9: Polish

- [ ] T030 Sweep `medflow-client/src` for remaining hard-coded English (`grep` for JSX text, `toast.`, `placeholder=`, `aria-label=`, `title=`, `alert(`/`confirm(`) and fix; confirm plural forms use i18next `count`
- [ ] T031 Check longest Spanish strings against spec 017 layouts at 360px (truncate or wrap rules only, no redesign)
- [ ] T032 Run `dotnet build MedFlow.sln`, `dotnet test MedFlow.Api.Tests`, `npm run build` in `medflow-client`; fix failures; walk quickstart.md and record which scenarios need manual verification

## Dependencies

Setup (T001-T004) -> Foundational (T005-T008) -> US1, US2, US3, US4 (US1 translations and US4 server work are independent of each other) -> US5 audit -> US6 -> Polish. T017 and T020 share `format.ts`; do sequentially. T026 follows T022-T025.

## Parallel Opportunities

T009-T016 (different files); T022-T024 (different controllers); T003 and T007 anytime after Setup.

## Implementation Strategy

MVP is US1 + US2 (default Spanish with persisted switcher). Then US4 (server) and US3 (formats), then audit and polish. Commit in groups: i18n core, client translations, formatting, server localization with tests, spec artifacts.
