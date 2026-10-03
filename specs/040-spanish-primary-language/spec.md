# Feature Specification: Spanish as the Primary Language

**Feature Branch**: `040-spanish-primary-language`

**Created**: 2026-10-03

**Status**: Draft

**Input**: GitHub issue #40 - "Make Spanish the primary language of the app. As a user, I want the app to be in Spanish by default, with English still available." (six acceptance points: Spanish default UI; English via a remembered switcher; locale-aware dates/times/numbers/currency; text in translation resources so languages can be added later; API error messages, patient portal and public pages follow the selected language; doctor-entered text is never translated or overwritten.)

## Clarifications

### Session 2026-10-03

Answered unattended with the recommended or most conservative option.

- Q: How should user-facing server messages reach the user in the selected language: server returns text in the requested language, or server returns stable codes that the client translates? → A: The client states the selected language on every request and the server returns the message text in that language; response shape, status codes and field names are unchanged (FR-011). Unsupported or missing language falls back to Spanish.
- Q: Are outgoing emails (portal invitation, intake link, reminders, password reset) and audit log entries translated by this feature? → A: No. They stay as they are today and are tracked as a follow-up; the web client and API response messages are in scope.
- Q: Where is the language preference stored? → A: On the device only (browser local storage); never on the account and never containing patient data (FR-014). Works identically for unauthenticated visitors.
- Q: Which default applies when the browser reports English and nothing is saved? → A: Spanish, always (issue point 1).
- Q: How are system-defined value names (appointment statuses, blood types, roles, enum values shown as labels) handled? → A: They are interface text and are translated through the same resources; the stored values are unchanged.
- Q: How are Identity (password rule) errors and other framework-generated messages handled? → A: They are mapped to localized equivalents server-side where the message is user-facing; any message without a translation is replaced by a generic localized message (FR-008).
- Q: Which currency is shown? → A: The current currency (US dollar) is kept; only formatting follows the language (assumption confirmed).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Use the whole app in Spanish by default (Priority: P1)

A doctor, staff member or patient opens MedFlow for the first time, with no saved preference, and every piece of interface text (labels, buttons, menus, messages, validation errors, empty states, dialogs, toasts, page titles) is in Spanish.

**Why this priority**: This is the core ask. Without it nothing else in the issue has value.

**Independent Test**: Clear any saved preference, open the app, and walk through each section while signed in as a doctor and as a patient. Confirm no English interface text remains.

**Acceptance Scenarios**:

1. **Given** a visitor with no saved language preference, **When** they open any page, **Then** all interface text is shown in Spanish.
2. **Given** a signed-in user in any section (dashboard, patients, appointments, billing, prescriptions, records, notes, templates, settings, reports, audit log, portal), **When** the page renders, **Then** labels, buttons, table headers, empty states, dialogs, confirmations and toasts are in Spanish.
3. **Given** a form with validation rules, **When** the user submits invalid data, **Then** each validation message is shown in Spanish.
4. **Given** the user's browser reports English as its language and no preference is saved, **When** they open the app, **Then** the app is still in Spanish.

---

### User Story 2 - Switch to English and have the choice remembered (Priority: P1)

A user who prefers English changes the language with a visible switcher. All interface text changes immediately without losing their place or unsaved work, and the choice persists across reloads and later visits on the same device.

**Why this priority**: English must remain a first-class option for existing English-speaking users.

**Independent Test**: Switch to English, reload and revisit the app, and confirm English persists; switch back to Spanish and confirm that persists too.

**Acceptance Scenarios**:

1. **Given** the app is in Spanish, **When** the user picks English in the switcher, **Then** all interface text on the current page changes to English immediately.
2. **Given** the user chose English, **When** they reload, close and reopen the app, or sign out and in on the same device, **Then** the app is still in English.
3. **Given** the switcher is placed in the signed-in layout, the sign-in/registration pages, the patient portal and every public page, **When** a user opens any of them, **Then** the switcher is reachable and shows the current language.
4. **Given** a half-filled form, **When** the language is switched, **Then** the entered values are preserved.
5. **Given** a saved preference that is not a supported language (corrupted or removed), **When** the app opens, **Then** it falls back to Spanish.

---

### User Story 3 - Dates, times, numbers and currency match the language (Priority: P2)

Dates, times, numbers and currency amounts appear in the conventions of the selected language, for example day-before-month ordering and Spanish month and weekday names in Spanish, versus the English equivalents in English.

