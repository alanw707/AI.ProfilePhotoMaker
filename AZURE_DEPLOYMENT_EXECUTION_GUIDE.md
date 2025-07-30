# Azure Deployment Execution Guide

**Version**: 2.0  
**Date**: July 30, 2025  
**Status**: Production Ready  
**Target**: Azure Cloud Infrastructure

## 🎯 Overview

This guide provides step-by-step instructions for executing the automated Azure deployment system. The infrastructure includes multiple deployment methods with built-in error handling and monitoring.

## 📋 Pre-Deployment Validation Checklist

### ✅ Phase 1: Prerequisites Verification

**Required Tools & Access**:
- [ ] Azure CLI installed and authenticated (`az login`)
- [ ] GitHub repository access with admin permissions
- [ ] Azure subscription with Contributor role access
- [ ] PowerShell Core 7+ (for local PowerShell deployment)
- [ ] Python 3.7+ with pip (for Python SDK deployment)

**Verification Commands**:
```bash
# Check Azure CLI authentication
az account show --query name

# Check subscription access
az account list --query "[].{Name:name, SubscriptionId:id, State:state}"

# Check resource group access (should exist or be creatable)
az group show --name "ai-profile-photo-maker-staging" || echo "Will be created"

# Check GitHub CLI (optional)
gh auth status
```

### ✅ Phase 2: Repository Configuration

**GitHub Secrets Validation**:
- [ ] `AZUREAPPSERVICE_CLIENTID_C73973894C7140DEAF8637A42FA0C131`
- [ ] `AZUREAPPSERVICE_TENANTID_011D6FB5A4BC43509D9B165F9842CEBC`
- [ ] `AZUREAPPSERVICE_SUBSCRIPTIONID_B9C8B148FA76469EB51C84A0DE3D63BB`
- [ ] `STAGING_SQL_ADMIN_PASSWORD`
- [ ] `STAGING_JWT_SECRET`
- [ ] `REPLICATE_API_TOKEN`
- [ ] `REPLICATE_WEBHOOK_SECRET`
- [ ] `PROD_SQL_ADMIN_PASSWORD` (for production)
- [ ] `PROD_JWT_SECRET` (for production)

**Validation Command**:
```bash
# Check if secrets are configured (will show names only)
gh secret list --repo YOUR_USERNAME/AI.ProfilePhotoMaker
```

### ✅ Phase 3: Infrastructure Files Validation

**Required Files Check**:
- [ ] `/infrastructure/main.bicep` (Infrastructure template)
- [ ] `/infrastructure/parameters.staging.json` (Staging parameters)
- [ ] `/infrastructure/parameters.prod.json` (Production parameters)
- [ ] `/.github/workflows/master-deployment.yml` (Main pipeline)
- [ ] `/.github/workflows/deploy-infrastructure-powershell.yml` (Infrastructure deployment)

**File Validation**:
```bash
# Check file existence
ls -la infrastructure/
ls -la .github/workflows/

# Validate Bicep template syntax
bicep build infrastructure/main.bicep --outfile /tmp/validation.json
echo "Bicep template validation: $?"
```

## 🚀 Deployment Execution Methods

### 🎯 Method 1: GitHub Actions (Recommended)

**Use Case**: Production deployments, CI/CD integration, team collaboration

#### Quick Deployment:
```bash
# Trigger full staging deployment
gh workflow run "🚀 Master Deployment Pipeline" \
  --field deployment_type=full \
  --field target_environment=staging

# Trigger infrastructure-only deployment
gh workflow run "🚀 Master Deployment Pipeline" \
  --field deployment_type=infrastructure-only \
  --field target_environment=staging

# Trigger production deployment
gh workflow run "🚀 Master Deployment Pipeline" \
  --field deployment_type=full \
  --field target_environment=production
```

#### Advanced Deployment Options:
```bash
# Deploy to both staging and production
gh workflow run "🚀 Master Deployment Pipeline" \
  --field deployment_type=full \
  --field target_environment=both

# Emergency deployment (skip quality gates)
gh workflow run "🚀 Master Deployment Pipeline" \
  --field deployment_type=full \
  --field target_environment=staging \
  --field skip_quality_gates=true

# Infrastructure validation only
gh workflow run "🏗️ Deploy Infrastructure (PowerShell)" \
  --field environment=staging \
  --field validate_only=true
```

### 🖥️ Method 2: Local Shell Script (Fast)

**Use Case**: Development, testing, troubleshooting Azure CLI issues

#### Staging Deployment:
```bash
# Navigate to infrastructure directory
cd infrastructure/

# Make script executable
chmod +x deploy-local.sh

# Execute deployment
./deploy-local.sh
```

