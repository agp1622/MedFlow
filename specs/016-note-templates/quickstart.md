# Quickstart / Validation

1. `dotnet test MedFlow.Api.Tests --filter NoteTemplate` (CRUD, validation limits, duplicates, cross-doctor 404, patient 403, copy-forward isolation).
2. `cd medflow-client && npm run build && npm run lint`.
3. Manual: run API + client, log in as doctor; open a patient, new note: pick SOAP -> headings appear; with text present picker asks to confirm; create template "Cardiology f/u" in Templates manager, edit, delete; save a note, reopen new note, click "Copy from last visit" -> prior text appears; as a second doctor confirm the first doctor's templates/notes are not visible.
