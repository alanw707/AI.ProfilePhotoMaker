# Environment Variables Reference

This document provides a comprehensive mapping of all environment variables used by the AI Profile Photo Maker application, preventing configuration mismatches between infrastructure and application code.

## Table of Contents

- [Quick Reference](#quick-reference)
- [Infrastructure vs Application Mapping](#infrastructure-vs-application-mapping)
- [Required Variables by Environment](#required-variables-by-environment)
- [Configuration Sources](#configuration-sources)
- [Common Naming Patterns](#common-naming-patterns)
- [Troubleshooting Guide](#troubleshooting-guide)
- [Validation Commands](#validation-commands)
- [Examples](#examples)

## Quick Reference

| Category | Infrastructure Name | Application Constant | Environment Type | Notes |
|----------|---------------------|---------------------|------------------|-------|
| Database | `MSSQL_SA_PASSWORD` | `MSSQL_SA_PASSWORD` | All | Required |
| Database | `ConnectionStrings__DefaultConnection` | `ConnectionStrings:DefaultConnection` | Production | Alternative to SA password |
| JWT | `JWT_SECRET` | `JWT_SECRET` | All | Min 32 chars |
| AI/ML | `REPLICATE_API_TOKEN` | `REPLICATE_API_TOKEN` | All | Starts with 'r8_' |
| AI/ML | `REPLICATE_WEBHOOK_SECRET` | `REPLICATE_WEBHOOK_SECRET` | All | Required |
| Storage | `AZURE_STORAGE_CONNECTION_STRING` | `AZURE_STORAGE_CONNECTION_STRING` | Production | **CRITICAL** |
| Storage | `AZURE_STORAGE_CONTAINER_NAME` | `AZURE_STORAGE_CONTAINER_NAME` | Production | **CRITICAL** |
| OAuth | `GOOGLE_CLIENT_ID` | `GOOGLE_CLIENT_ID` | All | Required for auth |
| OAuth | `GOOGLE_CLIENT_SECRET` | `GOOGLE_CLIENT_SECRET` | All | Required for auth |
| Payment | `STRIPE_SECRET_KEY` | `STRIPE_SECRET_KEY` | All | Required for payments |
| Payment | `STRIPE_PUBLISHABLE_KEY` | `STRIPE_PUBLISHABLE_KEY` | All | Required for payments |
| Payment | `STRIPE_WEBHOOK_SECRET` | `STRIPE_WEBHOOK_SECRET` | All | Required for payments |

## Infrastructure vs Application Mapping

### Critical Storage Configuration Issue

**The exact type of configuration mismatch we experienced:**

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
  name: 'AZURE_STORAGE_CONNECTION_STRING'  // ✅ Matches application constant
  value: '...'
}
```

```csharp
// Application Code (C#)
public const string AZURE_STORAGE_CONNECTION_STRING = "AZURE_STORAGE_CONNECTION_STRING";
var connectionString = GetEnvironmentVariable(AZURE_STORAGE_CONNECTION_STRING);
```

### Complete Infrastructure Mapping

| Application Constant | Infrastructure Variable | Bicep Template Key |
|----------------------|------------------------|-------------------|
| `MSSQL_SA_PASSWORD` | `MSSQL_SA_PASSWORD` | `MSSQL_SA_PASSWORD` |
| `JWT_SECRET` | `JWT_SECRET` | `JWT_SECRET` |
| `REPLICATE_API_TOKEN` | `REPLICATE_API_TOKEN` | `REPLICATE_API_TOKEN` |
| `REPLICATE_WEBHOOK_SECRET` | `REPLICATE_WEBHOOK_SECRET` | `REPLICATE_WEBHOOK_SECRET` |
| `AZURE_STORAGE_CONNECTION_STRING` | `AZURE_STORAGE_CONNECTION_STRING` | `AZURE_STORAGE_CONNECTION_STRING` |
| `AZURE_STORAGE_CONTAINER_NAME` | `AZURE_STORAGE_CONTAINER_NAME` | `AZURE_STORAGE_CONTAINER_NAME` |
| `GOOGLE_CLIENT_ID` | `GOOGLE_CLIENT_ID` | `GOOGLE_CLIENT_ID` |
| `GOOGLE_CLIENT_SECRET` | `GOOGLE_CLIENT_SECRET` | `GOOGLE_CLIENT_SECRET` |
| `STRIPE_SECRET_KEY` | `STRIPE_SECRET_KEY` | `STRIPE_SECRET_KEY` |
| `ASPNETCORE_ENVIRONMENT` | `ASPNETCORE_ENVIRONMENT` | `ASPNETCORE_ENVIRONMENT` |

### ASP.NET Core Configuration Alternatives

Some variables support both environment variable and configuration key formats:

| Environment Variable | Configuration Key | Example |
|---------------------|------------------|---------|
| `JWT_SECRET` | `JWT:Secret` or `Jwt:Secret` | Both work |
| `AZURE_STORAGE_CONNECTION_STRING` | `AzureStorage:ConnectionString` | Env var preferred |
| - | `ConnectionStrings:DefaultConnection` | Database alternative |
| - | `ConnectionStrings:AzureStorage` | Storage alternative |

## Required Variables by Environment

### Development Environment

**Required:**
- `JWT_SECRET` (minimum 32 characters)
- `REPLICATE_API_TOKEN` (starts with 'r8_')
- `REPLICATE_WEBHOOK_SECRET`
- `MSSQL_SA_PASSWORD` (8+ chars with complexity) OR `ConnectionStrings__DefaultConnection`
- `GOOGLE_CLIENT_ID` / `GOOGLE_CLIENT_SECRET` (required for authentication)
- `STRIPE_SECRET_KEY` / `STRIPE_PUBLISHABLE_KEY` / `STRIPE_WEBHOOK_SECRET` (required for payments)
- `AZURE_STORAGE_CONNECTION_STRING` (can use `UseDevelopmentStorage=true` in development only)
- `AZURE_STORAGE_CONTAINER_NAME`

### Production/Staging Environments

**Required (All development requirements PLUS):**
- `AZURE_STORAGE_CONNECTION_STRING` (**CRITICAL**: Real Azure Storage, not development storage)
- `AZURE_STORAGE_CONTAINER_NAME` (**CRITICAL**: Container name for blob storage)

**Why Azure Storage is Critical in Production:**
- Containerized environments cannot access local `/uploads` paths
- Missing Azure Storage causes 500 errors on file operations
- `UseDevelopmentStorage=true` cannot be used in production deployments

### Environment-Specific Validation

```csharp
// From EnvironmentConfiguration.cs - Environment-aware validation
if (_environment.IsProduction() || _environment.IsStaging())
{
    if (string.IsNullOrEmpty(azureStorage))
    {
        results.Add(new ValidationResult(false, AZURE_STORAGE_CONNECTION_STRING,
            $"Azure Storage connection string is REQUIRED in {_environment.EnvironmentName} environment. Local storage is not supported in containerized deployments."));
    }
}
```

## Configuration Sources

### Source Priority (Highest to Lowest)

1. **Environment Variables** (set in container/process environment)
2. **User Secrets** (development only, `dotnet user-secrets`)
3. **appsettings.{Environment}.json** (environment-specific config)
4. **appsettings.json** (base configuration)
5. **.env files** (loaded by application startup)

### .env File Loading Order

The application loads environment files in this order:
1. `.env`
2. `.env.{environment}` (e.g., `.env.development`)
3. `.env.local`
4. `.env.{environment}.local`

### Configuration Resolution Examples

```csharp
// From EnvironmentConfiguration.cs
public string? GetEnvironmentVariable(string key)
{
    // First check actual environment variable
    var value = Environment.GetEnvironmentVariable(key);
    
    // If not found, check configuration (handles both appsettings and environment)
    if (string.IsNullOrEmpty(value))
    {
        value = _configuration[key];
    }

    return value;
}
```

## Common Naming Patterns

### ASP.NET Core Conventions

| Pattern | Example | Usage |
|---------|---------|-------|
| `Section:Key` | `JWT:Secret` | Configuration hierarchy |
| `Section__Key` | `ConnectionStrings__DefaultConnection` | Environment variable equivalent |
| `CONSTANT_CASE` | `AZURE_STORAGE_CONNECTION_STRING` | Direct environment variables |

### Infrastructure Naming

| Environment | Pattern | Example |
|-------------|---------|---------|
| Container Apps | `CONSTANT_CASE` | `AZURE_STORAGE_CONNECTION_STRING` |
| App Service | `Section__Key` or `CONSTANT_CASE` | Both supported |
| Docker | `CONSTANT_CASE` | `AZURE_STORAGE_CONNECTION_STRING` |
| Kubernetes | `CONSTANT_CASE` | `AZURE_STORAGE_CONNECTION_STRING` |

## Troubleshooting Guide

### Common Configuration Issues

#### 1. Azure Storage Configuration Mismatch

**Problem:** Application cannot access blob storage, 500 errors on file operations

**Root Cause:** Infrastructure uses incorrect environment variable names

**Solution:**
```bicep
// ❌ WRONG - Application won't find this
{
  name: 'AzureStorage__ConnectionString'
  value: '...'
}

// ✅ CORRECT - Matches application constant
{
  name: 'AZURE_STORAGE_CONNECTION_STRING'
  value: '...'
}
```

#### 2. JWT Secret Not Found

**Problem:** Authentication fails, JWT token validation errors

**Symptoms:**
```
Warning: JWT Secret is not configured or is not long enough.
```

**Solutions:**
```bash
# Option 1: Set environment variable
export JWT_SECRET="your-super-secure-jwt-secret-key-minimum-32-characters"

# Option 2: Set in configuration
dotnet user-secrets set "JWT:Secret" "your-super-secure-jwt-secret-key-minimum-32-characters"
```

#### 3. Replicate API Token Issues

**Problem:** AI model operations fail

**Symptoms:**
```
Replicate API token is required
Replicate API token should start with 'r8_'
```

**Solution:**
```bash
# Set proper Replicate token
export REPLICATE_API_TOKEN="r8_your_actual_replicate_token_here"
```

#### 4. Database Connection Issues

**Problem:** Cannot connect to database

**Solutions:**
```bash
# Development: Set SA password
export MSSQL_SA_PASSWORD="YourComplexP@ssw0rd2024!"

# Production: Set full connection string
export ConnectionStrings__DefaultConnection="Server=tcp:server.database.windows.net,1433;Initial Catalog=mydb;User ID=admin;Password=password;Encrypt=True;"
```

#### 5. Google OAuth Configuration

**Problem:** OAuth login fails

**Common Issues:**
- Client ID contains help text or command output
- Client Secret is placeholder text
- Invalid format for Client ID (should end with `.apps.googleusercontent.com`)

**Validation from Code:**
```csharp
// Application validates Google Client ID format
if (!googleClientId.Contains(".apps.googleusercontent.com"))
{
    results.Add(new ValidationResult(false, GOOGLE_CLIENT_ID, 
        "Google Client ID format appears invalid. Expected format: 123456789-abc123.apps.googleusercontent.com"));
}
```

### Environment-Specific Issues

#### Development Environment

**Issue:** Azure Storage not working
**Solution:** Use development storage
```json
{
  "AzureStorage": {
    "ConnectionString": "UseDevelopmentStorage=true",
    "ContainerName": "profile-images"
  }
}
```

#### Production Environment

**Issue:** Local storage being used instead of Azure Storage
**Solution:** Ensure environment variables are set correctly
```bash
# Required in production
AZURE_STORAGE_CONNECTION_STRING="DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net"
AZURE_STORAGE_CONTAINER_NAME="profile-images"
```

## Validation Commands

### Pre-Deployment Validation

```bash
# Validate secrets before deployment
./scripts/validate-secrets.sh Production

# Development validation
./scripts/validate-secrets.sh Development
```

### Application Startup Validation

The application automatically validates environment variables on startup:

```csharp
// From Program.cs
await app.UseEnvironmentValidationAsync();
```

### Manual Testing Commands

```bash
# Test environment variable resolution
dotnet run --check-env-vars

# Test database connection
dotnet run --check-db-connection

# Test Replicate configuration
dotnet run --validate-replicate
```

### Configuration Debugging

```bash
# Check which configuration values are loaded
export ASPNETCORE_ENVIRONMENT=Development
dotnet run --debug-config

# View effective configuration
curl http://localhost:5032/api/config/debug
```

## Examples

### Complete .env File (Development)

```bash
# Required
MSSQL_SA_PASSWORD=YourComplexP@ssw0rd2024!
JWT_SECRET=YourSuperSecretJWTKeyAtLeast32CharactersLongGenerateWithOpenSSL
REPLICATE_API_TOKEN=r8_your_actual_replicate_token_here
REPLICATE_WEBHOOK_SECRET=whsec_wUh0bvV+/jGsxbEqPsRgHI1tdct1Y+KM

# Required for authentication and payments
AZURE_STORAGE_CONNECTION_STRING=UseDevelopmentStorage=true
AZURE_STORAGE_CONTAINER_NAME=profile-images
GOOGLE_CLIENT_ID=your_google_oauth_client_id.apps.googleusercontent.com
GOOGLE_CLIENT_SECRET=GOCSPX-your_google_oauth_client_secret
STRIPE_SECRET_KEY=sk_test_your_stripe_secret_key
STRIPE_PUBLISHABLE_KEY=pk_test_your_stripe_publishable_key
STRIPE_WEBHOOK_SECRET=whsec_your_stripe_webhook_secret
```

### Bicep Template Configuration (Production)

```bicep
// Complete environment variable configuration
env: [
  {
    name: 'ASPNETCORE_ENVIRONMENT'
    value: 'Production'
  }
  {
    name: 'ConnectionStrings__DefaultConnection'
    secretRef: 'connection-string'
  }
  {
    name: 'JWT_SECRET'
    secretRef: 'jwt-secret'
  }
  {
    name: 'REPLICATE_API_TOKEN'
    secretRef: 'replicate-token'
  }
  {
    name: 'REPLICATE_WEBHOOK_SECRET'
    secretRef: 'replicate-webhook-secret'
  }
  {
    name: 'AZURE_STORAGE_CONNECTION_STRING'  // ✅ CORRECT NAMING
    value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
  }
  {
    name: 'AZURE_STORAGE_CONTAINER_NAME'
    value: 'profile-images'
  }
  {
    name: 'GOOGLE_CLIENT_ID'
    secretRef: 'google-client-id'
  }
  {
    name: 'GOOGLE_CLIENT_SECRET'
    secretRef: 'google-client-secret'
  }
]
```

### Docker Compose Configuration

```yaml
services:
  api:
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - MSSQL_SA_PASSWORD=YourComplexP@ssw0rd2024!
      - JWT_SECRET=YourSuperSecretJWTKeyAtLeast32CharactersLongGenerateWithOpenSSL
      - REPLICATE_API_TOKEN=r8_your_actual_replicate_token_here
      - REPLICATE_WEBHOOK_SECRET=whsec_wUh0bvV+/jGsxbEqPsRgHI1tdct1Y+KM
      - AZURE_STORAGE_CONNECTION_STRING=UseDevelopmentStorage=true
      - AZURE_STORAGE_CONTAINER_NAME=profile-images
```

### User Secrets Configuration (Development)

```bash
# Set sensitive values using user secrets
dotnet user-secrets set "JWT:Secret" "YourSuperSecretJWTKeyAtLeast32CharactersLongGenerateWithOpenSSL"
dotnet user-secrets set "Replicate:ApiToken" "r8_your_actual_replicate_token_here"
dotnet user-secrets set "Replicate:WebhookSecret" "whsec_wUh0bvV+/jGsxbEqPsRgHI1tdct1Y+KM"
dotnet user-secrets set "GOOGLE_CLIENT_ID" "your_google_oauth_client_id.apps.googleusercontent.com"
dotnet user-secrets set "GOOGLE_CLIENT_SECRET" "GOCSPX-your_google_oauth_client_secret"
```

## Common Misconfiguration Patterns to Avoid

### 1. Naming Convention Mismatches

❌ **Avoid these common mistakes:**
```bicep
// Wrong: Double underscore for direct env vars
'AzureStorage__ConnectionString'

// Wrong: Inconsistent casing
'azure_storage_connection_string'

// Wrong: Configuration key format in environment
'AzureStorage:ConnectionString'
```

✅ **Use these correct patterns:**
```bicep
// Correct: Direct environment variable
'AZURE_STORAGE_CONNECTION_STRING'

// Correct: ASP.NET Core configuration equivalent
'ConnectionStrings__DefaultConnection'
```

### 2. Environment-Specific Requirements

❌ **Don't use in production:**
```
AZURE_STORAGE_CONNECTION_STRING=UseDevelopmentStorage=true
```

✅ **Production requires real Azure Storage:**
```
AZURE_STORAGE_CONNECTION_STRING=DefaultEndpointsProtocol=https;AccountName=prod;AccountKey=key;EndpointSuffix=core.windows.net
```

### 3. Placeholder Values

❌ **Don't deploy with placeholders:**
```
REPLICATE_API_TOKEN=REPLACE_WITH_PRODUCTION_TOKEN
GOOGLE_CLIENT_ID=YOUR_GOOGLE_CLIENT_ID
```

✅ **Always use real values:**
```
REPLICATE_API_TOKEN=r8_actual_token_here
GOOGLE_CLIENT_ID=123456789-abc123.apps.googleusercontent.com
```

## Security Best Practices

1. **Never commit .env files** with real secrets to version control
2. **Use Azure Key Vault** for production secrets
3. **Rotate secrets regularly** (every 90 days for production)
4. **Enable environment variable validation** on startup
5. **Use strong, unique passwords** (minimum 16 characters)
6. **Monitor secret usage** and access patterns

## Related Documentation

- [Environment Setup Guide](ENVIRONMENT_SETUP.md)
- [Unified Secrets Management](unified-secrets-management.md)
- [Deployment Checklist](../DEPLOYMENT_CHECKLIST.md)
- [Infrastructure Validation](infrastructure-validation.md)

---

**Always run `./scripts/validate-secrets.sh` before deployment to prevent configuration mismatches!**