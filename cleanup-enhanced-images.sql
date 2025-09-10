-- Script to check and clean up enhanced photo records from database
-- First, let's see what enhanced images exist

-- Check current enhanced images
SELECT 
    pi.Id,
    pi.Style,
    pi.IsGenerated,
    pi.IsOriginalUpload,
    pi.ProcessedImageUrl,
    pi.CreatedAt,
    up.UserId
FROM ProcessedImages pi
JOIN UserProfiles up ON pi.UserProfileId = up.Id
WHERE pi.Style LIKE '%Enhanced%' 
   OR (pi.IsGenerated = 1 AND pi.IsOriginalUpload = 0 AND pi.Style != 'Original')
ORDER BY pi.CreatedAt DESC;

-- Count of enhanced images
SELECT 
    COUNT(*) as EnhancedImageCount,
    COUNT(CASE WHEN pi.ProcessedImageUrl IS NOT NULL THEN 1 END) as WithProcessedUrl
FROM ProcessedImages pi
WHERE pi.Style LIKE '%Enhanced%' 
   OR (pi.IsGenerated = 1 AND pi.IsOriginalUpload = 0 AND pi.Style != 'Original');

-- Uncomment the following lines to DELETE enhanced images
-- WARNING: This will permanently remove enhanced image records!

/*
BEGIN TRANSACTION;

-- Delete enhanced images
DELETE pi 
FROM ProcessedImages pi
WHERE pi.Style LIKE '%Enhanced%' 
   OR (pi.IsGenerated = 1 AND pi.IsOriginalUpload = 0 AND pi.Style != 'Original');

-- Show count of deleted records
SELECT @@ROWCOUNT as DeletedRecords;

-- Uncomment COMMIT to apply changes, otherwise ROLLBACK
-- COMMIT;
ROLLBACK;
*/