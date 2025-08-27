using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using Microsoft.AspNetCore.Mvc;

namespace AI.ProfilePhotoMaker.API.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IReplicateApiClient _replicateApiClient;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IReplicateApiClient replicateApiClient, ILogger<AdminController> logger)
    {
        _replicateApiClient = replicateApiClient;
        _logger = logger;
    }

    /// <summary>
    /// Clean up orphaned models that exist on Replicate but not in database
    /// This endpoint bypasses frontend discovery logic to handle orphaned models
    /// </summary>
    [HttpPost("cleanup-orphaned-model")]
    public async Task<IActionResult> CleanupOrphanedModel([FromBody] CleanupRequest request)
    {
        try
        {
            _logger.LogWarning("🧹 Starting orphaned model cleanup for: {ModelId}", request.ModelId);
            
            if (string.IsNullOrEmpty(request.ModelId))
            {
                return BadRequest(new { success = false, error = "ModelId is required" });
            }

            // Call our enhanced deletion logic directly (tests the JSON parsing fix)
            var (success, errorMessage) = await _replicateApiClient.DeleteModelAsync(request.ModelId);
            
            if (success)
            {
                _logger.LogInformation("✅ Successfully cleaned up orphaned model: {ModelId}", request.ModelId);
                return Ok(new 
                { 
                    success = true, 
                    message = $"Orphaned model {request.ModelId} successfully deleted from Replicate",
                    modelId = request.ModelId
                });
            }
            else
            {
                _logger.LogError("❌ Failed to cleanup orphaned model {ModelId}: {Error}", request.ModelId, errorMessage);
                return BadRequest(new 
                { 
                    success = false, 
                    error = errorMessage ?? "Failed to delete orphaned model",
                    modelId = request.ModelId
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Exception during orphaned model cleanup for {ModelId}", request.ModelId);
            return StatusCode(500, new 
            { 
                success = false, 
                error = "Internal server error during cleanup",
                details = ex.Message,
                modelId = request.ModelId
            });
        }
    }
}

public class CleanupRequest
{
    public string ModelId { get; set; } = string.Empty;
}