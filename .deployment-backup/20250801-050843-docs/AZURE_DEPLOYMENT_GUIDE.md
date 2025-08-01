# Azure Deployment Guide - AI Profile Photo Maker

*Created: July 16, 2025*

## Overview

This guide provides complete instructions for deploying the AI Profile Photo Maker application to Microsoft Azure cloud platform. The deployment includes automated CI/CD pipelines, Infrastructure as Code, and production-ready configurations.

## Architecture Overview

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Angular       │    │   .NET 8.0      │    │   Azure SQL     │
│   Frontend      │────│   Backend API   │────│   Database      │
│ (Static Web App)│    │ (App Service)   │    │                 │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         │              ┌─────────────────┐              │
         │              │   Azure Blob    │              │
         └──────────────│   Storage       │──────────────┘
                        │  (Images)       │
                        └─────────────────┘
                                │
                    ┌─────────────────┐
                    │   Azure Key     │
                    │   Vault         │
                    │  (Secrets)      │
                    └─────────────────┘
```

## Deployment Components

### 1. Frontend Deployment
- **Service**: Azure Static Web App
- **Framework**: Angular 17+ with TypeScript
- **Build**: GitHub Actions automated deployment
- **Domain**: Custom domain with SSL certificate
- **CDN**: Global content delivery network

### 2. Backend API Deployment
- **Service**: Azure App Service
- **Framework**: .NET 8.0 with ASP.NET Core
- **Database**: Azure SQL Database
- **Storage**: Azure Blob Storage
- **Monitoring**: Application Insights

### 3. Infrastructure as Code
- **Template**: Bicep templates for Azure Resource Manager
- **Environments**: Staging and Production
- **Automation**: GitHub Actions deployment workflows
- **Security**: Azure Key Vault for secrets management

## Prerequisites

### Required Tools
- [ ] Azure CLI (latest version)
- [ ] Bicep CLI (latest version)
- [ ] Node.js 18+ and npm
- [ ] .NET 8.0 SDK
- [ ] Docker Desktop (for containerization)
- [ ] Git and GitHub account

### Azure Requirements
- [ ] Azure subscription with contributor access
- [ ] Azure DevOps or GitHub repository
- [ ] Domain name (optional, for custom domain)

### Third-Party Services
- [ ] Replicate API account and token
- [ ] Stripe account (for payments)
- [ ] OAuth providers (Google, Facebook)

## Quick Start Deployment

### Step 1: Clone and Setup
```bash
# Clone the repository
git clone https://github.com/YourUsername/AI.ProfilePhotoMaker.git
cd AI.ProfilePhotoMaker

# Install dependencies
cd AI.ProfilePhotoMaker.UI
npm install
cd ..

# Restore .NET packages
cd AI.ProfilePhotoMaker.API
dotnet restore
cd ..
```

### Step 2: Azure Login and Setup
```bash
# Login to Azure
az login

# Set subscription (if you have multiple)
az account set --subscription "Your-Subscription-ID"

# Create resource group
az group create --name ai-profile-photo-maker --location "East US"
```

### Step 3: Configure Parameters and Deploy Infrastructure

**Important**: Before deploying, you must update the parameter files with actual values. The initial deployment cannot use Key Vault references since the Key Vault doesn't exist yet.

```bash
# Navigate to infrastructure directory
cd infrastructure

# Edit parameter files to replace placeholders with actual values
# For staging:
vi parameters.staging.json
# Replace:
#   - REPLACE_WITH_STRONG_PASSWORD_STAGING_123! with your SQL admin password
#   - REPLACE_WITH_YOUR_REPLICATE_TOKEN with your Replicate API token
#   - REPLACE_WITH_YOUR_JWT_SECRET_KEY_STAGING_MIN_32_CHARS with a 32+ char secret

# For production:
vi parameters.prod.json
# Replace:
#   - REPLACE_WITH_STRONG_PASSWORD_123! with your SQL admin password
#   - REPLACE_WITH_YOUR_REPLICATE_TOKEN with your Replicate API token
#   - REPLACE_WITH_YOUR_JWT_SECRET_KEY_MIN_32_CHARS with a 32+ char secret

