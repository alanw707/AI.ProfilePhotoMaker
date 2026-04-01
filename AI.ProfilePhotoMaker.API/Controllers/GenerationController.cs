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
public class GenerationController : ControllerBase
{
    private readonly IReplicateApiClient _replicateApiClient;
    private readonly IBasicTierService _basicTierService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GenerationController> _logger;
    private readonly IPendingGenerationService _pendingGenerationService;

    public GenerationController(
        IReplicateApiClient replicateApiClient,
        IBasicTierService basicTierService,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        IConfiguration configuration,
        ILogger<GenerationController> logger,
        IPendingGenerationService pendingGenerationService)
    {
        _replicateApiClient = replicateApiClient;
        _basicTierService = basicTierService;
        _userManager = userManager;
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
        _pendingGenerationService = pendingGenerationService;
    }

    /// <summary>
    /// Generates images using a trained model and style (requires credits)
    /// </summary>
    [HttpPost("generate")]
    [Route("~/api/replicate/generate")]
    public async Task<IActionResult> GenerateImages([FromBody] GenerateImagesRequestDto dto)
    {
        _logger.LogInformation("Generation request received: TrainedModelVersion='{TrainedModelVersion}', UserId='{UserId}', Style='{Style}'",
            dto.TrainedModelVersion, dto.UserId, dto.Style);

        if (!ModelState.IsValid)
            return BadRequest(new { success = false, error = new { code = "InvalidModel", message = "Invalid input." } });

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });

        if (dto.NumOutputs < 1 || dto.NumOutputs > 4)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "InvalidNumOutputs", message = "NumOutputs must be between 1 and 4." }
            });
        }

        var normalizedRequestedStyle = StyleNameNormalizer.Normalize(dto.Style);
        if (string.IsNullOrWhiteSpace(normalizedRequestedStyle))
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "InvalidStyle", message = "A valid style is required." }
            });
        }

        var singleStyleExists = await _dbContext.Styles
            .AnyAsync(s => s.IsActive && s.Name == normalizedRequestedStyle);
        if (!singleStyleExists)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "InvalidStyle", message = $"Unknown style '{normalizedRequestedStyle}'." }
            });
        }

        // Check if user has sufficient credits for styled generation (5 credits per image)
        var availableCredits = await _basicTierService.GetAvailableCreditsAsync(userId);
        var requiredCredits = dto.NumOutputs * CreditCostConfig.GetCreditCost("styled_generation");
        var correlationId = $"styled_generation:{Guid.NewGuid()}";

        if (availableCredits < requiredCredits)
        {
            return BadRequest(new
            {
                success = false,
                error = new
                {
                    code = "InsufficientCredits",
                    message = $"Styled image generation requires {requiredCredits} credits. You have {availableCredits} credits. Please purchase more credits to generate styled images."
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
            var (success, trainedModel, errorCode, errorMessage) = await ReplicateHelpers.ValidateTrainedModelAsync(userId, _dbContext, _logger);
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
            var modelVersionToUse = ReplicateHelpers.FormatModelVersion(model.ReplicateModelId!, model.TrainedModelVersion!, _configuration);
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
                LoggingSanitizer.Sanitize(userInfo?.Gender ?? "NULL"),
                LoggingSanitizer.Sanitize(userInfo?.Ethnicity ?? "NULL"));

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
            creditConsumed = await _basicTierService.ConsumeCreditsAsync(
                userId,
                requiredCredits,
                "styled_generation",
                correlationId,
                HttpContext?.RequestAborted ?? CancellationToken.None);
            if (!creditConsumed.Success)
            {
                if (creditConsumed.Error == "insufficient_credits")
                {
                    _logger.LogWarning("Insufficient credits detected during styled generation for user {UserId}", LoggingSanitizer.SanitizeId(userId));
                    return BadRequest(new
                    {
                        success = false,
                        error = new
                        {
                            code = "InsufficientCredits",
                            message = $"Styled image generation requires {requiredCredits} credits. You have {availableCredits} credits. Please purchase more credits to generate styled images."
                        }
                    });
                }

                _logger.LogError("Failed to consume styled generation credits for user {UserId} before Replicate call", LoggingSanitizer.SanitizeId(userId));
                return StatusCode(500, new
                {
                    success = false,
                    error = new { code = "CreditConsumptionFailed", message = "Unable to charge credits for generation. Please try again." }
                });
            }

            var result = await _replicateApiClient.GenerateImagesAsync(modelVersionToUse, userId, normalizedRequestedStyle, userInfo);

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
                LoggingSanitizer.SanitizeId(userId),
                LoggingSanitizer.Sanitize(ageSec.HasValue ? $", model age ~{ageSec.Value:F1}s" : string.Empty));
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

            _logger.LogError(ex, "Error generating images for user {UserId}", LoggingSanitizer.SanitizeId(userId));
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
    /// Generates images for multiple styles using a trained model in a single consolidated request (requires credits)
    /// </summary>
    [HttpPost("batch")]
    [Route("~/api/replicate/generate/batch")]
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

        var normalizedRequestedStyles = dto.Styles
            .Select(StyleNameNormalizer.Normalize)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (!normalizedRequestedStyles.Any())
            return BadRequest(new { success = false, error = new { code = "NoStyles", message = "At least one valid style must be specified." } });

        var duplicateStyles = normalizedRequestedStyles
            .GroupBy(s => s, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicateStyles.Count > 0)
        {
            return BadRequest(new
            {
                success = false,
                error = new
                {
                    code = "DuplicateStyles",
                    message = "Duplicate styles are not allowed in batch requests.",
                    duplicateStyles
                }
            });
        }

        var activeStyles = await _dbContext.Styles
            .Where(s => s.IsActive)
            .Select(s => s.Name)
            .ToListAsync();

        var activeStyleSet = new HashSet<string>(activeStyles, StringComparer.Ordinal);

        var unknownStyles = normalizedRequestedStyles
            .Where(s => !activeStyleSet.Contains(s))
            .ToList();

        if (unknownStyles.Count > 0)
        {
            _logger.LogWarning(
                "Batch generation rejected due to unknown styles for user {UserId}: {Styles}",
                LoggingSanitizer.SanitizeId(userId),
                LoggingSanitizer.Sanitize(string.Join(",", unknownStyles)));

            return BadRequest(new
            {
                success = false,
                error = new
                {
                    code = "InvalidStyle",
                    message = "One or more requested styles are invalid.",
                    invalidStyles = unknownStyles
                }
            });
        }

        if (dto.NumOutputsPerStyle < 1 || dto.NumOutputsPerStyle > 4)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "InvalidNumOutputsPerStyle", message = "NumOutputsPerStyle must be between 1 and 4." }
            });
        }

        // Calculate total credits required (5 credits per image)
        var costPerImage = CreditCostConfig.GetCreditCost("styled_generation");
        var totalImages = normalizedRequestedStyles.Count * dto.NumOutputsPerStyle;
        var requiredCredits = totalImages * costPerImage;
        var correlationId = $"styled_generation:{Guid.NewGuid()}";

        // Check if user has sufficient credits for styled generation
        var availableCredits = await _basicTierService.GetAvailableCreditsAsync(userId);

        if (availableCredits < requiredCredits)
        {
            return BadRequest(new
            {
                success = false,
                error = new
                {
                    code = "InsufficientCredits",
                    message = $"Batch styled image generation requires {requiredCredits} credits. You have {availableCredits} credits. Please purchase more credits to generate styled images."
                }
            });
        }

        CreditConsumptionResult? creditConsumed = null;
        try
        {
            // Get user info from database for prompt generation
            var userProfile = await _dbContext.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);

            // Validate trained model using database as single source of truth
            var (success, trainedModel, errorCode, errorMessage) = await ReplicateHelpers.ValidateTrainedModelAsync(userId, _dbContext, _logger);
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
            var modelVersionToUse = ReplicateHelpers.FormatModelVersion(model.ReplicateModelId!, model.TrainedModelVersion!, _configuration);
            _logger.LogInformation("Using trained model version from database: ModelId={ModelId}, Version={Version}",
                model.ReplicateModelId, modelVersionToUse);

            var userInfo = userProfile != null ? new UserInfo
            {
                Gender = userProfile.Gender,
                Ethnicity = userProfile.Ethnicity
            } : null;

            _logger.LogInformation("Retrieved user info from database: Gender={Gender}, Ethnicity={Ethnicity}",
                LoggingSanitizer.Sanitize(userInfo?.Gender ?? "NULL"),
                LoggingSanitizer.Sanitize(userInfo?.Ethnicity ?? "NULL"));

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
            creditConsumed = await _basicTierService.ConsumeCreditsAsync(
                userId,
                requiredCredits,
                "styled_generation",
                correlationId,
                HttpContext?.RequestAborted ?? CancellationToken.None);
            if (!creditConsumed.Success)
            {
                if (creditConsumed.Error == "insufficient_credits")
                {
                    _logger.LogWarning("Insufficient credits detected during batch styled generation for user {UserId}", LoggingSanitizer.SanitizeId(userId));
                    return BadRequest(new
                    {
                        success = false,
                        error = new
                        {
                            code = "InsufficientCredits",
                            message = $"Batch styled image generation requires {requiredCredits} credits. You have {availableCredits} credits. Please purchase more credits to generate styled images."
                        }
                    });
                }

                _logger.LogError("Failed to consume batch styled generation credits for user {UserId} before Replicate calls", LoggingSanitizer.SanitizeId(userId));
                return StatusCode(500, new
                {
                    success = false,
                    error = new { code = "CreditConsumptionFailed", message = "Unable to charge credits for generation. Please try again." }
                });
            }

            foreach (var style in normalizedRequestedStyles)
            {
                try
                {
                    _logger.LogInformation("Starting generation for style {Style} for user {UserId}", LoggingSanitizer.Sanitize(style), LoggingSanitizer.SanitizeId(userId));
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
                    _logger.LogError(ex, "Error generating images for style {Style} for user {UserId}", LoggingSanitizer.Sanitize(style), LoggingSanitizer.SanitizeId(userId));
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
                // All generations failed - refund full charge
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

            var refundCredits = 0;
            if (creditConsumed?.Success == true && refundTotal > 0)
            {
                refundCredits = Math.Min(creditConsumed.CreditsConsumed, refundTotal);
                var refundResult = CreditConsumptionResult.Succeeded(
                    "styled_generation",
                    refundCredits,
                    creditConsumed?.CorrelationId ?? correlationId);
                await _basicTierService.RefundCreditsAsync(userId, refundResult);
            }

            var creditsCharged = requiredCredits - refundCredits;

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

            _logger.LogError(ex, "Error in batch image generation for user {UserId}", LoggingSanitizer.SanitizeId(userId));
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
    /// Enqueue generation to run automatically after training completes
    /// </summary>
    [HttpPost("queue")]
    [Route("~/api/replicate/generate/queue")]
    public async Task<IActionResult> QueueGeneration([FromBody] QueueGenerationRequest request)
    {
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("QueueGeneration rejected: model state invalid for user {UserId}", LoggingSanitizer.SanitizeId(User.FindFirst(ClaimTypes.NameIdentifier)?.Value));
            return BadRequest(new { success = false, error = new { code = "InvalidRequest", message = "Invalid generation request." } });
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });
        }

        if (request == null || string.IsNullOrWhiteSpace(request.TrainingId) || request.Styles == null || !request.Styles.Any())
        {
            _logger.LogWarning("QueueGeneration rejected: missing trainingId or styles. User {UserId}", LoggingSanitizer.SanitizeId(userId));
            return BadRequest(new { success = false, error = new { code = "InvalidRequest", message = "TrainingId and at least one style are required." } });
        }

        if (request.NumOutputsPerStyle < 1 || request.NumOutputsPerStyle > 4)
        {
            _logger.LogWarning("QueueGeneration rejected: invalid numOutputsPerStyle {Num} for user {UserId}", request.NumOutputsPerStyle, LoggingSanitizer.SanitizeId(userId));
            return BadRequest(new { success = false, error = new { code = "InvalidRequest", message = "numOutputsPerStyle must be between 1 and 4." } });
        }

        var modelRequest = await _dbContext.ModelCreationRequests
            .FirstOrDefaultAsync(m => m.UserId == userId && m.PendingTrainingRequestId == request.TrainingId);

        if (modelRequest == null)
        {
            _logger.LogWarning("QueueGeneration rejected: training not found for user {UserId}, trainingId {TrainingId}", LoggingSanitizer.SanitizeId(userId), LoggingSanitizer.SanitizeId(request.TrainingId));
            return NotFound(new { success = false, error = new { code = "TrainingNotFound", message = "Training not found for current user." } });
        }

        await _pendingGenerationService.EnqueueAsync(userId, request.TrainingId, request.Styles, request.NumOutputsPerStyle);

        return Ok(new { success = true, message = "Generation queued to run after training completes." });
    }

    /// <summary>
    /// Gets the status of an image generation prediction
    /// </summary>
    [HttpGet("status/{predictionId}")]
    [Route("~/api/replicate/generate/status/{predictionId}")]
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
                _logger.LogDebug("Found recent generated image for user {UserId}, checking Replicate for final status", LoggingSanitizer.SanitizeId(userId));
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
                    using var httpClient = new System.Net.Http.HttpClient
                    {
                        Timeout = TimeSpan.FromSeconds(5)
                    };

                    var response = await httpClient.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
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
}
