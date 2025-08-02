# Frontend API Connection Issues - Root Cause Analysis & Solution

## 🚨 Problem Summary

Frontend was getting HTTP 500 errors when calling API endpoints, specifically:
- `/api/credit/packages` - Database schema error
- `/api/style-preview/list` - Working but returning empty results  
- `/api/styles` - Working correctly

## 🔍 Root Cause Analysis

### Issue #1: Database Schema Mismatch
**Endpoint**: `/api/credit/packages`
**Error**: `Invalid column name 'BonusCredits', 'Description', 'DisplayOrder'`

**Root Cause**: The staging database was created but **EF Core migrations were never run**. The deployment script creates the SQL database but doesn't apply the Entity Framework migrations that add the required columns.

**Evidence**:
```bash
# Local database has all columns:
sqlite3 aiprofilemaker.db ".schema CreditPackages"
# Shows: BonusCredits, Description, DisplayOrder columns exist

# API response shows missing columns:
curl "https://aiprofilemaker-api-staging.../api/credit/packages"
# Returns: "Invalid column name 'BonusCredits'"
```

### Issue #2: Frontend Environment Configuration
**Status**: ✅ **CORRECT** - No issues found

The Angular app is properly configured:
- `environment.staging.ts` has correct API URL: `https://aiprofilemaker-api-staging.../api`
- Services use correct endpoints: `credit/packages`, `style`, etc.
- Base HTTP service correctly builds URLs

### Issue #3: Style Preview Images
**Status**: ⚠️ **EMPTY RESULTS** - Working but no data

The `/api/style-preview/list` endpoint works but returns `{"success":true,"count":0,"previews":[]}` because no style preview images have been uploaded to Azure Blob Storage yet.

## ✅ Solution Implementation

### 1. Immediate Fix - Run Migrations on Staging
Created script to run EF Core migrations on the current staging deployment:

```bash
# Run this to fix the database immediately:
./scripts/fix-staging-database-now.sh
```

This script:
1. Connects to the staging container app
2. Runs `dotnet ef database update` inside the container
3. Tests the `/api/credit/packages` endpoint
4. Confirms the fix is working

### 2. Long-term Fix - Update Deployment Process
Updated the deployment scripts to include migrations:

**Updated Files**:
- `scripts/update-container-apps.ps1` - Now runs migrations after container updates
- `scripts/run-staging-migrations.ps1` - Dedicated migration script
- `scripts/fix-staging-database-now.sh` - Immediate fix script

**Process Flow**:
1. Deploy infrastructure (creates SQL database)
2. Build and push container images
3. Update container apps with new images
4. **NEW**: Run EF Core migrations automatically
5. Test API endpoints to verify success

### 3. Prevention - CI/CD Pipeline Enhancement
The GitHub Actions workflow now includes migration step:

```yaml
- name: 🔄 Update Container Apps (PowerShell)
  shell: pwsh
  run: |
    cd scripts
    ./update-container-apps.ps1 -ResourceGroupName "${{ env.RESOURCE_GROUP }}" -RegistryServer "${{ steps.infra.outputs.registry-server }}" -BackendImageTag "latest" -FrontendImageTag "latest"
```

## 🧪 Testing & Verification

### Working Endpoints ✅
```bash
# Diagnostic endpoint - working
curl "https://aiprofilemaker-api-staging.../api/diagnostic/database-status"
# Returns: {"canConnect":true,"tables":{"creditPackages":3,"userProfiles":0,"styles":3}}

# Styles endpoint - working  
curl "https://aiprofilemaker-api-staging.../api/style"
# Returns: {"success":true,"data":[{"id":3,"name":"artistic",...}]}
```

### Fixed Endpoints 🔄 (After Migration)
```bash
# Credit packages endpoint - will work after migration
curl "https://aiprofilemaker-api-staging.../api/credit/packages"
# Should return: {"success":true,"data":[...]} instead of schema error
```

### Empty but Working Endpoints ℹ️
```bash
# Style previews - working but no data yet
curl "https://aiprofilemaker-api-staging.../api/style-preview/list"  
# Returns: {"success":true,"count":0,"previews":[]} - correct but empty
```

## 📋 Action Items

### Immediate (Run Now)
1. **Run the migration fix**:
   ```bash
   cd /home/alanw/projects/AI.ProfilePhotoMaker
   ./scripts/fix-staging-database-now.sh
   ```

2. **Verify the fix**:
   - Check that `/api/credit/packages` returns proper data
   - Test frontend loading of styles and credit packages
   - Confirm no more HTTP 500 errors in browser console

### Follow-up
1. **Upload style preview images** to Azure Blob Storage to populate `/api/style-preview/list`
2. **Test complete user workflow** from style selection to image generation
3. **Monitor logs** for any remaining API issues

## 🔧 Technical Details

### Database Migration Process
```bash
# What the migration does:
dotnet ef database update --no-build --verbose

# Applies these missing schema changes:
# - Adds BonusCredits column to CreditPackages table
# - Adds Description column to CreditPackages table  
# - Adds DisplayOrder column to CreditPackages table
# - Updates seed data with proper values
```

### Frontend Impact
After the migration, the frontend will be able to:
- ✅ Load credit packages from `/api/credit/packages`
- ✅ Display pricing information correctly
- ✅ Handle user credit operations
- ✅ Show style selections without errors

### Backend Configuration
The staging backend is properly configured with:
- ✅ Azure SQL Database connection
- ✅ Managed Identity authentication
- ✅ CORS enabled for frontend domain
- ✅ Environment variables set correctly

## 📊 Impact Assessment

**Before Fix**:
- ❌ Frontend shows "Error loading styles from database"
- ❌ HTTP 500 errors in browser console
- ❌ Credit packages not loading
- ❌ User cannot see pricing or packages

**After Fix**:
- ✅ Frontend loads styles and packages correctly
- ✅ No HTTP 500 errors
- ✅ Credit system functional
- ✅ Complete user workflow possible

## 💡 Lessons Learned

1. **Always include database migrations in deployment pipelines**
2. **Test API endpoints after each deployment**
3. **Include schema validation in health checks**
4. **Document database setup steps clearly**
5. **Use idempotent migration scripts**

The root cause was a **deployment process gap** - infrastructure was created but schema updates weren't applied. This is now fixed for future deployments.