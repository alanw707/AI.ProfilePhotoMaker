using AI.ProfilePhotoMaker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AI.ProfilePhotoMaker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RetentionPolicyController : ControllerBase
{
    private readonly IRetentionPolicyService _retentionPolicyService;
    private readonly ILogger<RetentionPolicyController> _logger;

    public RetentionPolicyController(
        IRetentionPolicyService retentionPolicyService,
        ILogger<RetentionPolicyController> logger)
    {
        _retentionPolicyService = retentionPolicyService;
        _logger = logger;
    }

    /// <summary>
    /// Mark a specific image for immediate deletion
    /// </summary>
    [HttpDelete("images/{imageId}")]
    public async Task<IActionResult> DeleteImage(int imageId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var result = await _retentionPolicyService.RequestImageDeletionAsync(imageId, userId);
        
        if (!result)
        {
            return NotFound(new { success = false, message = "Image not found or already deleted" });
        }

        return Ok(new { success = true, message = "Image marked for deletion" });
    }

    /// <summary>
    /// Mark all user's images for immediate deletion
    /// </summary>
    [HttpDelete("images")]
    public async Task<IActionResult> DeleteAllImages()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var deletedCount = await _retentionPolicyService.RequestAllImagesDeletionAsync(userId);
        
        return Ok(new { 
            success = true, 
            message = $"Marked {deletedCount} images for deletion",
            deletedCount = deletedCount
        });
    }

    /// <summary>
    /// Get images that are scheduled for deletion
    /// </summary>
    [HttpGet("scheduled-deletions")]
    public async Task<IActionResult> GetScheduledDeletions()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var scheduledImages = await _retentionPolicyService.GetImagesScheduledForDeletionAsync(userId);
        
        var response = scheduledImages.Select(img => new
        {
            img.Id,
            img.OriginalImageUrl,
            img.ProcessedImageUrl,
            img.Style,
            img.CreatedAt,
            img.ScheduledDeletionDate,
            img.IsMarkedForDeletion,
            img.UserRequestedDeletionDate,
            img.IsOriginalUpload,
            img.IsGenerated,
            RetentionPeriodDays = img.IsOriginalUpload ? 7 : 30,
            CanRestore = img.UserRequestedDeletionDate.HasValue && 
                        DateTime.UtcNow - img.UserRequestedDeletionDate.Value <= TimeSpan.FromDays(1)
        }).ToList();

        return Ok(new
        {
            success = true,
            scheduledForDeletion = response.Count,
            images = response
        });
    }

    /// <summary>
    /// Restore an image that was marked for deletion (within grace period)
    /// </summary>
    [HttpPost("images/{imageId}/restore")]
    public async Task<IActionResult> RestoreImage(int imageId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var result = await _retentionPolicyService.RestoreImageAsync(imageId, userId);
        
        if (!result)
        {
            return BadRequest(new { 
                success = false, 
                message = "Image not found, not eligible for restoration, or outside grace period" 
            });
        }

        return Ok(new { success = true, message = "Image restored successfully" });
    }

    /// <summary>
    /// Get retention information for a specific image
    /// </summary>
    [HttpGet("images/{imageId}")]
    public async Task<IActionResult> GetImageRetentionInfo(int imageId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var image = await _retentionPolicyService.GetImageRetentionInfoAsync(imageId, userId);
        
        if (image == null)
        {
            return NotFound(new { success = false, message = "Image not found" });
        }

        var response = new
        {
            image.Id,
            image.OriginalImageUrl,
            image.ProcessedImageUrl,
            image.Style,
            image.CreatedAt,
            image.ScheduledDeletionDate,
            image.IsMarkedForDeletion,
            image.UserRequestedDeletionDate,
            image.IsDeleted,
            image.DeletedAt,
            image.IsOriginalUpload,
            image.IsGenerated,
            RetentionPeriodDays = image.IsOriginalUpload ? 7 : 30,
            DaysUntilDeletion = Math.Max(0, (int)(image.ScheduledDeletionDate - DateTime.UtcNow).TotalDays),
            CanRestore = image.IsMarkedForDeletion && 
                        image.UserRequestedDeletionDate.HasValue && 
                        DateTime.UtcNow - image.UserRequestedDeletionDate.Value <= TimeSpan.FromDays(1)
        };

        return Ok(new { success = true, data = response });
    }

    /// <summary>
    /// Get retention policy information and summary for the user
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetRetentionSummary()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var scheduledImages = await _retentionPolicyService.GetImagesScheduledForDeletionAsync(userId);
        
        var now = DateTime.UtcNow;
        var originalUploads = scheduledImages.Where(img => img.IsOriginalUpload).ToList();
        var generatedImages = scheduledImages.Where(img => img.IsGenerated).ToList();

        var summary = new
        {
            success = true,
            retentionPolicy = new
            {
                originalUploads = new
                {
                    retentionPeriod = "7 days",
                    description = "Input photos (original uploads) are deleted after 7 days"
                },
                generatedImages = new
                {
                    retentionPeriod = "30 days", 
                    description = "AI headshots (generated photos) are deleted after 30 days"
                },
                userControl = "You can delete your data faster anytime via account settings"
            },
            currentImages = new
            {
                totalScheduledForDeletion = scheduledImages.Count,
                originalUploadsScheduled = originalUploads.Count,
                generatedImagesScheduled = generatedImages.Count,
                nearestDeletion = scheduledImages.Any() ? 
                    scheduledImages.Min(img => img.ScheduledDeletionDate) : (DateTime?)null
            }
        };

        return Ok(summary);
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}