#### Custom Deployment:
```bash
# Set custom parameters
export ENVIRONMENT="staging"
export RESOURCE_GROUP="ai-profile-photo-maker-staging"
export LOCATION="East US"

# Run deployment
./deploy-local.sh
```

### 🐍 Method 3: Python SDK (Azure CLI Alternative)

**Use Case**: Bypassing Azure CLI API issues, programmatic deployment

#### Setup:
```bash
# Install dependencies
cd infrastructure/
pip install -r requirements.txt

# Or install manually
pip install azure-identity azure-mgmt-resource
```

#### Staging Deployment:
```bash
# Basic deployment
python3 deploy_azure_sdk.py

# Custom environment
python3 deploy_azure_sdk.py --environment staging

# Validation only
python3 deploy_azure_sdk.py --validate

# Verbose output
python3 deploy_azure_sdk.py --verbose
```

#### Production Deployment:
```bash
python3 deploy_azure_sdk.py \
  --environment prod \
  --resource-group "ai-profile-photo-maker-production" \
  --location "East US"
```

### 🌐 Method 4: Azure Portal (Manual)

**Use Case**: Emergency deployment, Azure CLI unavailable

#### Steps:
1. **Navigate**: Azure Portal → Resource Groups → Create Deployment
2. **Template**: Upload `infrastructure/main.json` (compile Bicep first)
3. **Parameters**: Upload `infrastructure/parameters.staging.json`
4. **Deploy**: Review + Create

**Bicep to ARM Conversion**:
```bash
# Convert Bicep to ARM template
bicep build infrastructure/main.bicep --outfile infrastructure/main.json
```

## 📊 Real-Time Monitoring & Progress Tracking

### 🔍 GitHub Actions Monitoring

**GitHub UI**:
- Visit: `https://github.com/YOUR_USERNAME/AI.ProfilePhotoMaker/actions`
- Select: Latest workflow run
- Monitor: Real-time logs and progress

**CLI Monitoring**:
```bash
# List recent workflow runs
gh run list --workflow="🚀 Master Deployment Pipeline"

# Watch specific run (replace RUN_ID)
gh run watch RUN_ID

# View logs for failed run
gh run view RUN_ID --log-failed
```

### 📱 Azure Portal Monitoring

**Resource Group View**:
```bash
# Get resource group URL
echo "https://portal.azure.com/#@/resource/subscriptions/$(az account show --query id -o tsv)/resourcegroups/ai-profile-photo-maker-staging"
```

**Deployment Monitoring**:
1. Navigate to Resource Group
2. Click "Deployments" in left sidebar
3. Monitor progress in real-time
4. View deployment details and logs

### 📈 CLI Monitoring Commands

**Deployment Status**:
```bash
# Check deployment status
az deployment group list \
  --resource-group "ai-profile-photo-maker-staging" \
  --query "[0].{Name:name, State:properties.provisioningState, Timestamp:properties.timestamp}"

# Watch deployment in real-time
az deployment group show \
  --resource-group "ai-profile-photo-maker-staging" \
  --name "DEPLOYMENT_NAME" \
  --query "properties.provisioningState"
```

**Resource Verification**:
```bash
# List all resources in resource group
az resource list \
  --resource-group "ai-profile-photo-maker-staging" \
  --output table

# Check specific resource status
az webapp show \
  --name "aiprofilephotomakerapi-staging" \
  --resource-group "ai-profile-photo-maker-staging" \
  --query "{Name:name, State:state, DefaultHostName:defaultHostName}"
```

### 🚨 Real-Time Alerts Setup

**Azure Monitor Alerts**:
```bash
# Create deployment failure alert
az monitor activity-log alert create \
  --name "deployment-failure-alert" \
  --resource-group "ai-profile-photo-maker-staging" \
  --condition category=Administrative and operationName="Microsoft.Resources/deployments/write" and level=Error \
  --action-group YOUR_ACTION_GROUP_ID
```

## 🔧 Troubleshooting Guide

### ❌ Common Issue 1: Azure CLI API Issues

**Symptoms**:
- "The content for this response was already consumed"
- Timeout errors during deployment
- Authentication working but API calls failing

**Solutions**:
```bash
# Solution A: Use Python SDK method
python3 infrastructure/deploy_azure_sdk.py

# Solution B: Use PowerShell method
gh workflow run "🏗️ Deploy Infrastructure (PowerShell)" --field environment=staging

# Solution C: Use Azure Portal method
bicep build infrastructure/main.bicep --outfile infrastructure/main.json
# Then upload to Azure Portal
```

