# Repository Guidelines

## Project Structure & Module Organization
- `AI.ProfilePhotoMaker.API/`: ASP.NET Core Web API. Feature folders hold controllers, services, and EF Core models; shared cross-cutting code lives in `Configuration/`, `Constants/`, `Extensions/`, `Filters/`, and `Middleware/`. Data access is managed through `Data/` and `Migrations/`, SignalR hubs reside in `Hubs/`, and operational helpers/scripts are under `Scripts/`. API-specific Playwright checks sit in `tests/playwright/`.
- `AI.ProfilePhotoMaker.API.Tests/`: xUnit suite organized by concern—`Unit/`, `Integration/`, `Controllers/`, `Services/`, `Infrastructure/`, and `Performance/`—with shared fixtures and builders scoped per folder.
- `AI.ProfilePhotoMaker.UI/`: Angular front-end (`src/` feature modules, shared UI libraries, and services). Static assets live in `public/` and `.well-known/`; end-to-end setups live in `e2e/`, cross-browser utilities in `cypress/`, and supplementary docs/guides in `docs/`.
- `tests/`: Repository-level Playwright flows (`tests/e2e`) that validate the combined API + UI experience.
- `scripts/`: Automation for starting/stopping services, deployment validation, environment setup, and CI tooling.
- `docs/` & `infrastructure/`: Operational runbooks, deployment guidance, and infrastructure-as-code assets.

## Build, Test, and Development Commands
- Full stack: `./dev-start.sh` (SQL Server, API on 5032, UI on 4200), `./dev-stop.sh`, and `./dev-test.sh` for smoke checks. Use `./dev-rebuild.sh [--api-only|--ui-only]` to rebuild and restart dev services without touching containers.
- API: `dotnet build AI.ProfilePhotoMaker.sln`, then `dotnet run --project AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj` (or `cd AI.ProfilePhotoMaker.API && dotnet run`). Execute `dotnet test AI.ProfilePhotoMaker.API.Tests` for automated coverage. API release validation lives in `AI.ProfilePhotoMaker.API/tests/playwright` (`npm install` then `npx playwright test`).
- UI: From `AI.ProfilePhotoMaker.UI/`, run `npm run dev:local` for UI-only dev, or `npm run dev:fullstack:local` to proxy to the API. Build with `npm run build` or `npm run build:dev`. Karma/Jasmine unit tests via `npm test`, broader integration suites with `npm run test:integration`, and Playwright coverage through `npm run playwright:install && npm run test:e2e` (variants exist for chrome/mobile/debug).
- Ops scripts: `scripts/api-start.sh`, `scripts/ui-start.sh`, deployment validation helpers (`scripts/validate-*.sh`), and container tooling support day-to-day workflows.

## Coding Style & Naming Conventions
- C# (API): 4-space indentation; classes/methods PascalCase; locals/params camelCase; async methods end with `Async`. Keep feature logic co-located (controllers, services, validators, DTOs) and route shared middleware/extensions/constants to their dedicated folders.
- TypeScript/Angular (UI): 2-space indent, single quotes, ESLint + Prettier enforced. Follow feature-based modules within `src/app`, keep filenames kebab-case (e.g., `profile-card.component.ts`), and run `npm run lint` plus `npm run format` before committing.

## Testing Guidelines
- API: `AI.ProfilePhotoMaker.API.Tests` hosts unit, integration, controller, service, infrastructure, and performance suites. `dotnet test` exercises them; integration tests expect the SQL Server from `./dev-start.sh`.
- UI: Component and unit coverage via Karma (`npm test`), with extended scenarios in `npm run test:integration`. Playwright (`npm run test:e2e`) verifies UI flows; headless/targeted variants exist for CI and debugging.
- Cross-cutting E2E: Repository-level Playwright specs reside in `tests/e2e`, while API release readiness checks run from `AI.ProfilePhotoMaker.API/tests/playwright`. Keep all E2E tests deterministic and idempotent.

## Commit & Pull Request Guidelines
- Keep `main` stable—never open a PR without confirming with the team.
- Use Conventional Commits (e.g., `feat(ui): add cropping tool`, `fix(api): null check in upload`).
- PRs need a summary, linked issues, and test evidence (logs/screenshots for UI). Validate `npm run lint`, `npm run test`/`npm run test:e2e` as applicable, along with `dotnet build`, `dotnet test`, and API Playwright final validations when changing release-critical paths.

## Security & Configuration Tips
- Never commit secrets—follow `.env.example` and prefer environment variables or `dotnet user-secrets` locally.
- Sensitive values (e.g., `REPLICATE_API_TOKEN`, Azure Storage) must stay in env/secrets. If adjusting ports or hosts, update `proxy.conf*.json`, `docker-compose.yml`, relevant scripts, and Playwright configs under `tests/`.
- Consult `AI.ProfilePhotoMaker.API/SECURITY_NOTES.md` and `AI.ProfilePhotoMaker.API/MONITORING_SYSTEM_SUMMARY.md` when modifying auth, monitoring, or webhook integrations.
