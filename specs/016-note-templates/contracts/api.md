# API Contracts (all require Doctor role; patient tokens get 403)

DTOs: `NoteTemplateDto(int Id, string Name, string Body, bool IsBuiltIn, DateTime? UpdatedAt)`; `CreateNoteTemplateRequest(string Name, string Body)`; `UpdateNoteTemplateRequest(string Name, string Body)`; `CopyForwardDto(int NoteId, string Content, string? VisitType, DateTime NoteDate)`.

| Method | Route | Result |
|---|---|---|
| GET | /api/notetemplates?page&pageSize | `PagedResult<NoteTemplateDto>` of caller's own templates |
| GET | /api/notetemplates/builtin | `NoteTemplateDto[]` (SOAP) |
| POST | /api/notetemplates | 200 `NoteTemplateDto`; 400 validation; 409 duplicate name |
| PUT | /api/notetemplates/{id} | 200 `NoteTemplateDto`; 404 not owner/missing; 400; 409 |
| DELETE | /api/notetemplates/{id} | 204; 404 not owner/missing |
| GET | /api/medicalnotes/patient/{patientId}/latest | `CopyForwardDto` of caller's own latest note; 404 if none |