### ❌ Common Issue 2: Authentication Failures

**Symptoms**:
- "Please run 'az login'"
- Service principal authentication errors
- Permission denied errors

**Solutions**:
```bash
# Re-authenticate Azure CLI
az logout
az login

# Check current authentication
az account show

# Verify service principal permissions
az role assignment list --assignee YOUR_SERVICE_PRINCIPAL_ID --resource-group "ai-profile-photo-maker-staging"

# Test permissions
az resource list --resource-group "ai-profile-photo-maker-staging"
```

### ❌ Common Issue 3: Resource Conflicts

**Symptoms**:
- "Resource already exists"
- Name conflicts
- SKU unavailable in region

**Solutions**:
```bash
# Check existing resources
az resource list --resource-group "ai-profile-photo-maker-staging" --output table

# Clean up conflicting resources (CAUTION)
az resource delete --ids RESOURCE_ID

# Change location if SKU unavailable
# Edit parameters file: "location": "West US 2"

# Use different resource names
# Edit parameters file: "namePrefix": "aiprofilephotomaker2"
```

### ❌ Common Issue 4: Template Validation Errors

**Symptoms**:
- Bicep compilation errors
- ARM template validation failures
- Parameter errors

**Solutions**:
```bash
# Validate Bicep template
bicep build infrastructure/main.bicep --outfile /tmp/test.json

# Validate ARM template
az deployment group validate \
  --resource-group "ai-profile-photo-maker-staging" \
  --template-file infrastructure/main.json \
  --parameters @infrastructure/parameters.staging.json

# Check parameter format
cat infrastructure/parameters.staging.json | jq '.'
```

### ❌ Common Issue 5: GitHub Actions Failures

**Symptoms**:
- Workflow failures
- Secret access errors
- Permission errors

**Solutions**:
```bash
# Check repository secrets
gh secret list

# Re-run failed workflow
gh run rerun RUN_ID

# Check workflow permissions
cat .github/workflows/master-deployment.yml | grep -A 10 "permissions:"

# Manual trigger with specific parameters
gh workflow run "🚀 Master Deployment Pipeline" \
  --field deployment_type=infrastructure-only \
  --field target_environment=staging
```

## ✅ Success Validation Criteria

### 🎯 Infrastructure Validation Checklist

**Core Resources Deployed**:
- [ ] **App Service Plan**: `aiprofilephotomaker-asp-staging`
- [ ] **Web App**: `aiprofilephotomakerapi-staging`
- [ ] **Static Web App**: `aiprofilephotomaker-swa-staging`
- [ ] **SQL Server**: `aiprofilephotomaker-sql-staging-[unique]`
- [ ] **SQL Database**: `aiprofilephotomakerdb`
- [ ] **Storage Account**: `aiprofilephotomakersto[unique]`
- [ ] **Key Vault**: `aiprofilephotomaker-kv-staging-[unique]`
- [ ] **Application Insights**: `aiprofilephotomaker-ai-staging`

**Validation Commands**:
```bash
# Check all resources exist
az resource list \
  --resource-group "ai-profile-photo-maker-staging" \
  --query "[].{Name:name, Type:type, Status:properties.provisioningState}" \
  --output table

# Verify web app is running
curl -I https://aiprofilephotomakerapi-staging.azurewebsites.net/health

# Check database connectivity
az sql db show \
  --server "aiprofilephotomaker-sql-staging-[unique]" \
  --name "aiprofilephotomakerdb" \
  --resource-group "ai-profile-photo-maker-staging"
```

### 🔐 Security Validation

**Key Vault Secrets**:
- [ ] `JwtSecret` stored securely
- [ ] `ReplicateApiToken` stored securely  
- [ ] `DatabaseConnectionString` stored securely
- [ ] `ReplicateWebhookSecret` stored securely

**Validation**:
```bash
# List Key Vault secrets (names only)
az keyvault secret list \
  --vault-name "aiprofilephotomaker-kv-staging-[unique]" \
  --query "[].name" \
  --output table

# Test Key Vault access from Web App
az webapp config appsettings list \
  --name "aiprofilephotomakerapi-staging" \
  --resource-group "ai-profile-photo-maker-staging" \
  --query "[?contains(name, 'Jwt')]"
```

### 🌐 Connectivity Validation

**Network Configuration**:
- [ ] HTTPS enforced on all services
- [ ] CORS configured for frontend access
- [ ] SQL Server firewall allows Azure services
- [ ] Storage account allows public blob access

