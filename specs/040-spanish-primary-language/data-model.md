# Data Model: Spanish as the Primary Language

No database entities or migrations. Conceptual and client-side structures only.

- **Language preference**: `medflow_lang` in browser `localStorage`; allowed values `es`, `en`; anything else treated as absent (Spanish). Contains no account or patient data.
- **Translation resource set**: one TypeScript object per language (`es`, `en`) with identical nested keys; groups by area (common, auth, nav, patients, appointments, billing, prescriptions, notes, templates, intake, portal, availability, audit, enums, errors, validation).
- **Server message catalog**: map of message id to `{ es, en }` text with `{0}` style arguments, in `MedFlow.Api/Localization/Messages.cs`.
- **Requested language**: derived per request from `Accept-Language`; never stored.
