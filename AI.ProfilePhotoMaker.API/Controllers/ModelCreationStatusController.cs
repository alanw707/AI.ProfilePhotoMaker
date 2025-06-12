using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>
/// Controller for checking model creation and training status
/// </summary>
[ApiController]
[Route("api/model-creation")]
public class ModelCreationStatusController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ModelCreationStatusController> _logger;

    public ModelCreationStatusController(
        ApplicationDbContext context,
        ILogger<ModelCreationStatusController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get status of a model creation request
    /// </summary>
    [HttpGet("status/{requestId}")]
    public async Task<IActionResult> GetModelCreationStatus(string requestId)
    {
        try
        {
            var modelRequest = await _context.ModelCreationRequests
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (modelRequest == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Model creation request not found",
                    error = new { code = "NotFound", message = $"Request ID {requestId} not found" }
                });
            }

            return Ok(new
            {
                success = true,
                message = "Model creation status retrieved successfully",
                data = new
                {
                    requestId = modelRequest.Id,
                    userId = modelRequest.UserId,
                    modelName = modelRequest.ModelName,
                    replicateModelId = modelRequest.ReplicateModelId,
                    trainedModelVersion = modelRequest.TrainedModelVersion,
                    status = modelRequest.Status.ToString().ToLower(),
                    trainingImageZipUrl = modelRequest.TrainingImageZipUrl,
                    pendingTrainingRequestId = modelRequest.PendingTrainingRequestId,
                    createdAt = modelRequest.CreatedAt,
                    completedAt = modelRequest.CompletedAt,
                    errorMessage = modelRequest.ErrorMessage
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving model creation status for request {RequestId}", requestId);
            return StatusCode(500, new
            {
                success = false,
                message = "Error retrieving model creation status",
                error = new { code = "InternalError", message = ex.Message }
            });
        }
    }

    /// <summary>
    /// Get all model creation requests for a user
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserModelCreationRequests(string userId)
    {
        try
        {
            var modelRequests = await _context.ModelCreationRequests
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                message = $"Found {modelRequests.Count} model creation requests for user {userId}",
                data = modelRequests.Select(r => new
                {
                    requestId = r.Id,
                    modelName = r.ModelName,
                    replicateModelId = r.ReplicateModelId,
                    trainedModelVersion = r.TrainedModelVersion,
                    status = r.Status.ToString().ToLower(),
                    createdAt = r.CreatedAt,
                    completedAt = r.CompletedAt,
                    errorMessage = r.ErrorMessage
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving model creation requests for user {UserId}", userId);
            return StatusCode(500, new
            {
                success = false,
                message = "Error retrieving model creation requests",
                error = new { code = "InternalError", message = ex.Message }
            });
        }
    }

    /// <summary>
    /// Get all model creation requests (for debugging)
    /// </summary>
    [HttpGet("all")]
    public async Task<IActionResult> GetAllModelCreationRequests()
    {
        try
        {
            var modelRequests = await _context.ModelCreationRequests
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                message = $"Found {modelRequests.Count} total model creation requests",
                data = modelRequests.Select(r => new
                {
                    requestId = r.Id,
                    userId = r.UserId,
                    modelName = r.ModelName,
                    replicateModelId = r.ReplicateModelId,
                    trainedModelVersion = r.TrainedModelVersion,
                    status = r.Status.ToString().ToLower(),
                    createdAt = r.CreatedAt,
                    completedAt = r.CompletedAt,
                    errorMessage = r.ErrorMessage
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all model creation requests");
            return StatusCode(500, new
            {
                success = false,
                message = "Error retrieving model creation requests",
                error = new { code = "InternalError", message = ex.Message }
            });
        }
    }
}