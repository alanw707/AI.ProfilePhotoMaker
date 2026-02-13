# AI Profile Photo Maker

AI Profile Photo Maker is a full-stack platform for generating professional profile photos from user-uploaded selfies using AI model training and style-based generation.

## Overview

This repository includes:
- `AI.ProfilePhotoMaker.API`: ASP.NET Core 8 Web API
- `AI.ProfilePhotoMaker.UI`: Angular 19 frontend
- `AI.ProfilePhotoMaker.API.Tests`: API tests (xUnit)
- Docker, local development scripts, and deployment automation

Core capabilities:
- Account auth with JWT + Google OAuth
- Selfie upload validation and preprocessing
- AI training + generation workflow (Replicate integration)
- Style-based output generation and previewing
- Credit-based billing and Stripe integration
- Blob/local storage strategies

## Product Flow

1. Sign up / sign in
- UI: `AI.ProfilePhotoMaker.UI/src/app/auth`, `AI.ProfilePhotoMaker.UI/src/app/guards`
- API: `AI.ProfilePhotoMaker.API/Controllers/AuthController.cs`

2. Upload selfies
- UI: onboarding and upload pages in `AI.ProfilePhotoMaker.UI/src/app/pages`
- API: upload + validation in `AI.ProfilePhotoMaker.API/Controllers/ImageController.cs`

3. Start model training
- API orchestration and status tracking:
  - `AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs`
  - `AI.ProfilePhotoMaker.API/Controllers/ModelStatusController.cs`
  - `AI.ProfilePhotoMaker.API/Controllers/ReplicateWebhookController.cs`

4. Select style and generate images
- UI style flow: `AI.ProfilePhotoMaker.UI/src/app/pages`, `AI.ProfilePhotoMaker.UI/src/app/shared`
- API generation endpoints:
  - `AI.ProfilePhotoMaker.API/Controllers/StyleController.cs`
  - `AI.ProfilePhotoMaker.API/Controllers/ProfileController.cs`

5. Review, enhance, and manage outputs
- API management + enhancement:
  - `AI.ProfilePhotoMaker.API/Controllers/ProfileController.cs`
  - `AI.ProfilePhotoMaker.API/Controllers/EnhancementController.cs`

6. Purchase credits when needed
- API billing + Stripe integration:
  - `AI.ProfilePhotoMaker.API/Controllers/CreditController.cs`
  - `AI.ProfilePhotoMaker.API/Controllers/StripeWebhookController.cs`

## Architecture At A Glance

- Frontend: Angular SPA for auth, onboarding, style selection, dashboard, and gallery management
- Backend: ASP.NET Core API with business services, async background jobs, webhook handlers, and health checks
- Data: SQL Server via EF Core migrations in `AI.ProfilePhotoMaker.API/Migrations/`
- Storage: Azure Blob (or local alternatives for development)
- External integrations: Replicate (AI), Stripe (payments), Google OAuth
- Runtime options: Docker Compose local stack and Azure deployment workflow

## Technology Stack

| Area | Technology |
| --- | --- |
| Backend | .NET 8, ASP.NET Core Web API, EF Core, Serilog |
| Frontend | Angular 19, TypeScript, RxJS |
| Database | SQL Server |
| Auth | JWT, Google OAuth |
| AI | Replicate API + webhook callbacks |
| Payments | Stripe |
| Storage | Azure Blob Storage / local dev storage |
| Testing | xUnit, Moq, FluentAssertions, Karma, Playwright |
| DevOps | GitHub Actions, Docker Compose, Azure Container Apps |

## GitHub Workflows

Core automation is defined here:
- PR static analysis: `.github/workflows/pr-code-review.yml`
- Main deployment pipeline: `.github/workflows/simple-deploy.yml`

Current behavior:
- `pr-code-review.yml` runs on pull requests to `main`/`develop` and ignores markdown/doc-only changes.
- `simple-deploy.yml` runs on push to `main` (and manual dispatch).

## Quick Start (Docker Recommended)

1. Clone:
```bash
git clone https://github.com/alanw707/AI.ProfilePhotoMaker.git
cd AI.ProfilePhotoMaker
```

2. Create environment file:
```bash
cp .env.example .env
```

3. Set required environment values in `.env`:
- `MSSQL_SA_PASSWORD`
- `JWT_SECRET`
- `REPLICATE_API_TOKEN`
- `REPLICATE_WEBHOOK_SECRET`

Optional for local development:
- `ENABLE_REPLICATE_MOCK=true` to avoid live Replicate calls

4. Build and run containers:
```bash
docker compose build --no-cache
docker compose up -d
```

5. Access services:
- Frontend: `http://localhost:4200`
- API: `http://localhost:5032`

## Local Development (Without Docker)

Backend:
```bash
dotnet run --project AI.ProfilePhotoMaker.API
```

Frontend:
```bash
cd AI.ProfilePhotoMaker.UI
npm ci
npm run dev:local
```

## Build, Test, Quality

API:
```bash
dotnet build AI.ProfilePhotoMaker.sln
dotnet test AI.ProfilePhotoMaker.API.Tests
```

UI (`AI.ProfilePhotoMaker.UI`):
```bash
npm run lint
npm run format:check
npm test
```

E2E (`AI.ProfilePhotoMaker.UI`):
```bash
npm run test:e2e
```

## Project Structure

```text
AI.ProfilePhotoMaker/
├── AI.ProfilePhotoMaker.API/        # ASP.NET Core API
├── AI.ProfilePhotoMaker.API.Tests/  # API tests
├── AI.ProfilePhotoMaker.UI/         # Angular frontend
├── docs/                            # Product, architecture, operations
├── scripts/                         # Dev/build/deploy scripts
├── .github/workflows/               # PR analysis + deploy pipelines
├── docker-compose.yml               # Full local stack
└── README.md
```

## Documentation

- Documentation index: `docs/INDEX.md`
- Architecture overview: `docs/architecture/ARCHITECTURE_OVERVIEW.md`
- Environment setup: `docs/setup/ENVIRONMENT_SETUP.md`
- API reference: `docs/operations/API_REFERENCE.md`

## Security Notes

- Never commit secrets to source control
- Use environment variables, user-secrets, or Key Vault
- Avoid logging PII, auth tokens, or uploaded image content

## License

MIT. See `LICENSE.txt`.
