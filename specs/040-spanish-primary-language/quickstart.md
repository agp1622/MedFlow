# Quickstart: Validate Spanish as the Primary Language

## Prerequisites
`dotnet build MedFlow.sln`, `dotnet test MedFlow.Api.Tests`, `cd medflow-client && npm ci && npm run build`. For the manual pass run the API and `npm run dev`.

## Automated
- `dotnet test MedFlow.Api.Tests --filter Localization`: language negotiation (es, en, missing, unsupported, quality order), identical status codes across languages, portal/public messages localized, uniform password-reset response unchanged.
- `npm run build` fails if `en` and `es` resources differ or a translation key is wrong.

## Manual (browser)
1. Clear site data, open the app with the browser set to English: UI is Spanish (US1).
2. Use the switcher on login, doctor layout, portal, intake form, appointment-response and invitation pages: text flips to English, reload keeps it (US2); type in a form first and confirm input survives the switch.
3. Compare dates, times, amounts in billing, appointments, dashboard in both languages (US3).
4. Trigger errors (wrong password, duplicate template name, invalid intake form, expired link) in both languages (US4).
5. Create a note, a template and a patient with mixed Spanish and English text, switch language, confirm they display unchanged and re-saving does not alter them (US5).
6. Block local storage (private window): app is Spanish and the switcher still works for the session (edge case).
7. Check phone width (360px) for text overflow in the longest Spanish labels (spec 017).

## Adding a language later (no screen or component edits)

1. Client: create `medflow-client/src/i18n/resources/<code>.ts` exporting the same keys as `es.ts` (plural suffixes follow that language's rules), then register it in `resources` in `medflow-client/src/i18n/index.ts` and add its date-fns locale and Intl tag to `LOCALES` in `medflow-client/src/utils/format.ts`. The switcher lists registered languages automatically and shows each one's `language.short` and `language.name`.
2. API: add the language code to `Messages.SupportedLanguages` and a third text to each catalog entry in `MedFlow.Api/Localization/Messages.cs` (the entry tuple and `Localizer.Get` gain one case). Unsupported codes fall back to Spanish until then.
