using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Controllers.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AI.ProfilePhotoMaker.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class TrainingController : ControllerBase
{
    private readonly IReplicateApiClient _replicateApiClient;
    private readonly IBasicTierService _basicTierService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TrainingController> _logger;

    public TrainingController(
        IReplicateApiClient replicateApiClient,
        IBasicTierService basicTierService,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        IConfiguration configuration,
        ILogger<TrainingController> logger)
    {
        _replicateApiClient = replicateApiClient;
        _basicTierService = basicTierService;
        _userManager = userManager;
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Initiates model training for a user (requires credits)
    /// </summary>
    [HttpPost("train")]
    [Route("~/api/replicate/train")]
    public async Task<IActionResult> TrainModel([FromBody] TrainModelRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, error = new { code = "InvalidModel", message = "Invalid input." } });

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });

        // Check if user already has a trained model to prevent expensive re-training
        var existingModel = await _dbContext.ModelCreationRequests
            .Where(m => m.UserId == userId && m.Status == ModelCreationStatus.Ready)
            .OrderByDescending(m => m.CompletedAt)
            .FirstOrDefaultAsync();

        if (existingModel != null)
        {
            _logger.LogWarning(
                "User {UserId} attempted to train a new model but already has trained model {ModelId}",
                LoggingSanitizer.SanitizeId(userId),
                LoggingSanitizer.SanitizeId(existingModel.ReplicateModelId));
            return BadRequest(new
            {
                success = false,
                error = new
                {
                    code = "ModelAlreadyTrained",
                    message = $"You already have a trained model ({existingModel.ReplicateModelId}). You can generate photos using your existing model instead of training a new one."
                }
            });
        }

        // Get user profile for credit checking
        var userProfile = await _dbContext.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);

        // Check if user has sufficient credits for training (15 credits required)
        var availableCredits = await _basicTierService.GetAvailableCreditsAsync(userId);
        var requiredCredits = CreditCostConfig.GetCreditCost("model_training");

        if (availableCredits < requiredCredits)
        {
            return BadRequest(new
            {
                success = false,
                error = new
                {
                    code = "InsufficientCredits",
                    message = $"Model training requires {requiredCredits} credits. You have {availableCredits} credits. Please purchase more credits to train custom models."
                }
            });
        }

        // Create a stable job correlation id so credit usage/refunds can be attributed to this training request.
        // This id is also used as the ModelCreationRequest.Id.
        var trainingJobId = Guid.NewGuid().ToString();

        // Consume credits BEFORE calling Replicate; refund on failure
        var trainingCredits = await _basicTierService.ConsumeCreditsAsync(
            userId,
            "model_training",
            trainingJobId,
            HttpContext?.RequestAborted ?? CancellationToken.None);
        if (!trainingCredits.Success)
        {
            _logger.LogError("Failed to consume training credits for user {UserId} before starting Replicate training", LoggingSanitizer.SanitizeId(userId));
            return StatusCode(500, new
            {
                success = false,
                error = new { code = "CreditConsumptionFailed", message = "Unable to charge credits for training. Please try again." }
            });
        }

        try
        {
            // Convert image ZIP URL to external API format before passing to Replicate
            var externalImageZipUrl = ReplicateHelpers.ConvertToExternalApiUrl(dto.ImageZipUrl, _configuration, _logger);
            _logger.LogInformation("Converted ZIP URL from {OriginalUrl} to {ExternalUrl} for Replicate API",
                LoggingSanitizer.Sanitize(dto.ImageZipUrl),
                LoggingSanitizer.Sanitize(externalImageZipUrl));

            // Enforce user context: trust authenticated user over DTO
            if (!string.IsNullOrEmpty(dto.UserId) && !string.Equals(dto.UserId, userId, StringComparison.Ordinal))
            {
                await _basicTierService.RefundCreditsAsync(userId, trainingCredits);
                return BadRequest(new
                {
                    success = false,
                    error = new { code = "InvalidUserContext", message = "Request user does not match authenticated user." }
                });
            }

            var result = await _replicateApiClient.CreateModelTrainingAsync(userId, externalImageZipUrl, trainingJobId);

            var remainingCredits = await _basicTierService.GetAvailableCreditsAsync(userId);

            return Ok(new
            {
                success = true,
                data = new
                {
                    prediction = result,
                    creditsRemaining = remainingCredits,
                    creditsCost = requiredCredits
                },
                error = (object?)null
            });
        }
        catch (ReplicateApiException ex)
        {
            await _basicTierService.RefundCreditsAsync(userId, trainingCredits);
            _logger.LogError(ex, "Replicate API error during training for user {UserId}: {Status} {Code}",
                LoggingSanitizer.SanitizeId(userId),
                (int)ex.StatusCode,
                LoggingSanitizer.Sanitize(ex.ErrorCode));
            var status = ex.StatusCode switch
            {
                System.Net.HttpStatusCode.UnprocessableEntity => 400,
                System.Net.HttpStatusCode.Forbidden => 500,
                System.Net.HttpStatusCode.TooManyRequests => 429,
                _ => 500
            };

            // Truncate raw details to avoid excessively large payloads
            var raw = ex.RawContent ?? string.Empty;
            if (raw.Length > 800) raw = raw.Substring(0, 800) + "...";

            return StatusCode(status, new
            {
                success = false,
                error = new
                {
                    code = ex.ErrorCode,
                    message = ex.Message,
                    statusCode = (int)ex.StatusCode,
                    details = raw
                }
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            await _basicTierService.RefundCreditsAsync(userId, trainingCredits);
            _logger.LogError(ex, "Replicate auth failed during training for user {UserId}",
                LoggingSanitizer.SanitizeId(userId));
            return StatusCode(500, new
            {
                success = false,
                error = new
                {
                    code = "ReplicateAuthFailed",
                    message = "Replicate API authentication failed. Check your API token."
                }
            });
        }
        catch (InvalidOperationException ex)
        {
            await _basicTierService.RefundCreditsAsync(userId, trainingCredits);
            _logger.LogError(ex, "Replicate configuration error during training for user {UserId}",
                LoggingSanitizer.SanitizeId(userId));
            return StatusCode(500, new
            {
                success = false,
                error = new
                {
                    code = "ReplicateConfigError",
                    message = ex.Message
                }
            });
        }
        catch (Exception ex)
        {
            await _basicTierService.RefundCreditsAsync(userId, trainingCredits);
            _logger.LogError(ex, "Training failed for user {UserId}",
                LoggingSanitizer.SanitizeId(userId));
            // If training fails, we might want to refund the credit later.
            return StatusCode(500, new
            {
                success = false,
                error = new
                {
                    code = "TrainingFailed",
                    message = "Failed to start model training. Please try again later."
                }
            });
        }
    }

    /// <summary>
    /// Gets the status of a model training
    /// </summary>
    [HttpGet("status/{trainingId}")]
    [Route("~/api/replicate/train/status/{trainingId}")]
    public async Task<IActionResult> GetTrainingStatus(string trainingId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });

        // Enforce ownership: require a matching pending/creating model request for this user and trainingId
        var ownsTraining = await _dbContext.ModelCreationRequests
            .AnyAsync(m => m.UserId == userId && m.PendingTrainingRequestId == trainingId);

        if (!ownsTraining)
        {
            return NotFound(new { success = false, error = new { code = "NotFound", message = "Training not found." } });
        }

        var result = await _replicateApiClient.GetTrainingStatusAsync(trainingId);
        return Ok(new { success = true, data = result, error = (object?)null });
    }

    /// <summary>
    /// Finalizes a completed training by resolving and persisting the user's trained model version.
    /// This accelerates the background poller by performing the same work on-demand.
    /// </summary>
    [HttpPost("finalize/{trainingId}")]
    [Route("~/api/replicate/train/finalize/{trainingId}")]
    public async Task<IActionResult> FinalizeTraining(string trainingId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });

        // Verify the training belongs to the current user
        var modelRequest = await _dbContext.ModelCreationRequests
            .FirstOrDefaultAsync(m => m.UserId == userId && m.PendingTrainingRequestId == trainingId);

        if (modelRequest == null)
        {
            return NotFound(new { success = false, error = new { code = "NotFound", message = "Training not found." } });
        }

        try
        {
            // Resolve service on-demand to avoid changing constructor signature
            var trainingPollingService = HttpContext.RequestServices.GetRequiredService<ITrainingPollingService>();
            // Run the same completion processing used by the background poller
            await trainingPollingService.ProcessTrainingCompletion(trainingId);

            // Re-read current state
            await _dbContext.Entry(modelRequest).ReloadAsync();

            if (modelRequest.Status == ModelCreationStatus.Ready &&
                !string.IsNullOrEmpty(modelRequest.TrainedModelVersion) &&
                !string.IsNullOrEmpty(modelRequest.ReplicateModelId))
            {
                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        ready = true,
                        modelId = modelRequest.ReplicateModelId,
                        version = modelRequest.TrainedModelVersion,
                        status = modelRequest.Status.ToString(),
                        completedAt = modelRequest.CompletedAt
                    },
                    error = (object?)null
                });
            }

            if (modelRequest.Status == ModelCreationStatus.Failed)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = new
                    {
                        code = "TrainingFinalizeFailed",
                        message = modelRequest.ErrorMessage ?? "Training finalized with failure. Please retry training.",
                        status = modelRequest.Status.ToString()
                    }
                });
            }

            // Still finalizing; advise client to retry shortly
            return StatusCode(202, new
            {
                success = true,
                data = new
                {
                    ready = false,
                    status = modelRequest.Status.ToString(),
                    retryAfterSeconds = 15
                },
                error = (object?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finalizing training {TrainingId} for user {UserId}",
                LoggingSanitizer.SanitizeId(trainingId),
                LoggingSanitizer.SanitizeId(userId));
            return StatusCode(500, new
            {
                success = false,
                error = new { code = "FinalizeError", message = ex.Message }
            });
        }
    }
}
