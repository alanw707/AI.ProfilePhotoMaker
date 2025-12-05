using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using AI.ProfilePhotoMaker.API.Models.DTOs;

namespace AI.ProfilePhotoMaker.API.Controllers;

public class StyleGenerationResult
{
    public string Style { get; set; } = string.Empty;
    public bool Success { get; set; }
    public object? Result { get; set; }
    public string? Error { get; set; }
}

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ReplicateController : ControllerBase
{
    private readonly IReplicateApiClient _replicateApiClient;
    private readonly IBasicTierService _basicTierService;
    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReplicateController> _logger;
    private readonly IPendingGenerationService _pendingGenerationService;

    private static string Sid(string? value) => LoggingSanitizer.SanitizeId(value);
    private static string S(string? value) => LoggingSanitizer.Sanitize(value);

    public ReplicateController(
        IReplicateApiClient replicateApiClient,
        IBasicTierService basicTierService,
        ApplicationDbContext dbContext,
        IConfiguration configuration,
        ILogger<ReplicateController> logger,
        IPendingGenerationService pendingGenerationService)
    {
        _replicateApiClient = replicateApiClient;
        _basicTierService = basicTierService;
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
        _pendingGenerationService = pendingGenerationService;
    }

    /// <summary>
    /// Initiates model training for a user (requires purchased credits)
    /// </summary>
    [HttpPost("train")]
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

        // Check if user has sufficient purchased credits for training (15 credits required)
        var (weeklyCredits, purchasedCredits) = await _basicTierService.GetCreditBreakdownAsync(userId);
        var requiredCredits = CreditCostConfig.GetCreditCost("model_training");

        if (purchasedCredits < requiredCredits)
        {
            return BadRequest(new
            {
                success = false,
                error = new
                {
                    code = "InsufficientCredits",
                    message = $"Model training requires {requiredCredits} purchased credits. You have {purchasedCredits} purchased credits. Please purchase more credits to train custom models."
                }
            });
        }

        // Consume credits BEFORE calling Replicate; refund on failure
        var trainingCredits = await _basicTierService.ConsumeCreditsAsync(userId, "model_training");
        if (!trainingCredits.Success)
        {
            _logger.LogError("Failed to consume training credits for user {UserId} before starting Replicate training", Sid(userId));
            return StatusCode(500, new
            {
                success = false,
                error = new { code = "CreditConsumptionFailed", message = "Unable to charge credits for training. Please try again." }
            });
        }

        try
        {
            // Convert image ZIP URL to external API format before passing to Replicate
            var externalImageZipUrl = ConvertToExternalApiUrl(dto.ImageZipUrl);
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

            var result = await _replicateApiClient.CreateModelTrainingAsync(userId, externalImageZipUrl);

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
    /// Enqueue generation to run automatically after training completes
    /// </summary>
    [HttpPost("generate/queue")]
    public async Task<IActionResult> QueueGeneration([FromBody] QueueGenerationRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });
        }

        if (request == null || string.IsNullOrWhiteSpace(request.TrainingId) || request.Styles == null || !request.Styles.Any())
        {
            return BadRequest(new { success = false, error = new { code = "InvalidRequest", message = "TrainingId and styles are required." } });
        }

        var modelRequest = await _dbContext.ModelCreationRequests
            .FirstOrDefaultAsync(m => m.UserId == userId && m.PendingTrainingRequestId == request.TrainingId);

        if (modelRequest == null)
        {
            return NotFound(new { success = false, error = new { code = "TrainingNotFound", message = "Training not found for current user." } });
        }

        await _pendingGenerationService.EnqueueAsync(userId, request.TrainingId, request.Styles, request.NumOutputsPerStyle);

        return Ok(new { success = true, message = "Generation queued to run after training completes." });
    }

    /// <summary>
    /// Gets the status of a model training
    /// </summary>
    [HttpGet("train/status/{trainingId}")]
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
    [HttpPost("train/finalize/{trainingId}")]
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

    /// <summary>
    /// Generates images using a trained model and style (requires purchased credits)
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateImages([FromBody] GenerateImagesRequestDto dto)
    {
        _logger.LogInformation("Generation request received: TrainedModelVersion='{TrainedModelVersion}', UserId='{UserId}', Style='{Style}'",
            dto.TrainedModelVersion, dto.UserId, dto.Style);

        if (!ModelState.IsValid)
            return BadRequest(new { success = false, error = new { code = "InvalidModel", message = "Invalid input." } });

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });

        // Check if user has sufficient purchased credits for styled generation (5 credits per image)
        var (weeklyCredits, purchasedCredits) = await _basicTierService.GetCreditBreakdownAsync(userId);
        var requiredCredits = dto.NumOutputs * CreditCostConfig.GetCreditCost("styled_generation");

        if (purchasedCredits < requiredCredits)
        {
            return BadRequest(new
            {
                success = false,
                error = new
                {
                    code = "InsufficientCredits",
                    message = $"Styled image generation requires {requiredCredits} purchased credits. You have {purchasedCredits} purchased credits. Please purchase more credits to generate styled images."
                }
            });
        }

        AI.ProfilePhotoMaker.API.Models.ModelCreationRequest? model = null;
        CreditConsumptionResult? creditConsumed = null;
        try
        {
            // Get user info from database for prompt generation
            var userProfile = await _dbContext.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);

            // Validate trained model using database as single source of truth
            var (success, trainedModel, errorCode, errorMessage) = await ValidateTrainedModelAsync(userId);
            if (!success)
            {
                return BadRequest(new
                {
                    success = false,
                    error = new
                    {
                        code = errorCode,
                        message = errorMessage
                    }
                });
            }

            // trainedModel is guaranteed to be non-null after successful validation
            model = trainedModel!;

            // Check if the model is still available on Replicate                        
            var modelAvailable = await _replicateApiClient.CheckModelAvailabilityAsync(model.ReplicateModelId!);
            if (!modelAvailable)
            {
                _logger.LogWarning("Model {ModelId} is no longer available on Replicate for user {UserId}",
                    model.ReplicateModelId, userId);

                // Mark the model as failed instead of deleting it
                model.Status = ModelCreationStatus.Failed;
                model.ErrorMessage = "Model no longer available on Replicate";
                await _dbContext.SaveChangesAsync();

                return BadRequest(new
                {
                    success = false,
                    error = new
                    {
                        code = "ModelExpired",
                        message = "Your trained model has expired or been deleted. Please train a new model to generate styled images."
                    }
                });
            }

            // Use trained model from database as single source of truth
            var modelVersionToUse = FormatModelVersion(model.ReplicateModelId!, model.TrainedModelVersion!);
            _logger.LogInformation("Using trained model version from database: ModelId={ModelId}, Version={Version}",
                model.ReplicateModelId, modelVersionToUse);

            // If model was very recently completed, proactively wait a short time for version finalization
            if (model.CompletedAt.HasValue && DateTime.UtcNow - model.CompletedAt.Value < TimeSpan.FromMinutes(2))
            {
                try
                {
                    _logger.LogInformation("Model completed recently; waiting briefly for version finalization: {ModelId} {Version}",
                        model.ReplicateModelId, model.TrainedModelVersion);
                    var start = DateTime.UtcNow;
                    var finalized = await _replicateApiClient.WaitForModelVersionAvailabilityAsync(
                        model.ReplicateModelId!, model.TrainedModelVersion!, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(15));
                    var elapsed = DateTime.UtcNow - start;
                    if (finalized)
                    {
                        _logger.LogInformation("Proactive version wait succeeded in {ElapsedSec:F1}s for {ModelId}:{Version}",
                            elapsed.TotalSeconds, model.ReplicateModelId, model.TrainedModelVersion);
                    }
                    else
                    {
                        _logger.LogWarning("Proactive version wait finished without confirmation after {ElapsedSec:F1}s; proceeding",
                            elapsed.TotalSeconds);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Non-fatal error while waiting for version finalization prior to generation");
                }
            }

            var userInfo = userProfile != null ? new UserInfo
            {
                Gender = userProfile.Gender,
                Ethnicity = userProfile.Ethnicity
            } : null;

            _logger.LogInformation("Retrieved user info from database: Gender={Gender}, Ethnicity={Ethnicity}",
                S(userInfo?.Gender ?? "NULL"),
                S(userInfo?.Ethnicity ?? "NULL"));

            // Enforce user context: trust authenticated user over DTO
            if (!string.IsNullOrEmpty(dto.UserId) && !string.Equals(dto.UserId, userId, StringComparison.Ordinal))
            {
                return BadRequest(new
                {
                    success = false,
                    error = new { code = "InvalidUserContext", message = "Request user does not match authenticated user." }
                });
            }

            // Consume credits BEFORE calling Replicate; refund on failure
            creditConsumed = await _basicTierService.ConsumeCreditsAsync(userId, requiredCredits, "styled_generation");
            if (!creditConsumed.Success)
            {
                _logger.LogError("Failed to consume styled generation credits for user {UserId} before Replicate call", Sid(userId));
                return StatusCode(500, new
                {
                    success = false,
                    error = new { code = "CreditConsumptionFailed", message = "Unable to charge credits for generation. Please try again." }
                });
            }

            var result = await _replicateApiClient.GenerateImagesAsync(modelVersionToUse, userId, dto.Style, userInfo);

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
        catch (AI.ProfilePhotoMaker.API.Services.ImageProcessing.ReplicateApiException ex) when (ex.ErrorCode == "ReplicateVersionNotAvailable" || (int)ex.StatusCode == 422)
        {
            double? ageSec = null;
            try { ageSec = model?.CompletedAt.HasValue == true ? (double?)(DateTime.UtcNow - model!.CompletedAt!.Value).TotalSeconds : null; } catch { }
            _logger.LogWarning(
                ex,
                "Replicate reports version not available yet for user {UserId}{Age}",
                Sid(userId),
                S(ageSec.HasValue ? $", model age ~{ageSec.Value:F1}s" : string.Empty));
            // Refund if we pre-charged
            if (creditConsumed?.Success == true)
                await _basicTierService.RefundCreditsAsync(userId, creditConsumed);

            return StatusCode(409, new
            {
                success = false,
                error = new
                {
                    code = "ModelVersionFinalizing",
                    message = "Your model version is finalizing on Replicate. Please try again shortly.",
                    retryAfterSeconds = 20
                }
            });
        }
        catch (Exception ex)
        {
            if (creditConsumed?.Success == true)
                await _basicTierService.RefundCreditsAsync(userId, creditConsumed);

            _logger.LogError(ex, "Error generating images for user {UserId}", Sid(userId));
            // If generation fails, we might want to refund the credit
            // For now, we'll just return failure
            return StatusCode(500, new
            {
                success = false,
                error = new
                {
                    code = "GenerationFailed",
                    message = $"Failed to start image generation: {ex.Message}"
                }
            });
        }
    }

    /// <summary>
    /// Generates images for multiple styles using a trained model in a single consolidated request (requires purchased credits)
    /// </summary>
    [HttpPost("generate/batch")]
    public async Task<IActionResult> GenerateBatchImages([FromBody] GenerateBatchImagesRequestDto dto)
    {
        _logger.LogInformation("Batch generation request received: TrainedModelVersion='{TrainedModelVersion}', UserId='{UserId}', Styles=[{Styles}], NumOutputsPerStyle={NumOutputsPerStyle}",
            dto.TrainedModelVersion, dto.UserId, string.Join(", ", dto.Styles), dto.NumOutputsPerStyle);

        if (!ModelState.IsValid)
            return BadRequest(new { success = false, error = new { code = "InvalidModel", message = "Invalid input." } });

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });

        if (dto.Styles == null || !dto.Styles.Any())
            return BadRequest(new { success = false, error = new { code = "NoStyles", message = "At least one style must be specified." } });

        // Calculate total credits required (5 credits per image)
        var costPerImage = CreditCostConfig.GetCreditCost("styled_generation");
        var totalImages = dto.Styles.Count * dto.NumOutputsPerStyle;
        var requiredCredits = totalImages * costPerImage;

        // Check if user has sufficient purchased credits for styled generation
        var (weeklyCredits, purchasedCredits) = await _basicTierService.GetCreditBreakdownAsync(userId);

        if (purchasedCredits < requiredCredits)
        {
            return BadRequest(new
            {
                success = false,
                error = new
                {
                    code = "InsufficientCredits",
                    message = $"Batch styled image generation requires {requiredCredits} purchased credits. You have {purchasedCredits} purchased credits. Please purchase more credits to generate styled images."
                }
            });
        }

        CreditConsumptionResult? creditConsumed = null;
        try
        {
            // Get user info from database for prompt generation
            var userProfile = await _dbContext.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);

            // Validate trained model using database as single source of truth
            var (success, trainedModel, errorCode, errorMessage) = await ValidateTrainedModelAsync(userId);
            if (!success)
            {
                return BadRequest(new
                {
                    success = false,
                    error = new
                    {
                        code = errorCode,
                        message = errorMessage
                    }
                });
            }

            // trainedModel is guaranteed to be non-null after successful validation
            var model = trainedModel!;

            // Check if the model is still available on Replicate
            var modelAvailable = await _replicateApiClient.CheckModelAvailabilityAsync(model.ReplicateModelId!);
            if (!modelAvailable)
            {
                _logger.LogWarning("Model {ModelId} is no longer available on Replicate for user {UserId}",
                    model.ReplicateModelId, userId);

                // Mark the model as failed instead of deleting it
                model.Status = ModelCreationStatus.Failed;
                model.ErrorMessage = "Model no longer available on Replicate";
                await _dbContext.SaveChangesAsync();

                return BadRequest(new
                {
                    success = false,
                    error = new
                    {
                        code = "ModelExpired",
                        message = "Your trained model has expired or been deleted. Please train a new model to generate styled images."
                    }
                });
            }

            // Use trained model from database as single source of truth
            var modelVersionToUse = FormatModelVersion(model.ReplicateModelId!, model.TrainedModelVersion!);
            _logger.LogInformation("Using trained model version from database: ModelId={ModelId}, Version={Version}",
                model.ReplicateModelId, modelVersionToUse);

            var userInfo = userProfile != null ? new UserInfo
            {
                Gender = userProfile.Gender,
                Ethnicity = userProfile.Ethnicity
            } : null;

            _logger.LogInformation("Retrieved user info from database: Gender={Gender}, Ethnicity={Ethnicity}",
                S(userInfo?.Gender ?? "NULL"),
                S(userInfo?.Ethnicity ?? "NULL"));

            // Generate images for all styles in parallel
            // Enforce user context: trust authenticated user over DTO
            if (!string.IsNullOrEmpty(dto.UserId) && !string.Equals(dto.UserId, userId, StringComparison.Ordinal))
            {
                return BadRequest(new
                {
                    success = false,
                    error = new { code = "InvalidUserContext", message = "Request user does not match authenticated user." }
                });
            }

            // Process styles sequentially to avoid race conditions with Replicate API
            // when using the same model version for multiple concurrent requests
            var results = new List<StyleGenerationResult>();

            // Consume credits BEFORE calling Replicate; refund on failure
            creditConsumed = await _basicTierService.ConsumeCreditsAsync(userId, requiredCredits, "styled_generation");
            if (!creditConsumed.Success)
            {
                _logger.LogError("Failed to consume batch styled generation credits for user {UserId} before Replicate calls", Sid(userId));
                return StatusCode(500, new
                {
                    success = false,
                    error = new { code = "CreditConsumptionFailed", message = "Unable to charge credits for generation. Please try again." }
                });
            }

            foreach (var style in dto.Styles)
            {
                try
                {
                    _logger.LogInformation("Starting generation for style {Style} for user {UserId}", S(style), Sid(userId));
                    var result = await _replicateApiClient.GenerateImagesAsync(modelVersionToUse, userId, style, userInfo, dto.NumOutputsPerStyle);
                    results.Add(new StyleGenerationResult
                    {
                        Style = style,
                        Success = true,
                        Result = result,
                        Error = null
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating images for style {Style} for user {UserId}", S(style), Sid(userId));
                    results.Add(new StyleGenerationResult
                    {
                        Style = style,
                        Success = false,
                        Result = null,
                        Error = ex.Message
                    });
                }
            }

            // Count successful generations
            var successfulGenerations = results.Where(r => r.Success).ToList();
            var failedGenerations = results.Where(r => !r.Success).ToList();

            if (!successfulGenerations.Any())
            {
                // All generations failed – refund full charge
                if (creditConsumed?.Success == true)
                    await _basicTierService.RefundCreditsAsync(userId, creditConsumed);

                return StatusCode(500, new
                {
                    success = false,
                    error = new
                    {
                        code = "AllGenerationsFailed",
                        message = "Failed to start generation for any of the selected styles.",
                        details = failedGenerations.Select(f => new { f.Style, f.Error }).ToList()
                    }
                });
            }

            // Partial failures: refund unused credits for failed styles
            var failedImages = failedGenerations.Count * dto.NumOutputsPerStyle;
            var refundTotal = failedImages * costPerImage;

            int refundWeekly = 0;
            int refundPurchased = 0;
            if (creditConsumed?.Success == true && refundTotal > 0)
            {
                refundWeekly = Math.Min(creditConsumed.WeeklyCreditsConsumed, refundTotal);
                refundPurchased = Math.Min(creditConsumed.PurchasedCreditsConsumed, refundTotal - refundWeekly);

                var refundResult = CreditConsumptionResult.Succeeded("styled_generation_refund", refundWeekly, refundPurchased);
                await _basicTierService.RefundCreditsAsync(userId, refundResult);
            }

            var creditsCharged = requiredCredits - (refundWeekly + refundPurchased);

            var remainingCredits = await _basicTierService.GetAvailableCreditsAsync(userId);

            return Ok(new
            {
                success = true,
                data = new
                {
                    predictions = successfulGenerations.Select(g => new { Style = g.Style, Result = g.Result }).ToList(),
                    creditsRemaining = remainingCredits,
                    creditsCost = creditsCharged,
                    successfulStyles = successfulGenerations.Count,
                    failedStyles = failedGenerations.Count,
                    failures = failedGenerations.Select(f => new { Style = f.Style, Error = f.Error }).ToList()
                },
                error = (object?)null
            });
        }
        catch (Exception ex)
        {
            if (creditConsumed?.Success == true)
                await _basicTierService.RefundCreditsAsync(userId, creditConsumed);

            _logger.LogError(ex, "Error in batch image generation for user {UserId}", Sid(userId));
            // If generation fails, we might want to refund the credit
            // For now, we'll just return failure
            return StatusCode(500, new
            {
                success = false,
                error = new
                {
                    code = "BatchGenerationFailed",
                    message = $"Failed to start batch image generation: {ex.Message}"
                }
            });
        }
    }

    /// <summary>
    /// Gets the status of an image generation prediction
    /// </summary>
    [HttpGet("generate/status/{predictionId}")]
    public async Task<IActionResult> GetPredictionStatus(string predictionId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });

        // Enforce ownership: ensure prediction belongs to this user
        var ownsPrediction = await _dbContext.Predictions.AnyAsync(p => p.Id == predictionId && p.UserId == userId);
        if (!ownsPrediction)
        {
            return NotFound(new { success = false, error = new { code = "NotFound", message = "Prediction not found." } });
        }

        // Check if prediction is completed locally (via ProcessedImage table) to avoid Replicate API calls
        var userProfile = await _dbContext.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);
        if (userProfile != null)
        {
            var completedImages = await _dbContext.ProcessedImages
                .Where(pi => pi.UserProfileId == userProfile.Id && pi.IsGenerated == true)
                .OrderByDescending(pi => pi.CreatedAt)
                .Take(10) // Recent generations
                .ToListAsync();

            // If we have recent generated images, check if any correlation exists
            // This is a heuristic since we don't store prediction ID in ProcessedImage
            var recentGeneratedImage = completedImages.FirstOrDefault(pi =>
                pi.CreatedAt >= DateTime.UtcNow.AddMinutes(-30)); // Within last 30 minutes

            if (recentGeneratedImage != null)
            {
                _logger.LogDebug("Found recent generated image for user {UserId}, checking Replicate for final status", Sid(userId));
            }
        }

        var result = await _replicateApiClient.GetPredictionStatusAsync(predictionId);

        // If prediction succeeded and has output, try to fetch and return dataUrl
        if (result.Status == "succeeded" && result.Output != null)
        {
            string? imageUrl = null;

            // Handle both array and string outputs
            if (result.Output.Value.ValueKind == System.Text.Json.JsonValueKind.Array && result.Output.Value.GetArrayLength() > 0)
            {
                imageUrl = result.Output.Value[0].GetString();
            }
            else if (result.Output.Value.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                imageUrl = result.Output.Value.GetString();
            }

            if (!string.IsNullOrEmpty(imageUrl))
            {
                try
                {
                    using var httpClient = new System.Net.Http.HttpClient();
                    var response = await httpClient.GetAsync(imageUrl);
                    response.EnsureSuccessStatusCode();
                    var contentType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? "image/jpeg";
                    var imageBytes = await response.Content.ReadAsByteArrayAsync();
                    var base64 = System.Convert.ToBase64String(imageBytes);
                    var dataUrl = $"data:{contentType};base64,{base64}";
                    // Attach dataUrl to result by wrapping in a new object
                    return Ok(new
                    {
                        success = true,
                        data = new
                        {
                            result.Id,
                            result.Version,
                            result.Status,
                            result.Input,
                            result.Output,
                            result.Error,
                            result.Webhook,
                            result.Urls,
                            result.CreatedAt,
                            result.CompletedAt,
                            dataUrl
                        },
                        error = (object?)null
                    });
                }
                catch { /* ignore, fallback below */ }
            }
        }
        return Ok(new { success = true, data = result, error = (object?)null });
    }

    /// <summary>
    /// Checks if a model is available on Replicate
    /// </summary>
    [HttpGet("model/availability/{modelId}")]
    public async Task<IActionResult> CheckModelAvailability(string modelId)
    {
        if (string.IsNullOrEmpty(modelId))
            return BadRequest(new { success = false, error = new { code = "InvalidModel", message = "Model ID is required." } });

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });

        try
        {
            // URL decode the model ID since it's passed as a path parameter
            var decodedModelId = Uri.UnescapeDataString(modelId);
            var isAvailable = await _replicateApiClient.CheckModelAvailabilityAsync(decodedModelId);

            return Ok(new
            {
                success = true,
                data = new { available = isAvailable },
                error = (object?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking model availability for model {ModelId}", modelId);
            return StatusCode(500, new
            {
                success = false,
                error = new
                {
                    code = "AvailabilityCheckFailed",
                    message = "Failed to check model availability. Please try again later."
                }
            });
        }
    }

    /// <summary>
    /// Gets current user's credit information
    /// </summary>
    [HttpGet("credits")]
    public async Task<IActionResult> GetCredits()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });

        var availableCredits = await _basicTierService.GetAvailableCreditsAsync(userId);
        var profile = await _basicTierService.GetUserProfileWithCreditsAsync(userId);

        if (profile == null)
            return NotFound(new { success = false, error = new { code = "ProfileNotFound", message = "User profile not found." } });

        return Ok(new
        {
            success = true,
            data = new
            {
                availableCredits = availableCredits,
                subscriptionTier = profile.SubscriptionTier.ToString(),
                lastCreditReset = profile.LastCreditReset,
                nextResetDate = profile.LastCreditReset.AddDays(7)
            },
            error = (object?)null
        });
    }

    /// <summary>
    /// Enhances a user's uploaded photo using Flux Kontext Pro (basic tier feature)
    /// Provides professional photo enhancement using text-based image editing
    /// </summary>
    [HttpPost("enhance")]
    public async Task<IActionResult> EnhancePhoto([FromBody] EnhancePhotoRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, error = new { code = "InvalidModel", message = "Invalid input." } });

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });

        // Check if user has available credits
        var availableCredits = await _basicTierService.GetAvailableCreditsAsync(userId);
        if (availableCredits < 1)
        {
            var profile = await _basicTierService.GetUserProfileWithCreditsAsync(userId);
            var nextReset = profile?.LastCreditReset.AddDays(7) ?? DateTime.UtcNow.AddDays(7);

            return Ok(new
            {
                success = false,
                error = new
                {
                    code = "InsufficientCredits",
                    message = "No credits remaining. Credits reset weekly.",
                    nextResetDate = nextReset
                }
            });
        }

        try
        {
            // Validate required Replicate configuration before proceeding
            var fluxKontextProModelId = _configuration["Replicate:FluxKontextProModelId"];
            if (string.IsNullOrEmpty(fluxKontextProModelId))
            {
                _logger.LogError("FluxKontextProModelId configuration is missing for user {UserId}", Sid(userId));
                return StatusCode(500, new
                {
                    success = false,
                    error = new
                    {
                        code = "ConfigurationError",
                        message = "Photo enhancement service is temporarily unavailable. Please try again later."
                    }
                });
            }

            // Convert image URL to external API format before passing to Replicate
            var externalImageUrl = ConvertToExternalApiUrl(dto.ImageUrl);
            _logger.LogInformation("Converted image URL from {OriginalUrl} to {ExternalUrl} for Replicate API",
                LoggingSanitizer.Sanitize(dto.ImageUrl),
                LoggingSanitizer.Sanitize(externalImageUrl));

            // Enhance the uploaded photo
            var result = await _replicateApiClient.EnhancePhotoAsync(userId, externalImageUrl, dto.EnhancementType ?? "professional");

            // Only consume credit AFTER successful API call
            var creditConsumed = await _basicTierService.ConsumeCreditsAsync(userId, "photo_enhancement");
            if (!creditConsumed.Success)
            {
                _logger.LogError("Successfully created Replicate enhancement but failed to consume credits for user {UserId}", Sid(userId));
                // Note: In this case, the Replicate enhancement is already running but we couldn't charge credits
                // This is better than charging credits for failed enhancement requests
            }

            var remainingCredits = await _basicTierService.GetAvailableCreditsAsync(userId);

            return Ok(new
            {
                success = true,
                data = new
                {
                    prediction = result,
                    creditsRemaining = remainingCredits,
                    enhancementType = dto.EnhancementType ?? "professional"
                },
                error = (object?)null
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid configuration or parameters for photo enhancement for user {UserId}", Sid(userId));
            return BadRequest(new
            {
                success = false,
                error = new
                {
                    code = "InvalidRequest",
                    message = "Invalid request parameters. Please check your input and try again."
                }
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Service unavailable for photo enhancement for user {UserId}", Sid(userId));
            return StatusCode(503, new
            {
                success = false,
                error = new
                {
                    code = "ServiceUnavailable",
                    message = "Photo enhancement service is temporarily unavailable. Please try again later."
                }
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Replicate authentication failed during photo enhancement for user {UserId}", Sid(userId));
            return StatusCode(401, new
            {
                success = false,
                error = new
                {
                    code = "ReplicateAuthFailed",
                    message = "Enhancement failed to authenticate with Replicate. Verify API token configuration.",
                }
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error during photo enhancement for user {UserId}", Sid(userId));
            return StatusCode(502, new
            {
                success = false,
                error = new
                {
                    code = "NetworkError",
                    message = "Failed to connect to enhancement service. Please try again later."
                }
            });
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout during photo enhancement for user {UserId}", Sid(userId));
            return StatusCode(408, new
            {
                success = false,
                error = new
                {
                    code = "RequestTimeout",
                    message = "The enhancement is taking longer than expected (over 2 minutes). Please try again in a few moments - sometimes the service needs a break! 😊"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during photo enhancement for user {UserId}: {ErrorMessage}", Sid(userId), S(ex.Message));
            return StatusCode(500, new
            {
                success = false,
                error = new
                {
                    code = "EnhancementFailed",
                    message = "Failed to enhance photo due to an unexpected error. Please try again later."
                }
            });
        }
    }

    /// <summary>
    /// Converts a URL to use ExternalApiBaseUrl for external API access (like Replicate)
    /// This ensures external APIs can access images via publicly accessible HTTPS URLs
    /// </summary>
    private string ConvertToExternalApiUrl(string originalUrl)
    {
        // If already a fully qualified HTTP URL, return as-is
        if (originalUrl.StartsWith("http://") || originalUrl.StartsWith("https://"))
        {
            // Check if it's using localhost - convert to external API URL
            if (originalUrl.Contains("localhost") || originalUrl.Contains("127.0.0.1"))
            {
                var uri = new Uri(originalUrl);
                var relativePath = uri.PathAndQuery;

                var externalBaseUrl = _configuration["ExternalApiBaseUrl"];
                if (!string.IsNullOrEmpty(externalBaseUrl))
                {
                    return $"{externalBaseUrl.TrimEnd('/')}{relativePath}";
                }

                // Fallback to AppBaseUrl if ExternalApiBaseUrl not configured and AppBaseUrl is HTTPS
                var appBaseUrl = _configuration["AppBaseUrl"];
                if (!string.IsNullOrEmpty(appBaseUrl) && appBaseUrl.StartsWith("https://"))
                {
                    return $"{appBaseUrl.TrimEnd('/')}{relativePath}";
                }

                _logger.LogWarning("No ExternalApiBaseUrl configured and AppBaseUrl is not HTTPS - external APIs may not be able to access: {Url}", S(originalUrl));
            }
            return originalUrl;
        }

        // If it's a relative path, convert to external API URL
        if (originalUrl.StartsWith("/"))
        {
            var externalBaseUrl = _configuration["ExternalApiBaseUrl"];
            if (!string.IsNullOrEmpty(externalBaseUrl))
            {
                return $"{externalBaseUrl.TrimEnd('/')}{originalUrl}";
            }

            // Fallback to AppBaseUrl if ExternalApiBaseUrl not configured
            var appBaseUrl = _configuration["AppBaseUrl"];
            if (!string.IsNullOrEmpty(appBaseUrl) && appBaseUrl.StartsWith("https://"))
            {
                return $"{appBaseUrl.TrimEnd('/')}{originalUrl}";
            }

            _logger.LogWarning("No ExternalApiBaseUrl configured and AppBaseUrl is not HTTPS - external APIs may not be able to access: {Url}", S(originalUrl));
        }

        return originalUrl;
    }

    /// <summary>
    /// Health check endpoint for Replicate API connectivity and configuration
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> HealthCheck()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });

        try
        {
            var healthData = new
            {
                apiConnected = false,
                tokenValid = false,
                canCreateModels = false,
                accountStatus = "Unknown",
                configurationValid = false,
                externalUrlAccessible = false,
                error = (string?)null
            };

            // Check basic configuration
            var apiToken = Environment.GetEnvironmentVariable("REPLICATE_API_TOKEN") ?? _configuration["Replicate:ApiToken"];
            var fluxModelId = _configuration["Replicate:FluxTrainingModelId"];
            var externalApiBaseUrl = _configuration["ExternalApiBaseUrl"];

            if (string.IsNullOrEmpty(apiToken))
            {
                return Ok(new
                {
                    success = true,
                    data = healthData with { error = "REPLICATE_API_TOKEN not configured" }
                });
            }

            if (string.IsNullOrEmpty(fluxModelId) || !fluxModelId.Contains(':'))
            {
                return Ok(new
                {
                    success = true,
                    data = healthData with { error = "Replicate:FluxTrainingModelId not properly configured" }
                });
            }

            // Test basic API connectivity
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Token", apiToken);
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                var response = await httpClient.GetAsync("https://api.replicate.com/v1/account");

                if (response.IsSuccessStatusCode)
                {
                    healthData = healthData with
                    {
                        apiConnected = true,
                        tokenValid = true,
                        accountStatus = "Active"
                    };

                    // Try to check if we can create models (this is a simplified check)
                    var modelsResponse = await httpClient.GetAsync("https://api.replicate.com/v1/models");
                    if (modelsResponse.IsSuccessStatusCode)
                    {
                        healthData = healthData with { canCreateModels = true };
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    healthData = healthData with
                    {
                        apiConnected = true,
                        tokenValid = false,
                        error = "Invalid or expired API token"
                    };
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
                {
                    healthData = healthData with
                    {
                        apiConnected = true,
                        tokenValid = true,
                        accountStatus = "Payment Required",
                        error = "Replicate account requires payment"
                    };
                }
            }
            catch (HttpRequestException ex)
            {
                healthData = healthData with { error = $"Network error: {ex.Message}" };
            }
            catch (TaskCanceledException)
            {
                healthData = healthData with { error = "Request timeout connecting to Replicate API" };
            }

            // Check configuration validity
            healthData = healthData with
            {
                configurationValid = !string.IsNullOrEmpty(apiToken) &&
                                   !string.IsNullOrEmpty(fluxModelId) &&
                                   fluxModelId.Contains(':')
            };

            // Check external URL accessibility (basic check)
            if (!string.IsNullOrEmpty(externalApiBaseUrl))
            {
                try
                {
                    using var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(5);
                    var testResponse = await httpClient.GetAsync($"{externalApiBaseUrl.TrimEnd('/')}/api/image/health");
                    healthData = healthData with { externalUrlAccessible = testResponse.IsSuccessStatusCode };
                }
                catch
                {
                    healthData = healthData with { externalUrlAccessible = false };
                }
            }

            return Ok(new { success = true, data = healthData, error = (object?)null });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Replicate health check for user {UserId}", Sid(userId));
            return StatusCode(500, new
            {
                success = false,
                error = new
                {
                    code = "HealthCheckFailed",
                    message = "Failed to perform health check"
                }
            });
        }
    }

    /// <summary>
    /// Validates and retrieves trained model from database as single source of truth
    /// </summary>
    private async Task<(bool Success, ModelCreationRequest? Model, string? ErrorCode, string? ErrorMessage)>
        ValidateTrainedModelAsync(string userId)
    {
        // Get user's trained model from database
        var trainedModel = await _dbContext.ModelCreationRequests
            .Where(m => m.UserId == userId && m.Status == ModelCreationStatus.Ready)
            .OrderByDescending(m => m.CompletedAt)
            .FirstOrDefaultAsync();

        if (trainedModel == null)
        {
            return (false, null, "NoTrainedModel", "No trained model found. Please train a model before generating styled images.");
        }

        if (string.IsNullOrEmpty(trainedModel.ReplicateModelId))
        {
            _logger.LogError("User {UserId} has trained model without ReplicateModelId", Sid(userId));
            return (false, null, "IncompleteModel", "Your trained model data is incomplete. Please contact support or retrain your model.");
        }

        if (string.IsNullOrEmpty(trainedModel.TrainedModelVersion))
        {
            _logger.LogError("User {UserId} has trained model without TrainedModelVersion", Sid(userId));
            return (false, null, "IncompleteModel", "Your trained model data is incomplete. Please contact support or retrain your model.");
        }

        return (true, trainedModel, null, null);
    }

    /// <summary>
    /// Formats model version for Replicate API calls.
    /// Converts stored components into required format: "owner/modelId:versionHash"
    /// </summary>
    private string FormatModelVersion(string replicateModelId, string trainedModelVersion)
    {
        // If already in fully qualified format (owner/model:version), return as-is
        if (!string.IsNullOrEmpty(trainedModelVersion) && trainedModelVersion.Contains(":"))
        {
            var beforeColon = trainedModelVersion.Split(':')[0];
            if (beforeColon.Contains('/'))
            {
                return trainedModelVersion;
            }
        }

        if (string.IsNullOrEmpty(replicateModelId))
        {
            return trainedModelVersion; // fallback; let caller handle upstream validation
        }

        // Normalize owner/model
        // Prefer configured owner, fallback to project default
        string owner = _configuration["Replicate:Owner"] ?? Environment.GetEnvironmentVariable("REPLICATE_OWNER") ?? "alanw707";
        string modelName = replicateModelId;
        if (replicateModelId.Contains('/'))
        {
            var parts = replicateModelId.Split('/', 2);
            var incomingOwner = parts[0];
            modelName = parts[1];
            if (!string.IsNullOrWhiteSpace(incomingOwner) && !string.Equals(incomingOwner, "mock", StringComparison.OrdinalIgnoreCase))
            {
                owner = incomingOwner;
            }
        }
        var ownerAndModel = $"{owner}/{modelName}";

        return $"{ownerAndModel}:{trainedModelVersion}";
    }
}
