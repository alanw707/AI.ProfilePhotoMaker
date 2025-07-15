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
    /// Get images that are scheduled for deletion (expired)
    /// </summary>
    [HttpGet("expired-images")]
    public async Task<IActionResult> GetExpiredImages()
    {
        try
        {
            var expiredImages = await _retentionPolicyService.GetExpiredImagesAsync();

            return Ok(new
            {
                success = true,
                count = expiredImages.Count,
                images = expiredImages
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expired images");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    /// <summary>
    /// Manually trigger deletion of expired images (admin endpoint)
    /// </summary>
    [HttpPost("delete-expired")]
    public async Task<IActionResult> DeleteExpiredImages()
    {
        try
        {
            var deletedCount = await _retentionPolicyService.DeleteExpiredImagesAsync();

            return Ok(new
            {
                success = true,
                message = $"Deleted {deletedCount} expired images",
                deletedCount = deletedCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expired images");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    /// <summary>
    /// Set retention dates for existing images that don't have them
    /// </summary>
    [HttpPost("initialize-retention-dates")]
    public async Task<IActionResult> InitializeRetentionDates()
    {
        try
        {
            await _retentionPolicyService.SetRetentionDatesForExistingImagesAsync();

            return Ok(new
            {
                success = true,
                message = "Retention dates initialized for existing images"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing retention dates");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get retention policy information
    /// </summary>
    [HttpGet("policy-info")]
    public IActionResult GetPolicyInfo()
    {
        var policyInfo = new
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
                backgroundService = new
                {
                    checkInterval = "Every 6 hours",
                    description = "Background service automatically checks and deletes expired images"
                }
            }
        };

        return Ok(policyInfo);
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}