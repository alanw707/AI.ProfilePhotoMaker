---
title: "System Architecture: Azure SQL Authentication Deployment Guide"
system_id: "AIPM-AUTH-DEPLOY-001"
complexity: "medium"
status: "ready"
architectural_patterns:
  - "deployment-automation"
  - "zero-downtime-deployment"
  - "configuration-management"
scalability_metrics:
  current_capacity: "Production Ready"
  target_capacity: "10K users"
  scaling_approach: "horizontal"
technology_stack:
  - backend: "ASP.NET Core 8.0"
  - database: "Azure SQL Database"
  - deployment: "Azure Container Apps"
  - automation: "Bash Scripts"
design_timeline:
  start: "2025-08-10T08:21:45Z"
  review: "2025-08-10T09:00:00Z"
  completion: "2025-08-10T10:00:00Z"
linked_documents:
  - path: "fix-azure-sql-auth.sh"
  - path: "Services/Database/EnhancedDatabaseProviderService.cs"
dependencies:
  - system: "azure-container-apps"
    type: "external"
  - system: "azure-sql-database"
    type: "external"
quality_attributes:
  - attribute: "availability"
    priority: "critical"
  - attribute: "security"
    priority: "high"
  - attribute: "performance"
    priority: "high"
---

# Azure SQL Authentication Fix - Deployment Guide

## Executive Summary

This guide provides step-by-step instructions to resolve the Azure SQL Database authentication failures in production. The solution has been architected to provide immediate relief while establishing a foundation for future improvements.

## Current Issue

**Error**: Login failed for user 'aipmadmin' (SQL Error 18456, State: 1)
**Root Cause**: Username mismatch in connection configuration
**Impact**: Container Apps cannot connect to database, causing service unavailability

## Solution Architecture

### Components Deployed
1. **EnhancedDatabaseProviderService**: Improved connection string handling with multiple fallback mechanisms
2. **Enhanced Retry Logic**: Azure-specific transient error handling
3. **Connection Pool Optimization**: Better resource utilization
4. **Deployment Automation**: Script for zero-downtime deployment

## Deployment Instructions

### Prerequisites
- Azure CLI installed and authenticated
- Docker installed locally
- Access to Azure subscription with appropriate permissions
- SQL Admin password for the database

### Step 1: Pull Latest Changes

```bash
cd /home/alanw/projects/AI.ProfilePhotoMaker
git pull origin main
```

### Step 2: Review Configuration

Verify the following values match your Azure environment:
- Resource Group: `aiprofilemaker-v1`
- Container App: `aipm-api-v1`
- SQL Server: `aipm-sql-v1-6j74jubocuukg`
- SQL Database: `aipmdb`
- SQL User: `sqladmin` (NOT aipmadmin)

### Step 3: Execute Deployment Script

```bash
cd /home/alanw/projects/AI.ProfilePhotoMaker
./fix-azure-sql-auth.sh
```

The script will:
1. Verify SQL Server configuration
2. Update firewall rules
3. Test SQL authentication
4. Update Container App secrets
5. Build and deploy new application image
6. Monitor deployment status
7. Test health endpoints

### Step 4: Manual Verification

After the script completes, verify the deployment:

```bash
# Check Container App status
az containerapp show \
  --name aipm-api-v1 \
  --resource-group aiprofilemaker-v1 \
  --query "properties.runningStatus" -o tsv

# Test health endpoints
curl -s https://aipm-api-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io/api/health/live
curl -s https://aipm-api-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io/api/health/ready

# View recent logs
az containerapp logs show \
  --name aipm-api-v1 \
  --resource-group aiprofilemaker-v1 \
  --tail 50
```

## Troubleshooting

### Issue: Script Fails at SQL Connection Test

**Solution**: Verify SQL admin credentials
```bash
# Test from Azure Cloud Shell
sqlcmd -S aipm-sql-v1-6j74jubocuukg.database.windows.net \
       -d aipmdb \
       -U sqladmin \
       -P '<your-password>' \
       -Q 'SELECT 1'
```

### Issue: Container App Not Starting

**Solution**: Check environment variables
```bash
az containerapp show \
  --name aipm-api-v1 \
  --resource-group aiprofilemaker-v1 \
  --query "properties.template.containers[0].env[]" \
  -o table
```

### Issue: Health Endpoints Timeout

