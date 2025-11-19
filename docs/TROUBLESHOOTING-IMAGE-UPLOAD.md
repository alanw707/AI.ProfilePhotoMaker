# 🔧 Troubleshooting Guide: Image Upload 404 Errors in Production

> Status: Targeted troubleshooting note for a resolved production issue. Keep for historical context; storage behavior is documented more generally in `PRIVATE_BLOB_STORAGE.md` and `infrastructure-validation.md`.

## Problem Summary

**Issue**: Uploaded images show 404 errors in production environment while working correctly in development.

**Symptoms**:
- Images upload successfully but display as broken images
- Console shows 404 errors for URLs like: `https://app.aiprofilephotomaker.com/profile-images/prod/uploads/{uuid}`
- Works perfectly in development environment

**Root Cause**: Production is using `LocalStorageService` instead of `AzureBlobStorageService` due to missing Azure Storage environment variables.

## Technical Analysis

### 🔍 Storage Service Selection Logic

The application chooses storage service in `Program.cs:327-349`:

```csharp
var azureStorageConnectionString = builder.Configuration.GetConnectionString("AzureStorage") ?? 
                                  builder.Configuration["AzureStorage:ConnectionString"];

if (!string.IsNullOrEmpty(azureStorageConnectionString))
{
    builder.Services.AddScoped<IStorageService, AzureBlobStorageService>();
    Console.WriteLine("Using Azure Blob Storage for image storage");
}
else
{
    builder.Services.AddScoped<IStorageService, LocalStorageService>();
    Console.WriteLine("Using Local Storage for image storage (DEPRECATED)");
}
```

### 🚨 Why LocalStorageService Fails in Production

1. **LocalStorageService** generates web server URLs: `/profile-images/prod/uploads/...`
2. **Containerized environments** can't serve files from local filesystem paths
3. **AzureBlobStorageService** generates direct Azure Blob URLs: `https://{storage}.blob.core.windows.net/...`
4. **Azure Blob URLs** work universally across all environments

### 📊 URL Pattern Analysis

| Storage Service | URL Pattern | Production Compatible |
|-----------------|-------------|----------------------|
| LocalStorageService | `/profile-images/prod/uploads/...` | ❌ No - 404 errors |
| AzureBlobStorageService | `https://{storage}.blob.core.windows.net/...` | ✅ Yes - Direct access |

## 🔧 Solution

### Step 1: Verify Current Infrastructure Status

```bash
# Check current deployment status
az deployment group list --resource-group aiprofilemaker-v1 --query "[0].properties.provisioningState"

# Check container app environment variables
az containerapp show --name aipm-api-v1 --resource-group aiprofilemaker-v1 --query "properties.configuration.secrets[].name"
```

### Step 2: Force Infrastructure Redeploy

The Bicep template correctly creates Azure Storage environment variables, but the current deployment may not have them:

```bash
# Trigger a fresh deployment to ensure environment variables are set
git commit --allow-empty -m "Force redeploy to fix Azure Storage environment variables"
git push origin main
```

### Step 3: Verify Environment Variables Post-Deployment

After deployment completes, verify the container app has the required environment variables:

```bash
# Check environment variables in container app
az containerapp show --name aipm-api-v1 --resource-group aiprofilemaker-v1 \
  --query "properties.template.containers[0].env[?name=='AZURE_STORAGE_CONNECTION_STRING' || name=='AZURE_STORAGE_CONTAINER_NAME']"
```

Expected output should show both:
- `AZURE_STORAGE_CONNECTION_STRING`
- `AZURE_STORAGE_CONTAINER_NAME`

### Step 4: Manual Environment Variable Fix (If Needed)

If the automatic deployment doesn't work, manually set the environment variables:

```bash
# Get storage account details
STORAGE_ACCOUNT=$(az storage account list --resource-group aiprofilemaker-v1 --query "[0].name" -o tsv)
STORAGE_KEY=$(az storage account keys list --account-name $STORAGE_ACCOUNT --resource-group aiprofilemaker-v1 --query "[0].value" -o tsv)

# Set environment variables manually
az containerapp update --name aipm-api-v1 --resource-group aiprofilemaker-v1 \
  --set-env-vars \
  "AZURE_STORAGE_CONNECTION_STRING=DefaultEndpointsProtocol=https;AccountName=$STORAGE_ACCOUNT;AccountKey=$STORAGE_KEY;EndpointSuffix=core.windows.net" \
  "AZURE_STORAGE_CONTAINER_NAME=profile-images"
```