**Test Commands**:
```bash
# Test HTTPS enforcement
curl -I http://aiprofilephotomakerapi-staging.azurewebsites.net
# Should redirect to HTTPS

# Test CORS configuration
curl -H "Origin: https://aiprofilephotomaker-swa-staging.azurestaticapps.net" \
     -H "Access-Control-Request-Method: GET" \
     -H "Access-Control-Request-Headers: X-Requested-With" \
     -X OPTIONS \
     https://aiprofilephotomakerapi-staging.azurewebsites.net/api/health
```

### 📊 Performance Validation

**Resource Performance**:
- [ ] App Service Plan: Appropriate SKU (F1 for staging)
- [ ] SQL Database: Basic tier, 2GB storage
- [ ] Storage: Standard_LRS performance
- [ ] Application Insights: Monitoring active

**Monitoring Setup**:
```bash
# Check Application Insights instrumentation
az monitor app-insights component show \
  --app "aiprofilephotomaker-ai-staging" \
  --resource-group "ai-profile-photo-maker-staging" \
  --query "instrumentationKey"

# Verify monitoring endpoints
curl https://aiprofilephotomakerapi-staging.azurewebsites.net/health
```

## 🔄 Rollback Procedures

### 🚨 Emergency Rollback Scenarios

#### Scenario 1: Application Deployment Failure

**Quick Rollback**:
```bash
# Revert to previous deployment slot (if configured)
az webapp deployment slot swap \
  --name "aiprofilephotomakerapi-staging" \
  --resource-group "ai-profile-photo-maker-staging" \
  --slot "staging" \
  --target-slot "production"

# Or redeploy previous known-good version
gh workflow run "🚀 Master Deployment Pipeline" \
  --field deployment_type=application-only \
  --ref "PREVIOUS_COMMIT_HASH"
```

#### Scenario 2: Infrastructure Issues

**Infrastructure Rollback**:
```bash
# Option A: Redeploy previous template version
git checkout PREVIOUS_COMMIT
./infrastructure/deploy-local.sh

# Option B: Delete and recreate resource group
az group delete --name "ai-profile-photo-maker-staging" --yes
# Then redeploy from known-good state

# Option C: Use ARM template deployment history
az deployment group list \
  --resource-group "ai-profile-photo-maker-staging" \
  --query "[?properties.provisioningState=='Succeeded'] | [0]"
```

#### Scenario 3: Database Issues

**Database Recovery**:
```bash
# Restore from automated backup
az sql db restore \
  --dest-name "aiprofilephotomakerdb-restored" \
  --server "aiprofilephotomaker-sql-staging-[unique]" \
  --resource-group "ai-profile-photo-maker-staging" \
  --source-database "aiprofilephotomakerdb" \
  --time "2025-07-30T10:00:00"

# Update connection string to restored database
az keyvault secret set \
  --vault-name "aiprofilephotomaker-kv-staging-[unique]" \
  --name "DatabaseConnectionString" \
  --value "RESTORED_CONNECTION_STRING"
```

### 📋 Rollback Checklist

**Pre-Rollback**:
- [ ] Document the issue and rollback reason
- [ ] Notify stakeholders about the rollback
- [ ] Backup current state before rollback
- [ ] Identify the last known-good configuration

**During Rollback**:
- [ ] Execute rollback procedures
- [ ] Monitor rollback progress
- [ ] Validate services are restored
- [ ] Test critical functionality

**Post-Rollback**:
- [ ] Confirm all services operational
- [ ] Update monitoring and alerts
- [ ] Document lessons learned
- [ ] Plan fix for original issue

## 📈 Post-Deployment Actions

### 🔍 Immediate Verification

**Health Checks**:
```bash
# Check web app health
curl https://aiprofilephotomakerapi-staging.azurewebsites.net/health

# Check database connectivity
az sql db show-connection-string \
  --server "aiprofilephotomaker-sql-staging-[unique]" \
  --name "aiprofilephotomakerdb" \
  --client ado.net

# Test storage account access
az storage blob list \
  --container-name "profile-images" \
  --account-name "aiprofilephotomakersto[unique]"
```

### 🚀 Application Deployment

**Get Static Web App Token**:
```bash
# Get deployment token for frontend
az staticwebapp secrets list \
  --name "aiprofilephotomaker-swa-staging" \
  --resource-group "ai-profile-photo-maker-staging" \
  --query "properties.apiKey" \
  --output tsv
```

**Deploy Applications**:
```bash
# Deploy backend API
gh workflow run "🚀 Master Deployment Pipeline" \
  --field deployment_type=application-only \
  --field target_environment=staging

# Or deploy applications separately
gh workflow run "Deploy Application" \
  --field environment=staging
```

