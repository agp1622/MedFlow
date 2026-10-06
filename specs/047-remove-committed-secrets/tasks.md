# Tasks: Remove Committed Secrets and Load Them From Configuration

## Phase 1: Setup

- [x] T001 Add `UserSecretsId` to MedFlow.Api/MedFlow.Api.csproj for local secrets

## Phase 2: Foundational

- [x] T002 Create MedFlow.Api/Configuration/JwtKeyValidator.cs with `Validate(string? key, IHostEnvironment env)` (skips Development/Testing; throws without echoing the key)

## Phase 3: User Story 1 - No real credentials committed (P1)

- [x] T003 [US1] Blank secret values (Email:AppPassword, SeedUser passwords, Jwt:Key, DB password) in MedFlow.Api/appsettings.json and MedFlow.Api/appsettings.development.json; remove personal emails
- [x] T004 [US1] Remove hardcoded fallbacks in MedFlow.Infrastructure/Data/DbSeeder.cs; skip with warning when email/password missing
- [x] T005 [P] [US1] Update docker-compose.yml to use `${VAR}` from .env; add .env.example with placeholders only; ensure root .env is git-ignored

## Phase 4: User Story 2 - Production fail-fast (P1)

- [x] T006 [US2] Call JwtKeyValidator in MedFlow.Api/Program.cs before reading the key
- [x] T007 [US2] Add MedFlow.Api.Tests/SecretsConfigurationTests.cs: Production with missing/placeholder/short(31)/valid(32) key; Testing environment unaffected

## Phase 5: User Story 3 - Seed only in Development (P2)

- [x] T008 [US3] Confirm seeding stays inside the Development-only block in MedFlow.Api/Program.cs; test seeder skips without configured password (MedFlow.Api.Tests/SecretsConfigurationTests.cs)

## Phase 6: User Story 4 - Docs and infra (P2)

- [x] T009 [P] [US4] Add `emailAppPassword` secure param and `Email__AppPassword` app setting in infra/main.bicep
- [x] T010 [P] [US4] Update README.md: secrets per environment, user-secrets setup, rotation and git history scan notice

## Phase 7: Polish

- [x] T011 Run dotnet build, dotnet test, npm run build in medflow-client; grep tracked files for leaked values
