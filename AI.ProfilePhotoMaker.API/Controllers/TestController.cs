using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AI.ProfilePhotoMaker.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class TestController : ControllerBase
{
    private readonly IReplicateApiClient _replicateApiClient;
    private readonly IBasicTierService _basicTierService;
    private readonly ILogger<TestController> _logger;
    private readonly ApplicationDbContext _context;

    public TestController(IReplicateApiClient replicateApiClient, IBasicTierService basicTierService, ILogger<TestController> logger, ApplicationDbContext context)
    {
        _replicateApiClient = replicateApiClient;
        _basicTierService = basicTierService;
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Test Replicate API connection by attempting to list available models
    /// </summary>
    [HttpGet("replicate-connection")]
    public async Task<IActionResult> TestReplicateConnection()
    {
        try
        {
            // Test API connection by making a simple request
            // We'll test with a known model version to see if our credentials work
            var testResult = await _replicateApiClient.GetPredictionStatusAsync("dummy-id");
            
            // If we get here without an auth error, connection is working
            return Ok(new { 
                success = true, 
                message = "Replicate API connection successful", 
                data = new { connectionStatus = "Connected" },
                error = (object?)null 
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Replicate API authentication failed");
            return Ok(new { 
                success = false, 
                message = "Replicate API authentication failed", 
                data = (object?)null,
                error = new { code = "AuthError", message = ex.Message }
            });
        }
        catch (Exception ex) when (ex.Message.Contains("not found") || ex.Message.Contains("404"))
        {
            // Expected error for dummy ID - means auth worked but resource doesn't exist
            return Ok(new { 
                success = true, 
                message = "Replicate API connection successful (test prediction not found as expected)", 
                data = new { connectionStatus = "Connected", authStatus = "Valid" },
                error = (object?)null 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Replicate API connection test failed");
            return Ok(new { 
                success = false, 
                message = "Replicate API connection failed", 
                data = (object?)null,
                error = new { code = "ConnectionError", message = ex.Message }
            });
        }
    }

    /// <summary>
    /// Test the complete training workflow with real images
    /// WARNING: This will consume Replicate credits!
    /// </summary>
    [HttpPost("replicate-training-test")]
    public async Task<IActionResult> TestReplicateTraining([FromBody] TestTrainingRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.ImageZipUrl) || string.IsNullOrEmpty(request.UserId))
            {
                return BadRequest(new { 
                    success = false, 
                    error = new { code = "InvalidInput", message = "ImageZipUrl and UserId are required" } 
                });
            }

            _logger.LogInformation("Starting polling-based Replicate training test for user {UserId}", request.UserId);

            // Start polling-based model creation and training
            var modelCreationRequestId = await _replicateApiClient.InitiateModelCreationAndTrainingAsync(request.UserId, request.ImageZipUrl);

            return Ok(new {
                success = true,
                message = "Model creation and training initiated successfully (polling-based)",
                data = new {
                    modelCreationRequestId = modelCreationRequestId,
                    status = "pending",
                    note = "Training will start automatically when model creation completes via background polling"
                },
                error = (object?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Replicate training test failed for user {UserId}", request.UserId);
            return StatusCode(500, new { 
                success = false, 
                message = "Training test failed", 
                data = (object?)null,
                error = new { code = "TrainingError", message = ex.Message }
            });
        }
    }

    /// <summary>
    /// Test image generation with a specific trained model
    /// WARNING: This will consume Replicate credits!
    /// </summary>
    [HttpPost("replicate-generation-test")]
    public async Task<IActionResult> TestReplicateGeneration([FromBody] TestGenerationRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.TrainedModelVersion) || string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.Style))
            {
                return BadRequest(new { 
                    success = false, 
                    error = new { code = "InvalidInput", message = "TrainedModelVersion, UserId, and Style are required" } 
                });
            }

            _logger.LogInformation("Starting Replicate generation test for user {UserId} with style {Style}", request.UserId, request.Style);

            // Start image generation
            var generationResult = await _replicateApiClient.GenerateImagesAsync(
                request.TrainedModelVersion, 
                request.UserId, 
                request.Style, 
                request.UserInfo);

            return Ok(new { 
                success = true, 
                message = "Image generation started successfully", 
                data = new { 
                    predictionId = generationResult.Id,
                    status = generationResult.Status,
                    createdAt = generationResult.CreatedAt
                },
                error = (object?)null 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Replicate generation test failed for user {UserId}", request.UserId);
            return StatusCode(500, new { 
                success = false, 
                message = "Generation test failed", 
                data = (object?)null,
                error = new { code = "GenerationError", message = ex.Message }
            });
        }
    }
    /// <summary>
    /// Deletes all records from the ModelCreationRequests table
    /// </summary>
    [HttpDelete("clear-model-creation-requests")]
    public async Task<IActionResult> ClearModelCreationRequests()
    {
        try
        {
            var rowCount = await _context.ModelCreationRequests.CountAsync();
            if (rowCount == 0)
            {
                return Ok(new {
                    success = true,
                    message = "ModelCreationRequests table is already empty.",
                    data = new { rowsDeleted = 0 },
                    error = (object?)null
                });
            }

            // Use raw SQL for efficient deletion
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM ModelCreationRequests");

            return Ok(new {
                success = true,
                message = $"Successfully deleted {rowCount} rows from ModelCreationRequests table.",
                data = new { rowsDeleted = rowCount },
                error = (object?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear ModelCreationRequests table");
            return StatusCode(500, new {
                success = false,
                message = "Failed to clear ModelCreationRequests table.",
                data = (object?)null,
                error = new { code = "DatabaseError", message = ex.Message }
            });
        }
    }

    /// <summary>
    /// Test the basic tier image generation workflow
    /// This tests the complete flow: credit check -> consumption -> generation
    /// </summary>
    [HttpPost("basic-generation-test")]
    public async Task<IActionResult> TestBasicGeneration([FromBody] TestBasicGenerationRequest request)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { 
                    success = false, 
                    error = new { code = "InvalidUser", message = "User not authenticated" } 
                });
            }

            _logger.LogInformation("Testing basic generation for user {UserId} with gender {Gender}", userId, request.Gender);

            // Step 1: Check available credits
            var availableCredits = await _basicTierService.GetAvailableCreditsAsync(userId);
            _logger.LogInformation("User {UserId} has {Credits} available credits", userId, availableCredits);

            if (availableCredits <= 0)
            {
                return Ok(new {
                    success = false,
                    message = "No credits available",
                    data = new {
                        availableCredits = availableCredits,
                        creditStatus = "exhausted"
                    },
                    error = new { code = "InsufficientCredits", message = "No credits remaining. Credits reset weekly." }
                });
            }

            // Step 2: Consume a credit
            var creditConsumed = await _basicTierService.ConsumeCreditsAsync(userId, 1, "test_basic_generation");
            if (!creditConsumed)
            {
                return Ok(new {
                    success = false,
                    message = "Failed to consume credit",
                    data = (object?)null,
                    error = new { code = "CreditConsumptionFailed", message = "Could not consume credit for generation" }
                });
            }

            _logger.LogInformation("Successfully consumed 1 credit for user {UserId}", userId);

            // Step 3: Generate basic image using base FLUX model
            var userInfo = request.UserInfo ?? new Models.UserInfo { Gender = request.Gender };
            var generationResult = await _replicateApiClient.GenerateBasicImageAsync(userId, userInfo, request.Gender);

            // Step 4: Get updated credits
            var remainingCredits = await _basicTierService.GetAvailableCreditsAsync(userId);

            return Ok(new {
                success = true,
                message = "Basic image generation started successfully",
                data = new {
                    predictionId = generationResult.Id,
                    status = generationResult.Status,
                    createdAt = generationResult.CreatedAt,
                    creditsConsumed = 1,
                    creditsRemaining = remainingCredits,
                    generationType = "basic_tier_casual",
                    modelUsed = "base_flux"
                },
                error = (object?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Basic generation test failed for user");
            return StatusCode(500, new {
                success = false,
                message = "Basic generation test failed",
                data = (object?)null,
                error = new { code = "GenerationError", message = ex.Message }
            });
        }
    }

    /// <summary>
    /// Test endpoint to check current user's basic tier status
    /// </summary>
    [HttpGet("basic-tier-status")]
    public async Task<IActionResult> GetFreeTierStatus()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { 
                    success = false, 
                    error = new { code = "InvalidUser", message = "User not authenticated" } 
                });
            }

            var profile = await _basicTierService.GetUserProfileWithCreditsAsync(userId);
            if (profile == null)
            {
                return Ok(new {
                    success = false,
                    message = "User profile not found",
                    data = (object?)null,
                    error = new { code = "ProfileNotFound", message = "User profile does not exist" }
                });
            }

            var availableCredits = await _basicTierService.GetAvailableCreditsAsync(userId);

            return Ok(new {
                success = true,
                message = "Basic tier status retrieved successfully",
                data = new {
                    userId = userId,
                    subscriptionTier = profile.SubscriptionTier.ToString(),
                    availableCredits = availableCredits,
                    totalCredits = profile.Credits,
                    lastCreditReset = profile.LastCreditReset,
                    nextResetDate = profile.LastCreditReset.AddDays(7),
                    daysUntilReset = Math.Max(0, 7 - (DateTime.UtcNow - profile.LastCreditReset).Days),
                    canGenerate = availableCredits > 0
                },
                error = (object?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get basic tier status");
            return StatusCode(500, new {
                success = false,
                message = "Failed to get basic tier status",
                data = (object?)null,
                error = new { code = "StatusError", message = ex.Message }
            });
        }
    }

    /// <summary>
    /// Test endpoint to manually reset user's weekly credits (for testing purposes)
    /// </summary>
    [HttpPost("reset-credits")]
    public async Task<IActionResult> ResetCredits()
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { 
                    success = false, 
                    error = new { code = "InvalidUser", message = "User not authenticated" } 
                });
            }

            await _basicTierService.ResetWeeklyCreditsAsync(userId);
            var newCredits = await _basicTierService.GetAvailableCreditsAsync(userId);

            return Ok(new {
                success = true,
                message = "Credits reset successfully",
                data = new {
                    userId = userId,
                    newCredits = newCredits,
                    resetAt = DateTime.UtcNow
                },
                error = (object?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset credits");
            return StatusCode(500, new {
                success = false,
                message = "Failed to reset credits",
                data = (object?)null,
                error = new { code = "ResetError", message = ex.Message }
            });
        }
    }

    /// <summary>
    /// Test endpoint to enhance a user's photo (basic tier alternative)
    /// </summary>
    [HttpPost("photo-enhancement-test")]
    public async Task<IActionResult> TestPhotoEnhancement([FromBody] TestPhotoEnhancementRequest request)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { 
                    success = false, 
                    error = new { code = "InvalidUser", message = "User not authenticated" } 
                });
            }

            _logger.LogInformation("Testing photo enhancement for user {UserId} with image {ImageUrl}", userId, request.ImageUrl);

            // Step 1: Check available credits
            var availableCredits = await _basicTierService.GetAvailableCreditsAsync(userId);
            _logger.LogInformation("User {UserId} has {Credits} available credits", userId, availableCredits);

            if (availableCredits <= 0)
            {
                return Ok(new {
                    success = false,
                    message = "No credits available",
                    data = new {
                        availableCredits = availableCredits,
                        creditStatus = "exhausted"
                    },
                    error = new { code = "InsufficientCredits", message = "No credits remaining. Credits reset weekly." }
                });
            }

            // Step 2: Consume a credit
            var creditConsumed = await _basicTierService.ConsumeCreditsAsync(userId, 1, "test_photo_enhancement");
            if (!creditConsumed)
            {
                return Ok(new {
                    success = false,
                    message = "Failed to consume credit",
                    data = (object?)null,
                    error = new { code = "CreditConsumptionFailed", message = "Could not consume credit for enhancement" }
                });
            }

            _logger.LogInformation("Successfully consumed 1 credit for user {UserId}", userId);

            // Step 3: Enhance photo
            var enhancementResult = await _replicateApiClient.EnhancePhotoAsync(userId, request.ImageUrl, request.EnhancementType ?? "professional");

            // Step 4: Get updated credits
            var remainingCredits = await _basicTierService.GetAvailableCreditsAsync(userId);

            return Ok(new {
                success = true,
                message = "Photo enhancement started successfully",
                data = new {
                    predictionId = enhancementResult.Id,
                    status = enhancementResult.Status,
                    createdAt = enhancementResult.CreatedAt,
                    creditsConsumed = 1,
                    creditsRemaining = remainingCredits,
                    enhancementType = request.EnhancementType ?? "professional",
                    serviceType = "photo_enhancement"
                },
                error = (object?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Photo enhancement test failed for user");
            return StatusCode(500, new {
                success = false,
                message = "Photo enhancement test failed",
                data = (object?)null,
                error = new { code = "EnhancementError", message = ex.Message }
            });
        }
    }
}

/// <summary>
/// DTO for testing training workflow
/// </summary>
public record TestTrainingRequest(
    string UserId,
    string ImageZipUrl
);

/// <summary>
/// DTO for testing generation workflow
/// </summary>
public record TestGenerationRequest(
    string UserId,
    string TrainedModelVersion,
    string Style,
    Models.UserInfo? UserInfo = null
);

/// <summary>
/// DTO for testing free generation workflow
/// </summary>
public record TestBasicGenerationRequest(
    string Gender,
    Models.UserInfo? UserInfo = null
);

/// <summary>
/// DTO for testing photo enhancement workflow
/// </summary>
public record TestPhotoEnhancementRequest(
    string ImageUrl,
    string? EnhancementType = "professional"
);