**Why this priority**: Correct text with wrong date or money formats still reads as unfinished and can cause misreading of appointments and charges.

**Independent Test**: View appointments, billing, reports and patient records in each language and compare against the expected conventions for that language.

**Acceptance Scenarios**:

1. **Given** Spanish is selected, **When** a date, time, relative label (for example month and weekday names) or calendar appears, **Then** it uses Spanish names and ordering.
2. **Given** Spanish is selected, **When** a number or currency amount appears (billing, reports, charts), **Then** it uses Spanish separators and currency placement.
3. **Given** the user switches language, **When** the page refreshes its display, **Then** every date, time, number and amount switches conventions without changing the underlying value or the currency being charged.

---

### User Story 4 - Server messages, patient portal and public pages follow the language (Priority: P1)

Messages that originate from the server and are shown to users (for example "invalid credentials", validation failures, "slot unavailable") appear in the selected language. The patient portal and the public pages that people reach without an account (online booking if present, intake form, appointment response, invitation/registration link) are fully translated and honour the selected language, including for a first-time visitor arriving from an emailed link.

**Why this priority**: Patients are the least technical users and reach these pages directly; an English-only error or page undermines the feature.

**Independent Test**: In each language, trigger a server-side error (wrong password, invalid form, duplicate) and open each public page and the portal; confirm text is in the selected language.

**Acceptance Scenarios**:

1. **Given** a language is selected, **When** an action fails on the server with a user-facing message, **Then** the user sees that message in the selected language.
2. **Given** the server returns a message for which no translation exists, **When** it is displayed, **Then** a clear generic message in the selected language is shown rather than raw English or technical text.
3. **Given** a visitor opens the intake form, appointment response page or invitation page from a link, **When** the page loads, **Then** it appears in their saved language, or in Spanish if none is saved, and the switcher is available.
4. **Given** the patient portal is open, **When** the user switches language, **Then** all portal pages and messages follow it.
5. **Given** a server message is localized, **When** compared to the non-localized behavior, **Then** status codes, response shape and account-existence behaviour (for example uniform password-reset responses) are unchanged.

---

### User Story 5 - Doctor-entered content is never translated or altered (Priority: P1)

Free text and data entered by doctors, staff or patients (clinical notes, note templates, patient names, addresses, diagnoses, prescription text, intake answers, attachment names, custom labels) is displayed exactly as stored, in every interface language. Switching language never rewrites or machine-translates stored content, and saving a record never replaces entered text with interface text.

**Why this priority**: Clinical accuracy and data integrity are non-negotiable in a medical product.

**Independent Test**: Create a note, template and patient with Spanish and English text, switch language back and forth, and confirm the stored and displayed content is byte-for-byte unchanged.

**Acceptance Scenarios**:

1. **Given** stored notes, templates and patient data, **When** the language is switched, **Then** that content is displayed unchanged.
2. **Given** a form pre-filled from stored data, **When** it is saved after a language switch, **Then** the stored values are unchanged unless the user edited them.
3. **Given** built-in default values the app itself supplies (for example default template placeholders created by the system), **When** a user has already saved their own version, **Then** the user's version is never replaced by a translated default.

---

### User Story 6 - Add another language later without touching screens (Priority: P3)

A maintainer can add a third language by adding a new set of translation text and registering it, without editing any screen or component.

**Why this priority**: Explicitly requested in the issue but delivers no end-user value until a third language is needed.

**Independent Test**: Add a trial language resource file, register it, and confirm it appears in the switcher and renders; remove it afterwards.

**Acceptance Scenarios**:

1. **Given** all interface text lives in translation resources, **When** a new language resource set is registered, **Then** it is selectable and applied across the app with no screen changes.
2. **Given** a key is missing from a non-default language, **When** a screen renders, **Then** Spanish text is shown instead of a blank or a raw key.
3. **Given** the Spanish and English resources, **When** they are compared, **Then** they define the same set of keys.

---

### Edge Cases

