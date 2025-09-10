using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services;

public class RetentionPolicyService : IRetentionPolicyService
{
    private readonly ApplicationDbContext _context;
    private readonly IStorageService _storageService;
    private readonly StoragePathResolver _pathResolver;
    private readonly ILogger<RetentionPolicyService> _logger;

    public RetentionPolicyService(
        ApplicationDbContext context,
        IStorageService storageService,
        StoragePathResolver pathResolver,
        ILogger<RetentionPolicyService> logger)
    {
        _context = context;
        _storageService = storageService;
        _pathResolver = pathResolver;
        _logger = logger;
    }

    public async Task<int> DeleteExpiredImagesAsync()
    {
        var expiredImages = await GetExpiredImagesForDeletion();
        var deletedCount = 0;

        foreach (var image in expiredImages)
        {
            try
            {
                // Delete physical files first
                await DeletePhysicalFiles(image);

                // Then remove from database
                _context.ProcessedImages.Remove(image);
                await _context.SaveChangesAsync();

                deletedCount++;
                _logger.LogInformation(
                    "Deleted expired image {ImageId} of type {ImageType}, scheduled for {ScheduledDeletion}",
                    image.Id, GetImageType(image), image.ScheduledDeletionDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to delete expired image {ImageId}: {Error}",
                    image.Id, ex.Message);
            }
        }

        return deletedCount;
    }

    public async Task<List<ProcessedImageCleanupResult>> GetExpiredImagesAsync()
    {
        var expiredImages = await GetExpiredImagesForDeletion();
        var results = new List<ProcessedImageCleanupResult>();

        foreach (var image in expiredImages)
        {
            results.Add(new ProcessedImageCleanupResult
            {
                ImageId = image.Id,
                ImageUrl = image.IsOriginalUpload ? image.OriginalImageUrl : image.ProcessedImageUrl,
                ImageType = GetImageType(image),
                ScheduledDeletion = image.ScheduledDeletionDate,
                FileDeleted = false,
                DatabaseDeleted = false
            });
        }

        return results;
    }

