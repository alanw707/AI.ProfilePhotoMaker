# Development Guide

Deep-scan guide for local development. Reference the detailed docs for full setup and secrets handling.

## Prerequisites
- .NET SDK 8.x
- Node.js + npm
- SQL Server (local or containerized)

## Environment Setup
- Follow `docs/ENVIRONMENT_SETUP.md` and `docs/ENVIRONMENT_VARIABLES.md`.
- Use `.env.example` patterns; never commit secrets.

## Local Development
### Full stack
- `./dev-start.sh` (SQL Server + API + UI)
- `./dev-stop.sh`
- `./dev-rebuild.sh [--api-only|--ui-only]`

### API only
- `dotnet build AI.ProfilePhotoMaker.sln`
- `dotnet run --project AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj`
- Tests: `dotnet test AI.ProfilePhotoMaker.API.Tests`

### UI only
- `cd AI.ProfilePhotoMaker.UI`
- `npm install`
- `npm run dev:local` or `npm run dev:fullstack:local`
- Tests: `npm test`, `npm run test:integration`, `npm run test:e2e`

## Tooling
- API Playwright checks: `AI.ProfilePhotoMaker.API/tests/playwright`
- Repo E2E tests: `tests/e2e`
- Lint/format: `npm run lint`, `npm run format` (UI)

## Notes
- See `docs/LOCAL-BUILD-WORKFLOW.md` for build automation details.
- Use `dotnet user-secrets` for local API secrets if required.