- Saved preference is unavailable (private browsing, blocked storage): the app still works in Spanish for the session and the switcher still works for that session.
- A user opens an emailed link while a different language is saved: the saved language wins; with none saved, Spanish.
- Server messages that contain dynamic values (limits, names, dates): the values are inserted in the correct place for the language and formatted for it.
- Text stored by the system at the time of an action (for example audit entries, outgoing email bodies, reminders) is out of scope to translate retroactively; stored records keep the language they were written in.
- Very long Spanish strings: labels, buttons and table headers must not break layouts at phone widths (spec 017).
- Pluralisation (for example "1 paciente" vs "2 pacientes") is correct in both languages.
- Browser tab titles and accessible names (for screen readers) are also in the selected language.
- Language choice changes while a request is in flight: the response message is shown in the language selected at display time, or falls back to a generic message.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST present all client interface text (labels, buttons, menus, headings, table headers, placeholders, empty states, dialogs, confirmations, toasts, validation messages, page titles, accessible names) in Spanish by default, regardless of the browser's language, when no preference is saved.
- **FR-002**: The system MUST offer English as a second language through a language switcher reachable from the signed-in layout, the authentication pages, the patient portal and all public pages.
- **FR-003**: The system MUST apply a language change immediately, without a reload and without losing unsaved form input.
- **FR-004**: The system MUST remember the user's language choice on the device across reloads, sign-out/sign-in and later visits, and MUST fall back to Spanish when the saved value is missing, invalid or unreadable.
- **FR-005**: The system MUST format dates, times, month and weekday names, numbers and currency amounts according to the selected language, without altering the underlying values or the currency code of amounts.
- **FR-006**: The system MUST keep all interface text in translation resources separate from screens, with one resource set per language, so adding a language requires only a new resource set and its registration.
- **FR-007**: The system MUST fall back to Spanish text when a translation is missing in another language, and MUST never display raw translation keys to users.
- **FR-008**: The system MUST show user-facing server error messages in the selected language, and MUST show a generic localized message when a server message has no translation.
- **FR-009**: The system MUST make the patient portal and the public pages (intake form, appointment response, invitation, plus any other unauthenticated page) fully translated and responsive to the selected language, including for visitors with no account.
- **FR-010**: The system MUST NOT translate, rewrite or overwrite any text entered by doctors, staff or patients (notes, templates, patient data, intake answers, prescription text, free-text fields) on display, on save, or on language switch.
- **FR-011**: The system MUST NOT change response status codes, response structure, authorization behaviour or account-existence behaviour of any server endpoint as a result of language selection; language only affects the text of user-facing messages.
- **FR-012**: The system MUST handle plurals and inserted values correctly in both languages.
- **FR-013**: The system MUST keep Spanish and English resource sets complete and equivalent, verifiable by an automated check.
- **FR-014**: The system MUST treat the language preference as non-sensitive and MUST NOT include patient data in anything stored to remember it.

### Key Entities

- **Language preference**: The user's chosen interface language (Spanish or English), stored on the device; no account data involved.
- **Translation resource set**: The collection of all interface text for one language, keyed identically across languages.
- **User-facing server message**: An error or status message produced by the server that the interface shows to a user; identified by a stable meaning so it can be shown in any language.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With no saved preference, 100% of reviewed screens (every route for doctor, staff and patient, and every public page) show no English interface text.
- **SC-002**: A user can switch language in a single action from any page, and the change is visible within 1 second without losing form input.
- **SC-003**: The chosen language persists across reload and re-visit in 100% of tested cases, including when stored preference is invalid.
- **SC-004**: 100% of reviewed dates, times, numbers and currency amounts match the conventions of the selected language.
- **SC-005**: For every user-facing server error covered by tests, the message is shown in the selected language in 100% of cases.
- **SC-006**: In an automated before/after check across a language switch, 0 characters of doctor- or patient-entered content differ.
- **SC-007**: Spanish and English resource sets have identical key sets (0 missing keys either way), enforced by an automated check.
- **SC-008**: A third language can be added by supplying one resource set and one registration, with 0 screen or component edits.

## Assumptions

- Language is a per-device, per-browser preference; it is not stored on the user account or sent as profile data, so unauthenticated visitors get the same behaviour as signed-in users.
- Spanish means neutral/Latin-American-friendly Spanish (locale `es`), not a region-specific variant; English means `en`.
- Currency amounts keep their existing currency; only presentation changes with language. Currency is not converted.
- Outgoing emails, SMS or reminders and audit log entries are out of scope for translation; this feature covers the web client and the messages the API returns for display (follow-up issue if needed).
- The API selects message language from the language the client states with each call; a missing or unsupported value falls back to Spanish. System-defined labels (statuses, blood types, roles) are interface text and are translated; stored values never change.
- Spec 017 (mobile-friendly UI) layouts remain intact; longer Spanish text must fit them.
- Existing auth, authorization and privacy behaviour is unchanged, and no database schema change is needed.
- Seed or sample data supplied by the app (for example default note templates already stored) is data, not interface text, and is left as stored.