**Solution**: Check database connectivity
```bash
# View container logs for connection errors
az containerapp logs show \
  --name aipm-api-v1 \
  --resource-group aiprofilemaker-v1 \
  --grep "connection" \
  --tail 100
```

## Architecture Improvements Implemented

### 1. Connection String Resolution Hierarchy
```
Priority 1: Container Apps Environment Variable
    ↓ (fallback)
Priority 2: Configuration with Password Injection
    ↓ (fallback)
Priority 3: Component-based Construction
    ↓ (fallback)
Priority 4: Local Development Mode
```

### 2. Enhanced Retry Logic
- Handles Azure-specific transient errors
- Exponential backoff with jitter
- Maximum 5 retry attempts
- 30-second maximum delay

### 3. Connection Pool Optimization
```csharp
MinPoolSize: 5     // Maintain warm connections
MaxPoolSize: 100   // Prevent exhaustion
Lifetime: 300      // Recycle after 5 minutes
```

## Monitoring and Validation

### Key Metrics to Monitor
1. **Health Endpoint Response Time**: Should be < 5 seconds
2. **Container CPU Usage**: Should be < 50% average
3. **Database Connection Pool**: Monitor active connections
4. **Error Rate**: Should be < 1%

### Dashboard Commands
```bash
# Monitor real-time metrics
az monitor metrics list \
  --resource /subscriptions/{sub-id}/resourceGroups/aiprofilemaker-v1/providers/Microsoft.App/containerApps/aipm-api-v1 \
  --metric "UsageNanoCores" \
  --interval PT1M

# Check application insights (if configured)
az monitor app-insights query \
  --app aipm-ai-v1 \
  --analytics-query "requests | where timestamp > ago(1h) | summarize avg(duration) by bin(timestamp, 5m)"
```

## Rollback Procedure

If issues persist after deployment:

```bash
# List all revisions
az containerapp revision list \
  --name aipm-api-v1 \
  --resource-group aiprofilemaker-v1 \
  -o table

# Activate previous working revision
az containerapp revision activate \
  --name <previous-revision-name> \
  --app aipm-api-v1 \
  --resource-group aiprofilemaker-v1
```

## Future Improvements (Phase 2)

### Migrate to Managed Identity
1. Enable System-Assigned Managed Identity
2. Create database user for managed identity
3. Update connection string to use AAD authentication
4. Remove password from configuration

### Implementation Timeline
- Week 1: Test in staging environment
- Week 2: Implement gradual rollout
- Week 3: Complete migration
- Week 4: Remove SQL authentication

## Security Considerations

### Current State
- SQL authentication with strong passwords
- Secrets stored in Container App secrets (encrypted)
- Connection strings not logged
- Firewall rules restrict access

### Recommendations
1. Rotate SQL password quarterly
2. Implement Azure Key Vault integration
3. Enable SQL audit logging
4. Monitor failed authentication attempts

## Success Criteria Checklist

- [ ] Container App running without crashes
- [ ] Health endpoints responding < 5 seconds
- [ ] No authentication errors in logs
- [ ] Database queries executing successfully
- [ ] Minimum 1 replica always running
- [ ] Connection pool metrics normal
- [ ] Error rate < 1%
- [ ] Response times < 500ms p95

## Support Information

### Key Files Modified
- `/AI.ProfilePhotoMaker.API/Services/Database/EnhancedDatabaseProviderService.cs`
- `/AI.ProfilePhotoMaker.API/Extensions/DatabaseServiceExtensions.cs`
- `/fix-azure-sql-auth.sh`

### Related Documentation
- [Azure SQL Authentication Architecture](./azure-sql-authentication-architecture-2025-08-10-081522.md)
- [Azure Container Apps Documentation](https://docs.microsoft.com/azure/container-apps)
- [SQL Connection Troubleshooting](https://docs.microsoft.com/azure/sql-database/troubleshoot-connectivity)

### Contact for Issues
- Architecture Team: Review architecture decisions
- DevOps Team: Deployment script support
- Database Team: SQL Server configuration

## Conclusion

This deployment guide provides a complete solution to resolve the Azure SQL authentication issues. The enhanced architecture provides improved resilience, better monitoring, and a clear path to future improvements with Managed Identity.

The solution has been designed to:
1. Provide immediate relief with minimal changes
2. Improve system resilience with retry logic
3. Enhance observability with detailed logging
4. Establish foundation for future security improvements

Execute the deployment script and follow the verification steps to restore service availability.