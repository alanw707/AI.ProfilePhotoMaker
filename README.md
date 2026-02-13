# AI Profile Photo Maker

AI Profile Photo Maker is a full-stack app that generates professional profile photos from user-uploaded selfies.

The repository contains:
- `AI.ProfilePhotoMaker.API`: ASP.NET Core 8 Web API
- `AI.ProfilePhotoMaker.UI`: Angular 19 frontend
- `AI.ProfilePhotoMaker.API.Tests`: API test project
- Docker and local scripts for full-stack development

## What This Project Does

- Authenticated user accounts (JWT + Google OAuth)
- Selfie upload and validation pipeline
- AI model training/generation workflow (Replicate integration, with local mock support)
- Style-based photo generation and previews
- Credit-based usage and Stripe payment integration
- Blob/local image storage options

## Product Flow

1. Sign up / sign in
- UI: Angular auth pages and guards (`AI.ProfilePhotoMaker.UI/src/app/auth`, `AI.ProfilePhotoMaker.UI/src/app/guards`)
- API: authentication endpoints (`AI.ProfilePhotoMaker.API/Controllers/AuthController.cs`)

2. Upload selfies for training
- UI: upload and onboarding pages (`AI.ProfilePhotoMaker.UI/src/app/pages`)
- API: image upload and validation (`AI.ProfilePhotoMaker.API/Controllers/ImageController.cs`)

3. Start model training
- API: Replicate training orchestration and status tracking
  (`AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs`,
  `AI.ProfilePhotoMaker.API/Controllers/ModelStatusController.cs`,
  `AI.ProfilePhotoMaker.API/Controllers/ReplicateWebhookController.cs`)

4. Choose styles and generate headshots
- UI: style selection and generation workflow (`AI.ProfilePhotoMaker.UI/src/app/pages`, `AI.ProfilePhotoMaker.UI/src/app/shared`)
- API: style + generation endpoints
  (`AI.ProfilePhotoMaker.API/Controllers/StyleController.cs`,
  `AI.ProfilePhotoMaker.API/Controllers/ProfileController.cs`)

5. Review, enhance, and manage results
- API: gallery/profile/enhancement management
  (`AI.ProfilePhotoMaker.API/Controllers/ProfileController.cs`,
  `AI.ProfilePhotoMaker.API/Controllers/EnhancementController.cs`)

6. Purchase credits when needed
- UI: billing flows in app pages/services
- API: credit and Stripe handlers
  (`AI.ProfilePhotoMaker.API/Controllers/CreditController.cs`,
  `AI.ProfilePhotoMaker.API/Controllers/StripeWebhookController.cs`)

## Tech Stack

- Backend: .NET 8, ASP.NET Core Web API, EF Core, SQL Server
- Frontend: Angular 19, TypeScript
- Storage: Azure Blob Storage (or local/dev alternatives)
- Payments: Stripe
- Auth: JWT + Google OAuth
- Containers: Docker Compose

## Quick Start (Docker, Recommended)

1. Clone the repository:
```bash
git clone https://github.com/alanw707/AI.ProfilePhotoMaker.git
cd AI.ProfilePhotoMaker
```

2. Create your environment file:
```bash
cp .env.example .env
```

3. Set required values in `.env`:
- `MSSQL_SA_PASSWORD`
- `JWT_SECRET`
- `REPLICATE_API_TOKEN`
- `REPLICATE_WEBHOOK_SECRET`

For local development, you can keep `ENABLE_REPLICATE_MOCK=true` if you do not want to call live Replicate APIs.

4. Build and start:
```bash
docker compose build --no-cache
docker compose up -d
```

5. Open:
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

## Build, Test, and Quality

API:
```bash
dotnet build AI.ProfilePhotoMaker.sln
dotnet test AI.ProfilePhotoMaker.API.Tests
```

UI (run from `AI.ProfilePhotoMaker.UI`):
```bash
npm run lint
npm run format:check
npm test
```

E2E (run from `AI.ProfilePhotoMaker.UI`):
```bash
npm run test:e2e
```

## Project Structure

```text
AI.ProfilePhotoMaker/
├── AI.ProfilePhotoMaker.API/        # ASP.NET Core API
├── AI.ProfilePhotoMaker.API.Tests/  # xUnit tests
├── AI.ProfilePhotoMaker.UI/         # Angular frontend
├── docs/                            # Product, architecture, ops docs
├── scripts/                         # Dev/deploy scripts
├── docker-compose.yml               # Local full-stack containers
└── README.md
```

## Documentation

- Documentation index: `docs/INDEX.md`
- Architecture: `docs/architecture/ARCHITECTURE_OVERVIEW.md`
- Environment setup: `docs/setup/ENVIRONMENT_SETUP.md`
- API reference: `docs/operations/API_REFERENCE.md`

## Security Notes

- Do not commit secrets to git.
- Use environment variables or secret stores (Key Vault/user-secrets).
- Avoid logging PII, tokens, or uploaded image contents.

## License

MIT. See `LICENSE.txt`.
