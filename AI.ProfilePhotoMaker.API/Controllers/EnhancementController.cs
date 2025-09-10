using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>
/// Controller for OpenAI DALL-E 3 photo enhancement
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class EnhancementController : ControllerBase
{
    private readonly OpenAIImageGenerationService _openAIService;
    private readonly IBasicTierService _basicTierService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<EnhancementController> _logger;
    private readonly IWebHostEnvironment _environment;

    public EnhancementController(
        OpenAIImageGenerationService openAIService,
        IBasicTierService basicTierService,
        ApplicationDbContext dbContext,
        ILogger<EnhancementController> logger,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _openAIService = openAIService;
        _basicTierService = basicTierService;
        _dbContext = dbContext;
        _logger = logger;
        _environment = environment;
        
        // Validate OpenAI configuration at startup
        var apiKey = configuration["OpenAI:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("OpenAI API key not configured - enhancement features will be unavailable");
        }
    }

    /// <summary>
    /// Enhances a user's uploaded photo using OpenAI DALL-E 3
    /// Provides creative anime-style and 3D transformations
    /// </summary>
    [HttpPost("enhance")]
    public async Task<IActionResult> EnhancePhoto([FromBody] EnhancePhotoRequestDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new
            {
                success = false,
                error = new
                {
                    code = "Unauthorized",
                    message = "User authentication required"
                }
            });
        }

        try
        {
            _logger.LogInformation("Starting OpenAI photo enhancement for user {UserId} with style {EnhancementType}", userId, dto.EnhancementType);
            
            // Normalize image URL so the API can fetch it in all dev setups
            dto.ImageUrl = NormalizeImageUrlForServerAccess(dto.ImageUrl);
            _logger.LogInformation("Enhancement source image URL normalized to: {ImageUrl}", dto.ImageUrl);

            // Check credit availability (OpenAI costs 2 credits)
            var availableCredits = await _basicTierService.GetAvailableCreditsAsync(userId);
            if (availableCredits < 2)
            {
                _logger.LogWarning("User {UserId} has insufficient credits for OpenAI enhancement. Available: {Credits}", userId, availableCredits);
                return BadRequest(new
                {
                    success = false,
                    error = new
                    {
                        code = "InsufficientCredits",
                        message = "Insufficient credits for OpenAI enhancement. 2 credits required."
                    }
                });
            }

            // Consume credits BEFORE API call to prevent race conditions
            // TEMPORARY DEBUG: Skip credit check in development for OpenAI API testing
            bool creditConsumed;
            if (_environment.IsDevelopment() && dto.EnhancementType == "chibi")
            {
                _logger.LogWarning("DEVELOPMENT MODE: Skipping credit check for OpenAI API debugging");
                creditConsumed = true;
            }
            else
            {
                creditConsumed = await _basicTierService.ConsumeCreditsAsync(userId, 2, "openai_enhancement");
            }
            
            if (!creditConsumed)
            {
                _logger.LogError("Failed to consume credits for user {UserId} for OpenAI enhancement", userId);
                return StatusCode(500, new
                {
                    success = false,
                    error = new
                    {
                        code = "CreditConsumptionFailed",
                        message = "Failed to process credit deduction. Please try again."
                    }
                });
            }

            // Call OpenAI service to get base64 image data
            var base64ImageData = await _openAIService.EnhancePhotoQualityAsync(dto);
            
            var remainingCredits = await _basicTierService.GetAvailableCreditsAsync(userId);

            _logger.LogInformation("OpenAI photo enhancement completed successfully for user {UserId}", userId);

            // Return Replicate-compatible response format that frontend expects
            return Ok(new
            {
                success = true,
                data = new
                {
                    // Replicate-compatible fields that frontend validates
                    Id = Guid.NewGuid().ToString(),
                    Status = "succeeded",
                    Output = new[] { base64ImageData }, // Standard Replicate output format
                    CompletedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    
                    // OpenAI-specific fields
                    dataUrl = base64ImageData, // Frontend expects this field name and base64 format
                    creditsRemaining = remainingCredits,
                    enhancementType = dto.EnhancementType ?? "professional",
                    provider = "OpenAI"
                },
                error = (object?)null
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid configuration or parameters for OpenAI enhancement for user {UserId}", userId);
            
            // Credits already consumed - refund not implemented yet
            
            return BadRequest(new
            {
                success = false,
                error = new
                {
                    code = "InvalidRequest",
                    message = "Invalid enhancement request parameters."
                }
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "OpenAI service unavailable for user {UserId}", userId);
            
            // Credits already consumed - refund not implemented yet
            
            return StatusCode(503, new
            {
                success = false,
                error = new
                {
                    code = "ServiceUnavailable",
                    message = "OpenAI enhancement service is temporarily unavailable. Please try again later."
                }
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "OpenAI authentication failed for user {UserId}", userId);
            
            // Credits already consumed - refund not implemented yet
            
            return StatusCode(401, new
            {
                success = false,
                error = new
                {
                    code = "AuthenticationFailed",
                    message = "OpenAI service authentication failed. Please contact support."
                }
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error during OpenAI enhancement for user {UserId}", userId);
            
            // Credits already consumed - refund not implemented yet
            
            return StatusCode(502, new
            {
                success = false,
                error = new
                {
                    code = "NetworkError",
                    message = "Failed to connect to OpenAI service. Please try again later."
                }
            });
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout during OpenAI enhancement for user {UserId}", userId);
            
            // Credits already consumed - refund not implemented yet
            
            return StatusCode(408, new
            {
                success = false,
                error = new
                {
                    code = "RequestTimeout",
                    message = "OpenAI enhancement is taking longer than expected. Please try again in a few moments."
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during OpenAI enhancement for user {UserId}: {ErrorMessage}", userId, ex.Message);
            
            // Credits already consumed - refund not implemented yet
            
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
    /// In local development, uploads can point at Azurite via 127.0.0.1:10000.
    /// When the API runs in a different network context (container/WSL), that host may be unreachable.
    /// Rewrite such URLs to go through our own proxy endpoint so the server can always fetch them.
    /// </summary>
    private string NormalizeImageUrlForServerAccess(string originalUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(originalUrl)) return originalUrl;
            var uri = new Uri(originalUrl, UriKind.Absolute);

            var isAzuriteLocal = (uri.Host.Equals("127.0.0.1") || uri.Host.Equals("localhost"))
                                 && uri.Port == 10000
                                 && uri.AbsolutePath.StartsWith("/devstoreaccount1/", StringComparison.OrdinalIgnoreCase);

            if (isAzuriteLocal)
            {
                // Route via this API instance so middleware can proxy to Azurite reliably
                var scheme = Request.Scheme; // http in dev
                var host = Request.Host.Value; // e.g., localhost:5032
                var proxied = $"{scheme}://{host}{uri.AbsolutePath}{uri.Query}";
                _logger.LogDebug("Rewriting Azurite URL {Original} -> {Proxied}", originalUrl, proxied);
                return proxied;
            }

            return originalUrl;
        }
        catch
        {
            // If parsing fails, just return original URL
            return originalUrl;
        }
    }
}