### 📊 Monitoring Setup

**Configure Alerts**:
```bash
# Create deployment success notification
az monitor activity-log alert create \
  --name "deployment-success-staging" \
  --resource-group "ai-profile-photo-maker-staging" \
  --condition category=Administrative and operationName="Microsoft.Resources/deployments/write" and level=Informational \
  --action-group YOUR_ACTION_GROUP_ID

# Set up health check monitoring
az monitor log-analytics query \
  --workspace "aiprofilephotomaker-la-staging" \
  --analytics-query "requests | where url contains '/health' | summarize count() by bin(timestamp, 5m)"
```

### 🔐 Security Hardening

**Post-Deployment Security**:
```bash
# Verify HTTPS redirection
curl -I http://aiprofilephotomakerapi-staging.azurewebsites.net
# Should return 301/302 redirect to HTTPS

# Check Key Vault access policies
az keyvault show \
  --name "aiprofilephotomaker-kv-staging-[unique]" \
  --query "properties.accessPolicies[].permissions"

# Verify SQL Server firewall rules
az sql server firewall-rule list \
  --server "aiprofilephotomaker-sql-staging-[unique]" \
  --resource-group "ai-profile-photo-maker-staging"
```

## 💰 Cost Management

### 📊 Cost Monitoring

**Expected Monthly Costs**:

**Staging Environment (~$50-100/month)**:
- App Service Plan (F1): $0
- SQL Database (Basic): ~$5
- Storage Account: ~$2-5
- Key Vault: ~$0.03/transaction
- Application Insights: ~$2-10
- Static Web App: $0

**Production Environment (~$200-500/month)**:
- App Service Plan (B1): ~$55
- SQL Database (S0): ~$15
- Storage Account: ~$10-20
- Key Vault: ~$1-5
- Application Insights: ~$10-50
- Static Web App: $0

**Cost Monitoring Commands**:
```bash
# Get current month costs
az consumption usage list \
  --start-date "2025-07-01" \
  --end-date "2025-07-31" \
  --query "[?contains(instanceName, 'aiprofilephotomaker')]"

# Set up cost alerts
az consumption budget create \
  --resource-group "ai-profile-photo-maker-staging" \
  --budget-name "staging-monthly-budget" \
  --amount 100 \
  --time-grain Monthly
```

## 📞 Support & Escalation

### 🆘 Emergency Contacts

**Escalation Path**:
1. **Level 1**: Development Team
2. **Level 2**: DevOps/Infrastructure Team  
3. **Level 3**: Azure Support (if required)

### 📋 Issue Reporting Template

**For deployment issues, provide**:
- Environment (staging/production)
- Deployment method used
- Error messages and logs
- Steps to reproduce
- Expected vs actual behavior
- Rollback performed (yes/no)

### 🔗 Useful Resources

**Documentation**:
- [Azure CLI Reference](https://docs.microsoft.com/cli/azure/)
- [Bicep Documentation](https://docs.microsoft.com/azure/azure-resource-manager/bicep/)
- [GitHub Actions Documentation](https://docs.github.com/actions)

**Monitoring Links**:
- [Azure Portal](https://portal.azure.com)
- [Application Insights](https://portal.azure.com/#@/resource/subscriptions/SUB_ID/resourcegroups/ai-profile-photo-maker-staging/providers/microsoft.insights/components/aiprofilephotomaker-ai-staging)
- [GitHub Actions](https://github.com/YOUR_USERNAME/AI.ProfilePhotoMaker/actions)

---

## 🎯 Quick Reference Commands

### Fast Deployment
```bash
# GitHub Actions (recommended)
gh workflow run "🚀 Master Deployment Pipeline" --field deployment_type=full --field target_environment=staging

# Local deployment (fast)
cd infrastructure && ./deploy-local.sh

# Python SDK (CLI alternative)
python3 infrastructure/deploy_azure_sdk.py
```

### Monitoring
```bash
# Check deployment status
az deployment group list --resource-group "ai-profile-photo-maker-staging" --query "[0].properties.provisioningState"

# Health check
curl https://aiprofilephotomakerapi-staging.azurewebsites.net/health

# Resource list
az resource list --resource-group "ai-profile-photo-maker-staging" --output table
```

### Troubleshooting
```bash
# Re-authenticate
az logout && az login

# Check permissions
az role assignment list --assignee $(az account show --query user.name -o tsv)

# Validate template
bicep build infrastructure/main.bicep --outfile /tmp/validation.json
```

---

**Status**: ✅ **READY FOR EXECUTION**  
**Last Updated**: July 30, 2025  
**Next Review**: August 30, 2025