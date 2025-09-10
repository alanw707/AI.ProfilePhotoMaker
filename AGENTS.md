# Repository Guidelines

## Project Structure & Module Organization
- `AI.ProfilePhotoMaker.API/`: ASP.NET Core Web API (controllers, services, EF Core, middleware). Configuration in `appsettings*.json`.
- `AI.ProfilePhotoMaker.API.Tests/`: xUnit tests for API (arranged by feature: `Controllers/`, `Services/`).
- `AI.ProfilePhotoMaker.UI/`: Angular app (`src/`, `assets/`, unit tests, Playwright/Karma).
- Root tooling: `docker-compose.yml`, `dev-start.sh` / `dev-stop.sh` / `dev-test.sh`, integration tests (`test-*.py`, `*.spec.ts`), deployment docs in `docs/` and `infrastructure/`.

## Build, Test, and Development Commands
- Full stack: `./dev-start.sh` (runs SQL Server, API on 5032, UI on 4200), `./dev-stop.sh`, quick checks via `./dev-test.sh`.
- API: `dotnet build AI.ProfilePhotoMaker.sln`, `cd AI.ProfilePhotoMaker.API && dotnet run`, tests with `dotnet test AI.ProfilePhotoMaker.API.Tests`.
- UI: `cd AI.ProfilePhotoMaker.UI && npm run dev:local` (UI only) or `npm run dev:fullstack:local` (proxy to API); build with `npm run build`; unit tests `npm test`; e2e `npm run playwright:install && npm run test:e2e`.

## Coding Style & Naming Conventions
- C# (API): 4‑space indent; classes/methods PascalCase; locals/params camelCase; async methods end with `Async`. Organize code by feature (folders: `Controllers/`, `Services/`, `Models/`).
- TypeScript/Angular (UI): 2‑space indent, single quotes, ESLint + Prettier. File names kebab‑case (e.g., `user-profile.component.ts`). Run `npm run lint` and `npm run format`.

## Testing Guidelines
- API: xUnit in `AI.ProfilePhotoMaker.API.Tests/*`; arrange by feature; run with `dotnet test`.
- UI: small units with Karma/Jasmine (`npm test`); flows with Playwright (`npm run test:e2e`). Place spec files alongside code or in established `tests/` folders.
- Integration: use `./dev-test.sh` and root tests. Keep tests deterministic and idempotent.

## Commit & Pull Request Guidelines
- Never create Pull Request without confirming
- Conventional Commits (examples): `feat(ui): add cropping tool`, `fix(api): null check in upload`.
- PRs: include summary, linked issues, and test evidence (logs/screenshots for UI). Ensure `npm run lint` (UI), `dotnet build` and `dotnet test` (API), and relevant Playwright tests pass.

## Security & Configuration Tips
- Never commit secrets. Use `.env.example` as reference; prefer environment variables or `dotnet user-secrets` for local secrets.
- Sensitive values (e.g., `REPLICATE_API_TOKEN`, Azure Storage) must come from env/secrets. If changing ports, update `proxy.conf.json`, `docker-compose.yml`, and scripts.

