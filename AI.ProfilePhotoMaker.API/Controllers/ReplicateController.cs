using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AI.ProfilePhotoMaker.API.Controllers;

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

    public ReplicateController(
        IReplicateApiClient replicateApiClient,
        IBasicTierService basicTierService,
        ApplicationDbContext dbContext,
        IConfiguration configuration,
        ILogger<ReplicateController> logger)
    {
        _replicateApiClient = replicateApiClient;
        _basicTierService = basicTierService;
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
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
            _logger.LogWarning("User {UserId} attempted to train a new model but already has trained model {ModelId}", userId, existingModel.ReplicateModelId);
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

        try
        {
            // Convert image ZIP URL to external API format before passing to Replicate
            var externalImageZipUrl = ConvertToExternalApiUrl(dto.ImageZipUrl);
            _logger.LogInformation("Converted ZIP URL from {OriginalUrl} to {ExternalUrl} for Replicate API",
                dto.ImageZipUrl, externalImageZipUrl);

            // Enforce user context: trust authenticated user over DTO
            if (!string.IsNullOrEmpty(dto.UserId) && !string.Equals(dto.UserId, userId, StringComparison.Ordinal))
            {
                return BadRequest(new
                {
                    success = false,
                    error = new { code = "InvalidUserContext", message = "Request user does not match authenticated user." }
                });
            }

            var result = await _replicateApiClient.CreateModelTrainingAsync(userId, externalImageZipUrl);

            // Only consume credits AFTER successful API call
            var creditConsumed = await _basicTierService.ConsumeCreditsAsync(userId, "model_training");
            if (!creditConsumed)
            {
                _logger.LogError("Successfully created Replicate training but failed to consume credits for user {UserId}", userId);
                // Note: In this case, the Replicate training is already running but we couldn't charge credits
                // This is better than charging credits for failed training requests
            }

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
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Replicate auth failed during training for user {UserId}", userId);
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
            _logger.LogError(ex, "Replicate configuration error during training for user {UserId}", userId);
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
            _logger.LogError(ex, "Training failed for user {UserId}", userId);
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
                userInfo?.Gender ?? "NULL", userInfo?.Ethnicity ?? "NULL");

            // Enforce user context: trust authenticated user over DTO
            if (!string.IsNullOrEmpty(dto.UserId) && !string.Equals(dto.UserId, userId, StringComparison.Ordinal))
            {
                return BadRequest(new
                {
                    success = false,
                    error = new { code = "InvalidUserContext", message = "Request user does not match authenticated user." }
                });
            }

            var result = await _replicateApiClient.GenerateImagesAsync(modelVersionToUse, userId, dto.Style, userInfo);

            // Only consume credits AFTER successful API call (5 credits per image generated)
            var creditConsumed = await _basicTierService.ConsumeCreditsAsync(userId, requiredCredits, "styled_generation");
            if (!creditConsumed)
            {
                _logger.LogError("Successfully created Replicate prediction but failed to consume credits for user {UserId}", userId);
                // Note: In this case, the Replicate prediction is already running but we couldn't charge credits
                // This is better than charging credits for failed predictions
            }

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating images for user {UserId}", userId);
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
        var totalImages = dto.Styles.Count * dto.NumOutputsPerStyle;
        var requiredCredits = totalImages * CreditCostConfig.GetCreditCost("styled_generation");

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
                userInfo?.Gender ?? "NULL", userInfo?.Ethnicity ?? "NULL");

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

            var generationTasks = dto.Styles.Select<string, Task<dynamic>>(async (style) =>
            {
                try
                {
                    var result = await _replicateApiClient.GenerateImagesAsync(modelVersionToUse, userId, style, userInfo, dto.NumOutputsPerStyle);
                    return new { Style = style, Success = true, Result = result, Error = (string?)null };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating images for style {Style} for user {UserId}", style, userId);
                    return new { Style = style, Success = false, Result = (object?)null, Error = ex.Message };
                }
            });

            var results = await Task.WhenAll(generationTasks);

            // Count successful generations
            var successfulGenerations = results.Where(r => r.Success).ToList();
            var failedGenerations = results.Where(r => !r.Success).ToList();

            if (!successfulGenerations.Any())
            {
                // All generations failed
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

            // Only consume credits for successful generations
            var actualCreditsRequired = successfulGenerations.Count * dto.NumOutputsPerStyle * CreditCostConfig.GetCreditCost("styled_generation");
            var creditConsumed = await _basicTierService.ConsumeCreditsAsync(userId, actualCreditsRequired, "styled_generation");
            if (!creditConsumed)
            {
                _logger.LogError("Successfully created Replicate predictions but failed to consume credits for user {UserId}", userId);
                // Note: In this case, the Replicate predictions are already running but we couldn't charge credits
                // This is better than charging credits for failed predictions
            }

            var remainingCredits = await _basicTierService.GetAvailableCreditsAsync(userId);

            return Ok(new
            {
                success = true,
                data = new
                {
                    predictions = successfulGenerations.Select(g => new { Style = g.Style, Result = g.Result }).ToList(),
                    creditsRemaining = remainingCredits,
                    creditsCost = actualCreditsRequired,
                    successfulStyles = successfulGenerations.Count,
                    failedStyles = failedGenerations.Count,
                    failures = failedGenerations.Select(f => new { Style = f.Style, Error = f.Error }).ToList()
                },
                error = (object?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch image generation for user {UserId}", userId);
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
                _logger.LogDebug("Found recent generated image for user {UserId}, checking Replicate for final status", userId);
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
                _logger.LogError("FluxKontextProModelId configuration is missing for user {UserId}", userId);
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
                dto.ImageUrl, externalImageUrl);

            // Enhance the uploaded photo
            var result = await _replicateApiClient.EnhancePhotoAsync(userId, externalImageUrl, dto.EnhancementType ?? "professional");

            // Only consume credit AFTER successful API call
            var creditConsumed = await _basicTierService.ConsumeCreditsAsync(userId, "photo_enhancement");
            if (!creditConsumed)
            {
                _logger.LogError("Successfully created Replicate enhancement but failed to consume credits for user {UserId}", userId);
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
            _logger.LogError(ex, "Invalid configuration or parameters for photo enhancement for user {UserId}", userId);
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
            _logger.LogError(ex, "Service unavailable for photo enhancement for user {UserId}", userId);
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
            _logger.LogError(ex, "Replicate authentication failed during photo enhancement for user {UserId}", userId);
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
            _logger.LogError(ex, "Network error during photo enhancement for user {UserId}", userId);
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
            _logger.LogError(ex, "Request timeout during photo enhancement for user {UserId}", userId);
            return StatusCode(408, new
            {
                success = false,
                error = new
                {
                    code = "RequestTimeout",
                    message = "Enhancement request timed out. Please try again with a smaller image."
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during photo enhancement for user {UserId}: {ErrorMessage}", userId, ex.Message);
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

                _logger.LogWarning("No ExternalApiBaseUrl configured and AppBaseUrl is not HTTPS - external APIs may not be able to access: {Url}", originalUrl);
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

            _logger.LogWarning("No ExternalApiBaseUrl configured and AppBaseUrl is not HTTPS - external APIs may not be able to access: {Url}", originalUrl);
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
            _logger.LogError(ex, "Error during Replicate health check for user {UserId}", userId);
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
            _logger.LogError("User {UserId} has trained model without ReplicateModelId", userId);
            return (false, null, "IncompleteModel", "Your trained model data is incomplete. Please contact support or retrain your model.");
        }

        if (string.IsNullOrEmpty(trainedModel.TrainedModelVersion))
        {
            _logger.LogError("User {UserId} has trained model without TrainedModelVersion", userId);
            return (false, null, "IncompleteModel", "Your trained model data is incomplete. Please contact support or retrain your model.");
        }

        return (true, trainedModel, null, null);
    }

    /// <summary>
    /// TEMP DEBUG: Check user's model status for troubleshooting
    /// </summary>
    [HttpGet("debug/models/{userId}")]
    [AllowAnonymous]
    public async Task<IActionResult> DebugUserModels(string userId)
    {
        try
        {
            var models = await _dbContext.ModelCreationRequests
                .Where(m => m.UserId == userId)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new {
                    m.ModelName,
                    m.ReplicateModelId,
                    m.TrainedModelVersion,
                    m.Status,
                    m.CreatedAt,
                    m.CompletedAt
                })
                .ToListAsync();

            return Ok(new { success = true, data = models });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Formats model version for Replicate API calls.
    /// Converts stored components into required format: "owner/modelId:versionHash"
    /// </summary>
    private static string FormatModelVersion(string replicateModelId, string trainedModelVersion)
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

        // Standardize to canonical owner and extracted model name
        // replicateModelId can be "owner/model" or just "model"; always use canonical owner
        const string canonicalOwner = "alanw707";
        if (string.IsNullOrEmpty(replicateModelId))
        {
            return trainedModelVersion; // fallback; let caller handle upstream validation
        }

        var modelName = replicateModelId;
        var slashIdx = replicateModelId.IndexOf('/');
        if (slashIdx >= 0 && slashIdx < replicateModelId.Length - 1)
        {
            modelName = replicateModelId.Substring(slashIdx + 1);
        }

        return $"{canonicalOwner}/{modelName}:{trainedModelVersion}";
    }

    /// <summary>
    /// Generates basic images for users without trained models
    /// </summary>
    [HttpPost("generate-basic")]
    public async Task<IActionResult> GenerateBasicImage([FromBody] GenerateBasicImageRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, error = new { code = "InvalidModel", message = "Invalid input." } });

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });

        try
        {
            // Check credit availability for basic tier users
            if (!await _basicTierService.HasAvailableCreditsAsync(userId))
            {
                return BadRequest(new { 
                    success = false, 
                    error = new { 
                        code = "InsufficientCredits", 
                        message = "Insufficient credits to generate basic image." 
                    } 
                });
            }

            // Generate basic image
            var result = await _replicateApiClient.GenerateBasicImageAsync(userId, null, dto.Gender);
            
            // Consume credits after successful generation
            await _basicTierService.ConsumeCreditsAsync(userId, "casual_headshot_generation");

            return Ok(new { 
                success = true, 
                predictionId = result.Id, 
                status = result.Status,
                availableCredits = await _basicTierService.GetAvailableCreditsAsync(userId)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating basic image for user {UserId}", userId);
            return StatusCode(500, new { 
                success = false, 
                error = new { 
                    code = "GenerationFailed", 
                    message = "Failed to generate basic image. Please try again." 
                } 
            });
        }
    }
}
