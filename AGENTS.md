# Repository Guidelines

## Project Structure & Module Organization
- `AI.ProfilePhotoMaker.API/`: ASP.NET Core Web API (controllers, services, EF Core, middleware). App settings in `appsettings*.json`.
- `AI.ProfilePhotoMaker.API.Tests/`: xUnit test project for API.
- `AI.ProfilePhotoMaker.UI/`: Angular app (src, assets, tests, Playwright/Karma).
- Root tooling: `docker-compose.yml`, `dev-start.sh` / `dev-stop.sh` / `dev-test.sh`, Python and TS integration tests, deployment docs in `docs/` and `infrastructure/`.

## Build, Test, and Development Commands
- Full stack (recommended): `./dev-start.sh` to run SQL Server, API (5032), and UI (4200); `./dev-stop.sh` to stop; `./dev-test.sh` for quick health/integration checks.
- API:
  - Build: `dotnet build AI.ProfilePhotoMaker.sln`
  - Run: `cd AI.ProfilePhotoMaker.API && dotnet run`
  - Test: `dotnet test AI.ProfilePhotoMaker.API.Tests`
- UI:
  - Dev: `cd AI.ProfilePhotoMaker.UI && npm run dev:local`
  - Full‑stack dev from UI: `npm run dev:fullstack:local`
  - Build: `npm run build` (or `build:dev`)
  - Unit tests: `npm test` (Karma/Jasmine)
  - E2E: `npm run playwright:install && npm run test:e2e`

## Coding Style & Naming Conventions
- C# (API): 4‑space indent; PascalCase for classes/methods; camelCase for locals/params; use async suffix for async methods; organize by feature in `Controllers/`, `Services/`, `Models/`.
- TypeScript/Angular (UI): 2‑space indent, single quotes, ESLint + Prettier enforced (`npm run lint`, `format`). File names kebab‑case (e.g., `user-profile.component.ts`); classes PascalCase.

## Testing Guidelines
- API: xUnit tests live under `AI.ProfilePhotoMaker.API.Tests/*`. Add unit tests for new logic; prefer arranging tests by feature (e.g., `Controllers/`, `Services/`). Run with `dotnet test`.
- UI: small units via Karma (`npm test`), flows via Playwright (`npm run test:e2e`). Place spec files alongside code or in `tests/` as established.
- Integration: use `./dev-test.sh` and root `test-*.py` / `*.spec.ts` for end‑to‑end validation; keep them deterministic and idempotent.

## Commit & Pull Request Guidelines
- Commits: follow Conventional Commits (e.g., `feat(ui): add cropping tool`, `fix(api): null check in upload`). Keep changes scoped and descriptive.
- PRs: include summary, linked issues, test evidence (logs/screenshots for UI), and any config notes. Ensure: `npm run lint` (UI), `dotnet build` and `dotnet test` (API), Playwright passes for affected flows.

## Security & Configuration Tips
- Never commit secrets. Use `.env.example` as reference; local overrides via environment variables or `dotnet user-secrets`. Sensitive values (e.g., `REPLICATE_API_TOKEN`, Azure Storage) must come from env or secrets.
- Do not change default ports without updating `proxy.conf.json`, `docker-compose.yml`, and scripts.
