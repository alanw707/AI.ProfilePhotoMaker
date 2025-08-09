# Environment Variable Setup Guide

## Overview

The AI Profile Photo Maker application uses a comprehensive environment variable management system for secure configuration across different deployment environments.

## Quick Setup

### 1. Local Development

```bash
# Copy the development template
cp .env.development.template .env

# Edit the .env file with your actual values
nano .env
```

### 2. Required Variables

You must configure these variables before running the application:

```bash
# Database password (minimum 8 chars, complexity required)
MSSQL_SA_PASSWORD=YourSecurePassword123!

# JWT secret (minimum 32 chars, high entropy)
JWT_SECRET=YourSuperSecretJWTKeyAtLeast32CharactersLong

# Replicate API token for AI models
REPLICATE_API_TOKEN=r8_your_replicate_token_here

# Webhook secret for Replicate callbacks
REPLICATE_WEBHOOK_SECRET=your_webhook_secret_32_chars_min
```

## Environment Files

### Development
- **File**: `.env` or `.env.development`
- **Template**: `.env.development.template`
- **Features**: Payment simulation, detailed logging, auto-migrations

### Test
- **File**: `.env.test`
- **Template**: `.env.test.template`
- **Features**: Test database, conservative settings, real payment testing

### Production
- **File**: `.env.production`
- **Template**: `.env.production.template`
- **Features**: Azure Key Vault integration, minimal logging, live payments

## Security Best Practices

### Password Requirements

| Variable | Minimum Length | Requirements |
|----------|---------------|--------------|
| `MSSQL_SA_PASSWORD` | 8 chars | Mixed case, numbers, special chars |
| `JWT_SECRET` | 32 chars | Cryptographically secure |
| `REPLICATE_WEBHOOK_SECRET` | 32 chars | High entropy hex string |

### Secret Generation

```bash
# Generate JWT secret
openssl rand -base64 64

# Generate webhook secret
openssl rand -hex 32

# Generate strong password
openssl rand -base64 24
```

## Environment Loading Priority

1. **System Environment Variables** (highest priority)
2. **`.env.{environment}.local`** (environment-specific local overrides)
3. **`.env.local`** (local overrides)
4. **`.env.{environment}`** (environment-specific)
5. **`.env`** (default)
6. **appsettings.json** (lowest priority)

## Configuration Validation

The application validates all environment variables on startup:

### Required Variables
- ✅ `MSSQL_SA_PASSWORD` - Database access
- ✅ `JWT_SECRET` - Token signing
- ✅ `REPLICATE_API_TOKEN` - AI model access
- ✅ `REPLICATE_WEBHOOK_SECRET` - Webhook validation

### Optional Variables
- 🔶 `GOOGLE_CLIENT_ID` / `GOOGLE_CLIENT_SECRET` - OAuth
- 🔶 `STRIPE_*` - Payment processing
- 🔶 `AZURE_STORAGE_CONNECTION_STRING` - Cloud storage

## Azure App Service Configuration

### Method 1: Environment Variables
Configure in Azure Portal → App Service → Configuration → Application settings:

```
MSSQL_SA_PASSWORD=your_secure_password
JWT_SECRET=your_jwt_secret
REPLICATE_API_TOKEN=r8_your_token
```

### Method 2: Azure Key Vault (Recommended)
Use Key Vault references for production secrets:

```
JWT_SECRET=@Microsoft.KeyVault(SecretUri=https://myvault.vault.azure.net/secrets/jwt-secret/)
MSSQL_SA_PASSWORD=@Microsoft.KeyVault(SecretUri=https://myvault.vault.azure.net/secrets/db-password/)
```

## Docker Configuration

### Docker Compose
```yaml
services:
  api:
    environment:
      - MSSQL_SA_PASSWORD=${MSSQL_SA_PASSWORD}
      - JWT_SECRET=${JWT_SECRET}
      - REPLICATE_API_TOKEN=${REPLICATE_API_TOKEN}
```

### Container Apps
Set environment variables in the container app configuration:

```bash
az containerapp update \
  --name myapp \
  --resource-group mygroup \
  --set-env-vars MSSQL_SA_PASSWORD=secretvalue JWT_SECRET=anothersecret
```

## Troubleshooting

### Common Issues

| Issue | Solution |
|-------|----------|
| "Required environment variable not configured" | Check variable name spelling and ensure it's set |
| "JWT secret too short" | Use minimum 32 characters |
| "Database password complexity" | Include uppercase, lowercase, numbers, special chars |
| "Replicate token invalid" | Ensure token starts with 'r8_' |

### Validation Errors

The application logs detailed validation errors on startup:

```
❌ Environment validation failed with 2 errors:
  - JWT_SECRET: JWT secret must be at least 32 characters
  - REPLICATE_API_TOKEN: Replicate API token should start with 'r8_'
```

### Debug Commands

```bash
# Check environment variables
dotnet run --check-env

# Validate configuration
dotnet run --validate-config

# Test database connection
dotnet run --check-db-connection
```

## Development Workflow

### Initial Setup
1. Copy `.env.development.template` to `.env`
2. Fill in required values
3. Run `dotnet run` to validate configuration
4. Application will start with validation summary

### Environment Switching
```bash
# Switch to test environment
export ASPNETCORE_ENVIRONMENT=Test
cp .env.test.template .env.test

# Switch to production
export ASPNETCORE_ENVIRONMENT=Production
```

## Integration with CI/CD

### GitHub Actions
```yaml
env:
  MSSQL_SA_PASSWORD: ${{ secrets.DB_PASSWORD }}
  JWT_SECRET: ${{ secrets.JWT_SECRET }}
  REPLICATE_API_TOKEN: ${{ secrets.REPLICATE_TOKEN }}
```

### Azure DevOps
```yaml
variables:
- group: 'production-secrets'
- name: MSSQL_SA_PASSWORD
  value: $(db-password)
```

## Security Checklist

- [ ] All secrets use strong, unique values
- [ ] Production secrets stored in Azure Key Vault
- [ ] No secrets committed to version control
- [ ] Regular secret rotation (90 days)
- [ ] Environment-specific secret values
- [ ] Monitoring for secret access/usage
- [ ] Backup and recovery plan for secrets

## Support

For environment configuration issues:
1. Check the validation errors in application logs
2. Verify against `.env.example` for required format
3. Use the troubleshooting section above
4. Check Azure Key Vault access if using production secrets

---

**Security Warning**: Never commit actual secret values to version control. Always use templates and manage secrets through secure channels.