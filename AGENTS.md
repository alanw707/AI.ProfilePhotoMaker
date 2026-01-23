# AI.ProfilePhotoMaker - Agent Guidelines

Guidelines for coding agents working in this repository.

## Project Overview

- **Stack**: ASP.NET Core 8 Web API (C#) + Angular 19 (TypeScript)
- **Database**: EF Core with SQL Server; migrations in `AI.ProfilePhotoMaker.API/Migrations/`
- **Auth**: JWT + Google OAuth (`Services/Authentication/`)
- **Storage**: Azure Blob or local (`Services/Storage/`)
- **Payments**: Stripe (`Services/Payment/`)

## Build Commands

```bash
# API (.NET)
dotnet build AI.ProfilePhotoMaker.sln
dotnet run --project AI.ProfilePhotoMaker.API            # Port 5032
dotnet ef database update -p AI.ProfilePhotoMaker.API

# UI (Angular) - run from AI.ProfilePhotoMaker.UI/
npm ci && npm run dev:local                              # Port 4200
npm run build                                            # Production build

# Full Stack
./dev-start.sh                                           # SQL + API + UI
```

## Test Commands

```bash
# API (xUnit) - run single test with --filter
dotnet test AI.ProfilePhotoMaker.API.Tests
dotnet test AI.ProfilePhotoMaker.API.Tests --filter "FullyQualifiedName~ClassName"
dotnet test AI.ProfilePhotoMaker.API.Tests --filter "DisplayName~test_name"
dotnet test AI.ProfilePhotoMaker.API.Tests --filter "ClassName=MyTests&MethodName=MyTest"

# UI Unit Tests (Karma) - run from AI.ProfilePhotoMaker.UI/
npm test                                                 # All unit tests
npm run test:integration                                 # Integration tests

# E2E (Playwright) - run from AI.ProfilePhotoMaker.UI/
npm run test:e2e                                         # All E2E
npm run test:e2e:chrome                                  # Chrome only
npx playwright test tests/mytest.spec.ts                 # Single file
npx playwright test -g "test name"                       # By test name
```

## Lint & Format

```bash
# UI - run from AI.ProfilePhotoMaker.UI/
npm run lint                    # ESLint check
npm run lint:fix                # Auto-fix
npm run format                  # Prettier format
npm run quality:fix             # Lint + format

# API
dotnet build                    # Compiler warnings as lint
```

## Code Style - C# (.NET API)

**Formatting**: 4-space indent, nullable enabled, implicit usings

**Naming**:
- Classes/methods/properties: `PascalCase`
- Variables/parameters: `camelCase`
- Private fields: `_camelCase`
- Async methods: suffix `Async`

**Patterns**:
- Thin controllers; delegate to services
- `async/await` only; never `.Result` or `.Wait()`
- Register services in `Extensions/*ServiceExtensions.cs`
- Return typed DTOs from `Models/DTOs/`
- Use `ILogger<T>` with structured logging
- Guard clauses; max 3 levels nesting
- Use Options pattern for configuration

**Error Handling**:
- Fail fast with actionable messages
- Return accurate HTTP status codes (never raw stack traces)
- Use `[Authorize]` on sensitive endpoints

## Code Style - TypeScript (Angular UI)

**Formatting** (Prettier enforced):
- 2-space indent, single quotes, semicolons required
- Trailing commas (ES5), LF endings
- 100 char width (120 for HTML)

**Naming**:
- Files: `kebab-case` (`profile-card.component.ts`)
- Components: `PascalCase` class, `app-kebab-case` selector
- Directives: `appCamelCase` selector
- Variables/functions: `camelCase`
- Interfaces/types/classes: `PascalCase`
- Enums: `PascalCase` name, `UPPER_CASE` members

**Patterns**:
- Strongly type everything; avoid `any`
- Use `Observable` with proper cleanup (`takeUntil`, `DestroyRef`)
- HTTP calls in `services/*` using `HttpClient`
- Auth in `interceptors/`
- Prefer local state over global

**Imports**: Group logically (Angular core > third-party > app modules > relative)

## Project Structure

```
AI.ProfilePhotoMaker.API/
  Controllers/          # Thin HTTP endpoints
  Services/             # Business logic
  Models/DTOs/          # Data transfer objects
  Data/                 # DbContext
  Migrations/           # EF Core migrations
  Extensions/           # DI registration
  Filters/              # Action filters

AI.ProfilePhotoMaker.API.Tests/
  Unit/                 # xUnit + Moq + FluentAssertions
  Integration/          # WebApplicationFactory tests

AI.ProfilePhotoMaker.UI/
  src/app/              # Angular components/services
  e2e/                  # Playwright E2E tests
```

## Security Guidelines

- **Never commit secrets** - use `.env`, user-secrets, or Key Vault
- Required env vars: `JWT_SECRET`, `REPLICATE_API_TOKEN`, `REPLICATE_WEBHOOK_SECRET`
- Do not log PII, tokens, emails, or image contents
- Validate uploads: max 20 files, 10MB each, jpg/png/webp only
- Stripe: tokens/PaymentIntents only, never raw card data
- Webhooks require signature validation

## Testing Expectations

- Add/update tests when changing behavior
- Keep tests deterministic and idempotent
- Use test fixtures from shared folders
- API tests use: xUnit, Moq, FluentAssertions, AutoFixture
- Integration tests need SQL Server (`./dev-start.sh`)

## Commit Style

Conventional Commits: `type(scope): description`
```
feat(ui): add cropping tool
fix(api): null check in upload handler
refactor(api): extract validation to service
test(ui): add gallery component specs
```

## Pre-Commit Checklist

1. `dotnet build AI.ProfilePhotoMaker.sln` - compiles
2. `dotnet test AI.ProfilePhotoMaker.API.Tests` - passes
3. `npm run lint` (UI) - no errors
4. `npm run format:check` (UI) - valid
5. `npm test` (UI) - passes
6. No secrets in files

## Key Principles

- Prefer minimal, targeted edits; do not refactor unrelated code
- Follow existing patterns in `Services/`, `Controllers/`, `Models/`
- MVP scope: avoid over-engineering, YAGNI applies
- Explain non-trivial changes in commit messages
- When uncertain: ask about security, PII, payment flows, or breaking API changes
