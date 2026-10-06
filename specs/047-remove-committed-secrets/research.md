# Research

- **Decision**: Validate `Jwt:Key` in Program.cs via a static `JwtKeyValidator.Validate(key, environment)` called before the key is converted to bytes. Throws `InvalidOperationException` without echoing the key. Rationale: fail fast at build time of the host; simple to unit-test. Alternative: IValidateOptions on startup (heavier, key is read directly from configuration today).
- **Decision**: Enforce unless environment is Development or Testing. Rationale: tests use the Testing environment with a 51-char key; Production-like names must be strict.
- **Decision**: Placeholder = empty/whitespace or contains (case-insensitive) change, your, placeholder, replace, example, secret, or is under 32 chars.
- **Decision**: Seeder reads SeedUser:Email/Password/PatientEmail/PatientPassword with no fallbacks; skip + log warning if missing. Display names (first/last/specialty) may keep neutral defaults ("Demo", "Doctor", "General Medicine").
- **Decision**: Local dev values via `dotnet user-secrets` or env vars (`SeedUser__Password`, `Jwt__Key`, `Email__AppPassword`); docker-compose reads `${VAR}` from a git-ignored `.env`, with a committed `.env.example` holding placeholders only. Add UserSecretsId to the Api csproj.
- **Decision**: Bicep gets `@secure()` `emailAppPassword` param (default empty) mapped to `Email__AppPassword`; README recommends Key Vault references for production.
- **Decision**: appsettings connection string password for local SQL container is blanked too; supplied via env var / user-secrets.
