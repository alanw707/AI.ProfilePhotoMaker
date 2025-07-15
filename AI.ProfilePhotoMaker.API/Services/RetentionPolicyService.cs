using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services;

public class RetentionPolicyService : IRetentionPolicyService
{
    private readonly ApplicationDbContext _context;
    private readonly IStorageService _storageService;
    private readonly ILogger<RetentionPolicyService> _logger;

    public RetentionPolicyService(
        ApplicationDbContext context,
        IStorageService storageService,
        ILogger<RetentionPolicyService> logger)
    {
        _context = context;
        _storageService = storageService;
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
}