# Secrets Validation System Update - ALL SECRETS NOW REQUIRED

## Overview
Updated the entire secrets validation system to make ALL configured secrets REQUIRED across all environments. The principle is: **If a secret is referenced in infrastructure templates, GitHub Actions workflows, or application code, it's REQUIRED for that environment to function properly.**

## Key Changes Made

### 1. EnvironmentConfiguration.cs - Application Level Validation
- **Made ALL secrets required** - removed "optional" designation
- **Added comprehensive Stripe validation** with format checking:
  - `STRIPE_SECRET_KEY` (must start with `sk_`)
  - `STRIPE_PUBLISHABLE_KEY` (must start with `pk_`)
  - `STRIPE_WEBHOOK_SECRET` (must start with `whsec_`)
- **Updated Azure Storage validation** - now required in ALL environments
- **Enhanced error messages** - clearly state why each secret is required

### 2. Infrastructure Templates (simple-deploy.bicep)
- **Added Stripe parameters** to Bicep template:
  - `stripeSecretKey`
  - `stripePublishableKey` 
  - `stripeWebhookSecret`
- **Added Stripe environment variables** to container configuration
- **Added Stripe Key Vault secrets** for secure storage
- **Updated container secrets** to include all Stripe configurations

### 3. GitHub Actions Workflow (simple-deploy.yml)
- **Added ALL missing secrets** to validation environment:
  - `STRIPE_SECRET_KEY`
  - `STRIPE_PUBLISHABLE_KEY`
  - `STRIPE_WEBHOOK_SECRET`
  - `AZURE_STORAGE_CONNECTION_STRING`
  - `AZURE_STORAGE_CONTAINER_NAME`
- **Added validation calls** for all new required secrets
- **Updated deployment parameters** to pass Stripe secrets to Bicep template
- **Enhanced validation comments** to clarify requirement rationale

### 4. Bash Validation Scripts

#### validate-secrets.sh (Comprehensive)
- **Updated required secrets arrays** to include ALL secrets
- **Added format validation** for all Stripe secrets
- **Made Azure Storage required** in ALL environments
- **Enhanced cross-validation** between infrastructure and application
- **Updated workflow parameter validation** to include all secrets
- **Improved error messages** explaining requirement rationale

#### validate-deployment.sh (Infrastructure)
- **Added ALL missing secrets** to GitHub and Key Vault validation
- **Updated secret lists** to include Stripe and Azure Storage secrets
- **Enhanced validation coverage** for complete infrastructure validation

### 5. PowerShell Validation Scripts

#### validate-secrets.ps1 (Secret Management)
- **Expanded expected secrets configuration** to include all required secrets
- **Added Key Vault name mapping** for all new secrets
- **Enhanced update logic** to handle validation-only secrets
- **Improved error reporting** with clear requirement explanations

## Validation Coverage

### Required Secrets (ALL REQUIRED)
1. **Core Authentication & Security**
   - `JWT_SECRET` (minimum 32 characters)
   - `GOOGLE_CLIENT_ID` (must end with `.apps.googleusercontent.com`)
   - `GOOGLE_CLIENT_SECRET` (must start with `GOCSPX-`)

2. **AI/ML Services**
   - `REPLICATE_API_TOKEN` (must start with `r8_`)
   - `REPLICATE_WEBHOOK_SECRET` (configured value)

3. **Payment Processing (NEW - NOW REQUIRED)**
   - `STRIPE_SECRET_KEY` (must start with `sk_`)
   - `STRIPE_PUBLISHABLE_KEY` (must start with `pk_`)
   - `STRIPE_WEBHOOK_SECRET` (must start with `whsec_`)

4. **Cloud Infrastructure**
   - `AZURE_STORAGE_CONNECTION_STRING` (Azure format required)
   - `AZURE_STORAGE_CONTAINER_NAME` (container name)
   - `SQL_ADMIN_PASSWORD` (Azure SQL complexity requirements)

5. **Azure DevOps**
   - `AZURE_CLIENT_ID`
   - `AZURE_SUBSCRIPTION_ID`
   - `AZURE_TENANT_ID`

### Validation Points
- **Application Startup** - EnvironmentConfiguration.cs validates all secrets
- **GitHub Actions** - Pre-deployment validation blocks on missing secrets
- **Infrastructure Deployment** - Bicep template requires all parameters
- **Key Vault** - All secrets stored securely in Azure Key Vault
- **Cross-Validation** - Infrastructure vs Application requirements validated