### Step 5: Restart Container App

```bash
# Restart container app to pick up new environment variables
az containerapp restart --name aipm-api-v1 --resource-group aiprofilemaker-v1
```

### Step 6: Validate Fix

Run the E2E test to validate the fix:

```bash
# Run Playwright test to validate image upload flow
npx playwright test tests/e2e/image-upload-validation.spec.js --headed
```

Or manually test:
1. Upload an image in production
2. Verify the generated URL pattern
3. Check image accessibility

## 🧪 Testing & Validation

### Playwright E2E Test

The provided test in `tests/e2e/image-upload-validation.spec.js` validates:

1. **Upload Flow**: Complete image upload process
2. **URL Pattern**: Detects storage service type from URL structure
3. **Accessibility**: Verifies images are actually accessible
4. **Environment Validation**: Ensures production uses Azure Blob Storage

### Expected Results After Fix

**Before Fix (LocalStorageService)**:
```
🔍 Detected image URL: https://app.aiprofilephotomaker.com/profile-images/prod/uploads/abc123.jpg
📊 URL Analysis: { storageType: 'local', containerized: false }
🌐 Image accessibility: ❌ Not accessible (Status: 404)
```

**After Fix (AzureBlobStorageService)**:
```
🔍 Detected image URL: https://aipmstv1xyz.blob.core.windows.net/profile-images/prod/uploads/abc123.jpg
📊 URL Analysis: { storageType: 'azure', containerized: true }
🌐 Image accessibility: ✅ Accessible (Status: 200)
```

## 🚀 Prevention Measures

### 1. Enhanced Deployment Pipeline Validation

**✅ IMPLEMENTED**: The deployment pipeline now includes comprehensive storage validation:

#### Post-Deployment Storage Validation
- **Environment Variable Validation**: Checks for correct .NET naming patterns
- **Storage Account Verification**: Confirms Azure Storage account exists
- **Container Validation**: Verifies profile-images container is created
- **Health Check Integration**: Uses `/api/health/storage` endpoint for validation

#### Pre-Deployment Infrastructure Validation
- **Bicep Template Analysis**: Cross-references environment variables with application code
- **Naming Pattern Detection**: Identifies common misconfigurations (e.g., `AZURE_STORAGE_CONNECTION_STRING` vs `ConnectionStrings__AzureStorage`)
- **Secret Validation**: Comprehensive GitHub Actions secrets validation

#### Critical Storage Service Configuration Validation
The pipeline now detects and reports:
- ✅ **AzureBlobStorage**: Production correctly configured
- ❌ **LocalStorage**: Critical error - will cause 404s in production
- ⚠️ **Unknown Provider**: Unexpected configuration requiring investigation

**Location**: `.github/workflows/simple-deploy.yml` (lines 607-894)

### 2. Enhanced Application Health Monitoring

**✅ IMPLEMENTED**: Comprehensive health check system with storage-specific monitoring:

#### Storage Health Endpoints
- **`/api/health/storage`**: Detailed storage service health with provider type, connectivity, and operation tests
- **`/api/health/comprehensive`**: System-wide health including storage component status
- **`/api/health/performance`**: Performance metrics with storage-specific monitoring

#### Real-Time Storage Configuration Detection
The health system now provides:
- **Provider Type**: AzureBlobStorage, LocalStorage, or Azurite detection
- **Connection String Analysis**: Validates Azure Storage connection string format
- **Operation Testing**: Upload, download, delete, and exists operation validation
- **Configuration Details**: Account name, emulator detection, path information

#### Application Startup Validation
Enhanced logging in `Program.cs:327-349` now includes:
```
Using Azure Blob Storage for image storage     // ✅ Correct for production
Using Local Storage for image storage (DEPRECATED)  // ❌ Problem in production
```

**Location**: `AI.ProfilePhotoMaker.API/Services/Health/StorageHealthService.cs`

### 3. Comprehensive E2E Testing Suite

**✅ IMPLEMENTED**: Production-ready E2E testing with storage configuration validation:

#### Enhanced Image Upload Validation Tests
- **Pre-flight Health Checks**: Validates storage service before test execution
- **Retry Logic**: Robust login and upload procedures with automatic retry
- **URL Pattern Analysis**: Detects Azure Blob vs Local Storage vs Azurite patterns
- **Accessibility Validation**: Confirms uploaded images are accessible via HTTP requests

