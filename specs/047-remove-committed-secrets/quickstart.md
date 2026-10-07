# Quickstart

1. Local: `dotnet user-secrets set "Jwt:Key" "<32+ chars>" --project MedFlow.Api` (same for SeedUser and Email keys), or copy `.env.example` to `.env` for docker-compose.
2. `dotnet test MedFlow.Api.Tests` covers: Production start fails for missing/placeholder/short key, succeeds with a strong key; seeding skipped without configured password.
3. Manual: run in Development with secrets and sign in as the seeded doctor.
