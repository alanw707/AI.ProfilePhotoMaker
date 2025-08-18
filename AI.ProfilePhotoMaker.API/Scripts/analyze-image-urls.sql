-- =====================================================
-- Production Image URL Analysis Script
-- =====================================================
-- Purpose: Analyze current database state for image URL migration
-- Run this script to understand scope before migration
-- =====================================================

PRINT '========================================='
PRINT 'PRODUCTION IMAGE URL MIGRATION ANALYSIS'
PRINT '========================================='
PRINT ''

-- Overall database statistics
PRINT '1. OVERALL DATABASE STATISTICS'
PRINT '-----------------------------------------'

SELECT 
    'Total ProcessedImage Records' as Metric,
    COUNT(*) as Count,
    CAST(COUNT(*) as VARCHAR(20)) as DisplayValue
FROM ProcessedImages

UNION ALL

SELECT 
    'Records with OriginalImageUrl' as Metric,
    COUNT(*) as Count,
    CAST(COUNT(*) as VARCHAR(20)) as DisplayValue
FROM ProcessedImages 
WHERE OriginalImageUrl IS NOT NULL AND OriginalImageUrl != ''

UNION ALL

SELECT 
    'Records with ProcessedImageUrl' as Metric,
    COUNT(*) as Count,
    CAST(COUNT(*) as VARCHAR(20)) as DisplayValue
FROM ProcessedImages 
WHERE ProcessedImageUrl IS NOT NULL AND ProcessedImageUrl != ''

ORDER BY Count DESC;

PRINT ''
PRINT '2. MIGRATION SCOPE ANALYSIS'
PRINT '-----------------------------------------'

-- Records requiring migration
SELECT 
    'Relative OriginalImageUrl Paths' as Category,
    COUNT(*) as Count,
    CAST(ROUND(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM ProcessedImages WHERE OriginalImageUrl IS NOT NULL), 2) as VARCHAR(10)) + '%' as Percentage
FROM ProcessedImages 
WHERE OriginalImageUrl IS NOT NULL 
    AND OriginalImageUrl NOT LIKE 'http%'
    AND OriginalImageUrl NOT LIKE 'https%'

UNION ALL

SELECT 
    'Relative ProcessedImageUrl Paths' as Category,
    COUNT(*) as Count,
    CAST(ROUND(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM ProcessedImages WHERE ProcessedImageUrl IS NOT NULL), 2) as VARCHAR(10)) + '%' as Percentage
FROM ProcessedImages 
WHERE ProcessedImageUrl IS NOT NULL 
    AND ProcessedImageUrl NOT LIKE 'http%'
    AND ProcessedImageUrl NOT LIKE 'https%'

UNION ALL

SELECT 
    'Already Correct URLs (OriginalImageUrl)' as Category,
    COUNT(*) as Count,
    CAST(ROUND(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM ProcessedImages WHERE OriginalImageUrl IS NOT NULL), 2) as VARCHAR(10)) + '%' as Percentage
FROM ProcessedImages 
WHERE OriginalImageUrl IS NOT NULL 
    AND (OriginalImageUrl LIKE 'http%' OR OriginalImageUrl LIKE 'https%')

UNION ALL

SELECT 
    'Already Correct URLs (ProcessedImageUrl)' as Category,
    COUNT(*) as Count,
    CAST(ROUND(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM ProcessedImages WHERE ProcessedImageUrl IS NOT NULL), 2) as VARCHAR(10)) + '%' as Percentage
FROM ProcessedImages 
WHERE ProcessedImageUrl IS NOT NULL 
    AND (ProcessedImageUrl LIKE 'http%' OR ProcessedImageUrl LIKE 'https%')

ORDER BY Count DESC;

PRINT ''
PRINT '3. PATH PATTERN ANALYSIS'
PRINT '-----------------------------------------'

-- Common path patterns for relative URLs
SELECT TOP 10
    CASE 
        WHEN OriginalImageUrl LIKE '/%' THEN LEFT(OriginalImageUrl, CHARINDEX('/', OriginalImageUrl + '/', 2) - 1)
        ELSE 'Other'
    END as PathPattern,
    COUNT(*) as Count,
    'OriginalImageUrl' as UrlType
FROM ProcessedImages 
WHERE OriginalImageUrl IS NOT NULL 
    AND OriginalImageUrl NOT LIKE 'http%'
    AND OriginalImageUrl NOT LIKE 'https%'
GROUP BY 
    CASE 
        WHEN OriginalImageUrl LIKE '/%' THEN LEFT(OriginalImageUrl, CHARINDEX('/', OriginalImageUrl + '/', 2) - 1)
        ELSE 'Other'
    END

UNION ALL

SELECT TOP 10
    CASE 
        WHEN ProcessedImageUrl LIKE '/%' THEN LEFT(ProcessedImageUrl, CHARINDEX('/', ProcessedImageUrl + '/', 2) - 1)
        ELSE 'Other'
    END as PathPattern,
    COUNT(*) as Count,
    'ProcessedImageUrl' as UrlType
FROM ProcessedImages 
WHERE ProcessedImageUrl IS NOT NULL 
    AND ProcessedImageUrl NOT LIKE 'http%'
    AND ProcessedImageUrl NOT LIKE 'https%'
GROUP BY 
    CASE 
        WHEN ProcessedImageUrl LIKE '/%' THEN LEFT(ProcessedImageUrl, CHARINDEX('/', ProcessedImageUrl + '/', 2) - 1)
        ELSE 'Other'
    END

ORDER BY Count DESC;

PRINT ''
PRINT '4. SAMPLE RECORDS REQUIRING MIGRATION'
PRINT '-----------------------------------------'

