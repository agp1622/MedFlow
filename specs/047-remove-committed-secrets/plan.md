# Implementation Plan: Remove Committed Secrets and Load Them From Configuration

**Branch**: `047-remove-committed-secrets` | **Date**: 2026-10-06 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/047-remove-committed-secrets/spec.md` (GitHub #56)

## Summary

Blank all secret values in committed appsettings files, add a startup validator that rejects weak or placeholder
`Jwt:Key` outside Development/Testing, remove hardcoded seeder fallbacks (skip with warning when unset), keep the
seed call inside the Development-only block, and update compose, Bicep, and README.

## Technical Context

**Language/Version**: C# / .NET 8, TypeScript (client untouched)
**Primary Dependencies**: ASP.NET Core configuration, xUnit + WebApplicationFactory
**Storage**: N/A (no schema change, no migration)
**Testing**: MedFlow.Api.Tests with TestApiFactory plus a dedicated factory for Production
**Target Platform**: Azure App Service, Docker
**Project Type**: web-service + SPA
**Constraints**: no secret values in repo; Development must keep working via user-secrets or env vars

## Constitution Check

- I Git workflow: feature branch from dev. Pass.
- II Layering: validator lives in MedFlow.Api (composition root); seeder change in Infrastructure; Core untouched. Pass.
- III API contracts: no endpoint changes. Pass.
- IV Security: directly implements "secrets via configuration". Pass.
- V Simplicity: one small static validator class, no flags. Pass.

## Project Structure

```text
specs/047-remove-committed-secrets/  (spec, plan, research, data-model, quickstart, tasks)
MedFlow.Api/Configuration/JwtKeyValidator.cs   (new)
MedFlow.Api/Program.cs                          (call validator before key use)
MedFlow.Api/appsettings.json, appsettings.development.json  (blank secrets)
MedFlow.Infrastructure/Data/DbSeeder.cs         (no fallbacks)
docker-compose.yml, infra/main.bicep, README.md
MedFlow.Api.Tests/SecretsConfigurationTests.cs  (new)
```

**Structure Decision**: existing three-project layout; validator in Api because it depends on IHostEnvironment.
