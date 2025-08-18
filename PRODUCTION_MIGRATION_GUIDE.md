# Production Image URL Migration Guide

## Overview

This guide provides a comprehensive plan to migrate existing database records from relative image paths to full Azure Blob URLs. This addresses the production issue where the database stored relative paths (e.g., `/prod/uploads/...`) instead of full Azure URLs, causing 404 errors and triggering incorrect auto-repair deletion.

## Problem Summary

- **Root Cause**: Database stored relative paths instead of full Azure Blob URLs
- **Impact**: Images display as 404 errors, trigger auto-repair deletion
- **Recent Fix**: New uploads now save full URLs (commit 6ddaa70)
- **Remaining Issue**: Existing data still has relative paths

## Pre-Migration Checklist

### ✅ Database Backup
```sql
-- Create backup before migration
BACKUP DATABASE [YourDatabase] 
TO DISK = 'C:\Backups\YourDatabase_PreMigration_YYYYMMDD.bak'
WITH FORMAT, INIT, COMPRESSION;
```

### ✅ Validate Current State
Run analysis to understand scope:
```bash
# Using PowerShell script
./AI.ProfilePhotoMaker.API/Scripts/execute-image-url-migration.ps1 -DryRun

# Or using API directly
curl -X GET "https://your-api.azurewebsites.net/api/migration/analyze-image-urls"
```

### ✅ Environment Prerequisites
- Admin role access to API
- Azure Storage service operational
- StorageService properly configured
- Low-traffic maintenance window scheduled

## Migration Execution Plan

### Phase 1: Analysis and Validation

#### Step 1.1: Database Analysis
```bash
# Analyze current database state
GET /api/migration/analyze-image-urls
```

**Expected Output:**
- Total records count
- Records with relative paths
- Estimated processing time
- Sample data showing conversion examples

#### Step 1.2: Dry Run Validation
```bash
# Validate migration logic without changes
POST /api/migration/dry-run-image-migration
```

**Validation Checks:**
- ✅ All relative paths can be converted
- ✅ Converted URLs are valid Azure Blob URLs
- ✅ Storage files exist for converted paths
- ✅ No validation errors found

### Phase 2: Migration Execution

#### Step 2.1: Execute Migration
```bash
# Execute actual migration
POST /api/migration/execute-image-migration?confirmed=true
```

**Safety Features:**
- Batch processing (50 records per batch)
- Progress tracking and logging
- Automatic rollback script generation
- Validation of each URL conversion

#### Step 2.2: Monitor Progress
- Check application logs for batch completion
- Monitor database connection usage
- Watch for any error notifications

### Phase 3: Validation and Re-enablement

#### Step 3.1: Validate Migration Success
```bash
# Verify all URLs converted successfully
POST /api/migration/validate-migration
```

#### Step 3.2: Re-enable Auto-repair (if successful)
Update UI services to re-enable auto-repair functionality:

```typescript
// dashboard-state.service.ts
// image-state.service.ts
// Remove or modify the disabled auto-repair logic
```

## Migration Implementation Details

### Code Structure

1. **ProductionImageUrlMigration.cs**: Core migration logic
   - Batch processing for performance
   - URL validation and conversion
   - Progress tracking and error handling

2. **MigrationController.cs**: API endpoints
   - Analysis endpoint for assessment
   - Dry-run endpoint for validation  
   - Execution endpoint with safety checks
   - Validation endpoint for verification

3. **execute-image-url-migration.ps1**: Automated script
   - Complete migration workflow
   - Interactive confirmations
   - Progress reporting and error handling

### Safety Mechanisms

#### Validation Logic
```csharp
// Convert relative path to Azure URL
var convertedUrl = _storageService.GetImageUrl(relativePath);

// Validate converted URL exists in storage
var exists = await _storageService.ExistsAsync(convertedUrl);
if (!exists) {
    throw new ValidationException($"Converted URL not accessible: {convertedUrl}");
}
```

#### Batch Processing
- Process 50 records per batch
- 1 second delay every 10 batches
- Individual record error handling
- Progress tracking per batch

#### Rollback Capability
Automatic generation of rollback SQL script:
```sql
-- Generated rollback script
UPDATE ProcessedImages SET OriginalImageUrl = '/prod/uploads/original.jpg' WHERE Id = 123;
UPDATE ProcessedImages SET ProcessedImageUrl = '/prod/uploads/processed.jpg' WHERE Id = 123;
```

## Execution Options

### Option 1: PowerShell Script (Recommended)
```bash
# Development/Testing
./execute-image-url-migration.ps1 -ApiBaseUrl "https://localhost:5032" -DryRun

# Production
./execute-image-url-migration.ps1 -ApiBaseUrl "https://your-api.azurewebsites.net"

# Automated (use with caution)
./execute-image-url-migration.ps1 -ApiBaseUrl "https://your-api.azurewebsites.net" -Force
```

### Option 2: API Endpoints
```bash
# 1. Analysis
curl -X GET "https://your-api.azurewebsites.net/api/migration/analyze-image-urls" \
  -H "Authorization: Bearer YOUR_TOKEN"

# 2. Dry Run
curl -X POST "https://your-api.azurewebsites.net/api/migration/dry-run-image-migration" \
  -H "Authorization: Bearer YOUR_TOKEN"

# 3. Execute
curl -X POST "https://your-api.azurewebsites.net/api/migration/execute-image-migration?confirmed=true" \
  -H "Authorization: Bearer YOUR_TOKEN"

# 4. Validate
curl -X POST "https://your-api.azurewebsites.net/api/migration/validate-migration" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

### Option 3: Manual SQL (Emergency Only)
```sql
-- Get SQL scripts for manual execution
GET /api/migration/sql-scripts

