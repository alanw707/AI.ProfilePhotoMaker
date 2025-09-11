# Secret Management Workflow

This document establishes the proper workflow for updating secrets to prevent authentication failures and secret synchronization issues.

## The Problem We Solved

**Issue**: VS Code Azure SQL Database connection failure due to password mismatch
- **Local Development**: Used `Database!2024#Secure9$`
- **GitHub Actions**: Used `AzureSQL1fstbDFu!@9` (updated Aug 15, 2025)
- **Azure Key Vault**: Contained `AzureSQL1fstbDFu!@9`

**Root Cause**: Secret stores became desynchronized when GitHub Actions secrets were updated but local development secrets weren't updated to match.

## Secret Store Hierarchy

### 🔑 **Source of Truth: Azure Key Vault**
- **Purpose**: Production secrets used by deployed infrastructure
- **Access**: Via Azure CLI (`az keyvault secret show`)
- **Usage**: Container Apps read directly from Key Vault secrets

### 🏠 **Local Development: dotnet user-secrets**
- **Purpose**: Development environment secrets
- **Access**: Via `dotnet user-secrets` commands
- **Storage**: Local encrypted store per project

### 🚀 **CI/CD Pipeline: GitHub Actions Secrets**
- **Purpose**: Build and deployment secrets
- **Access**: Via GitHub CLI (`gh secret set`)
- **Usage**: GitHub Actions runners during deployment

## ✅ **Proper Secret Update Workflow**

### **Option 1: Standard Update (Recommended)**

```bash
# Step 1: Use the synchronization script
./scripts/sync-secrets.sh

# Step 2: Verify consistency
./scripts/sync-secrets.sh --validate-only

# Step 3: Test local development
dotnet run  # Test API startup
# Test VS Code SQL connection

# Step 4: Test CI/CD pipeline
git push  # Triggers deployment with validation
```

### **Option 2: Manual Update (Advanced)**

```bash
# Step 1: Update Azure Key Vault (if needed)
az keyvault secret set --vault-name "aipm-kv-v1-6j74jubocuukg" --name "JwtSecret" --value "new-secret-value"

# Step 2: Sync from Azure to other stores
./scripts/sync-secrets.sh --source azure

# Step 3: Validate consistency
./scripts/sync-secrets.sh --validate-only
```

### **Option 3: Emergency Local Update**

```bash
# Step 1: Update local development immediately
cd AI.ProfilePhotoMaker.API
dotnet user-secrets set "MSSQL_SA_PASSWORD" "new-password"

# Step 2: Sync to other stores
./scripts/sync-secrets.sh --source local --secret SQL_ADMIN_PASSWORD

# Step 3: Validate
./scripts/sync-secrets.sh --validate-only
```

## 🛠️ **Secret Synchronization Script Usage**

### **Basic Commands**

```bash
# Sync all secrets from Azure Key Vault (default)
./scripts/sync-secrets.sh

# Sync specific secret only
./scripts/sync-secrets.sh --secret SQL_ADMIN_PASSWORD

# Dry run (show what would change)
./scripts/sync-secrets.sh --dry-run

# Validate consistency without changes
./scripts/sync-secrets.sh --validate-only
```

### **Advanced Options**

```bash
# Sync from local development to other stores
./scripts/sync-secrets.sh --source local

# Sync specific secret from specific source
./scripts/sync-secrets.sh --source azure --secret JWT_SECRET

# Help and usage information
./scripts/sync-secrets.sh --help
```

## 🔍 **Secret Mappings**

| Local Development | GitHub Actions | Azure Key Vault | Special Handling |
|-------------------|----------------|------------------|------------------|
| `MSSQL_SA_PASSWORD` | `SQL_ADMIN_PASSWORD` | `ConnectionString` | Extracted from connection string |
| `JWT_SECRET` | `JWT_SECRET` | `JwtSecret` | Direct mapping |
| `REPLICATE_API_TOKEN` | `REPLICATE_API_TOKEN` | `ReplicateApiToken` | Direct mapping |
| `REPLICATE_WEBHOOK_SECRET` | `REPLICATE_WEBHOOK_SECRET` | `ReplicateWebhookSecret` | Direct mapping |
| `GOOGLE_CLIENT_ID` | `GOOGLE_CLIENT_ID` | `GoogleClientId` | Direct mapping |
| `GOOGLE_CLIENT_SECRET` | `GOOGLE_CLIENT_SECRET` | `GoogleClientSecret` | Direct mapping |
| `STRIPE_SECRET_KEY` | `STRIPE_SECRET_KEY` | `StripeSecretKey` | Direct mapping |
| `STRIPE_PUBLISHABLE_KEY` | `STRIPE_PUBLISHABLE_KEY` | `StripePublishableKey` | Direct mapping |
| `STRIPE_WEBHOOK_SECRET` | `STRIPE_WEBHOOK_SECRET` | `StripeWebhookSecret` | Direct mapping |

