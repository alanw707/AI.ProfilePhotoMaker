# 🚀 Azure Production Deployment Guide

## ✅ Security Audit Complete

**Status**: All secrets are properly externalized and secure for deployment.

- **Configuration Files**: ✅ Use placeholder values only
- **GitHub Workflows**: ✅ Use GitHub Secrets (`${{ secrets.* }}`)
- **Parameter Files**: ✅ No hardcoded secrets
- **Environment Files**: ✅ No sensitive data in repository

## 📋 Pre-Deployment Checklist

### 1. Azure Prerequisites
- [ ] Azure subscription with appropriate permissions
- [ ] Resource Group created: `ai-profile-photo-maker-prod`
- [ ] Azure CLI installed and authenticated

### 2. GitHub Secrets Configuration
Configure these GitHub repository secrets:

```
AZUREAPPSERVICE_CLIENTID_C73973894C7140DEAF8637A42FA0C131
AZUREAPPSERVICE_TENANTID_011D6FB5A4BC43509D9B165F9842CEBC  
AZUREAPPSERVICE_SUBSCRIPTIONID_B9C8B148FA76469EB51C84A0DE3D63BB
AZURE_CREDENTIALS
```

### 3. Production Secrets (Azure Key Vault)
Replace these placeholders in `infrastructure/parameters.prod.json`:

- `REPLACE_WITH_PROD_SQL_PASSWORD` → Strong SQL password (16+ chars)
- `REPLACE_WITH_REPLICATE_TOKEN` → Replicate API token
- `REPLACE_WITH_PROD_JWT_SECRET` → 256-bit JWT secret
- `REPLACE_WITH_WEBHOOK_SECRET` → Webhook secret for Replicate

## 🏗️ Deployment Process

### Step 1: Infrastructure Deployment
```bash
# Validate parameters
az deployment group validate \
  --resource-group "ai-profile-photo-maker-prod" \
  --template-file "infrastructure/main.bicep" \
  --parameters "@infrastructure/parameters.prod.json"

# Deploy infrastructure
az deployment group create \
  --resource-group "ai-profile-photo-maker-prod" \
  --template-file "infrastructure/main.bicep" \
  --parameters "@infrastructure/parameters.prod.json" \
  --mode Incremental
```

### Step 2: Secret Configuration
```bash
# Set secrets in Azure Key Vault (created by infrastructure)
az keyvault secret set --vault-name "aiprofilephotomaker-kv-prod" \
  --name "SqlConnectionString" \
  --value "Server=aiprofilephotomaker-sql-prod.database.windows.net;Database=aiprofilephotomaker-db-prod;User Id=aiprofileadmin;Password=YOUR_SQL_PASSWORD;TrustServerCertificate=true;"

az keyvault secret set --vault-name "aiprofilephotomaker-kv-prod" \
  --name "ReplicateApiToken" \
  --value "YOUR_REPLICATE_TOKEN"

az keyvault secret set --vault-name "aiprofilephotomaker-kv-prod" \
  --name "JwtSecret" \
  --value "YOUR_JWT_SECRET"

az keyvault secret set --vault-name "aiprofilephotomaker-kv-prod" \
  --name "ReplicateWebhookSecret" \
  --value "YOUR_WEBHOOK_SECRET"
```

### Step 3: Application Deployment
```bash
# Trigger via GitHub Actions
gh workflow run "🚀 Deploy Application" \
  --ref main \
  -f environment=production \
  -f deploy_frontend=true \
  -f deploy_backend=true \
  -f run_migrations=true
```

## 🔧 Infrastructure Components

### Core Services
| Service | Purpose | SKU |
|---------|---------|-----|
| App Service Plan | API hosting | B1 |
| Static Web App | Frontend hosting | Free |
| Azure SQL Database | Data storage | Basic |
| Redis Cache | Session/caching | Standard C1 |
| Storage Account | File storage | Standard_LRS |
| Key Vault | Secret management | Standard |
| Application Insights | Monitoring | Pay-as-you-go |

### Estimated Monthly Cost: $26-39

## 📊 Monitoring & Validation

### Post-Deployment Validation
```bash
# Run validation script
./infrastructure/scripts/validate-production-deployment.sh

# Health check endpoints
curl https://aiprofilephotomaker-app-prod.azurewebsites.net/health
curl https://aiprofilephotomaker.azurestaticapps.net
```

### Monitoring Dashboards
- **Application Insights**: Real-time application monitoring
- **Azure Monitor**: Infrastructure monitoring and alerts
- **Log Analytics**: Centralized logging and diagnostics

## 🔒 Security Features

- **Managed Identity**: Service-to-service authentication
- **Key Vault Integration**: Secure secret management
- **HTTPS Only**: TLS 1.2+ enforced
- **SQL Advanced Security**: Threat detection enabled
- **CORS Configuration**: Proper cross-origin policies

## 🚨 Troubleshooting

### Common Issues
1. **Key Vault Access**: Ensure Managed Identity has proper permissions
2. **SQL Connection**: Verify firewall rules allow Azure services
3. **Static Web App**: Check custom domain configuration
4. **Redis Cache**: Verify SSL connections enabled

### Rollback Procedure
```bash
# Use rollback script if needed
./infrastructure/scripts/rollback-production-deployment.sh --dry-run
./infrastructure/scripts/rollback-production-deployment.sh --confirm
```

## 📈 Performance Optimization

### Recommended Upgrades for Scale
- **App Service Plan**: B1 → S1 for better performance
- **SQL Database**: Basic → Standard for better performance
- **Redis Cache**: Standard C1 → Standard C2 for higher throughput
- **Storage Account**: Add CDN for global performance

## 🔄 CI/CD Pipeline

### Simplified Pipeline (3 Jobs)
1. **Frontend Linting**: ESLint error checking
2. **TypeScript Check**: Compilation validation  
3. **NET Code Quality**: Formatting validation

### Quality Gates
- Zero ESLint errors (deployment-blocking)
- TypeScript compilation success
- .NET code formatting compliance

## 📚 Next Steps

1. **Custom Domain**: Configure custom domain for Static Web App
2. **SSL Certificate**: Set up custom SSL certificate
3. **Monitoring Alerts**: Configure production alerts
4. **Backup Strategy**: Set up automated backups
5. **Scaling Policies**: Configure auto-scaling rules

## 🆘 Support

- **Infrastructure Issues**: Check Azure portal and diagnostics
- **Application Issues**: Review Application Insights logs
- **Security Concerns**: Audit Key Vault access logs
- **Performance Issues**: Review Redis Cache metrics