# Deploy to staging
./deploy.sh --environment staging

# Deploy to production (after testing)
./deploy.sh --environment prod
```

### Step 4: Update Key Vault Secrets (Post-Deployment)

**Note**: The Key Vault is created during infrastructure deployment. After deployment, you can optionally update the secrets in Key Vault for better security management.

```bash
# Get the Key Vault name from deployment output
KEYVAULT_NAME=$(az deployment group show \
  --resource-group ai-profile-photo-maker \
  --name main \
  --query properties.outputs.keyVaultName.value -o tsv)

# Update secrets in Key Vault (optional - they're already set from parameters)
az keyvault secret set --vault-name "$KEYVAULT_NAME" --name "SqlAdminPassword" --value "YourStrongPassword123!"
az keyvault secret set --vault-name "$KEYVAULT_NAME" --name "ReplicateApiToken" --value "your-replicate-token"
az keyvault secret set --vault-name "$KEYVAULT_NAME" --name "JwtSecret" --value "your-jwt-secret-key"
```

### Step 5: Setup GitHub Actions

**Important**: The Static Web App deployment token is only available AFTER the infrastructure is deployed.

```bash
# Get the Static Web App deployment token
STATIC_WEB_APP_NAME=$(az deployment group show \
  --resource-group ai-profile-photo-maker \
  --name main \
  --query properties.outputs.staticWebAppName.value -o tsv)

DEPLOYMENT_TOKEN=$(az staticwebapp secrets list \
  --name $STATIC_WEB_APP_NAME \
  --resource-group ai-profile-photo-maker \
  --query properties.apiKey -o tsv)

echo "Static Web App Deployment Token: $DEPLOYMENT_TOKEN"
```

1. Go to your GitHub repository settings → Secrets and variables → Actions
2. Add the following secrets:
   - `AZURE_STATIC_WEB_APPS_API_TOKEN` - Use the deployment token from above
   - `AZUREAPPSERVICE_CLIENTID_*` - From your service principal
   - `AZUREAPPSERVICE_TENANTID_*` - From your Azure tenant
   - `AZUREAPPSERVICE_SUBSCRIPTIONID_*` - Your Azure subscription ID

### Step 6: Deploy Application
```bash
# Trigger deployment by pushing to main branch
git add .
git commit -m "Initial Azure deployment"
git push origin main
```

## Environment Configuration

### Production Environment
- **Frontend URL**: `https://aiprofilephotomaker.azurestaticapps.net`
- **Backend API**: `https://aiprofilephotomakerapi.azurewebsites.net`
- **Database**: Azure SQL Database
- **Storage**: Azure Blob Storage with CDN

### Staging Environment
- **Frontend URL**: `https://aiprofilephotomaker-staging.azurestaticapps.net`
- **Backend API**: `https://aiprofilephotomakerapi-staging.azurewebsites.net`
- **Database**: Azure SQL Database (Basic tier)
- **Storage**: Azure Blob Storage

## Monitoring and Logging

### Application Insights
- **Performance Monitoring**: Response times, throughput, availability
- **Error Tracking**: Exceptions, failed requests, custom events
- **User Analytics**: User flows, page views, custom metrics
- **Alerting**: Automated alerts for performance issues

### Azure Monitor
- **Infrastructure Metrics**: CPU, memory, disk usage
- **Application Logs**: Structured logging with correlation IDs
- **Custom Dashboards**: Real-time monitoring dashboards
- **Cost Monitoring**: Resource usage and cost tracking

## Security Configuration

### Key Security Features
- **HTTPS Only**: All traffic encrypted with TLS 1.2+
- **CORS Configuration**: Proper cross-origin resource sharing
- **Security Headers**: X-Frame-Options, X-Content-Type-Options, etc.
- **Managed Identity**: Secure service-to-service authentication
- **Key Vault Integration**: Centralized secrets management

