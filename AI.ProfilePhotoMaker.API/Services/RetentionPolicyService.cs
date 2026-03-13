using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AI.ProfilePhotoMaker.API.Services;

public class RetentionPolicyService : IRetentionPolicyService
{
    private const string RetentionActorId = "system:retention";

    private readonly ApplicationDbContext _context;
    private readonly IStorageService _storageService;
    private readonly StoragePathResolver _pathResolver;
    private readonly IReplicateApiClient _replicateApiClient;
    private readonly ILogger<RetentionPolicyService> _logger;
    private readonly LegacyCompatibilityOptions _legacyOptions;
    private readonly TimeProvider _timeProvider;

    private static string S(string? value) => LoggingSanitizer.Sanitize(value);
    private static string Sid(string? value) => LoggingSanitizer.SanitizeId(value);
    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public RetentionPolicyService(
        ApplicationDbContext context,
        IStorageService storageService,
        StoragePathResolver pathResolver,
        IReplicateApiClient replicateApiClient,
        ILogger<RetentionPolicyService> logger,
        IOptions<LegacyCompatibilityOptions> legacyOptions,
        TimeProvider timeProvider)
    {
        _context = context;
        _storageService = storageService;
        _pathResolver = pathResolver;
        _replicateApiClient = replicateApiClient;
        _logger = logger;
        _legacyOptions = legacyOptions.Value;
        _timeProvider = timeProvider;
    }

