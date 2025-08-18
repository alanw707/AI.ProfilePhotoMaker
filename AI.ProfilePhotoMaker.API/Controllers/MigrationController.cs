using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Database.Migrations;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AI.ProfilePhotoMaker.API.Controllers
{
    /// <summary>
    /// Controller for executing production data migrations
    /// </summary>
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    public class MigrationController : BaseController
    {
        private readonly ProductionImageUrlMigration _imageMigration;
        private readonly IWebHostEnvironment _environment;

        public MigrationController(
            ApplicationDbContext context,
            IStorageService storageService,
            ILogger<MigrationController> logger,
            IWebHostEnvironment environment)
            : base(logger, context)
        {
            _imageMigration = new ProductionImageUrlMigration(context, storageService, logger);
            _environment = environment;
        }

        /// <summary>
        /// Analyze current database state to identify records requiring URL migration
        /// </summary>
        [HttpGet("analyze-image-urls")]
        public async Task<IActionResult> AnalyzeImageUrls()
        {
            try
            {
                Logger.LogInformation("Starting image URL migration analysis");

                var analysisResult = await _imageMigration.AnalyzeDatabaseStateAsync();

                var response = new
                {
                    DatabaseState = new
                    {
                        TotalRecords = analysisResult.TotalRecords,
                        RecordsWithRelativeOriginalPaths = analysisResult.RelativeOriginalPaths,
                        RecordsWithRelativeProcessedPaths = analysisResult.RelativeProcessedPaths,
                        EstimatedAffectedRecords = analysisResult.EstimatedAffectedRecords
                    },
                    Impact = new
                    {
                        MigrationRequired = analysisResult.EstimatedAffectedRecords > 0,
                        AffectedPercentage = analysisResult.TotalRecords > 0 
                            ? Math.Round((double)analysisResult.EstimatedAffectedRecords / analysisResult.TotalRecords * 100, 2)
                            : 0.0,
                        EstimatedProcessingTime = TimeSpan.FromSeconds(analysisResult.EstimatedAffectedRecords * 0.1), // ~100ms per record
                        RecommendedBatchSize = 50
                    },
                    SampleData = new
                    {
                        SampleRecords = analysisResult.SampleRelativePaths.Take(5).Select(r => new
                        {
                            r.Id,
                            r.OriginalImageUrl,
                            r.ProcessedImageUrl,
                            r.Style,
                            r.CreatedAt,
                            RequiresOriginalConversion = !string.IsNullOrEmpty(r.OriginalImageUrl) && !r.OriginalImageUrl.StartsWith("http"),
                            RequiresProcessedConversion = !string.IsNullOrEmpty(r.ProcessedImageUrl) && !r.ProcessedImageUrl.StartsWith("http")
                        }),
                        CommonPathPatterns = analysisResult.CommonPathPatterns
                    },
                    Recommendations = analysisResult.EstimatedAffectedRecords > 0 
                        ? new[]
                        {
                            "1. Run dry-run migration first to validate conversion logic",
                            "2. Create database backup before executing migration",
                            "3. Schedule migration during low-traffic period",
                            "4. Monitor application logs during migration",
                            "5. Test image display functionality after migration"
                        }
                        : new[]
                        {
                            "No migration required - all image URLs are already in correct format"
                        },
                    Timestamp = DateTime.UtcNow
                };

                return SuccessResponse(response);
            }
            catch (Exception ex)
            {
                LogError(ex, "Error analyzing image URL migration requirements");
                return ErrorResponse("AnalysisFailed", "Failed to analyze migration requirements", 500);
            }
        }

        /// <summary>
        /// Execute dry-run migration to validate conversion logic without making changes
        /// </summary>
        [HttpPost("dry-run-image-migration")]
        public async Task<IActionResult> DryRunImageMigration()
        {
            try
            {
                Logger.LogInformation("Starting dry-run image URL migration");

                var migrationResult = await _imageMigration.ExecuteMigrationAsync(dryRun: true);

                var response = new
                {
                    DryRun = true,
                    Results = new
                    {
                        Success = migrationResult.Success,
                        TotalRecordsAnalyzed = migrationResult.TotalRecordsToProcess,
                        WouldBeSuccessful = migrationResult.SuccessfullyProcessed,
                        WouldFail = migrationResult.FailedToProcess,
                        SuccessRate = migrationResult.SuccessRate,
                        ProcessingTime = migrationResult.Duration,
                        BatchesProcessed = migrationResult.ProcessedBatches.Count
                    },
                    ValidationIssues = migrationResult.ValidationErrors.Take(10).ToList(), // Show first 10 errors
                    SampleConversions = migrationResult.ProcessedBatches
                        .SelectMany(b => b.Records)
                        .Where(r => r.Success)
                        .Take(5)
                        .Select(r => new
                        {
                            r.Id,
                            OriginalConversion = !string.IsNullOrEmpty(r.NewOriginalImageUrl) 
                                ? new { From = r.OriginalOriginalImageUrl, To = r.NewOriginalImageUrl }
                                : null,
                            ProcessedConversion = !string.IsNullOrEmpty(r.NewProcessedImageUrl)
                                ? new { From = r.OriginalProcessedImageUrl, To = r.NewProcessedImageUrl }
                                : null
                        }),
                    NextSteps = migrationResult.Success && migrationResult.FailedToProcess == 0
                        ? new[]
                        {
                            "✅ Dry run completed successfully",
                            "✅ All records can be converted",
                            "🔄 Ready to execute actual migration",
                            "⚠️ Create database backup before proceeding",
                            "🚀 Call /execute-image-migration endpoint when ready"
                        }
                        : new[]
                        {
                            "❌ Dry run found validation issues",
                            "🔍 Review validation errors above",
                            "🛠️ Fix underlying storage issues before migration",
                            "🔄 Re-run dry-run after fixes"
                        },
                    Timestamp = DateTime.UtcNow
                };

                return SuccessResponse(response);
            }
            catch (Exception ex)
            {
                LogError(ex, "Error during dry-run image URL migration");
                return ErrorResponse("DryRunFailed", "Dry-run migration failed", 500);
            }
        }

        /// <summary>
        /// Execute actual image URL migration (converts relative paths to Azure URLs)
        /// </summary>
        [HttpPost("execute-image-migration")]
        public async Task<IActionResult> ExecuteImageMigration([FromQuery] bool confirmed = false)
        {
            // Safety check - require explicit confirmation
            if (!confirmed)
            {
                return ErrorResponse("ConfirmationRequired", 
                    "Migration requires confirmation. Add ?confirmed=true to execute. " +
                    "Ensure you have a database backup before proceeding.", 400);
            }

            // Additional safety check for non-development environments
            if (!_environment.IsDevelopment())
            {
                Logger.LogWarning("Production image migration requested - requires elevated privileges");
                // In production, you might want additional checks here
            }

            try
            {
                Logger.LogWarning("EXECUTING PRODUCTION IMAGE URL MIGRATION - DATABASE WILL BE MODIFIED");

                var migrationResult = await _imageMigration.ExecuteMigrationAsync(dryRun: false);

                // Generate rollback script
                var rollbackScript = await _imageMigration.GenerateRollbackScriptAsync(migrationResult);

                var response = new
                {
                    Migration = new
                    {
                        Success = migrationResult.Success,
                        TotalRecordsProcessed = migrationResult.TotalRecordsToProcess,
                        SuccessfullyMigrated = migrationResult.SuccessfullyProcessed,
                        Failed = migrationResult.FailedToProcess,
                        SuccessRate = migrationResult.SuccessRate,
                        ProcessingTime = migrationResult.Duration,
                        BatchesProcessed = migrationResult.ProcessedBatches.Count
                    },
                    Results = migrationResult.Success 
                        ? new
                        {
                            Status = "COMPLETED_SUCCESSFULLY",
                            Message = $"Successfully migrated {migrationResult.SuccessfullyProcessed} image URL records",
                            DatabaseUpdated = true,
                            RollbackAvailable = true
                        }
                        : new
                        {
                            Status = "COMPLETED_WITH_ERRORS", 
                            Message = $"Migration completed with {migrationResult.FailedToProcess} failures",
                            DatabaseUpdated = migrationResult.SuccessfullyProcessed > 0,
                            RollbackAvailable = migrationResult.SuccessfullyProcessed > 0
                        },
                    Errors = migrationResult.ValidationErrors.Any() 
                        ? migrationResult.ValidationErrors.Take(10).ToList()
                        : null,
                    RollbackScript = rollbackScript,
                    NextSteps = migrationResult.Success
                        ? new[]
                        {
                            "✅ Migration completed successfully",
                            "🧪 Test image display functionality",
                            "🔄 Re-enable auto-repair functionality if desired",
                            "📊 Monitor application for any image loading issues",
                            "💾 Store rollback script for emergency use"
                        }
                        : new[]
                        {
                            "⚠️ Migration completed with errors",
                            "🔍 Review error details above",
                            "🛠️ Fix remaining issues manually if needed",
                            "🔄 Consider running migration again for failed records",
                            "💾 Store rollback script for successful records"
                        },
                    Timestamp = DateTime.UtcNow
                };

                if (migrationResult.Success)
                {
                    Logger.LogInformation("Image URL migration completed successfully: {Successful} records migrated", 
                        migrationResult.SuccessfullyProcessed);
                }
                else
                {
                    Logger.LogWarning("Image URL migration completed with errors: {Successful} successful, {Failed} failed", 
                        migrationResult.SuccessfullyProcessed, migrationResult.FailedToProcess);
                }

                return SuccessResponse(response);
            }
            catch (Exception ex)
            {
                LogError(ex, "Error during image URL migration execution");
                return ErrorResponse("MigrationFailed", "Migration execution failed", 500);
            }
        }

        /// <summary>
        /// Get SQL scripts for manual migration analysis and execution
        /// </summary>
        [HttpGet("sql-scripts")]
        public async Task<IActionResult> GetSqlScripts()
        {
            try
            {
                var scripts = new
                {
                    AnalysisScript = @"
-- ANALYSIS: Count records with relative paths
SELECT 
    'Total Records' as Category,
    COUNT(*) as Count
FROM ProcessedImages
UNION ALL
SELECT 
    'Relative Original Paths' as Category,
    COUNT(*) as Count  
FROM ProcessedImages 
WHERE OriginalImageUrl IS NOT NULL 
    AND OriginalImageUrl NOT LIKE 'http%'
UNION ALL
SELECT 
    'Relative Processed Paths' as Category,
    COUNT(*) as Count
FROM ProcessedImages 
WHERE ProcessedImageUrl IS NOT NULL 
    AND ProcessedImageUrl NOT LIKE 'http%'
ORDER BY Category;

-- SAMPLE: Show records that need migration
SELECT TOP 10
    Id,
    OriginalImageUrl,
    ProcessedImageUrl,
    Style,
    UserProfileId,
    CreatedAt,
    CASE 
        WHEN OriginalImageUrl IS NOT NULL AND OriginalImageUrl NOT LIKE 'http%' THEN 1 
        ELSE 0 
    END as NeedsOriginalConversion,
    CASE 
        WHEN ProcessedImageUrl IS NOT NULL AND ProcessedImageUrl NOT LIKE 'http%' THEN 1 
        ELSE 0 
    END as NeedsProcessedConversion
FROM ProcessedImages
WHERE (OriginalImageUrl IS NOT NULL AND OriginalImageUrl NOT LIKE 'http%')
   OR (ProcessedImageUrl IS NOT NULL AND ProcessedImageUrl NOT LIKE 'http%')
ORDER BY CreatedAt DESC;",

                    ManualMigrationTemplate = @"
-- MANUAL MIGRATION TEMPLATE (USE WITH CAUTION)
-- Replace [STORAGE_BASE_URL] with your Azure Storage base URL
-- Example: https://yourstorageaccount.blob.core.windows.net/images/

-- Backup original data first
SELECT * INTO ProcessedImages_Backup_[TIMESTAMP] FROM ProcessedImages;

-- Update relative OriginalImageUrl paths
UPDATE ProcessedImages 
SET OriginalImageUrl = '[STORAGE_BASE_URL]' + LTRIM(OriginalImageUrl, '/')
WHERE OriginalImageUrl IS NOT NULL 
    AND OriginalImageUrl NOT LIKE 'http%';

-- Update relative ProcessedImageUrl paths  
UPDATE ProcessedImages
SET ProcessedImageUrl = '[STORAGE_BASE_URL]' + LTRIM(ProcessedImageUrl, '/')
WHERE ProcessedImageUrl IS NOT NULL 
    AND ProcessedImageUrl NOT LIKE 'http%';

-- Verification
SELECT 
    COUNT(*) as TotalRecords,
    SUM(CASE WHEN OriginalImageUrl LIKE 'http%' THEN 1 ELSE 0 END) as CorrectOriginalUrls,
    SUM(CASE WHEN ProcessedImageUrl LIKE 'http%' THEN 1 ELSE 0 END) as CorrectProcessedUrls
FROM ProcessedImages;",

                    RollbackTemplate = @"
-- ROLLBACK TEMPLATE (RESTORE FROM BACKUP)
-- Replace [TIMESTAMP] with your backup table timestamp

-- Verify backup exists
IF OBJECT_ID('ProcessedImages_Backup_[TIMESTAMP]') IS NOT NULL
BEGIN
    -- Restore original URLs from backup
    UPDATE pi
    SET 
        OriginalImageUrl = backup.OriginalImageUrl,
        ProcessedImageUrl = backup.ProcessedImageUrl
    FROM ProcessedImages pi
    INNER JOIN ProcessedImages_Backup_[TIMESTAMP] backup ON pi.Id = backup.Id;
    
    PRINT 'Rollback completed successfully';
END
ELSE
BEGIN
    PRINT 'ERROR: Backup table not found';
END"
                };

                return SuccessResponse(new
                {
                    Scripts = scripts,
                    Instructions = new[]
                    {
                        "1. Run Analysis Script to understand current state",
                        "2. Create database backup before any changes",
                        "3. Use API endpoints for safer automated migration",
                        "4. Manual scripts provided for emergency use only",
                        "5. Always test on staging environment first"
                    },
                    Warning = "⚠️ Manual SQL execution bypasses validation and rollback features. Use API endpoints for safer migration.",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                LogError(ex, "Error generating SQL scripts");
                return ErrorResponse("ScriptGenerationFailed", "Failed to generate SQL scripts", 500);
            }
        }

        /// <summary>
        /// Validate migration success and suggest re-enablement of auto-repair
        /// </summary>
        [HttpPost("validate-migration")]
        public async Task<IActionResult> ValidateMigration()
        {
            try
            {
                Logger.LogInformation("Validating image URL migration success");

                // Re-run analysis to check current state
                var analysisResult = await _imageMigration.AnalyzeDatabaseStateAsync();

                var isSuccessful = analysisResult.EstimatedAffectedRecords == 0;
                
                var response = new
                {
                    ValidationResults = new
                    {
                        MigrationSuccessful = isSuccessful,
                        TotalRecords = analysisResult.TotalRecords,
                        RemainingRelativePaths = analysisResult.EstimatedAffectedRecords,
                        AllUrlsCorrect = isSuccessful
                    },
                    Status = isSuccessful 
                        ? "MIGRATION_SUCCESSFUL" 
                        : "MIGRATION_INCOMPLETE",
                    Message = isSuccessful 
                        ? "✅ All image URLs have been successfully converted to Azure URLs"
                        : $"⚠️ {analysisResult.EstimatedAffectedRecords} records still have relative paths",
                    ReEnablementRecommendation = new
                    {
                        CanReEnableAutoRepair = isSuccessful,
                        Recommendation = isSuccessful 
                            ? "Safe to re-enable auto-repair functionality"
                            : "Do NOT re-enable auto-repair until all URLs are migrated",
                        NextSteps = isSuccessful 
                            ? new[]
                            {
                                "✅ Migration validation passed",
                                "🔄 Re-enable auto-repair in UI services",
                                "🧪 Test image display and deletion functionality", 
                                "📊 Monitor for any remaining issues"
                            }
                            : new[]
                            {
                                "❌ Migration validation failed",
                                "🔄 Run migration again for remaining records",
                                "🚫 Keep auto-repair disabled",
                                "🔍 Investigate why some records weren't migrated"
                            }
                    },
                    RemainingIssues = !isSuccessful ? analysisResult.SampleRelativePaths.Take(5) : null,
                    Timestamp = DateTime.UtcNow
                };

                return SuccessResponse(response);
            }
            catch (Exception ex)
            {
                LogError(ex, "Error validating migration");
                return ErrorResponse("ValidationFailed", "Failed to validate migration", 500);
            }
        }
    }
}