### Authentication & Authorization
- **JWT Tokens**: Secure token-based authentication
- **OAuth Integration**: Google and Facebook login
- **Role-Based Access**: User roles and permissions
- **API Rate Limiting**: Protection against abuse

## Cost Optimization

### Resource Sizing
- **Production**: Optimized for performance and availability
- **Staging**: Minimal resources for testing
- **Development**: Local development with Azure emulators

### Estimated Monthly Costs
- **Staging Environment**: $50-100/month
- **Production Environment**: $200-500/month
- **Additional Costs**: Replicate API usage, data transfer

## Deployment Sequence and Dependencies

### First-Time Deployment Order

1. **Update Parameter Files**: Replace all placeholder values with actual secrets
2. **Deploy Infrastructure**: Creates all Azure resources including Key Vault
3. **Get Deployment Tokens**: Retrieve Static Web App token after creation
4. **Configure GitHub Secrets**: Add deployment token to GitHub Actions
5. **Deploy Applications**: Push to main branch to trigger deployments

### Key Vault Chicken-and-Egg Solution

The parameter files initially use direct values instead of Key Vault references because:
- Key Vault doesn't exist before the first deployment
- Bicep needs the secrets to create resources (SQL Server, etc.)
- After deployment, secrets are automatically stored in the created Key Vault
- Future deployments can optionally use Key Vault references

## Troubleshooting

### Common Issues

#### Deployment Failures
```bash
# Check deployment status
az deployment group show --resource-group ai-profile-photo-maker --name deployment-name

# View deployment logs
az deployment group show --resource-group ai-profile-photo-maker --name deployment-name --query properties.error
```

#### Application Issues
```bash
# Check application logs
az webapp log tail --name aiprofilephotomakerapi --resource-group ai-profile-photo-maker

# Restart application
az webapp restart --name aiprofilephotomakerapi --resource-group ai-profile-photo-maker
```

#### Database Connection Issues
```bash
# Test database connection
az sql db show-connection-string --server your-sql-server --name your-database --client sqlcmd

# Check firewall rules
az sql server firewall-rule list --resource-group ai-profile-photo-maker --server your-sql-server
```

### Support Resources
- **Azure Documentation**: https://docs.microsoft.com/azure
- **GitHub Issues**: Create issues in the repository
- **Azure Support**: Azure portal support requests

## Next Steps

### Immediate Actions (Next 24 hours)
1. **Configure Secrets**: Set up all required secrets in Azure Key Vault
2. **Test Deployment**: Deploy to staging and verify all functionality
3. **Custom Domain**: Configure custom domain for production
4. **SSL Certificate**: Set up SSL certificate for custom domain

### Short-term Actions (Next Week)
1. **Performance Testing**: Load test the application
2. **Backup Strategy**: Configure database backups
3. **Monitoring Setup**: Configure alerts and dashboards
4. **Documentation**: Update team documentation

### Long-term Actions (Next Month)
1. **Scalability Planning**: Plan for user growth
2. **Cost Optimization**: Review and optimize resource usage
3. **Security Audit**: Conduct security assessment
4. **Feature Deployment**: Deploy new features using CI/CD

## Maintenance

### Regular Tasks
- **Security Updates**: Keep all dependencies updated
- **Performance Monitoring**: Review Application Insights regularly
- **Cost Review**: Monthly cost analysis and optimization
- **Backup Verification**: Test backup and restore procedures

### Automated Tasks
- **CI/CD Pipeline**: Automated testing and deployment
- **Security Scanning**: Automated vulnerability scanning
- **Performance Testing**: Automated load testing
- **Cost Alerts**: Automated cost monitoring and alerts

## Conclusion

The AI Profile Photo Maker application is now ready for production deployment on Azure. The infrastructure is designed for scalability, security, and maintainability. Follow this guide to deploy and maintain your application successfully.

For questions or issues, please create an issue in the GitHub repository or contact the development team.

---

*This guide is part of the AI Profile Photo Maker project documentation. Last updated: July 16, 2025*