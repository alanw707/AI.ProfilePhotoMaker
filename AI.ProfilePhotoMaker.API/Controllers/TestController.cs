using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class TestController : ControllerBase
{
    private readonly IReplicateApiClient _replicateApiClient;
    private readonly ILogger<TestController> _logger;
    private readonly ApplicationDbContext _context;

    public TestController(IReplicateApiClient replicateApiClient, ILogger<TestController> logger, ApplicationDbContext context)
    {
        _replicateApiClient = replicateApiClient;
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Test Replicate API connection by attempting to list available models
    /// </summary>
    [HttpGet("replicate-connection")]
    public async Task<IActionResult> TestReplicateConnection()
    {
        try
        {
            // Test API connection by making a simple request
            // We'll test with a known model version to see if our credentials work
            var testResult = await _replicateApiClient.GetPredictionStatusAsync("dummy-id");
            
            // If we get here without an auth error, connection is working
            return Ok(new { 
                success = true, 
                message = "Replicate API connection successful", 
                data = new { connectionStatus = "Connected" },
                error = (object?)null 
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Replicate API authentication failed");
            return Ok(new { 
                success = false, 
                message = "Replicate API authentication failed", 
                data = (object?)null,
                error = new { code = "AuthError", message = ex.Message }
            });
        }
        catch (Exception ex) when (ex.Message.Contains("not found") || ex.Message.Contains("404"))
        {
            // Expected error for dummy ID - means auth worked but resource doesn't exist
            return Ok(new { 
                success = true, 
                message = "Replicate API connection successful (test prediction not found as expected)", 
                data = new { connectionStatus = "Connected", authStatus = "Valid" },
                error = (object?)null 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Replicate API connection test failed");
            return Ok(new { 
                success = false, 
                message = "Replicate API connection failed", 
                data = (object?)null,
                error = new { code = "ConnectionError", message = ex.Message }
            });
        }
    }

    /// <summary>
    /// Test the complete training workflow with real images
    /// WARNING: This will consume Replicate credits!
    /// </summary>
    [HttpPost("replicate-training-test")]
    public async Task<IActionResult> TestReplicateTraining([FromBody] TestTrainingRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.ImageZipUrl) || string.IsNullOrEmpty(request.UserId))
            {
                return BadRequest(new { 
                    success = false, 
                    error = new { code = "InvalidInput", message = "ImageZipUrl and UserId are required" } 
                });
            }

            _logger.LogInformation("Starting polling-based Replicate training test for user {UserId}", request.UserId);

            // Start polling-based model creation and training
            var modelCreationRequestId = await _replicateApiClient.InitiateModelCreationAndTrainingAsync(request.UserId, request.ImageZipUrl);

            return Ok(new {
                success = true,
                message = "Model creation and training initiated successfully (polling-based)",
                data = new {
                    modelCreationRequestId = modelCreationRequestId,
                    status = "pending",
                    note = "Training will start automatically when model creation completes via background polling"
                },
                error = (object?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Replicate training test failed for user {UserId}", request.UserId);
            return StatusCode(500, new { 
                success = false, 
                message = "Training test failed", 
                data = (object?)null,
                error = new { code = "TrainingError", message = ex.Message }
            });
        }
    }

    /// <summary>
    /// Test image generation with a specific trained model
    /// WARNING: This will consume Replicate credits!
    /// </summary>
    [HttpPost("replicate-generation-test")]
    public async Task<IActionResult> TestReplicateGeneration([FromBody] TestGenerationRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.TrainedModelVersion) || string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.Style))
            {
                return BadRequest(new { 
                    success = false, 
                    error = new { code = "InvalidInput", message = "TrainedModelVersion, UserId, and Style are required" } 
                });
            }

            _logger.LogInformation("Starting Replicate generation test for user {UserId} with style {Style}", request.UserId, request.Style);

            // Start image generation
            var generationResult = await _replicateApiClient.GenerateImagesAsync(
                request.TrainedModelVersion, 
                request.UserId, 
                request.Style, 
                request.UserInfo);

            return Ok(new { 
                success = true, 
                message = "Image generation started successfully", 
                data = new { 
                    predictionId = generationResult.Id,
                    status = generationResult.Status,
                    createdAt = generationResult.CreatedAt
                },
                error = (object?)null 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Replicate generation test failed for user {UserId}", request.UserId);
            return StatusCode(500, new { 
                success = false, 
                message = "Generation test failed", 
                data = (object?)null,
                error = new { code = "GenerationError", message = ex.Message }
            });
        }
    }
    /// <summary>
    /// Deletes all records from the ModelCreationRequests table
    /// </summary>
    [HttpDelete("clear-model-creation-requests")]
    public async Task<IActionResult> ClearModelCreationRequests()
    {
        try
        {
            var rowCount = await _context.ModelCreationRequests.CountAsync();
            if (rowCount == 0)
            {
                return Ok(new {
                    success = true,
                    message = "ModelCreationRequests table is already empty.",
                    data = new { rowsDeleted = 0 },
                    error = (object?)null
                });
            }

            // Use raw SQL for efficient deletion
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM ModelCreationRequests");

            return Ok(new {
                success = true,
                message = $"Successfully deleted {rowCount} rows from ModelCreationRequests table.",
                data = new { rowsDeleted = rowCount },
                error = (object?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear ModelCreationRequests table");
            return StatusCode(500, new {
                success = false,
                message = "Failed to clear ModelCreationRequests table.",
                data = (object?)null,
                error = new { code = "DatabaseError", message = ex.Message }
            });
        }
    }
}

/// <summary>
/// DTO for testing training workflow
/// </summary>
public record TestTrainingRequest(
    string UserId,
    string ImageZipUrl
);

/// <summary>
/// DTO for testing generation workflow
/// </summary>
public record TestGenerationRequest(
    string UserId,
    string TrainedModelVersion,
    string Style,
    Models.UserInfo? UserInfo = null
);