## 🚨 **What NOT to Do**

### ❌ **Dangerous Practices**

```bash
# DON'T: Update secrets in isolation
dotnet user-secrets set "JWT_SECRET" "new-value"  # Only updates local

# DON'T: Manually update GitHub Actions without syncing
gh secret set JWT_SECRET "new-value"  # Creates inconsistency

# DON'T: Update Azure Key Vault without syncing
az keyvault secret set --name "JwtSecret" --value "new-value"  # Breaks dev
```

### ⚠️ **Warning Signs of Problems**

- VS Code database connection failures
- Local development works but deployments fail
- Authentication errors in different environments
- Secrets validation failures in GitHub Actions

## 🧪 **Testing After Secret Updates**

### **1. Local Development Testing**

```bash
# Test API startup
cd AI.ProfilePhotoMaker.API
dotnet run

# Verify endpoints
curl http://localhost:5032/api/health
curl http://localhost:5032/api/health/live
curl http://localhost:5032/api/health/ready
```

### **2. VS Code Database Testing**

1. Open VS Code Command Palette: `Ctrl+Shift+P`
2. Run: "MS SQL: Connect"
3. Select: "AI ProfilePhotoMaker - Production"
4. Test query:
   ```sql
   SELECT DB_NAME() as DatabaseName, SYSTEM_USER as CurrentUser;
   ```

### **3. CI/CD Pipeline Testing**

```bash
# Trigger deployment
git add .
git commit -m "Test secret synchronization"
git push

# Monitor GitHub Actions
gh run watch
```

## 📋 **Secret Rotation Schedule**

### **Regular Rotation (Recommended)**

| Secret Type | Rotation Frequency | Method |
|-------------|-------------------|---------|
| Database Passwords | Every 90 days | Azure Key Vault rotation |
| JWT Secrets | Every 180 days | Manual generation |
| API Tokens | As required by provider | Provider dashboard |
| OAuth Secrets | As required by provider | Provider dashboard |
| Webhook Secrets | Every 365 days | Manual generation |

### **Rotation Workflow**

```bash
# Step 1: Generate new secret value
NEW_SECRET=$(openssl rand -base64 32)

# Step 2: Update Azure Key Vault
az keyvault secret set --vault-name "aipm-kv-v1-6j74jubocuukg" --name "JwtSecret" --value "$NEW_SECRET"

# Step 3: Sync to all stores
./scripts/sync-secrets.sh --source azure --secret JWT_SECRET

# Step 4: Test and deploy
./scripts/sync-secrets.sh --validate-only
git push  # Deploy with new secrets
```

## 🔧 **Troubleshooting**

### **Secret Sync Script Issues**

```bash
# Check prerequisites
az account show  # Verify Azure login
gh auth status   # Verify GitHub login
dotnet --version # Verify .NET CLI

# Validate Azure access
az keyvault secret show --name "JwtSecret" --vault-name "aipm-kv-v1-6j74jubocuukg"

# Check GitHub repository access
gh repo view
```

### **VS Code Database Connection Issues**

1. **Verify password in VS Code matches Azure Key Vault**:
   ```bash
   # Get current password
   az keyvault secret show --name "ConnectionString" --vault-name "aipm-kv-v1-6j74jubocuukg" --query "value" -o tsv | cut -d';' -f4 | cut -d'=' -f2
   ```

2. **Update VS Code settings**:
   - Open `.vscode/settings.json`
   - Verify server name: `aipm-sql-v1-6j74jubocuukg.database.windows.net`
   - Use correct username: `sqladmin`
   - Enter correct password from step 1

3. **Test network connectivity**:
   ```bash
   # Check if your IP is whitelisted
   az sql server firewall-rule list --server aipm-sql-v1-6j74jubocuukg --resource-group aiprofilemaker-v1
   ```

## 🎯 **Best Practices**

1. **Always use the sync script** for secret updates
2. **Validate consistency** before and after changes
3. **Test in local development** before deployment
4. **Monitor GitHub Actions** for validation failures
5. **Document secret changes** in commit messages
6. **Use strong, unique passwords** for all secrets
7. **Rotate secrets regularly** according to the schedule
8. **Never commit secrets** to version control

## 📚 **Related Documentation**

- [Environment Variables Reference](ENVIRONMENT_VARIABLES.md)
- [Infrastructure Validation](../scripts/validate-infrastructure-config.sh)
- [Deployment Checklist](../deployment/DEPLOYMENT_CHECKLIST.md)
- [GitHub Actions Workflow](../.github/workflows/simple-deploy.yml)

---

**Remember**: When in doubt, run `./scripts/sync-secrets.sh --validate-only` to check consistency!
