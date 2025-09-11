# Infrastructure Configuration Validation

## Overview

This document describes the infrastructure validation system that prevents deployment failures due to configuration mismatches between Bicep templates and application code.

## Problem Solved

Previously, deployment failures occurred when environment variable names didn't match between:
- **Infrastructure (Bicep templates)**: What environment variables are provided to containers
- **Application (C# code)**: What environment variables the application expects

**Example of the issue we prevent:**
- Bicep template defines: `AzureStorage__ConnectionString`
- Application expects: `AZURE_STORAGE_CONNECTION_STRING`
- Result: 500 errors because app can't find the required configuration

## Solution Components

### 1. GitHub Actions Validation Step

The validation is integrated into `.github/workflows/simple-deploy.yml` as a pre-deployment step:

```yaml
- name: 🔍 Validate Infrastructure Configuration
```

This step:
1. Extracts environment variables from Bicep templates
2. Extracts expected variables from `EnvironmentConfiguration.cs`
3. Cross-references critical variables
4. Fails deployment if mismatches are detected

### 2. Local Validation Script

`scripts/validate-infrastructure-config.sh` provides the same validation locally:

```bash
# Basic validation
./scripts/validate-infrastructure-config.sh

# Verbose output for debugging
./scripts/validate-infrastructure-config.sh --verbose
```

### 3. Test Suite

`scripts/test-infrastructure-validation.sh` verifies the validation system works:

```bash
./scripts/test-infrastructure-validation.sh
```

## How It Works

### Variable Extraction

**From Bicep Templates:**
```bash
# Direct environment variables (UPPER_CASE)
BICEP_ENV_VARS=$(grep -E "^\s*name:\s*'[A-Z_]+'" infrastructure/simple-deploy.bicep)

# ASP.NET Core configuration patterns (Section__Key)
BICEP_CONFIG_VARS=$(grep -E "^\s*name:\s*'[A-Za-z]+__[A-Za-z]+'" infrastructure/simple-deploy.bicep)
```

**From Application Code:**
```bash
# Constants from EnvironmentConfiguration.cs
APP_ENV_VARS=$(grep -E "^\s*public const string [A-Z_]+ = \"[A-Z_]+\";" AI.ProfilePhotoMaker.API/Configuration/EnvironmentConfiguration.cs)
```

### Smart Mapping

The system understands ASP.NET Core configuration patterns:

| Application Expects | Infrastructure Provides | Status |
|-------------------|----------------------|--------|
| `JWT_SECRET` | `Jwt__Secret` | ✅ Valid mapping |
| `REPLICATE_API_TOKEN` | `Replicate__ApiToken` | ✅ Valid mapping |
| `AZURE_STORAGE_CONNECTION_STRING` | `AZURE_STORAGE_CONNECTION_STRING` | ✅ Direct match |
| `MSSQL_SA_PASSWORD` | `ConnectionStrings__DefaultConnection` | ✅ Connection string pattern |

### Critical Variables Checked

The validation focuses on these critical environment variables:

```bash
CRITICAL_VARS=(
    "AZURE_STORAGE_CONNECTION_STRING"    # Required in production
    "AZURE_STORAGE_CONTAINER_NAME"       # Required in production
    "JWT_SECRET"                         # Security critical
    "REPLICATE_API_TOKEN"                # Service integration
    "REPLICATE_WEBHOOK_SECRET"           # Security critical
    "GOOGLE_CLIENT_ID"                   # OAuth integration
    "GOOGLE_CLIENT_SECRET"               # OAuth integration
    "MSSQL_SA_PASSWORD"                  # Database access
    "ASPNETCORE_ENVIRONMENT"             # Runtime behavior
)
```

## Validation Output

### Success Example
```
✅ Environment variables defined in Bicep template:
  • AZURE_STORAGE_CONNECTION_STRING
  • AZURE_STORAGE_CONTAINER_NAME
  • GOOGLE_CLIENT_ID

✅ ASP.NET Core configuration variables in Bicep template:
  • ConnectionStrings__DefaultConnection
  • Jwt__Secret
  • Replicate__ApiToken

🔍 Cross-referencing critical environment variables...
  Checking AZURE_STORAGE_CONNECTION_STRING: ✅ MATCH (Direct environment variable)
  Checking JWT_SECRET: ✅ MATCH (Via config:Jwt__Secret)

✅ Infrastructure configuration validation PASSED
```

### Failure Example
```
❌ Found 1 critical error(s)
❌ Infrastructure and application environment variable configuration MISMATCH detected

🛠️ To resolve these issues:
  1. Check infrastructure/simple-deploy.bicep for environment variable names
  2. Compare against constants in AI.ProfilePhotoMaker.API/Configuration/EnvironmentConfiguration.cs
  3. Ensure exact name matching (case-sensitive)
  4. Common mistake: AzureStorage__ConnectionString vs AZURE_STORAGE_CONNECTION_STRING

🚫 DEPLOYMENT BLOCKED - Fix environment variable mismatches before deploying
```

## Integration Points

### Pre-Deployment Gate

The validation runs **before** actual deployment:

```yaml
jobs:
  validate-secrets:     # Step 1: Validate GitHub secrets
  deploy:              # Step 2: Deploy infrastructure
    needs: validate-secrets
    steps:
      - name: 🔍 Validate Infrastructure Configuration  # NEW: Pre-deployment validation
      - name: 🏗️ Deploy Infrastructure                   # Existing deployment
```

### Error Prevention

**Without validation:**
1. Deploy infrastructure with mismatched variable names
2. Application fails to start (500 errors)
3. Debug production issues
4. Redeploy with fixes

**With validation:**
1. Validation catches mismatch before deployment
2. Fix configuration locally
3. Deploy with correct configuration
4. Application starts successfully

## Configuration Patterns Supported

### Direct Environment Variables
```bicep
env: [
  {
    name: 'AZURE_STORAGE_CONNECTION_STRING'
    value: '...'
  }
]
```

### ASP.NET Core Configuration Pattern
```bicep
env: [
  {
    name: 'Jwt__Secret'
    secretRef: 'jwt-secret'
  }
]
```

### Connection String Pattern
```bicep
env: [
  {
    name: 'ConnectionStrings__DefaultConnection'
    secretRef: 'connection-string'
  }
]
```

## Maintenance

### Adding New Variables

When adding new environment variables:

1. **Add to EnvironmentConfiguration.cs:**
   ```csharp
   public const string NEW_VARIABLE = "NEW_VARIABLE";
   ```

2. **Add to Bicep template:**
   ```bicep
   {
     name: 'NEW_VARIABLE'
     value: '...'
   }
   ```

3. **Test validation:**
   ```bash
   ./scripts/validate-infrastructure-config.sh
   ```

### Updating Validation Logic

If new configuration patterns are introduced, update:

1. `scripts/validate-infrastructure-config.sh` - Local validation
2. `.github/workflows/simple-deploy.yml` - CI/CD validation
3. `scripts/test-infrastructure-validation.sh` - Test coverage

## Benefits

1. **Prevents Production Failures**: Catches mismatches before deployment
2. **Fast Feedback**: Validation runs in ~10 seconds vs hours of debugging
3. **Actionable Errors**: Clear instructions on how to fix mismatches
4. **Automated Protection**: No manual verification required
5. **Development Safety**: Works locally and in CI/CD

## Related Files

- `.github/workflows/simple-deploy.yml` - CI/CD integration
- `scripts/validate-infrastructure-config.sh` - Local validation
- `scripts/test-infrastructure-validation.sh` - Test suite
- `AI.ProfilePhotoMaker.API/Configuration/EnvironmentConfiguration.cs` - Application requirements
- `infrastructure/simple-deploy.bicep` - Infrastructure definition

## Future Enhancements

1. **JSON Schema Validation**: Generate schemas from EnvironmentConfiguration.cs
2. **IDE Integration**: VS Code extension for real-time validation
3. **Terraform Support**: Extend validation to Terraform templates
4. **Environment-Specific Validation**: Different rules for dev/staging/prod
5. **Auto-Fix Suggestions**: Generate corrected Bicep templates

---

## Appendix: Infrastructure Validation Implementation Summary (relocated)

The following implementation summary was previously in the repository root as `INFRASTRUCTURE_VALIDATION_SUMMARY.md` and is preserved here for completeness.

### Completed Implementation

1) Enhanced GitHub Actions Workflow (`.github/workflows/simple-deploy.yml`):
- Adds a pre-deployment step to validate infra configuration
- Extracts variables from Bicep templates and compares with `EnvironmentConfiguration.cs`
- Fails deployment with actionable messages on mismatch

2) Local Validation Script (`scripts/validate-infrastructure-config.sh`):
- Validates environment variables and ASP.NET Core config patterns
- Verbose mode and color-coded output

3) Test Suite (`scripts/test-infrastructure-validation.sh`):
- Positive and negative tests with auto-restore

4) Documentation (`docs/infrastructure-validation.md`):
- Problem description, implementation details, and roadmap

### Protection Against Configuration Mismatches

Key validations include:
- `AZURE_STORAGE_CONNECTION_STRING` matches between app and infra
- `JWT_SECRET` ↔ `Jwt__Secret` mapping
- `REPLICATE_API_TOKEN` ↔ `Replicate__ApiToken` mapping
- Database config via `ConnectionStrings__DefaultConnection`

### Deployment Safety

- Pre-deployment validation blocks mismatched deployments
- Intelligent pattern matching: direct env vars, config patterns, connection strings

### Usage Examples

```bash
./scripts/validate-infrastructure-config.sh        # standard
./scripts/validate-infrastructure-config.sh --verbose
./scripts/test-infrastructure-validation.sh        # test suite
```
