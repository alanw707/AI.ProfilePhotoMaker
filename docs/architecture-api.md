# Architecture - API

## Executive Summary
ASP.NET Core Web API providing authentication, payments, model training/inference, and storage integration for the product.

## Technology Stack
- .NET 8 / ASP.NET Core Web API
- Entity Framework Core + SQL Server
- Stripe, Replicate, Azure Blob Storage
- Serilog + Application Insights

## Architecture Pattern
Layered Web API (Controllers -> Services -> Data). Background workers handle training/polling and retention tasks.

## Data Architecture
- DbContext sets: 14 entities (see `docs/data-models-api.md`)
- EF Core migrations and model classes under `AI.ProfilePhotoMaker.API/Migrations` and `AI.ProfilePhotoMaker.API/Models`

## API Design
- Controllers: 22
- Endpoints detected: 130
- Base route pattern: `/api/[controller]`
- Detailed routes in `docs/api-contracts-api.md`

## Auth & Security
- Google OAuth + JWT
- Cloudflare Turnstile verification in auth flow

## Background Services
- StartupDiagnosticsHostedService, TrainingPollingBackgroundService, BasicTierBackgroundService, RetentionPolicyBackgroundService

## Integrations
- Stripe payments + webhooks
- Replicate/OpenAI for generation and enhancement
- Azure Blob Storage for image persistence

## Source Tree
See `docs/source-tree-analysis.md`.

## Development Workflow
See `docs/development-guide.md`.

## Deployment Architecture
See `docs/deployment-guide.md`.

## Testing Strategy
- xUnit in `AI.ProfilePhotoMaker.API.Tests`
- API Playwright checks in `AI.ProfilePhotoMaker.API/tests/playwright`
