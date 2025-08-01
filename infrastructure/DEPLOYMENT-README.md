# Production Deployment Guide

This document provides comprehensive instructions for deploying the AI Profile Photo Maker application to Azure in a production-ready configuration.

## 🚀 Quick Start

### Prerequisites

1. **Azure CLI** - Latest version installed and logged in
2. **Bicep CLI** - For infrastructure as code
3. **Required Azure Permissions** - Contributor role on target subscription
4. **GitHub Secrets** - All secrets configured (see below)

### One-Command Production Deployment

```bash
# Deploy infrastructure
./infrastructure/deploy.sh -e prod

# Validate deployment
./infrastructure/scripts/validate-production-deployment.sh

# Deploy application (via GitHub Actions)
gh workflow run "🚀 Deploy Application" --ref main -f environment=production
```

## 📋 Detailed Deployment Process

### Step 1: Infrastructure Deployment

The infrastructure includes all Azure resources with production-ready configurations:

- **App Service Plan** (S1 tier for production)
- **Web App** with managed identity and security hardening
- **Static Web App** for frontend hosting
- **SQL Server & Database** with backup and monitoring
- **Storage Account** with blob containers for image storage
- **Key Vault** for secure secret management
- **Redis Cache** for session and caching (Standard tier)
- **Container Registry** for Docker images (optional)
- **Application Insights** with comprehensive monitoring
- **Log Analytics Workspace** for centralized logging
- **Action Groups** and **Metric Alerts** for proactive monitoring

#### Deploy Infrastructure

```bash
# Production deployment
cd infrastructure
./deploy.sh -e prod -g ai-profile-photo-maker-prod -l "East US 2"

# Staging deployment
./deploy.sh -e staging -g ai-profile-photo-maker-staging -l "East US 2"

# Development deployment
./deploy.sh -e dev -g ai-profile-photo-maker-dev -l "East US 2"
```

#### Validate Infrastructure

```bash
# Validate Bicep template
./deploy.sh -e prod --validate

# Run comprehensive validation
./scripts/validate-production-deployment.sh
```

### Step 2: Application Deployment

Application deployment is handled through GitHub Actions with the following workflows:

1. **Infrastructure Deployment** (`.github/workflows/deploy-infrastructure.yml`)
2. **Application Deployment** (`.github/workflows/deploy-application.yml`)

#### Manual Application Deployment

```bash
# Trigger via GitHub CLI
gh workflow run "🚀 Deploy Application" \
  --ref main \
  -f environment=production \
  -f deploy_backend=true \
  -f deploy_frontend=true \
  -f run_migrations=true
```

### Step 3: Post-Deployment Validation

```bash
# Run comprehensive validation
./scripts/validate-production-deployment.sh

# Check application health
curl https://YOUR_APP_NAME.azurewebsites.net/health

# View deployment report
cat deployment-validation-report.json
```

## 🔐 Required Secrets

Configure these secrets in GitHub repository settings:

### Azure Authentication
- `AZUREAPPSERVICE_CLIENTID_*` - Service Principal Client ID
- `AZUREAPPSERVICE_TENANTID_*` - Azure Tenant ID
- `AZUREAPPSERVICE_SUBSCRIPTIONID_*` - Azure Subscription ID

### Application Secrets
- `PROD_SQL_ADMIN_PASSWORD` - Production SQL Server admin password
- `STAGING_SQL_ADMIN_PASSWORD` - Staging SQL Server admin password
- `PROD_JWT_SECRET` - Production JWT signing secret
- `STAGING_JWT_SECRET` - Staging JWT signing secret
- `REPLICATE_API_TOKEN` - API token for Replicate AI service
- `REPLICATE_WEBHOOK_SECRET` - Webhook signature validation secret

## 🏗️ Architecture Overview

### Infrastructure Components

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Static Web    │    │    Web App      │    │   SQL Database  │
│      App        │◄──►│   (.NET API)    │◄──►│   (Production)  │
│   (Frontend)    │    │                 │    │                 │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                        │                      │
         │                        ▼                      │
         │              ┌─────────────────┐              │
         │              │   Redis Cache   │              │
         │              │   (Sessions)    │              │
         │              └─────────────────┘              │
         │                        │                      │
         ▼                        ▼                      ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ Storage Account │    │   Key Vault     │    │ App Insights    │
│ (Profile Images)│    │   (Secrets)     │    │ (Monitoring)    │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

### Security Features

- **Managed Identity** for secure Azure service authentication
- **Key Vault** integration for secret management
- **HTTPS-only** communication with TLS 1.2 minimum
- **SQL Server** with Advanced Data Security
- **Storage Account** with HTTPS-only and access restrictions
- **Application Insights** for security monitoring

### Monitoring & Alerting

- **Application Insights** for application performance monitoring
- **Log Analytics** for centralized logging
- **Metric Alerts** for proactive monitoring:
  - Web App response time > 5 seconds
  - SQL Database DTU usage > 80%
  - Redis Cache memory usage > 85%
- **Availability Tests** for uptime monitoring
- **Action Groups** for alert notifications

## 🔄 Deployment Strategies

### Canary Deployment (Recommended)

Gradual rollout with traffic splitting:
- 25% → 50% → 100% traffic allocation
- Automatic rollback on health check failures
- Zero-downtime deployment

```bash
gh workflow run "🚀 Deploy Application" \
  --ref main \
  -f environment=production \
  -f deployment_strategy=canary
```

### Blue-Green Deployment

