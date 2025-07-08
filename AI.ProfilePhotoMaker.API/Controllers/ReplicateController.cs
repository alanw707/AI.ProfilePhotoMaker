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
    private readonly ILogger<ReplicateController> _logger;

    public ReplicateController(
        IReplicateApiClient replicateApiClient, 
        IBasicTierService basicTierService,
        ApplicationDbContext dbContext,
        ILogger<ReplicateController> logger)
    {
        _replicateApiClient = replicateApiClient;
        _basicTierService = basicTierService;
        _dbContext = dbContext;
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
            return BadRequest(new { 
                success = false, 
                error = new { 
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
            return BadRequest(new { 
                success = false, 
                error = new { 
                    code = "InsufficientCredits", 
                    message = $"Model training requires {requiredCredits} purchased credits. You have {purchasedCredits} purchased credits. Please purchase more credits to train custom models." 
                } 
            });
        }

        try
        {
            var result = await _replicateApiClient.CreateModelTrainingAsync(dto.UserId, dto.ImageZipUrl);
            
            // Only consume credits AFTER successful API call
            var creditConsumed = await _basicTierService.ConsumeCreditsAsync(userId, "model_training");
            if (!creditConsumed)
            {
                _logger.LogError("Successfully created Replicate training but failed to consume credits for user {UserId}", userId);
                // Note: In this case, the Replicate training is already running but we couldn't charge credits
                // This is better than charging credits for failed training requests
            }
            
            var remainingCredits = await _basicTierService.GetAvailableCreditsAsync(userId);
            
            return Ok(new { 
                success = true, 
                data = new {
                    prediction = result,
                    creditsRemaining = remainingCredits,
                    creditsCost = requiredCredits
                }, 
                error = (object?)null 
            });
        }
        catch (Exception)
        {
            // If training fails, we might want to refund the credit
            // For now, we'll just log the error and return failure
            return StatusCode(500, new { 
                success = false, 
                error = new { 
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
            return BadRequest(new { 
                success = false, 
                error = new { 
                    code = "InsufficientCredits", 
                    message = $"Styled image generation requires {requiredCredits} purchased credits. You have {purchasedCredits} purchased credits. Please purchase more credits to generate styled images." 
                } 
            });
        }

        try
        {
            // Get user info from database for prompt generation
            var userProfile = await _dbContext.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);
            
            // Get user's trained model from ModelCreationRequest
            var trainedModel = await _dbContext.ModelCreationRequests
                .Where(m => m.UserId == userId && m.Status == ModelCreationStatus.Ready)
                .OrderByDescending(m => m.CompletedAt)
                .FirstOrDefaultAsync();
            
            // Check if the model is still available on Replicate
            if (trainedModel != null && !string.IsNullOrEmpty(trainedModel.ReplicateModelId))
            {
                var modelAvailable = await _replicateApiClient.CheckModelAvailabilityAsync(trainedModel.ReplicateModelId);
                if (!modelAvailable)
                {
                    _logger.LogWarning("Model {ModelId} is no longer available on Replicate for user {UserId}", 
                        trainedModel.ReplicateModelId, userId);
                    
                    // Mark the model as failed instead of deleting it
                    trainedModel.Status = ModelCreationStatus.Failed;
                    trainedModel.ErrorMessage = "Model no longer available on Replicate";
                    await _dbContext.SaveChangesAsync();
                    
                    return BadRequest(new { 
                        success = false, 
                        error = new { 
                            code = "ModelExpired", 
                            message = "Your trained model has expired or been deleted. Please train a new model to generate styled images." 
                        } 
                    });
                }
            }
            
            // Ensure we have a model to use for generation
            var modelVersionToUse = dto.TrainedModelVersion;
            if (string.IsNullOrEmpty(modelVersionToUse) && trainedModel != null)
            {
                modelVersionToUse = trainedModel.TrainedModelVersion;
                _logger.LogInformation("Using model version from database: {ModelVersion}", modelVersionToUse);
            }
            
            if (string.IsNullOrEmpty(modelVersionToUse))
            {
                return BadRequest(new { 
                    success = false, 
                    error = new { 
                        code = "NoModelAvailable", 
                        message = "No trained model available for generation. Please train a model first." 
                    } 
                });
            }
            
            var userInfo = userProfile != null ? new UserInfo 
            { 
                Gender = userProfile.Gender, 
                Ethnicity = userProfile.Ethnicity 
            } : null;
            
            _logger.LogInformation("Retrieved user info from database: Gender={Gender}, Ethnicity={Ethnicity}", 
                userInfo?.Gender ?? "NULL", userInfo?.Ethnicity ?? "NULL");
            
            var result = await _replicateApiClient.GenerateImagesAsync(modelVersionToUse, dto.UserId, dto.Style, userInfo);
            
            // Only consume credits AFTER successful API call (5 credits per image generated)
            var creditConsumed = await _basicTierService.ConsumeCreditsAsync(userId, requiredCredits, "styled_generation");
            if (!creditConsumed)
            {
                _logger.LogError("Successfully created Replicate prediction but failed to consume credits for user {UserId}", userId);
                // Note: In this case, the Replicate prediction is already running but we couldn't charge credits
                // This is better than charging credits for failed predictions
            }
            
            var remainingCredits = await _basicTierService.GetAvailableCreditsAsync(userId);
            
            return Ok(new { 
                success = true, 
                data = new {
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
            return StatusCode(500, new { 
                success = false, 
                error = new { 
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
            return BadRequest(new { 
                success = false, 
                error = new { 
                    code = "InsufficientCredits", 
                    message = $"Batch styled image generation requires {requiredCredits} purchased credits. You have {purchasedCredits} purchased credits. Please purchase more credits to generate styled images." 
                } 
            });
        }

        try
        {
            // Get user info from database for prompt generation
            var userProfile = await _dbContext.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);
            
            // Get user's trained model from ModelCreationRequest
            var trainedModel = await _dbContext.ModelCreationRequests
                .Where(m => m.UserId == userId && m.Status == ModelCreationStatus.Ready)
                .OrderByDescending(m => m.CompletedAt)
                .FirstOrDefaultAsync();
            
            // Check if the model is still available on Replicate
            if (trainedModel != null && !string.IsNullOrEmpty(trainedModel.ReplicateModelId))
            {
                var modelAvailable = await _replicateApiClient.CheckModelAvailabilityAsync(trainedModel.ReplicateModelId);
                if (!modelAvailable)
                {
                    _logger.LogWarning("Model {ModelId} is no longer available on Replicate for user {UserId}", 
                        trainedModel.ReplicateModelId, userId);
                    
                    // Mark the model as failed instead of deleting it
                    trainedModel.Status = ModelCreationStatus.Failed;
                    trainedModel.ErrorMessage = "Model no longer available on Replicate";
                    await _dbContext.SaveChangesAsync();
                    
                    return BadRequest(new { 
                        success = false, 
                        error = new { 
                            code = "ModelExpired", 
                            message = "Your trained model has expired or been deleted. Please train a new model to generate styled images." 
                        } 
                    });
                }
            }
            
            // Ensure we have a model to use for generation
            var modelVersionToUse = dto.TrainedModelVersion;
            if (string.IsNullOrEmpty(modelVersionToUse) && trainedModel != null)
            {
                modelVersionToUse = trainedModel.TrainedModelVersion;
                _logger.LogInformation("Using model version from database: {ModelVersion}", modelVersionToUse);
            }
            
            if (string.IsNullOrEmpty(modelVersionToUse))
            {
                return BadRequest(new { 
                    success = false, 
                    error = new { 
                        code = "NoModelAvailable", 
                        message = "No trained model available for generation. Please train a model first." 
                    } 
                });
            }
            
            var userInfo = userProfile != null ? new UserInfo 
            { 
                Gender = userProfile.Gender, 
                Ethnicity = userProfile.Ethnicity 
            } : null;
            
            _logger.LogInformation("Retrieved user info from database: Gender={Gender}, Ethnicity={Ethnicity}", 
                userInfo?.Gender ?? "NULL", userInfo?.Ethnicity ?? "NULL");
            
            // Generate images for all styles in parallel
            var generationTasks = dto.Styles.Select<string, Task<dynamic>>(async (style) =>
            {
                try
                {
                    var result = await _replicateApiClient.GenerateImagesAsync(modelVersionToUse, dto.UserId, style, userInfo, dto.NumOutputsPerStyle);
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
                return StatusCode(500, new { 
                    success = false, 
                    error = new { 
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
            
            return Ok(new { 
                success = true, 
                data = new {
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
            return StatusCode(500, new { 
                success = false, 
                error = new { 
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
                    return Ok(new { success = true, data = new {
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
                    }, error = (object?)null });
                }
                catch { /* ignore, fallback below */ }
            }
        }
        return Ok(new { success = true, data = result, error = (object?)null });
    }

    /// <summary>
    /// Generates a basic casual headshot using base FLUX model (no custom training)
    /// </summary>
    [HttpPost("generate/basic")]
    public async Task<IActionResult> GenerateBasicImage([FromBody] GenerateBasicImageRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, error = new { code = "InvalidModel", message = "Invalid input." } });

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });

        // Check if user has available credits
        var hasCredits = await _basicTierService.HasAvailableCreditsAsync(userId);
        if (!hasCredits)
        {
            var availableCredits = await _basicTierService.GetAvailableCreditsAsync(userId);
            return BadRequest(new { 
                success = false, 
                error = new { 
                    code = "InsufficientCredits", 
                    message = $"No credits available. You have {availableCredits} credits remaining. Credits reset weekly." 
                } 
            });
        }

        // Consume a credit for this casual headshot generation
        var creditConsumed = await _basicTierService.ConsumeCreditsAsync(userId, "casual_headshot_generation");
        if (!creditConsumed)
        {
            return BadRequest(new { 
                success = false, 
                error = new { 
                    code = "CreditConsumptionFailed", 
                    message = "Failed to consume credit. Please try again." 
                } 
            });
        }

        try
        {
            // Use base FLUX model for basic tier - no custom training required
            var result = await _replicateApiClient.GenerateBasicImageAsync(userId, dto.UserInfo, dto.Gender);
            
            var remainingCredits = await _basicTierService.GetAvailableCreditsAsync(userId);
            
            return Ok(new { 
                success = true, 
                data = new { 
                    prediction = result,
                    creditsRemaining = remainingCredits
                }, 
                error = (object?)null 
            });
        }
        catch (Exception ex)
        {
            // If generation fails, we should consider refunding the credit
            // For now, we'll log the error and return failure
            return StatusCode(500, new { 
                success = false, 
                error = new { 
                    code = "GenerationFailed", 
                    message = "Failed to generate image. Please try again later." 
                } 
            });
        }
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
            
            return Ok(new { 
                success = true, 
                data = new { available = isAvailable },
                error = (object?)null 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking model availability for model {ModelId}", modelId);
            return StatusCode(500, new { 
                success = false, 
                error = new { 
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

        return Ok(new { 
            success = true, 
            data = new {
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
            
            return Ok(new { 
                success = false, 
                error = new { 
                    code = "InsufficientCredits", 
                    message = "No credits remaining. Credits reset weekly.",
                    nextResetDate = nextReset
                } 
            });
        }

        try
        {
            // Enhance the uploaded photo
            var result = await _replicateApiClient.EnhancePhotoAsync(userId, dto.ImageUrl, dto.EnhancementType ?? "professional");
            
            // Only consume credit AFTER successful API call
            var creditConsumed = await _basicTierService.ConsumeCreditsAsync(userId, "photo_enhancement");
            if (!creditConsumed)
            {
                _logger.LogError("Successfully created Replicate enhancement but failed to consume credits for user {UserId}", userId);
                // Note: In this case, the Replicate enhancement is already running but we couldn't charge credits
                // This is better than charging credits for failed enhancement requests
            }
            
            var remainingCredits = await _basicTierService.GetAvailableCreditsAsync(userId);
            
            return Ok(new { 
                success = true, 
                data = new { 
                    prediction = result,
                    creditsRemaining = remainingCredits,
                    enhancementType = dto.EnhancementType ?? "professional"
                }, 
                error = (object?)null 
            });
        }
        catch (Exception ex)
        {
            // If enhancement fails, we should consider refunding the credit
            // For now, we'll log the error and return failure
            return StatusCode(500, new { 
                success = false, 
                error = new { 
                    code = "EnhancementFailed", 
                    message = "Failed to enhance photo. Please try again later." 
                } 
            });
        }
    }
}