-- CAUTION: Bypasses validation and rollback features
-- Only use if API approach fails
```

## Risk Assessment and Mitigation

### High Risk Scenarios
- **Database corruption**: Mitigated by mandatory backup requirement
- **Wrong URL conversion**: Mitigated by dry-run validation
- **Storage access issues**: Mitigated by URL existence validation
- **Partial migration failure**: Mitigated by batch processing and rollback scripts

### Medium Risk Scenarios  
- **Performance impact**: Mitigated by batch processing with delays
- **Concurrent access issues**: Recommend low-traffic window
- **Memory usage**: Mitigated by processing in small batches

### Low Risk Scenarios
- **API timeout**: Each batch is independent, migration can resume
- **Rollback requirement**: Automatic rollback script generation

## Testing Strategy

### Pre-Production Testing
1. **Staging Environment**: Execute full migration on staging data
2. **Backup Restore Test**: Verify backup/restore process works
3. **Image Display Test**: Validate images load correctly post-migration
4. **Auto-repair Test**: Test auto-repair functionality after re-enablement

### Production Validation
1. **Spot Check**: Manually verify sample URLs work
2. **Dashboard Test**: Check user dashboard image display
3. **Upload Test**: Verify new uploads still work correctly
4. **Delete Test**: Test image deletion functionality

## Rollback Procedures

### Automatic Rollback (Preferred)
```sql
-- Use generated rollback script
-- Execute the SQL script saved during migration
```

### Manual Rollback (Backup Restore)
```sql
-- Restore from pre-migration backup
RESTORE DATABASE [YourDatabase] 
FROM DISK = 'C:\Backups\YourDatabase_PreMigration_YYYYMMDD.bak'
WITH REPLACE;
```

### Partial Rollback (Selective)
```sql
-- Rollback specific records if needed
UPDATE ProcessedImages 
SET OriginalImageUrl = 'original_relative_path'
WHERE Id IN (SELECT Id FROM FailedMigrationIds);
```

## Monitoring and Alerts

### During Migration
- Database connection count
- API response times
- Error rate monitoring
- Storage service availability

### Post-Migration
- Image loading success rate
- 404 error reduction
- User dashboard functionality
- Auto-repair activation status

## Performance Expectations

### Database Impact
- **Processing Speed**: ~100ms per record
- **Batch Size**: 50 records per batch
- **Memory Usage**: Minimal (streaming processing)
- **Connection Usage**: 1 connection per batch

### Estimated Timeline
```
1,000 records  = ~2 minutes
5,000 records  = ~10 minutes  
10,000 records = ~20 minutes
50,000 records = ~100 minutes
```

## Support and Troubleshooting

### Common Issues

#### Migration Fails Validation
- **Symptom**: Dry run shows validation errors
- **Cause**: Storage files missing for some paths
- **Solution**: Review storage consistency, clean up orphaned records first

#### Partial Migration Success
- **Symptom**: Some records fail to migrate
- **Cause**: Storage access issues or malformed paths
- **Solution**: Review error logs, fix storage issues, re-run migration

#### Performance Issues
- **Symptom**: Migration takes too long
- **Cause**: Database load or storage latency
- **Solution**: Increase batch delays, schedule during off-peak hours

### Escalation Path
1. **Application Logs**: Check detailed error messages
2. **Database Logs**: Review for connection or performance issues
3. **Storage Logs**: Verify Azure Storage accessibility
4. **Rollback**: Use generated rollback script if issues persist

## Post-Migration Tasks

### Immediate (0-1 hours)
- [ ] Validate sample images display correctly
- [ ] Check dashboard functionality
- [ ] Monitor error rates
- [ ] Test new image uploads

### Short-term (1-24 hours)
- [ ] Re-enable auto-repair functionality
- [ ] Monitor auto-repair behavior
- [ ] Validate no false positives in deletion
- [ ] User acceptance testing

### Long-term (1-7 days)
- [ ] Monitor overall system health
- [ ] Track image loading performance
- [ ] Collect user feedback
- [ ] Archive rollback scripts

## Success Criteria

### Technical Success
- ✅ 100% of relative paths converted to Azure URLs
- ✅ All converted URLs accessible and valid
- ✅ No data loss during migration
- ✅ Migration completed within estimated timeframe

### Functional Success
- ✅ Images display correctly in dashboard
- ✅ Image upload functionality unaffected
- ✅ Auto-repair functionality works correctly
- ✅ No false positive deletions

### User Success
- ✅ No user-visible service disruption
- ✅ Improved image loading reliability
- ✅ Resolved 404 errors
- ✅ Maintained all user data integrity

---

**Contact Information:**
- Technical Lead: [Your Contact]
- Database Administrator: [DBA Contact]  
- Infrastructure Team: [Infrastructure Contact]

**Emergency Contacts:**
- On-call Engineer: [Phone/Slack]
- Escalation Manager: [Phone/Email]