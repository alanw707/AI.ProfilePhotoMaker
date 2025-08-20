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
                    message = "User not authenticated",
                    error = new { code = "Unauthorized", message = "User ID not found in token" }
                });
            }

            var modelRequests = await _context.ModelCreationRequests
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            // Find the most recent completed model and validate against Replicate
            var completedModel = modelRequests
                .FirstOrDefault(r => r.Status == ModelCreationStatus.Ready && !string.IsNullOrEmpty(r.TrainedModelVersion));

            // Validate completed model against Replicate API to ensure it still exists
            if (completedModel != null && !string.IsNullOrEmpty(completedModel.ReplicateModelId))
            {
                await ValidateModelStatusAsync(completedModel);
            }

            // ModelCreationRequest is now the single source of truth for model data

            return Ok(new
            {
                success = true,
                message = $"Found {modelRequests.Count} model creation requests for current user",
                data = new
                {
                    totalRequests = modelRequests.Count,
                    hasTrainedModel = completedModel != null,
                    latestTrainedModel = completedModel != null ? new
                    {
                        requestId = completedModel.Id,
                        modelName = completedModel.ModelName,
                        replicateModelId = completedModel.ReplicateModelId,
                        trainedModelVersion = completedModel.TrainedModelVersion,
                        completedAt = completedModel.CompletedAt
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

    /// <summary>
    /// Validates model status against Replicate API and updates database if model no longer exists
    /// </summary>
    private async Task ValidateModelStatusAsync(ModelCreationRequest modelRequest)
    {
        try
        {
            if (string.IsNullOrEmpty(modelRequest.ReplicateModelId))
            {
                _logger.LogWarning("Cannot validate model: ReplicateModelId is null or empty for request {RequestId}", modelRequest.Id);
                return;
            }

            _logger.LogInformation("Validating model {ModelId} against Replicate API", modelRequest.ReplicateModelId);
            
            bool modelExists = await _replicateApiClient.CheckModelExistsAsync(modelRequest.ReplicateModelId);
            
            if (!modelExists)
            {
                _logger.LogWarning("Model {ModelId} no longer exists on Replicate, updating status to Failed", modelRequest.ReplicateModelId);
                
                // Update model status to Failed since it was deleted from Replicate
                modelRequest.Status = ModelCreationStatus.Failed;
                modelRequest.ErrorMessage = "Model was deleted from Replicate externally";
                
                // Save changes to database
                _context.ModelCreationRequests.Update(modelRequest);
                await _context.SaveChangesAsync();
            }
            else
            {
                _logger.LogInformation("Model {ModelId} validated successfully on Replicate", modelRequest.ReplicateModelId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating model {ModelId} against Replicate API", modelRequest.ReplicateModelId);
            // Don't fail the entire request if validation fails, just log the error
        }
    }
}