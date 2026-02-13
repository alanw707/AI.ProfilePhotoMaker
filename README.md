# AI Profile Photo Maker

A full-stack SaaS platform that generates professional headshots from user-uploaded selfies using AI model training and style-based image generation. Built with .NET 8 and Angular 19, deployed to Azure Container Apps with a fully automated CI/CD pipeline.

## How It Works

1. **Sign up** with email/password or Google OAuth
2. **Upload selfies** -- the app validates image quality, file type, and face detection client-side before upload
3. **Train a personal AI model** -- selfies are packaged and sent to Replicate for fine-tuning via async webhooks
4. **Choose a style and generate** -- select from a curated style catalog; the API dispatches generation jobs and streams results back to the gallery
5. **Enhance and download** -- apply optional AI enhancements, then download high-resolution outputs
6. **Purchase credits** -- a credit-based billing system powered by Stripe handles pay-as-you-go usage

## Architecture

```
                   +-----------+        +------------------+
  Browser -------> |  Angular  | -----> |  ASP.NET Core    |
  (SPA)            |  19 SPA   |  REST  |  Web API (.NET 8)|
                   +-----------+        +--------+---------+
                                                 |
                        +------------------------+------------------------+
                        |                        |                        |
                 +------+------+          +------+------+          +------+------+
                 |  SQL Server |          | Azure Blob  |          |  Replicate  |
                 |  (EF Core)  |          |  Storage    |          |  AI API     |
                 +-------------+          +-------------+          +------+------+
                                                                         |
                                                                   Webhooks
                                                                   (training complete,
                                                                    generation complete)
                        +------------------------------------------------+
                        |
                 +------+------+
                 |   Stripe    |
                 |  Payments   |
                 +-------------+
```

**Frontend** -- Angular SPA handling auth, onboarding, style selection, dashboard, and gallery management with client-side face detection (face-api.js).

**Backend** -- ASP.NET Core API with thin controllers, service-layer business logic, async background jobs, webhook handlers, and health checks. Structured logging via Serilog.

**Data** -- SQL Server with EF Core Code-First migrations. Repository pattern for data access.

**Infrastructure** -- Docker Compose for local development; GitHub Actions CI/CD deploys to Azure Container Apps.

## Technology Stack

| Area | Technology |
| --- | --- |
| Backend | .NET 8, ASP.NET Core Web API, EF Core, Serilog |
| Frontend | Angular 19, TypeScript, RxJS, face-api.js |
| Database | SQL Server |
| Auth | JWT, Google OAuth |
| AI | Replicate API (Flux model fine-tuning + inference) |
| Payments | Stripe (PaymentIntents, webhook verification) |
| Storage | Azure Blob Storage / local dev fallback |
| Testing | xUnit, Moq, FluentAssertions, Karma, Playwright |
| DevOps | GitHub Actions, Docker Compose, Azure Container Apps |

## Key Engineering Decisions

- **Webhook-driven async pipeline** -- AI training and generation are long-running operations (minutes). The API uses Replicate webhooks with HMAC signature verification rather than polling, keeping the system event-driven and scalable.
- **Credit-based billing** -- Stripe PaymentIntents with webhook confirmation ensure credits are only granted after successful payment. No raw card data touches the server.
- **Storage abstraction** -- A strategy pattern switches between Azure Blob Storage (production) and local filesystem (development) without code changes.
- **Client-side face detection** -- face-api.js validates selfies in the browser before upload, reducing server load and giving instant feedback.
- **Data retention and privacy** -- Background jobs enforce configurable retention policies. Users can export their data or delete their account (DSAR support).

## CI/CD Pipelines

| Workflow | Trigger | Purpose |
| --- | --- | --- |
| `pr-code-review.yml` | Pull requests to `main`/`develop` | Lint, build, test, security scan |
| `simple-deploy.yml` | Push to `main` | Build, test, deploy to Azure Container Apps |

## Quick Start

```bash
# Clone and configure
git clone https://github.com/alanw707/AI.ProfilePhotoMaker.git
cd AI.ProfilePhotoMaker
cp .env.example .env
# Set MSSQL_SA_PASSWORD, JWT_SECRET, REPLICATE_API_TOKEN, REPLICATE_WEBHOOK_SECRET

# Run with Docker
docker compose build --no-cache
docker compose up -d

# Frontend: http://localhost:4200
# API:      http://localhost:5032
```

For local development without Docker:

```bash
# Backend
dotnet run --project AI.ProfilePhotoMaker.API

# Frontend
cd AI.ProfilePhotoMaker.UI && npm ci && npm run dev:local
```

## Build and Test

```bash
# API
dotnet build AI.ProfilePhotoMaker.sln
dotnet test AI.ProfilePhotoMaker.API.Tests

# UI (from AI.ProfilePhotoMaker.UI/)
npm run lint && npm run format:check
npm test
npm run test:e2e
```

## Project Structure

```
AI.ProfilePhotoMaker/
├── AI.ProfilePhotoMaker.API/        # ASP.NET Core Web API
│   ├── Controllers/                 # Thin HTTP endpoints
│   ├── Services/                    # Business logic layer
│   ├── Models/DTOs/                 # Request/response contracts
│   ├── Data/                        # EF Core DbContext
│   ├── Migrations/                  # Database migrations
│   └── Extensions/                  # DI service registration
├── AI.ProfilePhotoMaker.API.Tests/  # xUnit + Moq + FluentAssertions
├── AI.ProfilePhotoMaker.UI/         # Angular 19 SPA
│   ├── src/app/auth/                # Auth components + guards
│   ├── src/app/pages/               # Feature pages
│   ├── src/app/services/            # HTTP + state services
│   └── src/app/shared/              # Reusable components
├── docs/                            # Architecture + operations docs
├── scripts/                         # Build and deploy scripts
├── .github/workflows/               # CI/CD pipelines
└── docker-compose.yml               # Full local stack
```

## Documentation

- [Architecture Overview](docs/architecture/ARCHITECTURE_OVERVIEW.md)
- [Environment Setup](docs/setup/ENVIRONMENT_SETUP.md)
- [API Reference](docs/operations/API_REFERENCE.md)
- [Documentation Index](docs/INDEX.md)

## License

MIT -- see [LICENSE.txt](LICENSE.txt).
