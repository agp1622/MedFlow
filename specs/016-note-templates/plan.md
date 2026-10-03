# Implementation Plan: Structured Note Templates

**Branch**: `claude/issue-16-note-templates` | **Date**: 2026-10-03 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/016-note-templates/spec.md` (GitHub issue #16)

## Summary

Add doctor-private note templates (CRUD) with a built-in read-only SOAP template, a template picker and copy-forward button on the existing new-note form, and a doctor-only "latest own note for patient" endpoint. Notes stay ordinary `MedicalNote` rows; templates only pre-fill text on the client.

## Technical Context

**Language/Version**: C# / .NET 8; TypeScript + React (Vite)
**Primary Dependencies**: ASP.NET Core Web API, EF Core (SQL Server), TanStack Query, react-hook-form + zod
**Storage**: new `NoteTemplates` table via EF Core migration
**Testing**: xUnit in `MedFlow.Api.Tests` with `TestApiFactory`
**Project Type**: web-service + SPA
**Constraints**: constitution principles I-V; doctor-only routes; DTOs only; `PagedResult<T>` for lists
**Scale/Scope**: tens of templates per doctor

## Constitution Check

- I Git workflow: branch off `dev`; the requester mandated the name `claude/issue-16-note-templates` (deviation from `NNN-name` is explicit, flagged in report). PASS with noted deviation.
- II Layered: entity/DTOs/interface in Core, repo in Infrastructure, thin controller. PASS.
- III Contracts: DTO records, `PagedResult`, typed `*Api` methods + TS types. PASS.
- IV Security: doctor id from token only; cross-doctor access returns 404; patient tokens forbidden by role attribute. PASS.
- V Simplicity: no variables/placeholders, no clinic-shared templates, no template link on notes. PASS.

## Project Structure

```text
specs/016-note-templates/  (spec, plan, research, data-model, contracts/api.md, quickstart, tasks)
MedFlow.Core/Entities/DomainEntities.cs      + NoteTemplate
MedFlow.Core/DTOs/Dtos.cs                    + NoteTemplateDto, Create/Update requests, CopyForwardDto
MedFlow.Core/Interfaces/IRepositories.cs     + INoteTemplateRepository, IMedicalNoteRepository.GetLatestForPatientAsync
MedFlow.Infrastructure/                      DbSet, config, repository, DI, migration
MedFlow.Api/Controllers/                     NoteTemplatesController; MedicalNotesController latest endpoint
MedFlow.Api.Tests/                           NoteTemplateTests
medflow-client/src/                          types, services.ts (noteTemplatesApi, medicalNotesApi.getLatest), template manager page/dialog, picker in note form
```

**Structure Decision**: extend existing projects; follow `MedicalNote` patterns.
