using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>
/// Controller for checking model creation and training status
/// </summary>
[ApiController]
[Route("api/model-creation")]
[Authorize]
public class ModelCreationStatusController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ModelCreationStatusController> _logger;
    private readonly IReplicateApiClient _replicateApiClient;

    public ModelCreationStatusController(
        ApplicationDbContext context,
        ILogger<ModelCreationStatusController> logger,
        IReplicateApiClient replicateApiClient)
    {
        _context = context;
        _logger = logger;
        _replicateApiClient = replicateApiClient;
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    /// <summary>
    /// Get all model creation requests for the current authenticated user
    /// </summary>
    [HttpGet("user/current")]
    public async Task<IActionResult> GetCurrentUserModelRequests()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Authentication required",
                    error = new { code = "Unauthorized", message = "User not authenticated" }
                });
            }

            var modelRequests = await _context.ModelCreationRequests
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            // Find the most recent model marked as Ready (regardless of TrainedModelVersion) and validate against Replicate
            var readyModel = modelRequests
                .FirstOrDefault(r => r.Status == ModelCreationStatus.Ready);

            // Validate ready model against Replicate API to ensure it still exists
            if (readyModel != null && !string.IsNullOrEmpty(readyModel.ReplicateModelId))
            {
                await ValidateModelStatusAsync(readyModel);
            }

            // ModelCreationRequest is now the single source of truth for model data

            return Ok(new
            {
                success = true,
                message = $"Found {modelRequests.Count} model creation requests for current user",
                data = new
                {
                    totalRequests = modelRequests.Count,
                    hasTrainedModel = readyModel != null && !string.IsNullOrEmpty(readyModel.TrainedModelVersion),
                    latestTrainedModel = (readyModel != null && !string.IsNullOrEmpty(readyModel.TrainedModelVersion)) ? new
                    {
                        requestId = readyModel.Id,
                        modelName = readyModel.ModelName,
                        replicateModelId = readyModel.ReplicateModelId,
                        trainedModelVersion = readyModel.TrainedModelVersion,
                        completedAt = readyModel.CompletedAt
                    } : null,
                    allRequests = modelRequests.Select(r => new
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
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving model creation requests for current user");
            return StatusCode(500, new
            {
                success = false,
                message = "Error retrieving model creation requests",
                error = new { code = "InternalError", message = ex.Message }
            });
        }
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

            // Validate against Replicate API if model is marked as Ready
            if (modelRequest.Status == ModelCreationStatus.Ready && !string.IsNullOrEmpty(modelRequest.ReplicateModelId))
            {
                await ValidateModelStatusAsync(modelRequest);
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
    /// Validates model status against both database consistency and Replicate API, updates if inconsistencies found
    /// </summary>
    private async Task ValidateModelStatusAsync(ModelCreationRequest modelRequest)
    {
        try
        {
            bool needsUpdate = false;
            
            // First check database consistency
            if (modelRequest.Status == ModelCreationStatus.Ready)
            {
                // Check for inconsistent "ready" status with missing trained model or error messages
                if (string.IsNullOrEmpty(modelRequest.TrainedModelVersion) || !string.IsNullOrEmpty(modelRequest.ErrorMessage))
                {
                    _logger.LogWarning("Model {RequestId} marked as Ready but has inconsistent data: TrainedModelVersion={TrainedModelVersion}, ErrorMessage={ErrorMessage}", 
                        modelRequest.Id, modelRequest.TrainedModelVersion ?? "NULL", modelRequest.ErrorMessage ?? "NULL");
                    
                    modelRequest.Status = ModelCreationStatus.Failed;
                    if (string.IsNullOrEmpty(modelRequest.ErrorMessage))
                    {
                        modelRequest.ErrorMessage = "Model marked as ready but training data is incomplete";
                    }
                    needsUpdate = true;
                }
            }
            
            // Then check against Replicate API if we have a model ID and status is still ready
            if (modelRequest.Status == ModelCreationStatus.Ready && !string.IsNullOrEmpty(modelRequest.ReplicateModelId))
            {
                _logger.LogInformation("Validating model {ModelId} against Replicate API", modelRequest.ReplicateModelId);
                
                bool modelExists = await _replicateApiClient.CheckModelExistsAsync(modelRequest.ReplicateModelId);
                
                
                if (!modelExists)
                {
                    _logger.LogWarning("Model {ModelId} no longer exists on Replicate, updating status to Failed", modelRequest.ReplicateModelId);
                    
                    modelRequest.Status = ModelCreationStatus.Failed;
                    modelRequest.ErrorMessage = "Model was deleted from Replicate externally";
                    needsUpdate = true;
                }
            }
            else if (string.IsNullOrEmpty(modelRequest.ReplicateModelId))
            {
                _logger.LogWarning("Cannot validate model against Replicate API: ReplicateModelId is null or empty for request {RequestId}", modelRequest.Id);
            }
            
            // Save changes if any updates were made
            if (needsUpdate)
            {
                _context.ModelCreationRequests.Update(modelRequest);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Updated model request {RequestId} with corrected status: {Status}", modelRequest.Id, modelRequest.Status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating model {ModelId} against database and Replicate API", modelRequest.ReplicateModelId);
            // Don't fail the entire request if validation fails, just log the error
        }
    }
}