#### Storage Configuration Issue Detection
- **Provider Type Validation**: Ensures production uses AzureBlobStorage
- **Environment-Specific Validation**: Different expectations for production vs development
- **Configuration Report Generation**: Detailed analysis with issues and recommendations
- **Health Endpoint Integration**: Tests all storage health endpoints

#### Multi-Environment Test Support
- **Production Validation**: Validates live production environment
- **Staging Validation**: Tests staging environment with same rigor
- **Cross-Browser Testing**: Firefox and Chrome validation
- **Mobile Testing**: iOS device emulation testing

#### Test Artifacts and Reporting
- **Comprehensive Reports**: HTML, JSON, and markdown test reports
- **Storage Validation Reports**: Specific storage configuration analysis
- **Screenshot and Video Capture**: Visual debugging for failed tests
- **Test Metadata Tracking**: Detailed test execution information

**Location**: `tests/e2e/image-upload-validation.spec.js` with `playwright.config.js`

**Usage**: 
```bash
# Run production validation
npx playwright test --project=production-validation

# Run with visual debugging
npx playwright test --headed --project=production-validation
```

### 4. Monitoring & Alerting

Enhanced monitoring capabilities:
- **Health Endpoint Monitoring**: Monitor `/api/health/storage` for storage service type
- **404 Error Rate Tracking**: Track image URL accessibility failures
- **Storage Performance Metrics**: Monitor upload/download operation success rates
- **Configuration Drift Detection**: Alert when storage provider type changes unexpectedly

## 📋 Implementation Status

### ✅ COMPLETED SOLUTIONS

- [x] **Infrastructure Fix**: Corrected Bicep template environment variable naming (ConnectionStrings__AzureStorage)
- [x] **Production Deployment**: Fixed Azure Storage connection string parsing error
- [x] **Pipeline Validation**: Added comprehensive post-deployment storage validation  
- [x] **Health Monitoring**: Implemented comprehensive storage health check system
- [x] **E2E Testing**: Created production-ready automated validation tests
- [x] **Documentation**: Complete troubleshooting guide with prevention measures

### 🎯 VERIFICATION CHECKLIST

- [x] **Bicep Template**: Azure Storage environment variables properly configured
- [x] **GitHub Actions Secrets**: All required secrets validated pre-deployment
- [x] **Environment Variables**: Container app receives correct .NET configuration patterns
- [x] **Storage Service Type**: Production confirmed using AzureBlobStorage
- [x] **Health Endpoints**: All storage health endpoints operational
- [x] **E2E Tests**: Automated tests validate complete upload flow
- [x] **Image Accessibility**: Uploaded images accessible via Azure Blob URLs
- [x] **Pipeline Integration**: Deployment pipeline validates storage configuration

### 🔄 ONGOING MONITORING

- **Health Endpoint**: Monitor `/api/health/storage` for storage service type
- **Application Logs**: Watch for "Using Azure Blob Storage" confirmation
- **Error Rates**: Track 404 errors on image URLs (should be zero)
- **E2E Tests**: Run automated validation tests regularly

## 🔍 Common Issues

### Issue: Deployment completes but environment variables still missing

**Solution**: Manual environment variable configuration (Step 4 above)

### Issue: Environment variables present but app still uses LocalStorageService

**Possible Causes**:
1. Container app hasn't restarted
2. Environment variable format incorrect
3. Connection string contains invalid characters

**Solution**: Restart container app and validate connection string format

### Issue: Azure Storage account doesn't exist

**Solution**: Redeploy infrastructure from scratch:
```bash
# Delete and recreate resource group
az group delete --name aiprofilemaker-v1 --yes
# Trigger deployment to recreate everything
git push origin main --force-with-lease
```

## 📞 Support

If issues persist:
1. Check container app logs: `az containerapp logs show --name aipm-api-v1 --resource-group aiprofilemaker-v1`
2. Validate Azure Storage account exists: `az storage account list --resource-group aiprofilemaker-v1`
3. Test storage connectivity manually: Use Azure Storage Explorer or portal

## 🎯 Success Metrics

After implementing the fix:
- [ ] Zero 404 errors on image URLs in production
- [ ] Application logs show "Using Azure Blob Storage for image storage"
- [ ] E2E tests pass consistently
- [ ] Generated image URLs use `blob.core.windows.net` domain
- [ ] Images load instantly without proxy/middleware delays
