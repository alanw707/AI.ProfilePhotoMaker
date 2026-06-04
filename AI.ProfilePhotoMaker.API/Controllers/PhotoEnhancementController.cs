using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Services.Storage;
using AI.ProfilePhotoMaker.API.Services.Security;
using AI.ProfilePhotoMaker.API.Controllers.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AI.ProfilePhotoMaker.API.Controllers;

[Route("api/photo-enhancement")]
[ApiController]
[Authorize]
public class PhotoEnhancementController : ControllerBase
{
    private readonly IReplicateApiClient _replicateApiClient;
    private readonly IBasicTierService _basicTierService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PhotoEnhancementController> _logger;
    private readonly IStorageService _storageService;
    private readonly ITurnstileVerificationService _turnstile;

    public PhotoEnhancementController(
        IReplicateApiClient replicateApiClient,
        IBasicTierService basicTierService,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        IConfiguration configuration,
        ILogger<PhotoEnhancementController> logger,
        IStorageService storageService,
        ITurnstileVerificationService turnstile)
    {
        _replicateApiClient = replicateApiClient;
        _basicTierService = basicTierService;
        _userManager = userManager;
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
        _storageService = storageService;
        _turnstile = turnstile;
    }

    /// <summary>
    /// Enhances a user's uploaded photo using Flux Kontext Pro (basic tier feature)
    /// Provides professional photo enhancement using text-based image editing
    /// </summary>
    [HttpPost("enhance")]
    public async Task<IActionResult> EnhancePhoto([FromBody] EnhancePhotoRequestDto dto)
    {
        CreditConsumptionResult? creditConsumed = null;

        if (!ModelState.IsValid)
            return BadRequest(new { success = false, error = new { code = "InvalidModel", message = "Invalid input." } });

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });
        }

        if (!user.EmailConfirmed)
        {
            return Ok(new
            {
                success = false,
                error = new
                {
                    code = "EmailNotVerified",
                    message = "Please verify your email address before using Photo Transform. Check your inbox (and spam) or resend verification from the app."
                }
            });
        }

        var turnstileOk = await _turnstile.VerifyAsync(dto.TurnstileToken, HttpContext?.Connection?.RemoteIpAddress?.ToString());
        if (!turnstileOk)
        {
            return Ok(new
            {
                success = false,
                error = new
                {
                    code = "BotVerificationFailed",
                    message = "Bot verification failed. Please try again."
                }
            });
        }

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
                    message = "No credits remaining. Credits top up weekly when your balance is below 5.",
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
                _logger.LogError("FluxKontextProModelId configuration is missing for user {UserId}", LoggingSanitizer.SanitizeId(userId));
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
            var externalImageUrl = ReplicateHelpers.ConvertToExternalApiUrl(dto.ImageUrl ?? string.Empty, _configuration, _logger);
            _logger.LogInformation("Converted image URL from {OriginalUrl} to {ExternalUrl} for Replicate API",
                LoggingSanitizer.Sanitize(dto.ImageUrl),
                LoggingSanitizer.Sanitize(externalImageUrl));

            var correlationId = $"photo_enhancement:{Guid.NewGuid()}";

            // Consume credit BEFORE calling Replicate to avoid post-hoc refunds/race conditions
            creditConsumed = await _basicTierService.ConsumeCreditsAsync(
                userId,
                CreditCostConfig.PhotoEnhancement,
                "photo_enhancement",
                correlationId,
                HttpContext?.RequestAborted ?? CancellationToken.None);
            if (creditConsumed is null || !creditConsumed.Success)
            {
                _logger.LogError("Failed to consume credits for photo enhancement for user {UserId}", LoggingSanitizer.SanitizeId(userId));
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

            // Enhance the uploaded photo
            var normalizedImageUrl = await ReplicateHelpers.NormalizeImageUrlForReplicateAsync(externalImageUrl, _configuration, _storageService, _logger);
            var result = await _replicateApiClient.EnhancePhotoAsync(userId, normalizedImageUrl, dto.EnhancementType ?? "professional");

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
            if (creditConsumed?.Success == true)
                await _basicTierService.RefundCreditsAsync(userId, creditConsumed);

            _logger.LogError(ex, "Invalid configuration or parameters for photo enhancement for user {UserId}", LoggingSanitizer.SanitizeId(userId));
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
            if (creditConsumed?.Success == true)
                await _basicTierService.RefundCreditsAsync(userId, creditConsumed);

            _logger.LogError(ex, "Service unavailable for photo enhancement for user {UserId}", LoggingSanitizer.SanitizeId(userId));
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
            if (creditConsumed?.Success == true)
                await _basicTierService.RefundCreditsAsync(userId, creditConsumed);

            _logger.LogError(ex, "Replicate authentication failed during photo enhancement for user {UserId}", LoggingSanitizer.SanitizeId(userId));
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
            if (creditConsumed?.Success == true)
                await _basicTierService.RefundCreditsAsync(userId, creditConsumed);

            _logger.LogError(ex, "Network error during photo enhancement for user {UserId}", LoggingSanitizer.SanitizeId(userId));
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
            if (creditConsumed?.Success == true)
                await _basicTierService.RefundCreditsAsync(userId, creditConsumed);

            _logger.LogError(ex, "Request timeout during photo enhancement for user {UserId}", LoggingSanitizer.SanitizeId(userId));
            return StatusCode(408, new
            {
                success = false,
                error = new
                {
                    code = "RequestTimeout",
                    message = "The enhancement is taking longer than expected (over 2 minutes). Please try again in a few moments - sometimes the service needs a break!"
                }
            });
        }
        catch (Exception ex)
        {
            if (creditConsumed?.Success == true)
                await _basicTierService.RefundCreditsAsync(userId, creditConsumed);

            _logger.LogError(ex, "Unexpected error during photo enhancement for user {UserId}: {ErrorMessage}", LoggingSanitizer.SanitizeId(userId), LoggingSanitizer.Sanitize(ex.Message));
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
}
