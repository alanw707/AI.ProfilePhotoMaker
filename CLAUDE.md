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
- **HTTPS Required**: All Replicate webhooks require HTTPS endpoints for signature validation

### Webhook Architecture
- **Enhanced Photo Feature**: Now uses consistent webhook pattern (previously conditional HTTP/HTTPS)
- **Performance**: 75-85% faster response times achieved through webhook optimization
- **Security**: All webhooks require signature validation via `REPLICATE_WEBHOOK_SECRET`
- **Reliability**: Unified architecture eliminates conditional polling logic

## Environment-Specific Secret Requirements

### 🔧 Development Environment
**Required Secrets:**
- `JWT_SECRET` (minimum 32 characters)
- `REPLICATE_API_TOKEN` (starts with 'r8_')
- `REPLICATE_WEBHOOK_SECRET` (**CRITICAL**: Required for all webhook operations including enhance photo)
- `MSSQL_SA_PASSWORD` (8+ chars with complexity) OR `ConnectionStrings__DefaultConnection`
- `GOOGLE_CLIENT_ID` (format: 123456789-abc123.apps.googleusercontent.com)
- `GOOGLE_CLIENT_SECRET` (starts with 'GOCSPX-')

**Optional Secrets:**
- `AZURE_STORAGE_CONNECTION_STRING` (can use `UseDevelopmentStorage=true`)
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
- **HTTPS Required for Webhooks**: All Replicate webhook endpoints must use HTTPS for security
- **Webhook Secret**: `REPLICATE_WEBHOOK_SECRET` must be configured in all environments for webhook validation
- Never directly commit to main branch when making changes