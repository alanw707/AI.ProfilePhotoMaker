using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services;

public class RetentionPolicyService : IRetentionPolicyService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RetentionPolicyService> _logger;

    public RetentionPolicyService(ApplicationDbContext context, ILogger<RetentionPolicyService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> RequestImageDeletionAsync(int imageId, string userId)
    {
        try
        {
            var userProfile = await _context.UserProfiles
                .FirstOrDefaultAsync(up => up.UserId == userId);

            if (userProfile == null)
            {
                _logger.LogWarning("User profile not found for user {UserId}", userId);
                return false;
            }

            var image = await _context.ProcessedImages
                .FirstOrDefaultAsync(img => img.Id == imageId && 
                                          img.UserProfileId == userProfile.Id && 
                                          !img.IsDeleted);

            if (image == null)
            {
                _logger.LogWarning("Image {ImageId} not found for user {UserId}", imageId, userId);
                return false;
            }

            // Mark for immediate deletion
            image.IsMarkedForDeletion = true;
            image.UserRequestedDeletionDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Image {ImageId} marked for deletion by user {UserId}", imageId, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking image {ImageId} for deletion for user {UserId}", imageId, userId);
            return false;
        }
    }

    public async Task<int> RequestAllImagesDeletionAsync(string userId)
    {
        try
        {
            var userProfile = await _context.UserProfiles
                .FirstOrDefaultAsync(up => up.UserId == userId);

            if (userProfile == null)
            {
                _logger.LogWarning("User profile not found for user {UserId}", userId);
                return 0;
            }

            var images = await _context.ProcessedImages
                .Where(img => img.UserProfileId == userProfile.Id && !img.IsDeleted)
                .ToListAsync();

            var deletionDate = DateTime.UtcNow;
            var deletedCount = 0;

            foreach (var image in images)
            {
                if (!image.IsMarkedForDeletion)
                {
                    image.IsMarkedForDeletion = true;
                    image.UserRequestedDeletionDate = deletionDate;
                    deletedCount++;
                }
            }

            if (deletedCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Marked {Count} images for deletion for user {UserId}", deletedCount, userId);
            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all images for deletion for user {UserId}", userId);
            return 0;
        }
    }

    public async Task<List<ProcessedImage>> GetImagesScheduledForDeletionAsync(string userId)
    {
        try
        {
            var userProfile = await _context.UserProfiles
                .FirstOrDefaultAsync(up => up.UserId == userId);

            if (userProfile == null)
            {
                return new List<ProcessedImage>();
            }

            return await _context.ProcessedImages
                .Where(img => img.UserProfileId == userProfile.Id && 
                             img.IsMarkedForDeletion && 
                             !img.IsDeleted)
                .OrderBy(img => img.ScheduledDeletionDate)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving images scheduled for deletion for user {UserId}", userId);
            return new List<ProcessedImage>();
        }
    }

    public async Task<bool> RestoreImageAsync(int imageId, string userId)
    {
        try
        {
            var userProfile = await _context.UserProfiles
                .FirstOrDefaultAsync(up => up.UserId == userId);

            if (userProfile == null)
            {
                _logger.LogWarning("User profile not found for user {UserId}", userId);
                return false;
            }

            var image = await _context.ProcessedImages
                .FirstOrDefaultAsync(img => img.Id == imageId && 
                                          img.UserProfileId == userProfile.Id && 
                                          img.IsMarkedForDeletion && 
                                          !img.IsDeleted);

            if (image == null)
            {
                _logger.LogWarning("Image {ImageId} not found or not eligible for restoration for user {UserId}", imageId, userId);
                return false;
            }

            // Can only restore if deletion was user-requested and within grace period
            if (image.UserRequestedDeletionDate.HasValue)
            {
                var gracePeriod = TimeSpan.FromDays(1); // 1 day grace period
                if (DateTime.UtcNow - image.UserRequestedDeletionDate.Value <= gracePeriod)
                {
                    image.IsMarkedForDeletion = false;
                    image.UserRequestedDeletionDate = null;
                    
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Image {ImageId} restored by user {UserId}", imageId, userId);
                    return true;
                }
            }

            _logger.LogWarning("Image {ImageId} cannot be restored - outside grace period for user {UserId}", imageId, userId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring image {ImageId} for user {UserId}", imageId, userId);
            return false;
        }
    }

    public async Task<ProcessedImage?> GetImageRetentionInfoAsync(int imageId, string userId)
    {
        try
        {
            var userProfile = await _context.UserProfiles
                .FirstOrDefaultAsync(up => up.UserId == userId);

            if (userProfile == null)
            {
                return null;
            }

            return await _context.ProcessedImages
                .FirstOrDefaultAsync(img => img.Id == imageId && 
                                          img.UserProfileId == userProfile.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving retention info for image {ImageId} for user {UserId}", imageId, userId);
            return null;
        }
    }
}