-- Show sample records that need migration
SELECT TOP 10
    Id,
    OriginalImageUrl,
    ProcessedImageUrl,
    Style,
    UserProfileId,
    CreatedAt,
    CASE 
        WHEN OriginalImageUrl IS NOT NULL AND OriginalImageUrl NOT LIKE 'http%' THEN 'YES' 
        ELSE 'NO' 
    END as NeedsOriginalConversion,
    CASE 
        WHEN ProcessedImageUrl IS NOT NULL AND ProcessedImageUrl NOT LIKE 'http%' THEN 'YES' 
        ELSE 'NO' 
    END as NeedsProcessedConversion
FROM ProcessedImages
WHERE (OriginalImageUrl IS NOT NULL AND OriginalImageUrl NOT LIKE 'http%')
   OR (ProcessedImageUrl IS NOT NULL AND ProcessedImageUrl NOT LIKE 'http%')
ORDER BY CreatedAt DESC;

PRINT ''
PRINT '5. USER IMPACT ANALYSIS'
PRINT '-----------------------------------------'

-- Users affected by migration
SELECT 
    'Users with Images Requiring Migration' as Metric,
    COUNT(DISTINCT UserProfileId) as Count
FROM ProcessedImages
WHERE (OriginalImageUrl IS NOT NULL AND OriginalImageUrl NOT LIKE 'http%')
   OR (ProcessedImageUrl IS NOT NULL AND ProcessedImageUrl NOT LIKE 'http%')

UNION ALL

SELECT 
    'Total Users with Images' as Metric,
    COUNT(DISTINCT UserProfileId) as Count
FROM ProcessedImages
WHERE (OriginalImageUrl IS NOT NULL AND OriginalImageUrl != '')
   OR (ProcessedImageUrl IS NOT NULL AND ProcessedImageUrl != '');

PRINT ''
PRINT '6. TIMELINE ANALYSIS'
PRINT '-----------------------------------------'

-- When were problematic records created
SELECT 
    CAST(CreatedAt as DATE) as CreatedDate,
    COUNT(*) as RecordsNeedingMigration,
    COUNT(DISTINCT UserProfileId) as UsersAffected
FROM ProcessedImages
WHERE (OriginalImageUrl IS NOT NULL AND OriginalImageUrl NOT LIKE 'http%')
   OR (ProcessedImageUrl IS NOT NULL AND ProcessedImageUrl NOT LIKE 'http%')
GROUP BY CAST(CreatedAt as DATE)
ORDER BY CreatedDate DESC;

PRINT ''
PRINT '7. MIGRATION READINESS SUMMARY'
PRINT '-----------------------------------------'

DECLARE @TotalRecords INT = (SELECT COUNT(*) FROM ProcessedImages)
DECLARE @RelativeOriginal INT = (
    SELECT COUNT(*) FROM ProcessedImages 
    WHERE OriginalImageUrl IS NOT NULL AND OriginalImageUrl NOT LIKE 'http%'
)
DECLARE @RelativeProcessed INT = (
    SELECT COUNT(*) FROM ProcessedImages 
    WHERE ProcessedImageUrl IS NOT NULL AND ProcessedImageUrl NOT LIKE 'http%'
)
DECLARE @TotalAffected INT = (
    SELECT COUNT(*) FROM ProcessedImages
    WHERE (OriginalImageUrl IS NOT NULL AND OriginalImageUrl NOT LIKE 'http%')
       OR (ProcessedImageUrl IS NOT NULL AND ProcessedImageUrl NOT LIKE 'http%')
)

SELECT 
    'Migration Readiness Summary' as Assessment,
    CASE 
        WHEN @TotalAffected = 0 THEN '✅ NO MIGRATION NEEDED'
        WHEN @TotalAffected < 100 THEN '🟡 SMALL MIGRATION (< 100 records)'
        WHEN @TotalAffected < 1000 THEN '🟠 MEDIUM MIGRATION (< 1000 records)'
        ELSE '🔴 LARGE MIGRATION (1000+ records)'
    END as MigrationSize,
    CAST(@TotalAffected as VARCHAR(10)) + ' / ' + CAST(@TotalRecords as VARCHAR(10)) as AffectedRecords,
    CAST(ROUND(@TotalAffected * 100.0 / @TotalRecords, 2) as VARCHAR(10)) + '%' as AffectedPercentage,
    CASE 
        WHEN @TotalAffected = 0 THEN 'No action required'
        WHEN @TotalAffected < 100 THEN 'Can migrate immediately'
        WHEN @TotalAffected < 1000 THEN 'Schedule maintenance window'
        ELSE 'Plan carefully, use batch processing'
    END as Recommendation;

PRINT ''
PRINT '8. NEXT STEPS'
PRINT '-----------------------------------------'

IF @TotalAffected > 0
BEGIN
    PRINT 'Migration is required. Recommended steps:'
    PRINT '1. Create database backup'
    PRINT '2. Test migration on staging environment'
    PRINT '3. Run API dry-run: POST /api/migration/dry-run-image-migration'
    PRINT '4. Execute migration: POST /api/migration/execute-image-migration?confirmed=true'
    PRINT '5. Validate results: POST /api/migration/validate-migration'
    PRINT '6. Re-enable auto-repair functionality'
    PRINT ''
    PRINT 'Estimated processing time: ' + CAST(@TotalAffected * 0.1 as VARCHAR(10)) + ' seconds'
END
ELSE
BEGIN
    PRINT '✅ All image URLs are already in correct format!'
    PRINT 'No migration required.'
END

PRINT ''
PRINT '========================================='
PRINT 'ANALYSIS COMPLETE'
PRINT '========================================='