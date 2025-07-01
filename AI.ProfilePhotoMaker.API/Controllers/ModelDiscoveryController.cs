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

            _logger.LogInformation("Starting model discovery and sync for user {UserId}", userId);

            var result = await _modelDiscoveryService.DiscoverAndSyncUserModelsAsync(userId);

            if (result.Success)
            {
                _logger.LogInformation("Model discovery completed successfully for user {UserId}. Found: {Found}, Added: {Added}, Removed: {Removed}", 
                    userId, result.ModelsFound, result.ModelsAdded, result.ModelsRemoved);
                
                return Ok(new { 
                    success = true, 
                    data = result,
                    message = result.Message 
                });
            }
            else
            {
                _logger.LogWarning("Model discovery failed for user {UserId}: {Message}", userId, result.Message);
                return BadRequest(new { 
                    success = false, 
                    error = result.Message 
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during model discovery and sync");
            return StatusCode(500, new { 
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

            return Ok(new { 
                success = true, 
                data = result 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during quick model check");
            return StatusCode(500, new { 
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

            return Ok(new { 
                success = true, 
                data = status 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting model sync status");
            return StatusCode(500, new { 
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
                request.ModelId, request.VersionId, userId);

            var success = await _modelDiscoveryService.SyncSpecificModelAsync(userId, request.ModelId, request.VersionId);

            if (success)
            {
                return Ok(new { 
                    success = true, 
                    message = $"Successfully synced model {request.ModelId}" 
                });
            }
            else
            {
                return BadRequest(new { 
                    success = false, 
                    error = $"Failed to sync model {request.ModelId}" 
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing specific model {ModelId}", request.ModelId);
            return StatusCode(500, new { 
                success = false, 
                error = "Internal server error syncing specific model" 
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