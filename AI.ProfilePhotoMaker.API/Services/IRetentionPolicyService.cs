using AI.ProfilePhotoMaker.API.Models;

namespace AI.ProfilePhotoMaker.API.Services;

public class ProcessedImageCleanupResult
{
    public int ImageId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string ImageType { get; set; } = string.Empty;
    public DateTime ScheduledDeletion { get; set; }
    public bool FileDeleted { get; set; }
    public bool DatabaseDeleted { get; set; }
    public string Error { get; set; } = string.Empty;
}

public interface IRetentionPolicyService
{
    /// <summary>
    /// Marks a user's image for immediate deletion
    /// </summary>
    Task<bool> RequestImageDeletionAsync(int imageId, string userId);

    /// <summary>
    /// Marks all of a user's images for immediate deletion
    /// </summary>
    Task<int> RequestAllImagesDeletionAsync(string userId);

    /// <summary>
    /// Gets images that are scheduled for deletion for a user
    /// </summary>
    Task<List<ProcessedImage>> GetImagesScheduledForDeletionAsync(string userId);

    /// <summary>
    /// Restores an image that was marked for deletion (if still within grace period)
    /// </summary>
    Task<bool> RestoreImageAsync(int imageId, string userId);

    /// <summary>
    /// Gets retention information for a specific image
    /// </summary>
    Task<ProcessedImage?> GetImageRetentionInfoAsync(int imageId, string userId);

    // Additional utility methods for background service and admin operations

    /// <summary>
    /// Deletes all expired images based on their scheduled deletion dates
    /// </summary>
    Task<int> DeleteExpiredImagesAsync();

    /// <summary>
    /// Gets a list of expired images for cleanup reporting
    /// </summary>
    Task<List<ProcessedImageCleanupResult>> GetExpiredImagesAsync();

    /// <summary>
    /// Sets retention dates for existing images that don't have them
    /// </summary>
    Task SetRetentionDatesForExistingImagesAsync();

    /// <summary>
    /// Cleans up orphaned enhanced image files older than specified age
    /// </summary>
    /// <param name="maxAge">Maximum age of files to keep (older files will be deleted) - typically 1 hour for enhanced images</param>
    /// <returns>Number of enhanced images cleaned up</returns>
    Task<int> CleanupOrphanedEnhancedImagesAsync(TimeSpan maxAge);

    /// <summary>
    /// Gets images that are scheduled for deletion within the specified number of days, grouped by user
    /// </summary>
    /// <param name="daysBeforeDeletion">Number of days before deletion (e.g., 14 for 14-day warning, 7 for 7-day warning)</param>
    /// <param name="windowSizeDays">Size of the window in days to check (default 1 day). Images with ScheduledDeletionDate between (now + daysBeforeDeletion - windowSizeDays/2) and (now + daysBeforeDeletion + windowSizeDays/2) will be included</param>
    /// <returns>Dictionary keyed by userId, containing email and list of images approaching deletion</returns>
    Task<Dictionary<string, (string? Email, List<ProcessedImage> Images)>> GetImagesApproachingDeletionAsync(int daysBeforeDeletion, int windowSizeDays = 1);
}