    public async Task SetRetentionDatesForExistingImagesAsync()
    {
        var imagesWithoutRetentionDates = await _context.ProcessedImages
            .Where(img => img.ScheduledDeletionDate == default(DateTime))
            .ToListAsync();

        foreach (var image in imagesWithoutRetentionDates)
        {
            image.SetScheduledDeletionDate();
        }

        if (imagesWithoutRetentionDates.Any())
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation(
                "Set retention dates for {Count} existing images",
                imagesWithoutRetentionDates.Count);
        }
    }

    private async Task<List<Models.ProcessedImage>> GetExpiredImagesForDeletion()
    {
        var now = DateTime.UtcNow;
        return await _context.ProcessedImages
            .Where(img => img.ScheduledDeletionDate <= now)
            .ToListAsync();
    }

    private async Task DeletePhysicalFiles(Models.ProcessedImage image)
    {
        var filesToDelete = new List<string>();

        // Add original image URL if it exists and is a local file
        if (!string.IsNullOrEmpty(image.OriginalImageUrl) && IsLocalFile(image.OriginalImageUrl))
        {
            filesToDelete.Add(image.OriginalImageUrl);
        }

        // Add processed image URL if it exists and is a local file
        if (!string.IsNullOrEmpty(image.ProcessedImageUrl) && IsLocalFile(image.ProcessedImageUrl))
        {
            filesToDelete.Add(image.ProcessedImageUrl);
        }

        // Delete files using storage service
        foreach (var fileUrl in filesToDelete)
        {
            try
            {
                await _storageService.DeleteImageAsync(fileUrl);
                _logger.LogDebug("Deleted file: {FileUrl}", fileUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete file {FileUrl}: {Error}", fileUrl, ex.Message);
                // Continue with other files even if one fails
            }
        }
    }

    private static bool IsLocalFile(string url)
    {
        // Check if URL is a local file (starts with / or contains localhost)
        return !string.IsNullOrEmpty(url) &&
               (url.StartsWith("/") || url.Contains("localhost"));
    }

    private static string GetImageType(Models.ProcessedImage image)
    {
        if (image.IsOriginalUpload)
            return "Original Upload";
        if (image.IsGenerated)
            return "AI Generated";
        return "Unknown";
    }

    // Implement required interface methods

    public async Task<bool> RequestImageDeletionAsync(int imageId, string userId)
    {
        var image = await _context.ProcessedImages
            .Include(img => img.UserProfile)
            .FirstOrDefaultAsync(img => img.Id == imageId && img.UserProfile.UserId == userId);

        if (image == null)
        {
            return false;
        }

        // Mark for immediate deletion by setting scheduled deletion to now
        image.ScheduledDeletionDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Image {ImageId} marked for immediate deletion by user {UserId}", imageId, userId);

        return true;
    }

    public async Task<int> RequestAllImagesDeletionAsync(string userId)
    {
        var userProfile = await _context.UserProfiles
            .Include(p => p.ProcessedImages)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (userProfile?.ProcessedImages == null)
        {
            return 0;
        }

        var count = userProfile.ProcessedImages.Count;
        foreach (var image in userProfile.ProcessedImages)
        {
            image.ScheduledDeletionDate = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("All {Count} images marked for immediate deletion by user {UserId}", count, userId);

        return count;
    }

    public async Task<List<ProcessedImage>> GetImagesScheduledForDeletionAsync(string userId)
    {
        var now = DateTime.UtcNow;
        return await _context.ProcessedImages
            .Include(img => img.UserProfile)
            .Where(img => img.UserProfile.UserId == userId && img.ScheduledDeletionDate <= now.AddDays(1))
            .OrderBy(img => img.ScheduledDeletionDate)
            .ToListAsync();
    }

    public async Task<bool> RestoreImageAsync(int imageId, string userId)
    {
        var image = await _context.ProcessedImages
            .Include(img => img.UserProfile)
            .FirstOrDefaultAsync(img => img.Id == imageId && img.UserProfile.UserId == userId);

        if (image == null)
        {
            return false;
        }

        // Restore by recalculating the scheduled deletion date
        image.SetScheduledDeletionDate();

        await _context.SaveChangesAsync();
        _logger.LogInformation("Image {ImageId} restored by user {UserId}, new deletion date: {DeletionDate}",
            imageId, userId, image.ScheduledDeletionDate);

        return true;
    }

    public async Task<ProcessedImage?> GetImageRetentionInfoAsync(int imageId, string userId)
    {
        return await _context.ProcessedImages
            .Include(img => img.UserProfile)
            .FirstOrDefaultAsync(img => img.Id == imageId && img.UserProfile.UserId == userId);
    }

    public async Task<int> CleanupOrphanedEnhancedImagesAsync(TimeSpan maxAge)
    {
        var deletedCount = 0;
        var cutoffTime = DateTime.UtcNow.Subtract(maxAge);

        try
        {
            // Get the enhanced directory prefix for all users
            var enhancedPrefix = _pathResolver.GetDirectoryPrefix(StorageType.Enhanced);
            _logger.LogDebug("Starting enhanced image cleanup for prefix: {Prefix}, cutoff time: {CutoffTime}",
                enhancedPrefix, cutoffTime);

            // List all files in the enhanced directory
            var enhancedFiles = await _storageService.ListFilesAsync(enhancedPrefix);
            
            // Also check for legacy enhanced files without environment prefix (for backward compatibility)
            var legacyEnhancedFiles = await _storageService.ListFilesAsync("enhanced/");
            
            // Combine both lists and remove duplicates
            var allEnhancedFiles = enhancedFiles.Concat(legacyEnhancedFiles).Distinct().ToList();

            if (!allEnhancedFiles.Any())
            {
                _logger.LogDebug("No enhanced files found for cleanup in either prefixed ({Prefix}) or legacy (enhanced/) paths", enhancedPrefix);
                return 0;
            }
            
            _logger.LogInformation("Found {PrefixedCount} enhanced files in prefixed path and {LegacyCount} in legacy path for cleanup", 
                enhancedFiles.Count, legacyEnhancedFiles.Count);

            // Build a set of enhanced file paths that are referenced in the database
            var referencedEnhancedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var envPrefix = _pathResolver.GetDirectoryPrefix(StorageType.Enhanced);
                var legacyPrefix = "enhanced/";

                var dbReferenced = await _context.ProcessedImages
                    .Where(pi => (!string.IsNullOrEmpty(pi.ProcessedImageUrl) &&
                                  (pi.ProcessedImageUrl.StartsWith(envPrefix) || pi.ProcessedImageUrl.StartsWith(legacyPrefix))) ||
                                  (!string.IsNullOrEmpty(pi.OriginalImageUrl) &&
                                  (pi.OriginalImageUrl.StartsWith(envPrefix) || pi.OriginalImageUrl.StartsWith(legacyPrefix))))
                    .Select(pi => new { pi.ProcessedImageUrl, pi.OriginalImageUrl })
                    .ToListAsync();

                foreach (var p in dbReferenced)
                {
                    if (!string.IsNullOrEmpty(p.ProcessedImageUrl)) referencedEnhancedPaths.Add(p.ProcessedImageUrl);
                    if (!string.IsNullOrEmpty(p.OriginalImageUrl)) referencedEnhancedPaths.Add(p.OriginalImageUrl);
                }

                _logger.LogInformation("Found {Count} enhanced paths referenced in database; these will be preserved", referencedEnhancedPaths.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load referenced enhanced paths from database; proceeding with cautious cleanup");
            }

            // Use combined list for processing and skip anything referenced in DB
            var filesToProcess = allEnhancedFiles.Where(p => !referencedEnhancedPaths.Contains(p)).ToList();

            foreach (var filePath in filesToProcess)
            {
                try
                {
                    // Get file info to check creation date
                    var fileInfo = await _storageService.GetFileInfoAsync(filePath);

                    if (fileInfo == null)
                    {
                        _logger.LogWarning("Could not get file info for enhanced image: {FilePath}", filePath);
                        continue;
                    }

                    // Check if file is older than the cutoff time
                    if (fileInfo.CreatedAt <= cutoffTime)
                    {
                        var deleted = await _storageService.DeleteImageAsync(filePath);

                        if (deleted)
                        {
                            deletedCount++;
                            _logger.LogInformation(
                                "Deleted orphaned enhanced image: {FilePath} (created: {CreatedAt}, age: {Age})",
                                filePath,
                                fileInfo.CreatedAt,
                                DateTime.UtcNow.Subtract(fileInfo.CreatedAt));
                        }
                        else
                        {
                            _logger.LogWarning("Failed to delete enhanced image: {FilePath}", filePath);
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Enhanced image {FilePath} is too recent (created: {CreatedAt})",
                            filePath, fileInfo.CreatedAt);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing enhanced image {FilePath}: {Error}", filePath, ex.Message);
                    // Continue with next file
                }
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation("Enhanced image cleanup completed: deleted {DeletedCount} orphaned files", deletedCount);
            }
            else
            {
                _logger.LogDebug("Enhanced image cleanup completed: no files deleted");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform enhanced image cleanup: {Error}", ex.Message);
            throw;
        }

        return deletedCount;
    }
}
