# Quickstart

1. `dotnet test MedFlow.Api.Tests --filter TwoFactor` covers enable, gating, replay, lockout, recovery, disable, Google gating, and challenge/bearer separation.
2. Manual: run API and client; sign in; open Security (`/security`); scan the QR with an authenticator app; confirm a code; save recovery codes; sign out; sign in again and enter a code; try a recovery code; disable with password and code.
3. Migration check: `export PATH=$PATH:~/.dotnet/tools; dotnet ef migrations has-pending-model-changes -p MedFlow.Infrastructure -s MedFlow.Api`.
