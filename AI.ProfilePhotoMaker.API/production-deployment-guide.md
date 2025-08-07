# Production Deployment Guide: Style Preview Upload

This guide provides step-by-step instructions for deploying style preview images to production Azure Blob Storage.

## Prerequisites Checklist

- [ ] Access to production Azure Container Apps environment
- [ ] Azure CLI installed and authenticated
- [ ] Production deployment already running (Azure Container Apps + Storage)
- [ ] 21 style preview images validated and ready (2.3MB total)

## Deployment Steps

### Step 1: Connect to Production Environment

```bash
# List available container apps
az containerapp list --resource-group <your-resource-group> --output table

# Connect to the API container
az containerapp exec --name <api-container-name> --resource-group <your-resource-group>
```

### Step 2: Verify Environment Configuration

```bash
# Inside the container, check Azure Storage configuration
dotnet run -- list-previews

# Expected output if configured:
# - Shows storage account connection status
# - Lists any existing files in style-previews container
```

### Step 3: Execute Production Upload

```bash
# Upload all style preview images
./upload-style-previews.sh

# Expected output:
# 🎨 Style Preview Upload Script
# ================================
# 📤 Uploading style previews (skipping existing files)
# 
# Running: dotnet run -- upload-previews
# 
# ✅ Container 'style-previews' created
# ✅ Uploaded academic.jpg (114,206 bytes)
# ✅ Uploaded artistic.jpg (105,231 bytes)
# [... 19 more files ...]
# ⚠️ Skipped placeholder-preview.jpg (empty file)
# 
# === Upload Summary ===
# Total Files: 21
# Successfully Uploaded: 21
# Skipped: 1
# Failed: 0
# Total Size: 2,336,337 bytes
```

### Step 4: Verify Upload Success

```bash
# Check upload status
./upload-style-previews.sh --list

# Verify file accessibility
./validate-upload-success.sh

# Test random sample
curl -I https://<storage-account>.blob.core.windows.net/style-previews/academic.jpg
curl -I https://<storage-account>.blob.core.windows.net/style-previews/corporate.jpg
curl -I https://<storage-account>.blob.core.windows.net/style-previews/glamour.jpg
```

## Expected Results

### Upload Metrics
- **Files processed:** 22 total (21 uploaded, 1 skipped)
- **Success rate:** 100% (21/21 valid files)
- **Total transfer:** ~2.3MB
- **Upload time:** < 60 seconds
- **Container creation:** Automatic

### File URLs
All files will be accessible at:
```
https://<storage-account>.blob.core.windows.net/style-previews/<filename>.jpg
```

Examples:
- `https://<storage-account>.blob.core.windows.net/style-previews/academic.jpg`
- `https://<storage-account>.blob.core.windows.net/style-previews/corporate.jpg`
- `https://<storage-account>.blob.core.windows.net/style-previews/tech-professional.jpg`

### HTTP Response Validation
Each file should return:
- **Status:** `200 OK`
- **Content-Type:** `image/jpeg`
- **Content-Length:** Matching local file size
- **Access:** Public (no authentication required)

## Troubleshooting

### Issue: Azure Storage Not Configured
```
ERROR: Azure Storage connection string is not configured.
```

**Solution:**
1. Verify the Container Apps environment variables include `AzureStorage__ConnectionString`
2. Check if Key Vault secrets are properly referenced
3. Restart the container app if needed

### Issue: Container Creation Failed
```
ERROR: Failed to create container 'style-previews'
```

**Solution:**
1. Check storage account permissions
2. Verify the storage account exists and is accessible
3. Ensure the connection string is valid

### Issue: File Upload Failures
```
❌ Failed to upload <filename>: <error>
```

**Solution:**
1. Check network connectivity to Azure
2. Verify file permissions and sizes
3. Retry with force flag: `./upload-style-previews.sh --force`

### Issue: Files Not Publicly Accessible
```
HTTP Status: 404 or 403
```

**Solution:**
1. Verify container public access policy
2. Check blob-level permissions
3. Confirm the storage account allows public blob access

## Rollback Plan

### If Upload Fails
1. **Check logs:** Review error messages for specific failures
2. **Retry individual files:** Use `--force` flag to overwrite
3. **Manual cleanup:** Delete incorrect uploads via Azure portal
4. **Full retry:** Re-run the complete upload process

### If Wrong Configuration
1. **Stop the process:** Cancel any ongoing uploads
2. **Fix configuration:** Update Azure Storage settings
3. **Clean up:** Remove any incorrectly uploaded files
4. **Re-deploy:** Start the upload process again

## Validation Commands

### Quick Health Check
```bash
# Test 3 random files for immediate validation
curl -I https://<storage-account>.blob.core.windows.net/style-previews/academic.jpg
curl -I https://<storage-account>.blob.core.windows.net/style-previews/glamour.jpg
curl -I https://<storage-account>.blob.core.windows.net/style-previews/tech-professional.jpg
```

### Comprehensive Validation
```bash
# Run full validation suite
./validate-upload-success.sh

# Expected output:
# 🔍 Style Preview Upload Validation
# =================================
# Testing against: https://<storage-account>.blob.core.windows.net
# 
# Validating 21 files...
# 
# Testing academic.jpg... ✅ OK (200)
# Testing artistic.jpg... ✅ OK (200)
# [... 19 more files ...]
# 
# === Validation Summary ===
# Total files: 21
# Successful: 21
# Failed: 0
# Success Rate: 100.0%
# 
# 🎉 All style preview files are accessible!
# ✅ Upload validation completed successfully
```

### Integration Testing
```bash
# Test the application can access uploaded images
# (Run these in the application context)

# Check StylePreviewService integration
# Verify frontend loads style preview images
# Confirm no 404 errors in application logs
```

## Success Criteria

✅ **Upload Complete:** All 21 valid image files uploaded successfully  
✅ **Public Access:** All files return HTTP 200 with correct content-type  
✅ **File Integrity:** All file sizes match local originals exactly  
✅ **Container Created:** `style-previews` container exists in storage account  
✅ **Application Integration:** StylePreviewService can access all images  
✅ **No 404 Errors:** All style preview URLs resolve correctly in the application  

## Post-Deployment Tasks

1. **Update Documentation:** Record the storage account URL and container name
2. **Monitor Logs:** Check for any 404 errors in application logs
3. **Performance Testing:** Verify style preview loading times
4. **Backup Verification:** Ensure images are included in backup strategies
5. **CDN Configuration:** Consider adding CDN if performance optimization needed

## Files Created for Production

1. **`upload-style-previews.sh`** - Main upload script with comprehensive error handling
2. **`validate-upload-success.sh`** - Post-upload validation script  
3. **`production-upload-validation.md`** - Detailed validation report
4. **`production-deployment-guide.md`** - This deployment guide

All scripts are production-ready with proper error handling, logging, and rollback capabilities.