using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>
/// Debug controller for troubleshooting model detection issues
/// </summary>
[ApiController]
[Route("api/debug")]
public class DebugController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IReplicateApiClient _replicateApiClient;
    private readonly IModelDiscoveryService _modelDiscoveryService;
    private readonly ILogger<DebugController> _logger;

    public DebugController(
        ApplicationDbContext context,
        IReplicateApiClient replicateApiClient,
        IModelDiscoveryService modelDiscoveryService,
        ILogger<DebugController> logger)
    {
        _context = context;
        _replicateApiClient = replicateApiClient;
        _modelDiscoveryService = modelDiscoveryService;
        _logger = logger;
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    /// <summary>
    /// Get comprehensive debug information about user's model status
    /// </summary>
    [HttpGet("user-model-status")]
    public async Task<IActionResult> GetUserModelDebugStatus()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "User not authenticated" });
            }

            // Get UserProfile data
            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);
            
            // Get ModelCreationRequest data
            var modelRequests = await _context.ModelCreationRequests
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            // Get ProcessedImages count
            var processedImagesCount = await _context.ProcessedImages
                .Where(p => p.UserProfile.UserId == userId)
                .CountAsync();

            // Get OriginalStyle images count (uploaded selfies)
            var originalImagesCount = await _context.ProcessedImages
                .Where(p => p.UserProfile.UserId == userId && p.Style == "Original")
                .CountAsync();

            var debugInfo = new
            {
                userId = userId,
                timestamp = DateTime.UtcNow,
                userProfile = new
                {
                    exists = userProfile != null,
                    credits = userProfile?.Credits,
                    subscriptionTier = userProfile?.SubscriptionTier.ToString(),
                    lastCreditReset = userProfile?.LastCreditReset,
                    note = "Model data moved to ModelCreationRequest table"
                },
                modelCreationRequests = new
                {
                    totalRequests = modelRequests.Count,
                    requests = modelRequests.Select(r => new
                    {
                        id = r.Id,
                        modelName = r.ModelName,
                        replicateModelId = r.ReplicateModelId,
                        trainedModelVersion = r.TrainedModelVersion,
                        status = r.Status.ToString(),
                        createdAt = r.CreatedAt,
                        completedAt = r.CompletedAt,
                        errorMessage = r.ErrorMessage,
                        trainingImageZipUrl = r.TrainingImageZipUrl,
                        pendingTrainingRequestId = r.PendingTrainingRequestId
                    }).ToList(),
                    readyModels = modelRequests
                        .Where(r => r.Status == ModelCreationStatus.Ready && !string.IsNullOrEmpty(r.TrainedModelVersion))
                        .Select(r => new
                        {
                            modelName = r.ModelName,
                            replicateModelId = r.ReplicateModelId,
                            trainedModelVersion = r.TrainedModelVersion,
                            completedAt = r.CompletedAt
                        }).ToList()
                },
                images = new
                {
                    totalProcessedImages = processedImagesCount,
                    originalUploadedImages = originalImagesCount
                },
                detectionLogic = new
                {
                    tier1_userProfileHasModel = false /* Model data moved to ModelCreationRequest */,
                    tier2_modelRequestsHaveReady = modelRequests.Any(r => r.Status == ModelCreationStatus.Ready && !string.IsNullOrEmpty(r.TrainedModelVersion)),
                    tier3_pendingTraining = modelRequests.Any(r => r.Status == ModelCreationStatus.Creating || r.Status == ModelCreationStatus.Pending),
                    expectedUserPattern = $"user-{userId}",
                    recommendedAction = GetRecommendedAction(userProfile, modelRequests)
                }
            };

            return Ok(new { success = true, data = debugInfo });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user model debug status");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    private string GetRecommendedAction(UserProfile? userProfile, List<ModelCreationRequest> modelRequests)
    {
        if (false /* Model data moved to ModelCreationRequest */)
        {
            return "Model found in UserProfile - should show as trained";
        }

        var readyModel = modelRequests.FirstOrDefault(r => r.Status == ModelCreationStatus.Ready && !string.IsNullOrEmpty(r.TrainedModelVersion));
        if (readyModel != null)
        {
            return $"Model found in ModelCreationRequest but not synced to UserProfile. Should sync: {readyModel.ReplicateModelId}";
        }

        if (modelRequests.Any(r => r.Status == ModelCreationStatus.Creating || r.Status == ModelCreationStatus.Pending))
        {
            return "Training in progress - wait for completion";
        }

        return "No trained model found - user needs to start training";
    }

    /// <summary>
    /// Test the model-creation endpoint that dashboard uses
    /// </summary>
    [HttpGet("test-model-creation-endpoint")]
    public async Task<IActionResult> TestModelCreationEndpoint()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "User not authenticated" });
            }

            // This simulates what the dashboard calls
            var modelRequests = await _context.ModelCreationRequests
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var completedModel = modelRequests
                .FirstOrDefault(r => r.Status == ModelCreationStatus.Ready && !string.IsNullOrEmpty(r.TrainedModelVersion));

            var result = new
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
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing model creation endpoint");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Discover user models by directly scanning Replicate API
    /// </summary>
    [HttpGet("discover-user-models/{userId}")]
    public async Task<IActionResult> DiscoverUserModels(string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { success = false, error = "UserId is required" });
            }

            _logger.LogInformation("Starting direct Replicate API scan for user {UserId}", userId);

            var discoveredModels = await _replicateApiClient.FindUserModelsByPatternAsync(userId);

            var result = new
            {
                success = true,
                message = $"Found {discoveredModels.Count} models via direct Replicate API scan",
                data = new
                {
                    userId = userId,
                    searchPattern = $"user-{userId}-*",
                    discoveredModels = discoveredModels.Select(model => new
                    {
                        name = model.Name,
                        owner = model.Owner,
                        fullModelId = $"{model.Owner}/{model.Name}",
                        latestVersion = model.LatestVersion,
                        createdAt = model.CreatedAt,
                        updatedAt = model.UpdatedAt,
                        description = model.Description,
                        runCount = model.RunCount,
                        coverImageUrl = model.CoverImageUrl
                    }).ToList(),
                    hasModels = discoveredModels.Count > 0,
                    mostRecentModel = discoveredModels.FirstOrDefault()
                }
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering user models via Replicate API");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Test if the specific model mentioned by user exists on Replicate
    /// </summary>
    [HttpGet("test-specific-model")]
    public async Task<IActionResult> TestSpecificModel()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "User not authenticated" });
            }

            // Test the specific model mentioned by user
            string specificModelId = "alanw707/user-b99678bd-cb87-40c1-a7bf-b889f1e00c08-20250630040811";
            
            _logger.LogInformation("Testing specific model existence: {ModelId}", specificModelId);

            bool modelExists = await _replicateApiClient.CheckModelExistsAsync(specificModelId);

            var result = new
            {
                success = true,
                message = $"Tested specific model: {specificModelId}",
                data = new
                {
                    modelId = specificModelId,
                    exists = modelExists,
                    userId = userId,
                    expectedPattern = $"user-{userId}",
                    conclusion = modelExists ? 
                        "Model exists - there may be a database sync issue" : 
                        "Model does not exist on Replicate - user needs to train a new model"
                }
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing specific model");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Manually syncs the known model for your specific user ID (emergency fix)
    /// </summary>
    [HttpPost("manual-model-sync")]
    public async Task<IActionResult> ManualModelSync()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            // For your specific user, manually sync the known model
            if (userId == "b99678bd-cb87-40c1-a7bf-b889f1e00c08")
            {
                var knownModelId = "alanw707/user-b99678bd-cb87-40c1-a7bf-b889f1e00c08-20250630040811";
                
                // Use the model discovery service to sync this specific model
                var syncResult = await _modelDiscoveryService.SyncSpecificModelAsync(userId, knownModelId, null);
                
                if (syncResult)
                {
                    return Ok(new { 
                        success = true, 
                        message = $"Successfully synced model {knownModelId} to UserProfile",
                        modelId = knownModelId
                    });
                }
                else
                {
                    return BadRequest(new { 
                        success = false, 
                        message = "Failed to sync model - model may not exist on Replicate"
                    });
                }
            }
            else
            {
                return BadRequest(new { 
                    success = false, 
                    message = "Manual sync only available for specific test user"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during manual model sync for user {UserId}", userId);
            return StatusCode(500, new { 
                success = false, 
                error = ex.Message 
            });
        }
    }

    /// <summary>
    /// Repairs model versions by fetching actual version IDs from Replicate API
    /// </summary>
    [HttpPost("repair-model-versions")]
    public async Task<IActionResult> RepairModelVersions()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = "User not authenticated" });
            }

            _logger.LogInformation("Starting model version repair for user {UserId}", userId);

            var repairResult = await _modelDiscoveryService.RepairModelVersionsAsync(userId);

            return Ok(new
            {
                success = repairResult.Success,
                message = repairResult.Success ? 
                    $"Repair completed. Found: {repairResult.ModelsFound}, Repaired: {repairResult.ModelsRepaired}, Errors: {repairResult.ModelsWithErrors}" :
                    $"Repair failed: {repairResult.ErrorMessage}",
                data = repairResult
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during model version repair");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Repairs model versions for a specific user (testing endpoint)
    /// </summary>
    [HttpPost("repair-model-versions/{userId}")]
    public async Task<IActionResult> RepairModelVersionsForUser(string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { success = false, error = "UserId is required" });
            }

            _logger.LogInformation("Starting model version repair for user {UserId}", userId);

            var repairResult = await _modelDiscoveryService.RepairModelVersionsAsync(userId);

            return Ok(new
            {
                success = repairResult.Success,
                message = repairResult.Success ? 
                    $"Repair completed. Found: {repairResult.ModelsFound}, Repaired: {repairResult.ModelsRepaired}, Errors: {repairResult.ModelsWithErrors}" :
                    $"Repair failed: {repairResult.ErrorMessage}",
                data = repairResult
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during model version repair for user {UserId}", userId);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Test the new ModelDiscoveryService to see if it finds and syncs models
    /// </summary>
    [HttpPost("test-model-discovery/{userId}")]
    public async Task<IActionResult> TestModelDiscovery(string userId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { success = false, error = "UserId is required" });
            }

            _logger.LogInformation("Testing ModelDiscoveryService for user {UserId}", userId);

            // Get user state before discovery
            var userProfileBefore = await _context.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);
            
            // Run model discovery
            var discoveryResult = await _modelDiscoveryService.DiscoverAndSyncUserModelsAsync(userId);
            
            // Get user state after discovery
            var userProfileAfter = await _context.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);
            
            // Get sync status
            var syncStatus = await _modelDiscoveryService.GetModelSyncStatusAsync(userId);

            var result = new
            {
                success = true,
                message = "Model discovery test completed",
                data = new
                {
                    userId = userId,
                    userProfileBefore = "Model fields moved to ModelCreationRequest",
                    discoveryResult = discoveryResult,
                    userProfileAfter = "Model fields moved to ModelCreationRequest",
                    syncStatus = syncStatus,
                    changesDetected = false // Model data now in ModelCreationRequest
                }
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing model discovery");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}