-- Targeted cleanup for temporary enhanced upload records accidentally persisted to the gallery
-- Keeps final enhanced outputs (e.g., Style like 'Enhanced-%')

-- Preview candidates
SELECT 
    pi.Id,
    pi.Style,
    pi.IsGenerated,
    pi.IsOriginalUpload,
    pi.OriginalImageUrl,
    pi.ProcessedImageUrl,
    pi.CreatedAt,
    up.UserId
FROM ProcessedImages pi
JOIN UserProfiles up ON pi.UserProfileId = up.Id
WHERE 
    -- Temp enhanced uploads were saved with plain 'Enhanced' style
    (pi.Style = 'Enhanced' OR pi.Style = '')
    AND (pi.OriginalImageUrl IS NULL OR pi.OriginalImageUrl = '')
    AND (pi.ProcessedImageUrl LIKE '%/enhanced/%' OR pi.ProcessedImageUrl LIKE '%\\enhanced\\%')
ORDER BY pi.CreatedAt DESC;

-- Count
SELECT COUNT(*) AS TempEnhancedUploadCount
FROM ProcessedImages pi
WHERE 
    (pi.Style = 'Enhanced' OR pi.Style = '')
    AND (pi.OriginalImageUrl IS NULL OR pi.OriginalImageUrl = '')
    AND (pi.ProcessedImageUrl LIKE '%/enhanced/%' OR pi.ProcessedImageUrl LIKE '%\\enhanced\\%');

-- To DELETE the above records, uncomment the transaction block
/*
BEGIN TRANSACTION;

DELETE pi
FROM ProcessedImages pi
WHERE 
    (pi.Style = 'Enhanced' OR pi.Style = '')
    AND (pi.OriginalImageUrl IS NULL OR pi.OriginalImageUrl = '')
    AND (pi.ProcessedImageUrl LIKE '%/enhanced/%' OR pi.ProcessedImageUrl LIKE '%\\enhanced\\%');

SELECT @@ROWCOUNT AS DeletedRecords;

-- COMMIT; -- Uncomment to commit
ROLLBACK;   -- Default to rollback to be safe
*/

