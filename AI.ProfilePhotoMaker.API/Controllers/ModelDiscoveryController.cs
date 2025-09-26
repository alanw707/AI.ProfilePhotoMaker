using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>
/// Controller for model discovery and synchronization with Replicate
/// </summary>
[ApiController]
[Route("api/model-discovery")]
[Authorize]
public class ModelDiscoveryController : ControllerBase
{
    private readonly IModelDiscoveryService _modelDiscoveryService;
    private readonly ILogger<ModelDiscoveryController> _logger;
    private static string S(string? value) => LoggingSanitizer.Sanitize(value);
    private static string Sid(string? value) => LoggingSanitizer.SanitizeId(value);

    public ModelDiscoveryController(
        IModelDiscoveryService modelDiscoveryService,
        ILogger<ModelDiscoveryController> logger)
    {
        _modelDiscoveryService = modelDiscoveryService;
        _logger = logger;
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    /// <summary>
    /// Discovers and syncs user models from Replicate to database
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> DiscoverAndSyncModels()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "User not authenticated" });
            }

            _logger.LogInformation("Starting model discovery and sync for user {UserId}", Sid(userId));

            var result = await _modelDiscoveryService.DiscoverAndSyncUserModelsAsync(userId);

            if (result.Success)
            {
                _logger.LogInformation("Model discovery completed successfully for user {UserId}. Found: {Found}, Added: {Added}, Removed: {Removed}",
                    Sid(userId), result.ModelsFound, result.ModelsAdded, result.ModelsRemoved);

                return Ok(new
                {
                    success = true,
                    data = result,
                    message = result.Message
                });
            }
            else
            {
                _logger.LogWarning("Model discovery failed for user {UserId}: {Message}", Sid(userId), S(result.Message));
                return BadRequest(new
                {
                    success = false,
                    error = result.Message
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during model discovery and sync");
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error during model discovery"
            });
        }
    }

    /// <summary>
    /// Quick database-only check for user's trained model (no API calls)
    /// </summary>
    [HttpGet("check")]
    public async Task<IActionResult> QuickModelCheck()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "User not authenticated" });
            }

            var result = await _modelDiscoveryService.QuickDatabaseCheckAsync(userId);

            return Ok(new
            {
                success = true,
                data = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during quick model check");
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error during quick model check"
            });
        }
    }

    /// <summary>
    /// Gets the current model sync status for the authenticated user
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetModelSyncStatus()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "User not authenticated" });
            }

            var status = await _modelDiscoveryService.GetModelSyncStatusAsync(userId);

            return Ok(new
            {
                success = true,
                data = status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting model sync status");
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error getting model sync status"
            });
        }
    }

    /// <summary>
    /// Manually syncs a specific model to the database
    /// </summary>
    [HttpPost("sync-specific")]
    public async Task<IActionResult> SyncSpecificModel([FromBody] SyncSpecificModelRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "User not authenticated" });
            }

            if (string.IsNullOrEmpty(request.ModelId))
            {
                return BadRequest(new { success = false, error = "ModelId is required" });
            }

            if (string.IsNullOrEmpty(request.VersionId))
            {
                return BadRequest(new { success = false, error = "VersionId is required" });
            }

            _logger.LogInformation("Manually syncing model {ModelId} version {VersionId} for user {UserId}",
                S(request.ModelId), S(request.VersionId), Sid(userId));

            var success = await _modelDiscoveryService.SyncSpecificModelAsync(userId, request.ModelId, request.VersionId);

            if (success)
            {
                return Ok(new
                {
                    success = true,
                    message = $"Successfully synced model {request.ModelId}"
                });
            }
            else
            {
                return BadRequest(new
                {
                    success = false,
                    error = $"Failed to sync model {request.ModelId}"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing specific model {ModelId}", S(request.ModelId));
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error syncing specific model"
            });
        }
    }

    /// <summary>
    /// Manual override for restoring a model that was incorrectly marked as deleted
    /// Admin/Support endpoint for handling false negative detections
    /// </summary>
    [HttpPost("override-deletion")]
    public async Task<IActionResult> OverrideModelDeletion([FromBody] OverrideModelDeletionRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "User not authenticated" });
            }

            if (string.IsNullOrEmpty(request.ModelId))
            {
                return BadRequest(new { success = false, error = "ModelId is required" });
            }

            if (string.IsNullOrEmpty(request.Reason))
            {
                return BadRequest(new { success = false, error = "Reason is required for audit trail" });
            }

            var overriddenBy = User.Identity?.Name ?? userId;

            _logger.LogWarning(
                "Manual override deletion request for model {ModelId} by {OverriddenBy}. " +
                "Target User: {UserId}, Reason: {Reason}",
                request.ModelId, overriddenBy, request.UserId ?? userId, request.Reason);

            var targetUserId = request.UserId ?? userId; // Allow admin to override for other users

            var success = await _modelDiscoveryService.OverrideModelDeletionAsync(
                targetUserId, request.ModelId, request.Reason, overriddenBy);

            if (success)
            {
                return Ok(new
                {
                    success = true,
                    message = $"Successfully restored model {request.ModelId} for user {targetUserId}",
                    auditInfo = new
                    {
                        modelId = request.ModelId,
                        userId = targetUserId,
                        overriddenBy = overriddenBy,
                        reason = request.Reason,
                        timestamp = DateTime.UtcNow
                    }
                });
            }
            else
            {
                return BadRequest(new
                {
                    success = false,
                    error = $"Failed to restore model {request.ModelId}. Model may not exist or verification failed."
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during model deletion override for {ModelId}", S(request.ModelId));
            return StatusCode(500, new
            {
                success = false,
                error = "Internal server error during model deletion override"
            });
        }
    }
}

/// <summary>
/// Request model for syncing a specific model
/// </summary>
public class SyncSpecificModelRequest
{
    public string ModelId { get; set; } = string.Empty;
    public string VersionId { get; set; } = string.Empty;
}

/// <summary>
/// Request model for overriding model deletion
/// </summary>
public class OverrideModelDeletionRequest
{
    public string ModelId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? UserId { get; set; } // Optional - for admin override of other users
}