## Environment-Specific Behavior

### Previous Behavior (REMOVED)
- Azure Storage was "optional" in Development
- Stripe was marked as "optional" 
- Some secrets had warnings instead of errors

### New Behavior (ALL ENVIRONMENTS)
- **ALL secrets are REQUIRED** regardless of environment
- **Consistent validation** across Development, Staging, Production
- **Clear error messages** explaining infrastructure references
- **No warnings** - missing secrets are always errors
- **Validation blocks deployment** until all secrets are provided

## Breaking Changes

### For Developers
- **ALL secrets must now be configured** in user-secrets or environment variables
- **No "optional" secrets** - all configured secrets are required
- **Stripe configuration required** even for development (can use test keys)
- **Azure Storage required** in all environments (no development storage fallback)

### For Deployments
- **GitHub Actions requires ALL secrets** - deployment blocks without them
- **Infrastructure deployment requires ALL parameters** - Bicep template updated
- **Key Vault must contain ALL secrets** - validation checks all expected secrets

## Migration Guide

### For Local Development
1. **Configure ALL required secrets** in dotnet user-secrets:
   ```bash
   dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
   dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..."
   dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..."
   dotnet user-secrets set "AzureStorage:ContainerName" "profile-images"
   ```

2. **Ensure Azure Storage** is configured (not development storage):
   ```bash
   dotnet user-secrets set "AzureStorage:ConnectionString" "DefaultEndpointsProtocol=https;AccountName=..."
   ```

### For GitHub Actions
1. **Add ALL missing secrets** to repository secrets:
   - `STRIPE_SECRET_KEY`
   - `STRIPE_PUBLISHABLE_KEY`
   - `STRIPE_WEBHOOK_SECRET`
   - `AZURE_STORAGE_CONNECTION_STRING`
   - `AZURE_STORAGE_CONTAINER_NAME`

### For Production Deployment
1. **Verify ALL secrets** are configured in GitHub repository
2. **Run validation scripts** to ensure complete configuration:
   ```bash
   ./scripts/validate-secrets.sh Production
   ./infrastructure/validate-deployment.sh
   ```

## Validation Commands

### Comprehensive Validation
```bash
# All environments validation
./scripts/validate-secrets.sh Production

# Infrastructure validation
./infrastructure/validate-deployment.sh

# PowerShell validation (Windows/Azure Cloud Shell)
./infrastructure/validate-secrets.ps1 -Environment Production
```

### Quick Checks
```bash
# Check user secrets
dotnet user-secrets list --project AI.ProfilePhotoMaker.API

# Check GitHub secrets
gh secret list

# Validate application startup
dotnet run --project AI.ProfilePhotoMaker.API
```

## Error Resolution

### Common Issues
1. **Missing Stripe secrets** - Add test or production Stripe keys
2. **Azure Storage Development storage** - Must use real Azure Storage
3. **Google OAuth format errors** - Ensure proper OAuth client ID/secret format
4. **Replicate token format** - Must start with `r8_`

### Quick Fixes
```bash
# Set Stripe test keys for development
dotnet user-secrets set "Stripe:SecretKey" "sk_test_51..."
dotnet user-secrets set "Stripe:PublishableKey" "pk_test_51..."
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..."

# Set Azure Storage (replace with real connection string)
dotnet user-secrets set "AzureStorage:ConnectionString" "DefaultEndpointsProtocol=https;AccountName=youraccount;AccountKey=..."
dotnet user-secrets set "AzureStorage:ContainerName" "profile-images"
```

## Files Modified

### Application Code
- `AI.ProfilePhotoMaker.API/Configuration/EnvironmentConfiguration.cs`

### Infrastructure
- `infrastructure/simple-deploy.bicep`
- `.github/workflows/simple-deploy.yml`

### Validation Scripts
- `scripts/validate-secrets.sh`
- `infrastructure/validate-deployment.sh`
- `infrastructure/validate-secrets.ps1`

## Summary

The secrets validation system now enforces a **single, consistent principle**: **ALL secrets referenced in infrastructure, workflows, or application code are REQUIRED**. This eliminates the distinction between "optional" and "required" secrets, ensuring complete environment consistency and preventing configuration drift between environments.

All validation scripts, infrastructure templates, and application code now align with this principle, providing comprehensive coverage and clear error messages when any required secret is missing.