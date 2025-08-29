# Environment Setup Guide

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

# Google OAuth credentials
GOOGLE_CLIENT_ID=123456789-abc123.apps.googleusercontent.com
GOOGLE_CLIENT_SECRET=GOCSPX-your_secret_here
```

## Environment Variables Reference

| Category | Variable Name | Environment Type | Notes |
|----------|---------------|------------------|-------|
| Database | `MSSQL_SA_PASSWORD` | All | Required |
| Database | `ConnectionStrings__DefaultConnection` | Production | Alternative to SA password |
| JWT | `JWT_SECRET` | All | Min 32 chars |
| AI/ML | `REPLICATE_API_TOKEN` | All | Starts with 'r8_' |
| AI/ML | `REPLICATE_WEBHOOK_SECRET` | All | **CRITICAL** for webhooks |
| Storage | `AZURE_STORAGE_CONNECTION_STRING` | Production | **CRITICAL** |
| Storage | `AZURE_STORAGE_CONTAINER_NAME` | Production | **CRITICAL** |
| OAuth | `GOOGLE_CLIENT_ID` | All | Required for auth |
| OAuth | `GOOGLE_CLIENT_SECRET` | All | Required for auth |
| Payment | `STRIPE_SECRET_KEY` | All | Required for payments |
| Payment | `STRIPE_PUBLISHABLE_KEY` | All | Required for payments |
| Payment | `STRIPE_WEBHOOK_SECRET` | All | Required for payments |

## Secret Management Workflow

### Secret Store Hierarchy

🔑 **Source of Truth: Azure Key Vault**
- **Purpose**: Production secrets used by deployed infrastructure
- **Access**: Via Azure CLI (`az keyvault secret show`)
- **Usage**: Container Apps read directly from Key Vault secrets

🏠 **Local Development: dotnet user-secrets**
- **Purpose**: Development environment secrets
- **Access**: Via `dotnet user-secrets` commands
- **Storage**: Local encrypted store per project

🚀 **CI/CD Pipeline: GitHub Actions Secrets**
- **Purpose**: Build and deployment secrets
- **Access**: Via GitHub CLI (`gh secret set`)
- **Usage**: GitHub Actions runners during deployment

### Proper Secret Update Workflow

```bash
# Step 1: Use the synchronization script
./scripts/sync-secrets.sh

# Step 2: Verify consistency
./scripts/sync-secrets.sh --validate-only

# Step 3: Test local development
dotnet run  # Test API startup

# Step 4: Test CI/CD pipeline
git push  # Triggers deployment with validation
```

### Manual Secret Management

#### Local Development Setup
```bash
# Set required secrets for local development
dotnet user-secrets set "JWT_SECRET" "YourSuperSecretJWTKeyAtLeast32CharactersLong"
dotnet user-secrets set "REPLICATE_API_TOKEN" "r8_your_replicate_token_here"
dotnet user-secrets set "REPLICATE_WEBHOOK_SECRET" "your_webhook_secret_32_chars_min"
dotnet user-secrets set "GOOGLE_CLIENT_ID" "123456789-abc123.apps.googleusercontent.com"
dotnet user-secrets set "GOOGLE_CLIENT_SECRET" "GOCSPX-your_secret_here"
```

#### Production Secret Management
```bash
# GitHub Actions secrets (for CI/CD)
gh secret set JWT_SECRET --body "YourSuperSecretJWTKeyAtLeast32CharactersLong"
gh secret set REPLICATE_API_TOKEN --body "r8_your_replicate_token_here"
gh secret set REPLICATE_WEBHOOK_SECRET --body "your_webhook_secret_32_chars_min"

# Azure Key Vault (for production runtime)
az keyvault secret set --vault-name your-keyvault --name "JWT-SECRET" --value "YourSuperSecretJWTKeyAtLeast32CharactersLong"
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
- **File**: Environment variables via Azure Container Apps
- **Source**: Azure Key Vault secrets
- **Features**: Production security, minimal logging, manual migrations

## Critical Configuration Issues & Solutions

### Azure Storage Configuration
❌ **WRONG - This causes deployment failures:**
```bicep
// Infrastructure (Bicep) - INCORRECT NAMING
{
  name: 'AzureStorage__ConnectionString'  // ❌ Double underscore
  value: '...'
}
```

✅ **CORRECT - Application expects this naming:**
```bicep
// Infrastructure (Bicep) - CORRECT NAMING
{
  name: 'AZURE_STORAGE_CONNECTION_STRING'  // ✅ Single underscore
  value: '...'
}
```

### Database Connection Priority
1. **ConnectionStrings__DefaultConnection** (preferred in production)
2. **MSSQL_SA_PASSWORD** (fallback for development)

## Validation Commands

### Local Development Validation
```bash
# Test database connection
dotnet ef database update

# Test API startup
dotnet run --urls=http://localhost:5032

# Test all required secrets
./scripts/validate-secrets.sh Development
```

### Production Validation
```bash
# Validate deployment secrets
./scripts/validate-secrets.sh Production

# Test deployed API
curl https://api.aiprofilephotomaker.com/health

# Verify Azure storage access
az storage container list --connection-string "$AZURE_STORAGE_CONNECTION_STRING"
```

## Troubleshooting

### Common Issues

#### 1. Authentication Failures
**Symptom**: VS Code Azure SQL Database connection failure
**Cause**: Secret stores became desynchronized
**Solution**: Run `./scripts/sync-secrets.sh` to resynchronize all stores

#### 2. Azure Storage Failures (500 errors)
**Symptom**: Upload failures, inaccessible `/uploads` paths
**Cause**: Missing or invalid `AZURE_STORAGE_CONNECTION_STRING`
**Solution**: Verify infrastructure generates correct connection string

#### 3. Webhook Validation Failures
**Symptom**: Replicate webhook signature validation fails
**Cause**: Missing or incorrect `REPLICATE_WEBHOOK_SECRET`
**Solution**: Ensure secret is configured in all environments with same value

#### 4. OAuth Deployment Continuity
**Symptom**: Google authentication fails after deployment
**Cause**: OAuth credentials not properly configured in production
**Solution**: Verify `GOOGLE_CLIENT_ID` and `GOOGLE_CLIENT_SECRET` in Azure Container Apps

### Emergency Recovery

If all secret stores become inconsistent:

1. **Identify Source of Truth**: Check Azure Key Vault for production values
2. **Reset All Stores**: Use `./scripts/emergency-secret-reset.sh`
3. **Validate Consistency**: Run validation across all environments
4. **Test Functionality**: Verify authentication, storage, and webhooks work

## Security Best Practices

1. **Never commit secrets** to version control
2. **Use different secrets** for development and production where possible
3. **Rotate secrets regularly** (quarterly for production)
4. **Monitor secret usage** through Azure Key Vault logs
5. **Use managed identities** where possible instead of connection strings
6. **Validate secrets** before deployment to prevent production failures

## Next Steps

After setting up your environment:
1. Follow the [Local Development Guide](LOCAL_DEVELOPMENT.md)
2. Review the [Deployment Guide](../deployment/DEPLOYMENT_GUIDE.md)
3. Configure [Azure Architecture](../architecture/AZURE_ARCHITECTURE.md)