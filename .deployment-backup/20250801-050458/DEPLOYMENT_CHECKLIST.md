# Azure Deployment Checklist

## Pre-Deployment Validation ✅

### 1. Parameter File Validation
- [ ] JSON syntax is valid
- [ ] namePrefix is "aiprofile" (not "aiapp" or "aiprofilephotomaker")
- [ ] namePrefix length ≤ 14 characters for storage account naming
- [ ] environmentName is "staging" or "production"
- [ ] SQL password meets complexity requirements (8+ chars, uppercase, lowercase, number, special char)
- [ ] All required parameters are present and valid

**Validation Command**: `./scripts/validate-parameters.sh parameters.staging.standardized.json`

### 2. Template Validation
- [ ] Bicep template syntax is valid
- [ ] Template builds successfully to ARM JSON
- [ ] No syntax errors or warnings
- [ ] Resource dependencies are correctly defined

**Validation Command**: `az bicep build --file main.bicep`

### 3. Naming Convention Check
- [ ] No conflicting "aiapp" pattern resources exist
- [ ] No problematic "aiprofilephotomaker" pattern resources exist
- [ ] Expected resource names are available or safe to update
- [ ] Storage account name will be ≤24 characters

**Validation Command**: `./scripts/check-naming-conflicts.sh ai-profile-photo-maker-staging aiprofile staging`

### 4. Azure Deployment Validation
- [ ] Azure CLI is logged in with correct subscription
- [ ] Resource group exists and is accessible
- [ ] User has Contributor or Owner permissions
- [ ] Template passes Azure deployment validation
- [ ] No Azure policy violations

**Validation Command**: `./scripts/validate-deployment.sh`

## Deployment Execution ✅

### 5. Pre-Deployment Backup
- [ ] Current resource inventory captured
- [ ] Existing SQL databases backed up (if any)
- [ ] Storage account data backed up (if any)
- [ ] Key Vault secrets documented (if updating)

**Backup Command**: `./scripts/azure-resource-audit.sh`

### 6. Deployment Process
- [ ] Deployment executed with monitoring
- [ ] All resources created/updated successfully
- [ ] No deployment errors or warnings
- [ ] Deployment outputs captured

**Deployment Command**: `./scripts/deploy-standardized.sh`

### 7. Post-Deployment Verification
- [ ] All expected resources are present
- [ ] Web App is running and accessible
- [ ] SQL Server and database are accessible
- [ ] Storage account and containers are configured
- [ ] Key Vault secrets are accessible to applications
- [ ] Application Insights is collecting data
- [ ] Static Web App is deployed and accessible

**Verification Command**: Built into deployment script

## Post-Deployment Cleanup ✅

### 8. Duplicate Resource Cleanup
- [ ] Old "aiapp" pattern resources identified
- [ ] Data migration completed (if needed)
- [ ] Duplicate resources safely deleted
- [ ] Resource cleanup verified

**Cleanup Command**: `./scripts/azure-resource-cleanup.sh`

### 9. Application Testing
- [ ] Frontend application loads correctly
- [ ] API endpoints respond correctly
- [ ] Database connectivity verified
- [ ] Image upload/processing works
- [ ] Authentication flows work
- [ ] All integrations functional

### 10. Monitoring and Alerts
- [ ] Application Insights is receiving telemetry
- [ ] Log Analytics workspace is collecting logs
- [ ] No critical errors in application logs
- [ ] Performance metrics are within acceptable ranges

## Security Verification ✅

### 11. Security Configuration
- [ ] HTTPS is enforced on all web applications
- [ ] TLS 1.2 minimum is configured
- [ ] SQL Server firewall rules are appropriate
- [ ] Key Vault access policies are configured correctly
- [ ] Storage account access is properly configured
- [ ] No sensitive data in configuration files

### 12. Access Control
- [ ] Web App managed identity is configured
- [ ] Key Vault access permissions are minimal and appropriate
- [ ] SQL Server authentication is working
- [ ] Storage account access is secure

## Documentation Updates ✅

### 13. Project Documentation
- [ ] README updated with new resource names
- [ ] Deployment instructions updated
- [ ] Architecture diagrams updated (if needed)
- [ ] API documentation updated with new URLs

### 14. Team Communication
- [ ] Team notified of infrastructure changes
- [ ] New URLs communicated to stakeholders
- [ ] Any breaking changes documented
- [ ] Support documentation updated

## Rollback Preparedness ✅

### 15. Rollback Plan
- [ ] Backup locations documented
- [ ] Rollback procedures tested
- [ ] Emergency contact information available
- [ ] Rollback scripts ready if needed

---

## Quick Commands Reference

```bash
# Full validation and deployment workflow
./scripts/validate-deployment.sh
./scripts/deploy-standardized.sh
./scripts/azure-resource-cleanup.sh

# Individual validation steps
./scripts/validate-parameters.sh parameters.staging.standardized.json
./scripts/check-naming-conflicts.sh ai-profile-photo-maker-staging aiprofile staging
az bicep build --file main.bicep

# Monitoring and verification
az resource list --resource-group ai-profile-photo-maker-staging --output table
az deployment group list --resource-group ai-profile-photo-maker-staging --output table
```

## Emergency Procedures

If deployment fails:
1. Check deployment logs for specific errors
2. Verify all prerequisites are met
3. Ensure Azure CLI is logged into correct subscription
4. Check for resource quotas or policy restrictions
5. Contact Azure support if needed

If rollback is needed:
1. Stop any running deployments
2. Restore from backups using provided scripts
3. Verify application functionality
4. Investigate root cause before retry
