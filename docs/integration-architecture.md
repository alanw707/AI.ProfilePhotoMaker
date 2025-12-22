# Integration Architecture

Deep-scan summary of cross-part integrations and external dependencies.

## UI -> API
- Angular UI calls ASP.NET Core API via HTTP (proxy config at `AI.ProfilePhotoMaker.UI/proxy.conf.json`).
- Auth flow initiated from UI and handled by API (`AuthController`, OAuth endpoints).

## API -> Data Stores
- SQL Server via EF Core (`ApplicationDbContext`).
- Azure Blob Storage for image persistence.

## API -> External Services
- Replicate and OpenAI for model inference/enhancement (see `ReplicateController`, `EnhancementController`).
- Stripe for payments and webhooks (`StripeWebhookController`, payment services).
- Application Insights for telemetry.

## Webhooks
- Replicate webhooks for training/prediction status (`ReplicateWebhookController`).
- Stripe webhooks for payment events.

## Auth Flow (High Level)
- OAuth provider (Google) redirects back to API.
- API issues JWT/cookie and persists user state.
- UI uses authenticated API calls for profile and generation workflows.
