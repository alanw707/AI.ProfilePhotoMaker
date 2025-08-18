using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace AI.ProfilePhotoMaker.API.Database.Migrations
{
    /// <summary>
    /// Production Data Migration: Convert relative image paths to full Azure Blob URLs
    /// 
    /// PROBLEM: Database contains relative paths (/prod/uploads/...) instead of full Azure URLs
    /// SOLUTION: Convert existing relative paths to proper Azure Blob URLs using StorageService
    /// SAFETY: Includes validation, rollback capability, and batch processing
    /// </summary>
    public class ProductionImageUrlMigration
    {
        private readonly ApplicationDbContext _context;
        private readonly IStorageService _storageService;
        private readonly ILogger<ProductionImageUrlMigration> _logger;
        private readonly int _batchSize = 50; // Process in batches to avoid memory issues

        public ProductionImageUrlMigration(
            ApplicationDbContext context,
            IStorageService storageService,
            ILogger<ProductionImageUrlMigration> logger)
        {
            _context = context;
            _storageService = storageService;
            _logger = logger;
        }

        /// <summary>
        /// Analyze current database state - identify all records with relative paths
        /// </summary>
        public async Task<AnalysisResult> AnalyzeDatabaseStateAsync()
        {
            _logger.LogInformation("Starting database analysis for image URL migration");

            var result = new AnalysisResult();

            try
            {
                // Count total records
                result.TotalRecords = await _context.ProcessedImages.CountAsync();

                // Count records with relative paths in OriginalImageUrl
                result.RelativeOriginalPaths = await _context.ProcessedImages
                    .Where(img => img.OriginalImageUrl != null && 
                                  !img.OriginalImageUrl.StartsWith("http") &&
                                  !img.OriginalImageUrl.StartsWith("https"))
                    .CountAsync();

                // Count records with relative paths in ProcessedImageUrl  
                result.RelativeProcessedPaths = await _context.ProcessedImages
                    .Where(img => img.ProcessedImageUrl != null && 
                                  !img.ProcessedImageUrl.StartsWith("http") &&
                                  !img.ProcessedImageUrl.StartsWith("https"))
                    .CountAsync();

                // Get sample records for validation
                result.SampleRelativePaths = await _context.ProcessedImages
                    .Where(img => (img.OriginalImageUrl != null && !img.OriginalImageUrl.StartsWith("http")) ||
                                  (img.ProcessedImageUrl != null && !img.ProcessedImageUrl.StartsWith("http")))
                    .Select(img => new SampleRecord
                    {
                        Id = img.Id,
                        OriginalImageUrl = img.OriginalImageUrl,
                        ProcessedImageUrl = img.ProcessedImageUrl,
                        Style = img.Style,
                        UserProfileId = img.UserProfileId,
                        CreatedAt = img.CreatedAt
                    })
                    .Take(10)
                    .ToListAsync();

                // Identify storage patterns
                var commonPrefixes = await _context.ProcessedImages
                    .Where(img => img.OriginalImageUrl != null && !img.OriginalImageUrl.StartsWith("http"))
                    .Select(img => img.OriginalImageUrl!)
                    .Take(100)
                    .ToListAsync();

                result.CommonPathPatterns = commonPrefixes
                    .Where(path => !string.IsNullOrEmpty(path))
                    .Select(path => path.Split('/')[1]) // Get first directory after root
                    .GroupBy(x => x)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .ToDictionary(g => g.Key, g => g.Count());

                result.EstimatedAffectedRecords = Math.Max(result.RelativeOriginalPaths, result.RelativeProcessedPaths);

                _logger.LogInformation("Database analysis completed: {TotalRecords} total, {AffectedRecords} affected", 
                    result.TotalRecords, result.EstimatedAffectedRecords);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during database analysis");
                throw;
            }
        }

        /// <summary>
        /// Execute the migration in batches with validation and rollback capability
        /// </summary>
        public async Task<MigrationResult> ExecuteMigrationAsync(bool dryRun = true)
        {
            _logger.LogInformation("Starting image URL migration (DryRun: {DryRun})", dryRun);

            var result = new MigrationResult
            {
                StartTime = DateTime.UtcNow,
                DryRun = dryRun
            };

            try
            {
                // Get all records with relative paths
                var affectedRecords = await GetAffectedRecordsAsync();
                result.TotalRecordsToProcess = affectedRecords.Count;

                _logger.LogInformation("Found {Count} records to process", affectedRecords.Count);

                // Process in batches
                var batches = affectedRecords.Chunk(_batchSize);
                var batchNumber = 0;

                foreach (var batch in batches)
                {
                    batchNumber++;
                    _logger.LogInformation("Processing batch {BatchNumber} ({Count} records)", 
                        batchNumber, batch.Length);

                    var batchResult = await ProcessBatchAsync(batch, dryRun);
                    result.ProcessedBatches.Add(batchResult);

                    result.SuccessfullyProcessed += batchResult.SuccessCount;
                    result.FailedToProcess += batchResult.FailureCount;
                    result.ValidationErrors.AddRange(batchResult.ValidationErrors);

                    // Add delay between batches to reduce database load
                    if (!dryRun && batchNumber % 10 == 0)
                    {
                        await Task.Delay(1000);
                    }
                }

                result.EndTime = DateTime.UtcNow;
                result.Success = result.FailedToProcess == 0;

                _logger.LogInformation("Migration completed: {Successful} successful, {Failed} failed", 
                    result.SuccessfullyProcessed, result.FailedToProcess);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during migration execution");
                result.EndTime = DateTime.UtcNow;
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Get all ProcessedImage records that need URL conversion
        /// </summary>
        private async Task<List<ProcessedImageRecord>> GetAffectedRecordsAsync()
        {
            return await _context.ProcessedImages
                .Where(img => (img.OriginalImageUrl != null && !img.OriginalImageUrl.StartsWith("http")) ||
                              (img.ProcessedImageUrl != null && !img.ProcessedImageUrl.StartsWith("http")))
                .Select(img => new ProcessedImageRecord
                {
                    Id = img.Id,
                    OriginalImageUrl = img.OriginalImageUrl,
                    ProcessedImageUrl = img.ProcessedImageUrl,
                    UserProfileId = img.UserProfileId
                })
                .ToListAsync();
        }

        /// <summary>
        /// Process a batch of records, converting relative paths to Azure URLs
        /// </summary>
        private async Task<BatchResult> ProcessBatchAsync(ProcessedImageRecord[] batch, bool dryRun)
        {
            var batchResult = new BatchResult
            {
                BatchNumber = 0, // Will be set by caller
                Records = new List<MigrationRecord>()
            };

            foreach (var record in batch)
            {
                var migrationRecord = new MigrationRecord
                {
                    Id = record.Id,
                    OriginalOriginalImageUrl = record.OriginalImageUrl,
                    OriginalProcessedImageUrl = record.ProcessedImageUrl
                };

                try
                {
                    // Convert OriginalImageUrl if it's a relative path
                    if (!string.IsNullOrEmpty(record.OriginalImageUrl) && 
                        !record.OriginalImageUrl.StartsWith("http"))
                    {
                        var convertedUrl = _storageService.GetImageUrl(record.OriginalImageUrl);
                        migrationRecord.NewOriginalImageUrl = convertedUrl;

                        // Validate the converted URL
                        if (!await ValidateUrlAsync(convertedUrl))
                        {
                            migrationRecord.ValidationErrors.Add($"Original URL validation failed: {convertedUrl}");
                        }
                    }

                    // Convert ProcessedImageUrl if it's a relative path  
                    if (!string.IsNullOrEmpty(record.ProcessedImageUrl) && 
                        !record.ProcessedImageUrl.StartsWith("http"))
                    {
                        var convertedUrl = _storageService.GetImageUrl(record.ProcessedImageUrl);
                        migrationRecord.NewProcessedImageUrl = convertedUrl;

                        // Validate the converted URL
                        if (!await ValidateUrlAsync(convertedUrl))
                        {
                            migrationRecord.ValidationErrors.Add($"Processed URL validation failed: {convertedUrl}");
                        }
                    }

                    // Update database if not dry run and no validation errors
                    if (!dryRun && migrationRecord.ValidationErrors.Count == 0)
                    {
                        await UpdateDatabaseRecordAsync(record.Id, 
                            migrationRecord.NewOriginalImageUrl, 
                            migrationRecord.NewProcessedImageUrl);
                        
                        migrationRecord.Success = true;
                        batchResult.SuccessCount++;
                    }
                    else if (dryRun)
                    {
                        migrationRecord.Success = migrationRecord.ValidationErrors.Count == 0;
                        if (migrationRecord.Success)
                        {
                            batchResult.SuccessCount++;
                        }
                        else
                        {
                            batchResult.FailureCount++;
                            batchResult.ValidationErrors.AddRange(migrationRecord.ValidationErrors);
                        }
                    }
                    else
                    {
                        migrationRecord.Success = false;
                        batchResult.FailureCount++;
                        batchResult.ValidationErrors.AddRange(migrationRecord.ValidationErrors);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing record {Id}", record.Id);
                    migrationRecord.Success = false;
                    migrationRecord.ValidationErrors.Add($"Processing error: {ex.Message}");
                    batchResult.FailureCount++;
                }

                batchResult.Records.Add(migrationRecord);
            }

            return batchResult;
        }

        /// <summary>
        /// Validate that a converted URL is accessible
        /// </summary>
        private async Task<bool> ValidateUrlAsync(string url)
        {
            try
            {
                // For Azure Blob URLs, we can check if the blob exists
                if (url.Contains("blob.core.windows.net"))
                {
                    // Extract the path from the URL to check existence
                    var uri = new Uri(url);
                    var path = uri.AbsolutePath.TrimStart('/');
                    
                    // Remove container name to get the blob path
                    var pathParts = path.Split('/', 2);
                    if (pathParts.Length > 1)
                    {
                        return await _storageService.ExistsAsync(pathParts[1]);
                    }
                }

                // For other URLs, just check format
                return Uri.TryCreate(url, UriKind.Absolute, out _);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Update a database record with new URLs
        /// </summary>
        private async Task UpdateDatabaseRecordAsync(int id, string? newOriginalUrl, string? newProcessedUrl)
        {
            var record = await _context.ProcessedImages.FindAsync(id);
            if (record != null)
            {
                if (!string.IsNullOrEmpty(newOriginalUrl))
                {
                    record.OriginalImageUrl = newOriginalUrl;
                }

                if (!string.IsNullOrEmpty(newProcessedUrl))
                {
                    record.ProcessedImageUrl = newProcessedUrl;
                }

                await _context.SaveChangesAsync();
                
                _logger.LogDebug("Updated record {Id}: Original={Original}, Processed={Processed}", 
                    id, newOriginalUrl, newProcessedUrl);
            }
        }

        /// <summary>
        /// Create rollback script for the migration
        /// </summary>
        public async Task<string> GenerateRollbackScriptAsync(MigrationResult migrationResult)
        {
            var script = new System.Text.StringBuilder();
            script.AppendLine("-- ROLLBACK SCRIPT FOR IMAGE URL MIGRATION");
            script.AppendLine($"-- Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            script.AppendLine($"-- Migration executed: {migrationResult.StartTime:yyyy-MM-dd HH:mm:ss} UTC");
            script.AppendLine();

            foreach (var batch in migrationResult.ProcessedBatches)
            {
                foreach (var record in batch.Records.Where(r => r.Success))
                {
                    if (!string.IsNullOrEmpty(record.NewOriginalImageUrl) && 
                        !string.IsNullOrEmpty(record.OriginalOriginalImageUrl))
                    {
                        script.AppendLine($"UPDATE ProcessedImages SET OriginalImageUrl = '{record.OriginalOriginalImageUrl}' WHERE Id = {record.Id};");
                    }

                    if (!string.IsNullOrEmpty(record.NewProcessedImageUrl) && 
                        !string.IsNullOrEmpty(record.OriginalProcessedImageUrl))
                    {
                        script.AppendLine($"UPDATE ProcessedImages SET ProcessedImageUrl = '{record.OriginalProcessedImageUrl}' WHERE Id = {record.Id};");
                    }
                }
            }

            return script.ToString();
        }
    }

    #region Data Transfer Objects

    public class AnalysisResult
    {
        public int TotalRecords { get; set; }
        public int RelativeOriginalPaths { get; set; }
        public int RelativeProcessedPaths { get; set; }
        public int EstimatedAffectedRecords { get; set; }
        public List<SampleRecord> SampleRelativePaths { get; set; } = new();
        public Dictionary<string, int> CommonPathPatterns { get; set; } = new();
    }

    public class SampleRecord
    {
        public int Id { get; set; }
        public string? OriginalImageUrl { get; set; }
        public string? ProcessedImageUrl { get; set; }
        public string Style { get; set; } = string.Empty;
        public int UserProfileId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class MigrationResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool DryRun { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int TotalRecordsToProcess { get; set; }
        public int SuccessfullyProcessed { get; set; }
        public int FailedToProcess { get; set; }
        public List<BatchResult> ProcessedBatches { get; set; } = new();
        public List<string> ValidationErrors { get; set; } = new();
        
        public TimeSpan Duration => EndTime - StartTime;
        public double SuccessRate => TotalRecordsToProcess == 0 ? 100.0 : (double)SuccessfullyProcessed / TotalRecordsToProcess * 100.0;
    }

    public class BatchResult
    {
        public int BatchNumber { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<MigrationRecord> Records { get; set; } = new();
        public List<string> ValidationErrors { get; set; } = new();
    }

    public class MigrationRecord
    {
        public int Id { get; set; }
        public string? OriginalOriginalImageUrl { get; set; }
        public string? OriginalProcessedImageUrl { get; set; }
        public string? NewOriginalImageUrl { get; set; }
        public string? NewProcessedImageUrl { get; set; }
        public bool Success { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
    }

    public class ProcessedImageRecord
    {
        public int Id { get; set; }
        public string? OriginalImageUrl { get; set; }
        public string? ProcessedImageUrl { get; set; }
        public int UserProfileId { get; set; }
    }

    #endregion
}