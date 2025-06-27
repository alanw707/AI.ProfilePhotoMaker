using AI.ProfilePhotoMaker.API.Models;

namespace AI.ProfilePhotoMaker.API.Services;

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
}