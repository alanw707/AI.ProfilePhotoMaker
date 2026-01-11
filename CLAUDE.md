### Important Rules
- Never create new deployment scripts unless explicitly asked
- Don't preserve legacy code - always remove it
- Apply YAGNI principle - keep implementation simple
- Building MVP production - no enterprise-grade solutions needed unless requested to do so
- Use Playwright tests instead of curl for web applications
- Verify work before marking complete
- Never directly commit to main branch

## Data Retention Policy

**30-day retention** for all images (aligned with Replicate model persistence).

`RetentionPolicyBackgroundService` runs every 6 hours:
- Deletes expired images
- Sends deletion warnings (14 and 7 days before)
- Cleans up orphaned enhanced images
- Removes Replicate models when users have no remaining headshots

## Development Environment

### Ngrok Setup
```bash
# Always use reserved domain
ngrok http 5032 --domain=clear-anteater-usually.ngrok-free.app

# Never use random URLs - breaks webhook config
```

### Local Development
```bash
# Rebuild API and UI
./dev-rebuild.sh

# API only
./dev-rebuild.sh --api-only

# Docker containers
./dev-rebuild.sh --docker
```

## Required Secrets (All Environments)

All secrets are validated by `./scripts/validate-secrets.sh`:

| Secret | Format | Notes |
|--------|--------|-------|
| `JWT_SECRET` | 32+ chars | Authentication |
| `REPLICATE_API_TOKEN` | `r8_*` | AI model API |
| `REPLICATE_WEBHOOK_SECRET` | `whsec_*` | Webhook validation |
| `GOOGLE_CLIENT_ID` | `*-*.apps.googleusercontent.com` | OAuth |
| `GOOGLE_CLIENT_SECRET` | `GOCSPX-*` | OAuth |
| `STRIPE_SECRET_KEY` | `sk_*` | Payment processing |
| `STRIPE_PUBLISHABLE_KEY` | `pk_*` | Frontend payments |
| `STRIPE_WEBHOOK_SECRET` | `whsec_*` | Payment webhooks |
| `MSSQL_SA_PASSWORD` | 8+ chars, complex | Database |
| `AZURE_STORAGE_CONNECTION_STRING` | Connection string | Blob storage |
| `AZURE_STORAGE_CONTAINER_NAME` | `profile-images` | Storage container |

### Validate Before Deployment
```bash
./scripts/validate-secrets.sh Production
./scripts/validate-secrets.sh Development
```

## Webhook Architecture

- **HTTPS required** - All Replicate webhooks need HTTPS (use ngrok reserved domain)
- **Signature validation** - `REPLICATE_WEBHOOK_SECRET` validates all webhook calls
- **Unified pattern** - No conditional HTTP/HTTPS logic

## Key Services

| Service | Purpose |
|---------|---------|
| `RetentionPolicyBackgroundService` | Image cleanup every 6 hours |
| `TrainingPollingBackgroundService` | Model training status polling |
| `BasicTierBackgroundService` | Basic tier credit management |
| `TurnstileVerificationService` | Bot protection |
| `ReplicateSignatureValidationAttribute` | Webhook signature validation |

## Configuration

Key `appsettings.json` sections:
- `Replicate:StyleTuning` - Pro vs casual style inference parameters
- `IpRateLimiting` - Endpoint rate limits
- `RetentionNotifications` - Deletion warning days (14, 7)
- `LegacyCompatibility` - Backward compatibility toggles

## Testing

- **E2E**: Playwright tests in `tests/e2e/`
- **Integration**: `AI.ProfilePhotoMaker.API.Tests/Integration/`
- **Unit**: `AI.ProfilePhotoMaker.API.Tests/Unit/`

Run Playwright tests:
```bash
cd tests/e2e && npx playwright test
```

## Infrastructure

- **Deploy**: `.github/workflows/simple-deploy.yml`
- **IaC**: `infrastructure/simple-deploy.bicep`
- **Azure Container Apps** with managed identities
- **Azure SQL Database** (MSSQL)
- **Azure Blob Storage** for images
