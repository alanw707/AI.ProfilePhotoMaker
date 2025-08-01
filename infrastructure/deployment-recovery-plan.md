# 🚀 Azure Deployment Recovery Plan

## Current Issues Analysis

### ❌ Failed Deployments Root Causes
1. **Redis Cache Drift** - Template includes Redis but original design doesn't need it
2. **Key Vault Missing** - Critical security component absent from deployments  
3. **Metric Name Errors** - Using deprecated metric names `ResponseTime` → `HttpResponseTime`
4. **Key Vault URI Truncation** - Name too long causing malformed URIs
5. **App Service Plan SKU Conflicts** - Concurrent updates to SKU properties

### 📊 Resource Status (13 → 10 Target)
- ✅ **10 Essential Resources** - Core infrastructure working
- ⚠️ **3 Problematic Resources** - Need cleanup/fix
- ❌ **1 Missing Critical** - Key Vault completely absent

## 🛠️ Recovery Steps

### Phase 1: Infrastructure Cleanup
```bash
# Run cleanup script
chmod +x infrastructure/cleanup-and-redeploy.sh
./infrastructure/cleanup-and-redeploy.sh
```

### Phase 2: Template Corrections Applied
- ✅ Fixed Key Vault naming (prevent URI truncation)
- ✅ Corrected metric names (`HttpResponseTime`)
- ✅ Removed Redis Cache references
- ✅ Improved resource naming consistency

### Phase 3: Redeployment Strategy

#### Option A: Full Clean Redeploy (RECOMMENDED)
```bash
# Delete entire resource group and start fresh
az group delete --name "ai-profile-photo-maker-staging" --yes --no-wait

# Wait for deletion (5-10 minutes)
az group show --name "ai-profile-photo-maker-staging" || echo "Group deleted"

# Redeploy with corrected template
az deployment group create \
  --resource-group "ai-profile-photo-maker-staging" \
  --template-file infrastructure/main.json \
  --parameters @infrastructure/parameters.staging.json \
  --name "clean-deploy-$(date +%Y%m%d-%H%M%S)"
```

#### Option B: Incremental Fix (FASTER)
```bash
# Fix existing deployment issues incrementally
az deployment group create \
  --resource-group "ai-profile-photo-maker-staging" \
  --template-file infrastructure/main.json \
  --parameters @infrastructure/parameters.staging.json \
  --mode Incremental \
  --name "fix-deploy-$(date +%Y%m%d-%H%M%S)"
```

### Phase 4: Validation Checklist
- [ ] Key Vault created and accessible
- [ ] Web App can access Key Vault secrets
- [ ] All 10 essential resources running
- [ ] No problematic resources remain
- [ ] Monitoring alerts properly configured
- [ ] Application deployment pipeline functional

## 🔧 GitHub Actions Workflow Fixes

### Current Workflow Issues
1. **ARM Template Validation** disabled due to API issues
2. **Multiple retry logic** causing deployment conflicts
3. **Output parsing failures** preventing pipeline success

### Recommended Workflow Updates
- Simplify deployment logic (remove excessive retries)
- Fix output parsing using TSV format
- Enable proper error handling without circular retries
- Implement proper rollback mechanisms

## 🚨 Emergency Protocols

### If Deployment Still Fails
1. **Manual Resource Creation**
   - Create Key Vault manually via portal
   - Configure access policies for Web App
   - Update connection strings manually

2. **Simplified Template Approach**
   - Deploy core resources only (App Service, SQL, Storage)
   - Add monitoring and alerts in separate deployment
   - Incremental complexity addition

3. **Switch to ARM Template**
   - Use generated ARM template directly
   - Bypass Bicep compilation issues
   - Direct Azure CLI deployment

## 📋 Success Metrics
- ✅ All deployments succeed without retries
- ✅ Application can access all required services
- ✅ Monitoring and alerting functional
- ✅ No orphaned or problematic resources
- ✅ GitHub Actions pipeline green

## 🎯 Next Steps Priority
1. **IMMEDIATE**: Run cleanup script to remove problematic resources
2. **HIGH**: Redeploy with corrected Bicep template
3. **MEDIUM**: Update GitHub Actions workflow for reliability
4. **LOW**: Implement monitoring dashboard and alerts optimization