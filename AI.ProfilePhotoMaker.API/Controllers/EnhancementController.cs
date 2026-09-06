using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Services.Storage;
using AI.ProfilePhotoMaker.API.Services.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>
/// Controller for OpenAI GPT Image photo enhancement
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class EnhancementController : ControllerBase
{
    private readonly OpenAIImageGenerationService _openAIService;
    private readonly IBasicTierService _basicTierService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EnhancementController> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly HttpClient _httpClient;
    private readonly IStorageService _storageService;
    private readonly IOutcomePackageService? _outcomePackageService;
    private readonly StoragePathResolver _pathResolver;
    private readonly ITurnstileVerificationService _turnstile;

    private static string S(string? value) => LoggingSanitizer.Sanitize(value);
    private static string Sid(string? value) => LoggingSanitizer.SanitizeId(value);

    public EnhancementController(
        OpenAIImageGenerationService openAIService,
        IBasicTierService basicTierService,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        ILogger<EnhancementController> logger,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        HttpClient httpClient,
        IStorageService storageService,
        StoragePathResolver pathResolver,
        ITurnstileVerificationService turnstile,
        IOutcomePackageService? outcomePackageService = null)
    {
        _openAIService = openAIService;
        _basicTierService = basicTierService;
        _userManager = userManager;
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
        _environment = environment;
        _httpClient = httpClient;
        _storageService = storageService;
        _outcomePackageService = outcomePackageService;
        _pathResolver = pathResolver;
        _turnstile = turnstile;

        // Configure HttpClient for OpenAI API health checks
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? configuration["OpenAI:ApiKey"];
        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            var tail = apiKey.Length >= 4 ? apiKey[^4..] : "****";
            _logger.LogDebug("OpenAI health HttpClient configured (len={Len}, tail=****{Tail})", apiKey.Length, S(tail));
        }

        // Note: OpenAI service will throw InvalidOperationException if API key is missing
        // This allows the controller to be constructed but will fail at service call time with proper error handling
    }

    /// <summary>
    /// Health check endpoint for OpenAI service connectivity
    /// Tests if OpenAI API is configured and accessible without consuming credits
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public async Task<IActionResult> HealthCheck()
    {
        try
        {
            _logger.LogInformation("OpenAI health check requested");

            // Simple connectivity test - check OpenAI models endpoint (lightweight)
            var response = await _httpClient.GetAsync("https://api.openai.com/v1/models");

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("OpenAI health check passed");
                return Ok(new
                {
                    success = true,
                    service = "OpenAI",
                    status = "healthy",
                    message = "OpenAI service is accessible and configured correctly",
                    timestamp = DateTime.UtcNow
                });
            }
            else
            {
                _logger.LogWarning("OpenAI health check failed with status {StatusCode}", response.StatusCode);
                return StatusCode(503, new
                {
                    success = false,
                    service = "OpenAI",
                    status = "unhealthy",
                    message = $"OpenAI service returned status: {response.StatusCode}",
                    timestamp = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI health check failed with exception");
            return StatusCode(503, new
            {
                success = false,
                service = "OpenAI",
                status = "unhealthy",
                message = "OpenAI service health check failed",
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Enhances a user's uploaded photo using OpenAI GPT Image.
    /// Provides professional cleanup and creative transformations.
    /// </summary>
    [HttpPost("enhance")]
    [Route("~/api/replicate/enhance")]
    public async Task<IActionResult> EnhancePhoto([FromBody] EnhancePhotoRequestDto dto)
    {
        CreditConsumptionResult? creditConsumptionResult = null;

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

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
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

        try
        {
            var validationError = ValidateEnhancementRequestForUser(dto, userId);
            if (validationError != null)
            {
                return validationError;
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

            _logger.LogInformation("Starting OpenAI photo enhancement for user {UserId} with style {EnhancementType}", Sid(userId), S(dto.EnhancementType));

            var isPremiumAugmentation = IsPremiumAugmentation(dto.EnhancementType);
            var isProfessionalRefinement = IsProfessionalRefinement(dto.EnhancementType);
            if (isPremiumAugmentation && _outcomePackageService != null && !await HasPremiumAugmentationAllowanceAsync(userId))
            {
                return StatusCode(402, new
                {
                    success = false,
                    error = new
                    {
                        code = "PremiumAugmentationEntitlementRequired",
                        message = "Unlock a Pro Package before applying premium augmentations."
                    }
                });
            }

            if (isProfessionalRefinement && _outcomePackageService != null && !await HasRefinementAllowanceAsync(userId))
            {
                return StatusCode(402, new
                {
                    success = false,
                    error = new
                    {
                        code = "RefinementEntitlementRequired",
                        message = "Unlock a Starter or Pro Package before applying guided refinements."
                    }
                });
            }

            if (string.IsNullOrWhiteSpace(dto.ImageStoragePath) && !string.IsNullOrWhiteSpace(dto.ImageUrl))
            {
                // Legacy fallback only. New enhancement uploads use ImageStoragePath so the server
                // reads directly from storage instead of re-downloading through public/proxy URLs.
                dto.ImageUrl = await NormalizeImageUrlForServerAccessAsync(dto.ImageUrl);
                _logger.LogInformation("Enhancement source image URL normalized to: {ImageUrl}", S(dto.ImageUrl));
            }

            // Package refinements and premium augmentations are paid for by their separate
            // outcome-package allowances, not the legacy credit ledger.
            var requiredCredits = _outcomePackageService != null && (isPremiumAugmentation || isProfessionalRefinement)
                ? 0
                : CreditCostConfig.GetCreditCost("photo_enhancement");
            var availableCredits = await _basicTierService.GetAvailableCreditsAsync(userId);
            if (availableCredits < requiredCredits)
            {
                _logger.LogWarning("User {UserId} has insufficient credits for OpenAI enhancement. Available: {Credits}", Sid(userId), availableCredits);
                return BadRequest(new
                {
                    success = false,
                    error = new
                    {
                        code = "InsufficientCredits",
                        message = $"Insufficient credits for OpenAI enhancement. {requiredCredits} credit required."
                    }
                });
            }

            if (requiredCredits > 0)
            {
                // Consume legacy credits before the provider call to prevent races.
                var correlationId = $"photo_enhancement:{Guid.NewGuid()}";
                creditConsumptionResult = await _basicTierService.ConsumeCreditsAsync(
                    userId,
                    requiredCredits,
                    "photo_enhancement",
                    correlationId,
                    HttpContext?.RequestAborted ?? CancellationToken.None);

                if (creditConsumptionResult == null || !creditConsumptionResult.Success)
                {
                    if (creditConsumptionResult?.Error == "insufficient_credits")
                    {
                        _logger.LogWarning("Insufficient credits detected during OpenAI enhancement for user {UserId}", Sid(userId));
                        return BadRequest(new
                        {
                            success = false,
                            error = new
                            {
                                code = "InsufficientCredits",
                                message = $"Insufficient credits for OpenAI enhancement. {requiredCredits} credit required."
                            }
                        });
                    }

                    _logger.LogError("Failed to consume credits for user {UserId} for OpenAI enhancement", Sid(userId));
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
            }

            // Call OpenAI service to get base64 image data
            var base64ImageData = await _openAIService.EnhancePhotoQualityAsync(dto);
            if (isPremiumAugmentation && _outcomePackageService != null && !await _outcomePackageService.ConsumePremiumAugmentationAsync(userId, HttpContext?.RequestAborted ?? CancellationToken.None))
            {
                await HandleEnhancementRefundAsync(userId, creditConsumptionResult, "premium augmentation entitlement race", "Premium augmentation allowance was no longer available after generation.");
                return StatusCode(409, new { success = false, error = new { code = "PremiumAugmentationEntitlementUnavailable", message = "Premium augmentation allowance was already used. Your credit was refunded." } });
            }

            if (isProfessionalRefinement && _outcomePackageService != null && !await _outcomePackageService.ConsumeRefinementAsync(userId, cancellationToken: HttpContext?.RequestAborted ?? CancellationToken.None))
            {
                await HandleEnhancementRefundAsync(userId, creditConsumptionResult, "refinement entitlement race", "Refinement allowance was no longer available after generation.");
                return StatusCode(409, new { success = false, error = new { code = "RefinementEntitlementUnavailable", message = "Refinement allowance was already used. Your credit was refunded." } });
            }

            var storedResult = await SaveEnhancementResultAsync(base64ImageData, dto, userId, HttpContext?.RequestAborted ?? CancellationToken.None);
            var remainingCredits = await _basicTierService.GetAvailableCreditsAsync(userId);

            _logger.LogInformation("OpenAI photo enhancement completed successfully for user {UserId}", Sid(userId));

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
                    provider = "OpenAI",
                    processedImageId = storedResult.ProcessedImageId,
                    storagePath = storedResult.StoragePath
                },
                error = (object?)null
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid configuration or parameters for OpenAI enhancement for user {UserId}", Sid(userId));

            await HandleEnhancementRefundAsync(userId, creditConsumptionResult, "invalid request", ex.Message);

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
            await HandleEnhancementRefundAsync(userId, creditConsumptionResult, "failed enhancement", ex.Message);

            // Check if this is a configuration issue (missing API key) vs service failure
            if (ex.Message.Contains("OpenAI API key is required but not configured"))
            {
                _logger.LogError(ex, "OpenAI API key not configured - service cannot function");

                return StatusCode(500, new
                {
                    success = false,
                    error = new
                    {
                        code = "ConfigurationError",
                        message = "OpenAI enhancement service is not properly configured. Please contact support."
                    }
                });
            }
            else
            {
                _logger.LogError(ex, "OpenAI service unavailable for user {UserId}", Sid(userId));

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
        }
        catch (UnauthorizedAccessException ex)
        {
            await HandleEnhancementRefundAsync(userId, creditConsumptionResult, "authentication failure", ex.Message);

            _logger.LogError(ex, "OpenAI authentication failed for user {UserId}", Sid(userId));

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
            await HandleEnhancementRefundAsync(userId, creditConsumptionResult, "network error", ex.Message);

            _logger.LogError(ex, "Network error during OpenAI enhancement for user {UserId}", Sid(userId));

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
            await HandleEnhancementRefundAsync(userId, creditConsumptionResult, "timeout", ex.Message);

            _logger.LogError(ex, "Request timeout during OpenAI enhancement for user {UserId}", Sid(userId));

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
            await HandleEnhancementRefundAsync(userId, creditConsumptionResult, "unexpected error", ex.Message);

            _logger.LogError(ex, "Unexpected error during OpenAI enhancement for user {UserId}: {ErrorMessage}", Sid(userId), S(ex.Message));

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

    private async Task<bool> HasPremiumAugmentationAllowanceAsync(string userId)
    {
        if (_outcomePackageService == null) return true;
        var entitlements = await _outcomePackageService.GetUserEntitlementsAsync(userId, HttpContext?.RequestAborted ?? CancellationToken.None);
        return entitlements.Any(e => string.Equals(e.Status, "Active", StringComparison.OrdinalIgnoreCase) && e.RemainingPremiumAugmentations > 0);
    }

    private async Task<bool> HasRefinementAllowanceAsync(string userId)
    {
        if (_outcomePackageService == null) return true;
        var entitlements = await _outcomePackageService.GetUserEntitlementsAsync(userId, HttpContext?.RequestAborted ?? CancellationToken.None);
        return entitlements.Any(e => string.Equals(e.Status, "Active", StringComparison.OrdinalIgnoreCase) && e.RemainingRefinements > 0);
    }

    private async Task<(int ProcessedImageId, string StoragePath)> SaveEnhancementResultAsync(string dataUrl, EnhancePhotoRequestDto dto, string userId, CancellationToken cancellationToken)
    {
        var bytes = DecodeImageDataUrl(dataUrl);
        var fileName = $"enhancement-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.png";
        await using var stream = new MemoryStream(bytes);
        var storedPath = await _storageService.SaveImageAsync(stream, fileName, userId, "enhanced");
        var profile = await _dbContext.UserProfiles.SingleAsync(p => p.UserId == userId, cancellationToken);
        var processedImage = new ProcessedImage
        {
            OriginalImageUrl = dto.ImageStoragePath ?? dto.ImageUrl ?? string.Empty,
            ProcessedImageUrl = storedPath,
            Style = dto.EnhancementType ?? "professional",
            UserProfileId = profile.Id,
            CreatedAt = DateTime.UtcNow,
            IsGenerated = true,
            IsOriginalUpload = false,
            Provider = "OpenAI",
            ProviderModel = _configuration["OpenAI:ImageModel"] ?? "gpt-image-2",
            GenerationMode = IsPremiumAugmentation(dto.EnhancementType) ? "premium_augmentation" : "photo_refinement",
            PromptVersion = "enhancement-v1",
            CreditCost = CreditCostConfig.GetCreditCost("photo_enhancement"),
            GenerationStatus = "succeeded",
            CorrelationId = $"photo_enhancement:{Guid.NewGuid()}"
        };
        processedImage.SetScheduledDeletionDate();
        _dbContext.ProcessedImages.Add(processedImage);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (processedImage.Id, storedPath);
    }

    private static byte[] DecodeImageDataUrl(string dataUrl)
    {
        var commaIndex = dataUrl.IndexOf(',');
        var payload = commaIndex >= 0 ? dataUrl[(commaIndex + 1)..] : dataUrl;
        return Convert.FromBase64String(payload);
    }

    private static bool IsPremiumAugmentation(string? enhancementType)
    {
        var normalized = (enhancementType ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "relighting" or "professional_polish" or "outfit_upgrade" or "background_upgrade" or "skin_tone_polish" or "sharpen_detail" or "skin_smoothing" or "wrinkle_softening";
    }

    private static bool IsProfessionalRefinement(string? enhancementType)
    {
        var normalized = (enhancementType ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "headshot_linkedin" or "headshot_creator" or "headshot_office" or "headshot_studio";
    }

    private IActionResult? ValidateEnhancementRequestForUser(EnhancePhotoRequestDto dto, string userId)
    {
        var enhancementType = dto.EnhancementType ?? "professional";
        if (string.Equals(enhancementType, "headshot", StringComparison.OrdinalIgnoreCase) &&
            !IsOpenAIHeadshotMvpEnabled())
        {
            return BadRequest(new
            {
                success = false,
                error = new
                {
                    code = "FeatureDisabled",
                    message = "Headshot enhancement is not enabled."
                }
            });
        }

        if (string.IsNullOrWhiteSpace(dto.ImageStoragePath))
        {
            return null;
        }

        var storagePath = dto.ImageStoragePath.Trim();
        string decodedPath;
        try
        {
            decodedPath = Uri.UnescapeDataString(storagePath);
        }
        catch
        {
            return InvalidStoragePathResponse();
        }

        if (!string.Equals(storagePath, decodedPath, StringComparison.Ordinal) ||
            Uri.TryCreate(storagePath, UriKind.Absolute, out _) ||
            storagePath.Contains('\\') ||
            storagePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(p => p == "." || p == ".."))
        {
            return InvalidStoragePathResponse();
        }

        var allowedPrefixes = new[]
        {
            _pathResolver.GetDirectoryPrefix(StorageType.Enhanced, userId),
            _pathResolver.GetDirectoryPrefix(StorageType.Generated, userId),
            _pathResolver.GetDirectoryPrefix(StorageType.GeneratedPrivate, userId),
            $"enhanced/{userId}/",
            $"generated/{userId}/",
            $"generated-private/{userId}/"
        };
        if (!allowedPrefixes.Any(prefix => storagePath.StartsWith(prefix, StringComparison.Ordinal) && storagePath.Length > prefix.Length))
        {
            _logger.LogWarning(
                "Rejected enhancement storage path for user {UserId}. Path={StoragePath}, ExpectedPrefixes={ExpectedPrefixes}",
                Sid(userId),
                S(storagePath),
                string.Join(",", allowedPrefixes.Select(S)));
            return InvalidStoragePathResponse();
        }

        return null;
    }

    private bool IsOpenAIHeadshotMvpEnabled()
    {
        var configured = _configuration.GetValue<bool?>("Features:OpenAIHeadshotMvp");
        return configured ?? !_environment.IsProduction();
    }

    private IActionResult InvalidStoragePathResponse()
    {
        return BadRequest(new
        {
            success = false,
            error = new
            {
                code = "InvalidStoragePath",
                message = "Invalid enhancement image source."
            }
        });
    }

    /// <summary>
    /// In local development, uploads can point at Azurite via 127.0.0.1:10000.
    /// When the API runs in a different network context (container/WSL), that host may be unreachable.
    /// Rewrite such URLs to go through our own proxy endpoint so the server can always fetch them.
    /// </summary>
    private async Task<string> NormalizeImageUrlForServerAccessAsync(string originalUrl)
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
                _logger.LogDebug("Rewriting Azurite URL {Original} -> {Proxied}", S(originalUrl), S(proxied));
                return proxied;
            }

            // Production Azure Blob: if container is private (no SAS) generate a short-lived SAS URL
            // Pattern: https://{account}.blob.core.windows.net/{container}/{blobPath}
            var isAzureBlob = uri.Host.Contains(".blob.core.windows.net", StringComparison.OrdinalIgnoreCase);
            var hasSas = !string.IsNullOrEmpty(uri.Query) && uri.Query.Contains("sig=", StringComparison.OrdinalIgnoreCase);
            if (isAzureBlob && !hasSas)
            {
                var path = uri.AbsolutePath.Trim('/');
                var segments = path.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 2)
                {
                    var container = segments[0];
                    var blobPath = segments[1];
                    var containerAndPath = $"{container}/{blobPath}";
                    try
                    {
                        var sasUrl = await _storageService.GenerateSasUrlAsync(containerAndPath, TimeSpan.FromMinutes(5));
                        if (!string.IsNullOrEmpty(sasUrl))
                        {
                            _logger.LogDebug("Generated SAS URL for Azure blob (container={Container}, path={Path})", S(container), S(blobPath));
                            return sasUrl;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to generate SAS URL for blob (container={Container}, path={Path})", S(container), S(blobPath));
                    }
                }
            }

            return originalUrl;
        }
        catch
        {
            // If parsing fails, just return original URL
            return originalUrl;
        }
    }

    private async Task HandleEnhancementRefundAsync(string userId, CreditConsumptionResult? consumptionResult, string scenario, string details)
    {
        var creditsToRefund = consumptionResult?.TotalCreditsConsumed ?? 0;
        var refundSuccess = await _basicTierService.RefundCreditsAsync(userId, consumptionResult);

        if (creditsToRefund > 0)
        {
            await _basicTierService.LogUsageAsync(userId, "openai_enhancement_refund",
                $"Refund for {scenario}: {details}", creditsCost: -creditsToRefund);
        }

        if (refundSuccess)
        {
            _logger.LogInformation("Successfully refunded {Credits} credits to user {UserId} for OpenAI {Scenario}", creditsToRefund, Sid(userId), scenario);
        }
        else
        {
            _logger.LogError("Failed to refund credits to user {UserId} for OpenAI {Scenario}", Sid(userId), scenario);
        }
    }
}