Zero-downtime deployment with slot swapping:
- Deploy to staging slot
- Run validation tests
- Swap staging and production slots

```bash
gh workflow run "🚀 Deploy Application" \
  --ref main \
  -f environment=production \
  -f deployment_strategy=bluegreen
```

### Standard Deployment

Direct deployment to production:
- Fastest deployment method
- Brief downtime during deployment
- Recommended for development/staging

## 🔧 Configuration Management

### Environment-Specific Settings

Each environment has its own parameter file:
- `parameters.dev.json` - Development environment
- `parameters.staging.json` - Staging environment  
- `parameters.prod.json` - Production environment

### Resource Sizing by Environment

| Resource | Development | Staging | Production |
|----------|------------|---------|------------|
| App Service Plan | F1 (Free) | B1 (Basic) | S1 (Standard) |
| SQL Database | Basic 5 DTU | Basic 5 DTU | Standard S1 |
| Redis Cache | Basic C0 | Basic C0 | Standard C1 |
| Storage Account | LRS | LRS | LRS |

## 🔍 Monitoring & Observability

### Application Insights Metrics

- **Request Rate** - Requests per second
- **Response Time** - Average response time
- **Failure Rate** - Failed request percentage  
- **Availability** - Uptime percentage
- **Dependencies** - External service call performance

### Custom Metrics

- **Profile Generation Time** - AI processing duration
- **Image Upload Success Rate** - Storage operation success
- **Redis Hit Rate** - Cache effectiveness
- **Database Connection Pool** - SQL connection usage

### Log Queries

Access logs through Application Insights:

```kusto
// Failed requests in last 24 hours
requests
| where timestamp > ago(24h)
| where success == false
| summarize count() by resultCode

// Slow requests (>5 seconds)
requests  
| where timestamp > ago(1h)
| where duration > 5000
| project timestamp, name, duration, resultCode
```

## 🚨 Disaster Recovery & Rollback

### Automated Rollback

```bash
# Rollback application to previous version
./scripts/rollback-production-deployment.sh -t app

# Rollback infrastructure to previous deployment
./scripts/rollback-production-deployment.sh -t infrastructure

# Complete rollback (all components)
./scripts/rollback-production-deployment.sh -t all --force
```

### Manual Recovery Steps

1. **Application Issues**:
   - Use deployment slots for instant rollback
   - Redeploy previous working version via GitHub Actions
   - Check Application Insights for error details

2. **Database Issues**:
   - Restore from automated daily backups
   - Point-in-time restore (up to 35 days)
   - Use backup database created during rollback

3. **Infrastructure Issues**:
   - Redeploy previous working Bicep template
   - Check Azure Activity Log for deployment errors
   - Validate resource health in Azure Portal

### Backup Strategy

- **SQL Database**: Automated daily backups with 35-day retention
- **Storage Account**: Blob versioning and soft delete (30 days)
- **Key Vault**: Soft delete enabled with 90-day retention
- **Application Code**: Source control in GitHub with tagged releases

## 📊 Cost Optimization

### Production Environment

- **Reserved Instances** for App Service Plan (40% savings)
- **Azure Hybrid Benefit** for SQL Server licensing
- **Storage Account** lifecycle policies for old images
- **Auto-scaling** rules to optimize compute costs

### Development/Staging

- **Scheduled shutdown** outside business hours
- **Free tier** resources where possible
- **Shared App Service Plans** across environments

### Cost Monitoring

- **Azure Cost Management** alerts at $1000/month threshold
- **Resource tagging** for cost allocation
- **Regular cost reviews** and optimization recommendations

## 🔧 Troubleshooting

### Common Issues

1. **Deployment Failures**:
   ```bash
   # Check deployment logs
   az deployment group show --resource-group RESOURCE_GROUP --name DEPLOYMENT_NAME
   
   # Validate template
   az deployment group validate --resource-group RESOURCE_GROUP --template-file main.bicep
   ```

2. **Application Health Issues**:
   ```bash
   # Check Web App logs
   az webapp log tail --name WEB_APP_NAME --resource-group RESOURCE_GROUP
   
   # Test endpoints manually
   curl -v https://YOUR_APP.azurewebsites.net/health
   ```

3. **Database Connection Issues**:
   ```bash
   # Test SQL connectivity
   az sql db show-connection-string --client ado.net --name DATABASE_NAME --server SERVER_NAME
   
   # Check firewall rules
   az sql server firewall-rule list --resource-group RESOURCE_GROUP --server SERVER_NAME
   ```

### Support Contacts

- **Azure Support**: Create support ticket in Azure Portal
- **Application Issues**: Check GitHub Issues and Actions logs
- **Emergency Contacts**: Update email in Action Groups

## 📚 Additional Resources

- [Azure App Service Documentation](https://docs.microsoft.com/en-us/azure/app-service/)
- [Azure SQL Database Documentation](https://docs.microsoft.com/en-us/azure/sql-database/)
- [Azure Key Vault Documentation](https://docs.microsoft.com/en-us/azure/key-vault/)
- [Application Insights Documentation](https://docs.microsoft.com/en-us/azure/application-insights/)
- [Bicep Documentation](https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/)

---

**Next Steps After Deployment**:
1. ✅ Validate all services are running
2. ✅ Configure custom domain and SSL certificate
3. ✅ Set up monitoring dashboards
4. ✅ Configure backup schedules
5. ✅ Test disaster recovery procedures
6. ✅ Schedule regular security reviews