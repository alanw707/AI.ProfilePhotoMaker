- remember never create a new deployment script, stick with simple-deployment and build images locally
- Don't preserve legacy old code, always remove them
- Always apply YAGNI software principle, you ain't gonna need it
- rememeber we're build a MVP production, doesn't need enterprise grade solutions yet
- use Playwright tests instead of curl for web applications whenever possible

## Development Environment

### Ngrok Setup
- **Reserved domain**: `clear-anteater-usually.ngrok-free.app`
- **Always use**: `ngrok http 5032 --domain=clear-anteater-usually.ngrok-free.app`
- **Never use**: `ngrok http 5032` (creates random URLs that break the config)

## Environment-Specific Secret Requirements

### 🔧 Development Environment
**Required Secrets:**
- `JWT_SECRET` (minimum 32 characters)
- `REPLICATE_API_TOKEN` (starts with 'r8_')
- `REPLICATE_WEBHOOK_SECRET`
- `MSSQL_SA_PASSWORD` (8+ chars with complexity) OR `ConnectionStrings__DefaultConnection`

**Optional Secrets:**
- `AZURE_STORAGE_CONNECTION_STRING` (can use `UseDevelopmentStorage=true`)
- `GOOGLE_CLIENT_ID` / `GOOGLE_CLIENT_SECRET` (for OAuth testing)
- `STRIPE_SECRET_KEY` (for payment testing)

### 🎯 Production/Staging Environments
**Required Secrets:**
- All development requirements PLUS:
- `AZURE_STORAGE_CONNECTION_STRING` (**CRITICAL**: Real Azure Storage, not development storage)
- `AZURE_STORAGE_CONTAINER_NAME` (**CRITICAL**: Container name for blob storage)

**Pre-Deployment Validation:**
```bash
# Validate secrets before deployment
./scripts/validate-secrets.sh Production

# Development validation
./scripts/validate-secrets.sh Development
```

### ⚠️ Critical Notes
- **Never deploy with `UseDevelopmentStorage=true` to production**
- **Azure Storage is required in containerized environments** (Production/Staging)
- **Missing Azure Storage causes 500 errors** due to inaccessible `/uploads` paths
- **Always run secret validation before deployment** to prevent production incidents