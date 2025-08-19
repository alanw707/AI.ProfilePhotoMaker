# Unified Secrets Management & OAuth Deployment Continuity

This document provides comprehensive guidance for managing secrets across all stores and ensuring OAuth deployment continuity.

## Overview

Our unified secrets management strategy ensures that:
- All secrets are synchronized across dotnet user-secrets, GitHub Actions, and Azure KeyVault
- OAuth functionality is preserved during deployments
- Security best practices are followed throughout
- Validation ensures consistency and correctness

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Secrets Inventory](#secrets-inventory)
3. [Synchronization Process](#synchronization-process)
4. [Validation Framework](#validation-framework)
5. [Deployment Continuity](#deployment-continuity)
6. [Security Best Practices](#security-best-practices)
7. [Troubleshooting](#troubleshooting)
8. [Emergency Recovery](#emergency-recovery)

## Architecture Overview

### Secret Stores Hierarchy

```
dotnet user-secrets (Source of Truth)
    ↓
GitHub Actions Secrets (CI/CD)
    ↓
Azure Container Apps (Production)
```

### Key Components

- **dotnet user-secrets**: Local development and source of truth
- **GitHub Actions**: CI/CD pipeline secrets
- **Azure Container Apps**: Production runtime secrets
- **Azure KeyVault**: Future enhancement for enterprise security

## Secrets Inventory

### Required Secrets (All Environments)

These secrets use the SAME values across development and production:

| Secret | Purpose | Format | Store |
|--------|---------|--------|-------|
| `GOOGLE_CLIENT_ID` | OAuth authentication | `123456-abc.apps.googleusercontent.com` | All |
| `GOOGLE_CLIENT_SECRET` | OAuth authentication | `GOCSPX-...` | All |
| `REPLICATE_API_TOKEN` | AI model API access | `r8_...` | All |
| `REPLICATE_WEBHOOK_SECRET` | Webhook validation | `32+ chars` | All |

### Environment-Specific Secrets

These secrets have different values per environment:

| Secret | Development | Production |
|--------|-------------|------------|
| `JWT_SECRET` | Dev token (32+ chars) | Production token |
| `ConnectionStrings:DefaultConnection` | Local SQL Server | Azure SQL |
| `AzureStorage:ConnectionString` | Local/Dev storage | Production storage |

### GitHub Actions Only

These secrets are only used in CI/CD:

- `AZURE_CLIENT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `AZURE_TENANT_ID`
- `SQL_ADMIN_PASSWORD`

## Synchronization Process

### 1. Prerequisites

```bash
# Ensure you're in the project root
cd AI.ProfilePhotoMaker

# Verify dotnet user-secrets is configured
dotnet user-secrets list --project AI.ProfilePhotoMaker.API

# Verify GitHub CLI is authenticated
gh auth status
```

### 2. Add Missing Secrets to dotnet user-secrets

The Replicate secrets are currently missing from dotnet user-secrets. Use the secure synchronization script:

```bash
# Execute secure synchronization
./ClaudeDocs/Analysis/Security/secure-replicate-sync.sh
```

This script will:
- Securely prompt for the Replicate API token and webhook secret
- Validate secret formats (API token starts with `r8_`, webhook secret 32+ chars)
- Store them in dotnet user-secrets
- Provide audit trail

### 3. Synchronization Script Usage

Create a comprehensive sync script (future enhancement):

```bash
# scripts/sync-all-secrets.sh
# Pull from dotnet user-secrets and sync to GitHub Actions
# Validate consistency across all stores
# Report any discrepancies
```

### 4. Current Manual Process

Until automated sync is available:

```bash
# Get values from dotnet user-secrets
dotnet user-secrets list --project AI.ProfilePhotoMaker.API

# Update GitHub Actions secrets as needed
gh secret set GOOGLE_CLIENT_ID --body "value_from_user_secrets"
gh secret set GOOGLE_CLIENT_SECRET --body "value_from_user_secrets"
# etc.
```

## Validation Framework

### Comprehensive Validation

Run the validation script before any deployment:

```bash
./scripts/validate-secrets.sh
```

This validates:
- All required secrets are present in dotnet user-secrets
- All required secrets are present in GitHub Actions
- Secret formats are correct (JWT length, Replicate token format, etc.)
- Infrastructure configuration includes all required parameters
- Application configuration supports all secrets

### Validation Results

- **✅ Success**: All validations passed, ready for deployment
- **⚠️ Warnings**: Non-critical issues, deployment can proceed
- **❌ Errors**: Critical issues, must be fixed before deployment

### Pre-Deployment Checklist

- [ ] All secrets present in dotnet user-secrets
- [ ] GitHub Actions secrets synchronized
- [ ] Secret formats validated
- [ ] Infrastructure configuration updated
- [ ] Validation script passes without errors

## Deployment Continuity

### Infrastructure Updates

The deployment infrastructure has been updated to support all secrets:

#### Bicep Template (`infrastructure/simple-deploy.bicep`)

- ✅ Added `replicateWebhookSecret` parameter
- ✅ Added `Replicate__WebhookSecret` environment variable
- ✅ All OAuth secrets properly configured

#### GitHub Actions Workflow (`.github/workflows/simple-deploy.yml`)

- ✅ Passes all required secrets to deployment
- ✅ Includes validation step with all parameters
- ✅ Includes `REPLICATE_WEBHOOK_SECRET` in deployment

### OAuth Enhancements

#### Enhanced Logging

The `ResolveBackendBaseUrl()` method now includes comprehensive logging:

```csharp
// Logs all configuration sources and resolution steps
// Helps troubleshoot OAuth redirect issues
// Provides clear visibility into URL resolution logic
```

#### Configuration Sources

OAuth base URL is resolved in this order:
1. Azure production configuration (`Authentication:OAuth:BaseUrl`)
2. Forwarded headers (`X-Forwarded-Proto` + `X-Forwarded-Host`)
3. Environment override (`OAUTH_BASE_URL`)
4. Local development detection
5. Configured OAuth base URL
6. Fallback to current request

## Security Best Practices

### Secret Handling

1. **Never commit secrets to version control**
2. **Use secure synchronization scripts**
3. **Validate secret formats before storage**
4. **Rotate secrets regularly (every 90 days)**
5. **Audit secret access and usage**

### Production Security

1. **Use Azure KeyVault for production secrets** (future)
2. **Enable audit logging for all secret access**
3. **Implement least-privilege access**
4. **Monitor for unauthorized secret access**
5. **Use managed identities where possible**

### Development Security

1. **Use dotnet user-secrets for local development**
2. **Never put real secrets in .env files**
3. **Use placeholder values in committed files**
4. **Validate development environment setup**

## Troubleshooting

### Common Issues

#### 1. OAuth Token Exchange Failed

**Symptoms**: `token_exchange_failed` error in production

**Diagnosis**:
```bash
# Check OAuth configuration
./scripts/validate-secrets.sh

# Verify Container App environment variables
az containerapp show --name aipm-api-v1 --resource-group aiprofilemaker-v1 --query properties.configuration.secrets
```

**Solutions**:
- Ensure Google OAuth secrets are correctly set
- Verify redirect URI configuration matches Google Console
- Check enhanced logging output for URL resolution issues

#### 2. Missing Secrets in Deployment

**Symptoms**: Application startup failures, configuration errors

**Diagnosis**:
```bash
# Run comprehensive validation
./scripts/validate-secrets.sh

# Check deployment parameters
grep -A 20 "deployment group create" .github/workflows/simple-deploy.yml
```

**Solutions**:
- Run secrets synchronization
- Update infrastructure parameters
- Redeploy with complete secret set

#### 3. Secret Format Errors

**Symptoms**: Validation failures, authentication errors

**Diagnosis**:
```bash
# Check secret formats
dotnet user-secrets list --project AI.ProfilePhotoMaker.API | grep -E "(JWT|REPLICATE|GOOGLE)"
```

**Solutions**:
- JWT Secret: Must be 32+ characters
- Replicate API Token: Must start with `r8_`
- Google Client Secret: Must start with `GOCSPX-`

### Advanced Debugging

#### OAuth Flow Debugging

1. **Enable enhanced logging**: Already implemented in `ResolveBackendBaseUrl()`
2. **Check Container App logs**:
   ```bash
   az containerapp logs show --name aipm-api-v1 --resource-group aiprofilemaker-v1
   ```
3. **Verify redirect URIs**:
   - Production: `https://api.aiprofilephotomaker.com/api/auth/external-login-callback`
   - Development: `http://localhost:5032/api/auth/external-login-callback`

#### Infrastructure Debugging

1. **Validate Bicep template**:
   ```bash
   az bicep build --file infrastructure/simple-deploy.bicep
   ```
2. **Test deployment validation**:
   ```bash
   az deployment group validate --resource-group aiprofilemaker-v1 --template-file infrastructure/simple-deploy.bicep --parameters @test-params.json
   ```

## Emergency Recovery

### Restore Points

Git restore point available:
```bash
# Switch to backup branch
git checkout backup/pre-unified-secrets-oauth-plan

# Or reset to specific commit
git reset --hard 4ee3f02
```

### Recovery Procedures

#### 1. OAuth Failure Recovery

If OAuth stops working after deployment:

```bash
# 1. Revert to known good state
git checkout backup/pre-unified-secrets-oauth-plan

# 2. Verify secrets are correct
./scripts/validate-secrets.sh

# 3. Redeploy with verified configuration
# (Use GitHub Actions or manual deployment)

# 4. Test OAuth flow
# (Use Playwright tests or manual testing)
```

#### 2. Secrets Corruption Recovery

If secrets become corrupted or lost:

```bash
# 1. Check what secrets are available
dotnet user-secrets list --project AI.ProfilePhotoMaker.API
gh secret list

# 2. Restore from backup sources
# - Azure KeyVault (if configured)
# - Secure password manager
# - Team lead or administrator

# 3. Re-synchronize all secrets
./ClaudeDocs/Analysis/Security/secure-replicate-sync.sh

# 4. Validate and redeploy
./scripts/validate-secrets.sh
```

#### 3. Complete System Recovery

For complete failure:

```bash
# 1. Clone fresh repository
git clone https://github.com/alanw707/AI.ProfilePhotoMaker.git

# 2. Restore secrets from secure sources
# Follow initial setup procedures

# 3. Validate complete configuration
./scripts/validate-secrets.sh

# 4. Deploy from scratch
# Follow deployment procedures
```

## Future Enhancements

### Planned Improvements

1. **Azure KeyVault Integration**
   - Store production secrets in KeyVault
   - Use managed identities for access
   - Implement automatic secret rotation

2. **Automated Secret Sync**
   - Script to sync from dotnet user-secrets to all stores
   - Scheduled validation and sync
   - Automated drift detection and alerts

3. **Enhanced Monitoring**
   - Secret access monitoring
   - OAuth flow success rate tracking
   - Automated health checks

4. **Security Hardening**
   - Secret encryption at rest
   - Additional audit logging
   - Compliance reporting

### Implementation Timeline

- **Phase 1 (Current)**: Manual sync and validation ✅
- **Phase 2 (Next)**: Azure KeyVault integration
- **Phase 3 (Future)**: Full automation and monitoring

## Conclusion

This unified secrets management approach provides:

- **Consistency**: All secrets synchronized across environments
- **Security**: Best practices and secure handling throughout
- **Reliability**: OAuth deployment continuity guaranteed
- **Maintainability**: Clear processes and comprehensive documentation
- **Recovery**: Multiple restore points and recovery procedures

Follow this guide to ensure robust, secure, and reliable secrets management for the AI Profile Photo Maker application.

---

**Last Updated**: 2025-08-13  
**Version**: 1.0  
**Status**: Production Ready