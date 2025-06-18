using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AI.ProfilePhotoMaker.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ReplicateController : ControllerBase
{
    private readonly IReplicateApiClient _replicateApiClient;
    private readonly IBasicTierService _basicTierService;

    public ReplicateController(IReplicateApiClient replicateApiClient, IBasicTierService basicTierService)
    {
        _replicateApiClient = replicateApiClient;
        _basicTierService = basicTierService;
    }

    /// <summary>
    /// Initiates model training for a user
    /// </summary>
    [HttpPost("train")]
    public async Task<IActionResult> TrainModel([FromBody] TrainModelRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, error = new { code = "InvalidModel", message = "Invalid input." } });

        var result = await _replicateApiClient.CreateModelTrainingAsync(dto.UserId, dto.ImageZipUrl);
        return Ok(new { success = true, data = result, error = (object?)null });
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
    /// Generates images using a trained model and style
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateImages([FromBody] GenerateImagesRequestDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, error = new { code = "InvalidModel", message = "Invalid input." } });

        var result = await _replicateApiClient.GenerateImagesAsync(dto.TrainedModelVersion, dto.UserId, dto.Style, dto.UserInfo);
        return Ok(new { success = true, data = result, error = (object?)null });
    }

    /// <summary>
    /// Gets the status of an image generation prediction
    /// </summary>
    [HttpGet("generate/status/{predictionId}")]
    public async Task<IActionResult> GetPredictionStatus(string predictionId)
    {
        var result = await _replicateApiClient.GetPredictionStatusAsync(predictionId);
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

        // Consume a credit for this generation
        var creditConsumed = await _basicTierService.ConsumeCreditsAsync(userId, 1, "basic_generation");
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

        // Consume credit for enhancement
        var creditConsumed = await _basicTierService.ConsumeCreditsAsync(userId, 1, "photo_enhancement");
        if (!creditConsumed)
        {
            return StatusCode(500, new { 
                success = false, 
                error = new { 
                    code = "CreditConsumptionFailed", 
                    message = "Failed to process request. Please try again." 
                } 
            });
        }

        try
        {
            // Enhance the uploaded photo
            var result = await _replicateApiClient.EnhancePhotoAsync(userId, dto.ImageUrl, dto.EnhancementType ?? "professional");
            
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