    public async Task<int> DeleteExpiredImagesAsync()
    {
        var auditTimestamp = UtcNow;
        var expiredImages = await GetExpiredImagesForDeletion(includeUserProfile: true);
        var deletedCount = 0;
        var affectedUserIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var image in expiredImages)
        {
            try
            {
                if (image.UserProfile?.UserId is { Length: > 0 } userId)
                {
                    affectedUserIds.Add(userId);
                }

                // Delete physical files first
                await DeletePhysicalFiles(image);

                // Then remove from database and record the reason for the admin diagnostics view
                if (image.UserProfile?.UserId is { Length: > 0 } targetUserId)
                {
                    _context.AdminAuditLogs.Add(new AdminAuditLog
                    {
                        AdminUserId = RetentionActorId,
                        TargetUserId = targetUserId,
                        Action = "ImageDeletedByRetention",
                        Details = $"Retention policy deleted {GetImageType(image)} image {image.Id}",
                        OldValue = image.IsOriginalUpload ? image.OriginalImageUrl : image.ProcessedImageUrl,
                        NewValue = "Deleted",
                        CreatedAt = auditTimestamp
                    });
                }

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
                    image.Id, S(ex.Message));
            }
        }

        if (affectedUserIds.Count > 0)
        {
            var replicateDeletes = await CleanupReplicateModelsForUsersAsync(affectedUserIds);
            if (replicateDeletes > 0)
            {
                _logger.LogInformation(
                    "Retention policy cleanup deleted {ModelCount} Replicate model(s) for {UserCount} user(s) with no remaining headshot images",
                    replicateDeletes,
                    affectedUserIds.Count);
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

    private async Task<List<Models.ProcessedImage>> GetExpiredImagesForDeletion(bool includeUserProfile = false)
    {
        var now = UtcNow;
        var query = _context.ProcessedImages.AsQueryable();
        if (includeUserProfile)
        {
            query = query.Include(img => img.UserProfile);
        }

        return await query
            .Where(img => img.ScheduledDeletionDate <= now)
            .ToListAsync();
    }

    private async Task<int> CleanupReplicateModelsForUsersAsync(HashSet<string> userIds)
    {
        var usersWithHeadshotImages = await _context.ProcessedImages
            .Include(img => img.UserProfile)
            .Where(img => userIds.Contains(img.UserProfile.UserId))
            .Where(img => img.IsGenerated)
            .Where(img =>
                string.IsNullOrEmpty(img.Style) ||
                (!EF.Functions.Like(img.Style, "Enhanced%") &&
                 img.Style != "Background Remover" &&
                 img.Style != "Social Media" &&
                 img.Style != "Cartoon"))
            .Select(img => img.UserProfile.UserId)
            .Distinct()
            .ToListAsync();

        var usersWithoutHeadshotImages = userIds
            .Where(userId => !usersWithHeadshotImages.Contains(userId, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (!usersWithoutHeadshotImages.Any())
        {
            return 0;
        }

        var modelRequests = await _context.ModelCreationRequests
            .Where(request => usersWithoutHeadshotImages.Contains(request.UserId))
            .Where(request => !string.IsNullOrWhiteSpace(request.ReplicateModelId))
            .Where(request => request.Status == ModelCreationStatus.Ready || request.Status == ModelCreationStatus.Failed)
            .ToListAsync();

        if (!modelRequests.Any())
        {
            return 0;
        }

        var deletedModels = 0;
        foreach (var modelGroup in modelRequests
                     .GroupBy(request => request.ReplicateModelId!, StringComparer.OrdinalIgnoreCase))
        {
            var modelId = modelGroup.Key;
            try
            {
                var (success, errorMessage) = await _replicateApiClient.DeleteModelAsync(modelId);
                if (success)
                {
                    foreach (var request in modelGroup)
                    {
                        request.Status = ModelCreationStatus.Failed;
                        request.ErrorMessage = "Deleted by retention policy after headshot images expired.";
                        request.TrainedModelVersion = null;
                    }

                    deletedModels++;
                }
                else
                {
                    _logger.LogWarning(
                        "Retention policy failed to delete Replicate model {ModelId}: {Error}",
                        S(modelId),
                        S(errorMessage));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Retention policy error deleting Replicate model {ModelId}", S(modelId));
            }
        }

        if (deletedModels > 0)
        {
            await _context.SaveChangesAsync();
        }

        return deletedModels;
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
                _logger.LogDebug("Deleted file: {FileUrl}", S(fileUrl));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete file {FileUrl}: {Error}", S(fileUrl), S(ex.Message));
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
        image.ScheduledDeletionDate = UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Image {ImageId} marked for immediate deletion by user {UserId}", imageId, Sid(userId));

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
            image.ScheduledDeletionDate = UtcNow;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("All {Count} images marked for immediate deletion by user {UserId}", count, Sid(userId));

        return count;
    }

    public async Task<List<ProcessedImage>> GetImagesScheduledForDeletionAsync(string userId)
    {
        var now = UtcNow;
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
            imageId, Sid(userId), image.ScheduledDeletionDate);

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
        var now = UtcNow;
        var cutoffTime = now.Subtract(maxAge);

        try
        {
            // Get the enhanced directory prefix for all users
            var enhancedPrefix = _pathResolver.GetDirectoryPrefix(StorageType.Enhanced);
            _logger.LogDebug("Starting enhanced image cleanup for prefix: {Prefix}, cutoff time: {CutoffTime}",
                S(enhancedPrefix), cutoffTime);

            // List all files in the enhanced directory
            var enhancedFiles = await _storageService.ListFilesAsync(enhancedPrefix);

            // Also check for legacy enhanced files without environment prefix (for backward compatibility)
            List<string> legacyEnhancedFiles;
            if (_legacyOptions.EnableLegacyEnhancedPathLookup)
            {
                legacyEnhancedFiles = await _storageService.ListFilesAsync("enhanced/");
            }
            else
            {
                legacyEnhancedFiles = new List<string>();
                _logger.LogDebug(
                    "Skipping legacy enhanced path lookup because LegacyCompatibility.EnableLegacyEnhancedPathLookup is disabled."
                );
            }

            // Combine both lists and remove duplicates
            var allEnhancedFiles = enhancedFiles.Concat(legacyEnhancedFiles).Distinct().ToList();

            if (!allEnhancedFiles.Any())
            {
                _logger.LogDebug("No enhanced files found for cleanup in either prefixed ({Prefix}) or legacy (enhanced/) paths", S(enhancedPrefix));
                return 0;
            }

            if (_legacyOptions.EnableLegacyEnhancedPathLookup)
            {
                _logger.LogInformation("Found {PrefixedCount} enhanced files in prefixed path and {LegacyCount} in legacy path for cleanup",
                    enhancedFiles.Count, legacyEnhancedFiles.Count);
            }
            else
            {
                _logger.LogInformation("Found {PrefixedCount} enhanced files in prefixed path; legacy path lookup disabled via configuration",
                    enhancedFiles.Count);
            }

            // Build a set of enhanced file paths that are referenced in the database
            var referencedEnhancedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var envPrefix = _pathResolver.GetDirectoryPrefix(StorageType.Enhanced);
                var legacyPrefix = "enhanced/";
                var allowLegacyInDbScan = _legacyOptions.EnableLegacyEnhancedPathLookup;

                var dbReferenced = await _context.ProcessedImages
                    .Where(pi => (!string.IsNullOrEmpty(pi.ProcessedImageUrl) &&
                                  (pi.ProcessedImageUrl.StartsWith(envPrefix) ||
                                   (allowLegacyInDbScan && pi.ProcessedImageUrl.StartsWith(legacyPrefix)))) ||
                                  (!string.IsNullOrEmpty(pi.OriginalImageUrl) &&
                                   (pi.OriginalImageUrl.StartsWith(envPrefix) ||
                                    (allowLegacyInDbScan && pi.OriginalImageUrl.StartsWith(legacyPrefix)))))
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
                        _logger.LogWarning("Could not get file info for enhanced image: {FilePath}", S(filePath));
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
                                S(filePath),
                                fileInfo.CreatedAt,
                                now.Subtract(fileInfo.CreatedAt));
                        }
                        else
                        {
                            _logger.LogWarning("Failed to delete enhanced image: {FilePath}", S(filePath));
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Enhanced image {FilePath} is too recent (created: {CreatedAt})",
                            S(filePath), fileInfo.CreatedAt);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing enhanced image {FilePath}: {Error}", S(filePath), S(ex.Message));
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
            _logger.LogError(ex, "Failed to perform enhanced image cleanup: {Error}", S(ex.Message));
            throw;
        }

        return deletedCount;
    }

    public async Task<Dictionary<string, (string? Email, List<ProcessedImage> Images)>> GetImagesApproachingDeletionAsync(int daysBeforeDeletion, int windowSizeDays = 1)
    {
        var nowDate = UtcNow.Date;
        DateTime windowStart;
        DateTime windowEnd;

        if (windowSizeDays <= 1)
        {
            var targetDate = nowDate.AddDays(daysBeforeDeletion);
            windowStart = targetDate;
            windowEnd = targetDate.AddDays(1);
        }
        else
        {
            var halfWindow = windowSizeDays / 2;
            windowStart = nowDate.AddDays(daysBeforeDeletion - halfWindow);
            windowEnd = windowStart.AddDays(windowSizeDays);
        }

        var images = await _context.ProcessedImages
            .Include(img => img.UserProfile)
            .ThenInclude(up => up.User)
            .Where(img => img.ScheduledDeletionDate >= windowStart && img.ScheduledDeletionDate < windowEnd)
            .ToListAsync();

        var result = new Dictionary<string, (string? Email, List<ProcessedImage> Images)>(StringComparer.OrdinalIgnoreCase);

        var groupedByUser = images
            .Where(img => !string.IsNullOrWhiteSpace(img.UserProfile?.UserId))
            .GroupBy(img => img.UserProfile!.UserId, StringComparer.OrdinalIgnoreCase);

        foreach (var userGroup in groupedByUser)
        {
            var userId = userGroup.Key;
            var firstImage = userGroup.First();
            var email = firstImage.UserProfile?.User?.Email;
            var userImages = userGroup.ToList();

            result[userId] = (email, userImages);
        }

        return result;
    }
}
