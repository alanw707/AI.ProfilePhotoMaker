using System.Security.Claims;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services.Security;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Controllers;

[ApiController]
[Authorize]
[Route("api/headshots")]
public class HeadshotsController : ControllerBase
{
    private readonly IHeadshotGenerationService _headshotGenerationService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITurnstileVerificationService _turnstile;
    private readonly ApplicationDbContext _dbContext;
    private readonly IStorageService _storageService;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<HeadshotsController> _logger;

    private static string S(string? value) => LoggingSanitizer.Sanitize(value);
    private static string Sid(string? value) => LoggingSanitizer.SanitizeId(value);

    public HeadshotsController(
        IHeadshotGenerationService headshotGenerationService,
        UserManager<ApplicationUser> userManager,
        ITurnstileVerificationService turnstile,
        ApplicationDbContext dbContext,
        IStorageService storageService,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<HeadshotsController> logger)
    {
        _headshotGenerationService = headshotGenerationService;
        _userManager = userManager;
        _turnstile = turnstile;
        _dbContext = dbContext;
        _storageService = storageService;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet("resumable-preview")]
    public async Task<IActionResult> GetResumablePreview([FromQuery] int? previewId = null)
    {
        if (!IsOpenAIHeadshotMvpEnabled())
        {
            return BadRequest(new { success = false, data = (object?)null, error = new { code = "FeatureDisabled", message = "Instant headshot generation is not enabled." } });
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { success = false, data = (object?)null, error = new { code = "Unauthorized", message = "User authentication required." } });
        }

        var query = _dbContext.ProcessedImages
            .Include(i => i.UserProfile)
            .Where(i =>
                i.UserProfile.UserId == userId &&
                i.GenerationStatus == "succeeded" &&
                (i.GenerationMode == "instant_headshot" || i.GenerationMode == "instant_headshot_promoted_preview" ||
                 i.GenerationMode == "premium_augmentation" || i.GenerationMode == "photo_refinement"));

        // Automatic suggestions remain Free Previews; explicit gallery links can reopen paid work.
        if (!previewId.HasValue)
        {
            query = query.Where(i => i.FailureReason != null && i.FailureReason.StartsWith("raw-preview:"));
        }

        var preview = previewId.HasValue
            ? await query.FirstOrDefaultAsync(i => i.Id == previewId.Value, HttpContext.RequestAborted)
            : await query.OrderByDescending(i => i.CreatedAt).FirstOrDefaultAsync(HttpContext.RequestAborted);

        if (preview == null)
        {
            return Ok(new { success = true, data = (object?)null, error = (object?)null });
        }

        var isPaidCandidate = preview.FailureReason?.StartsWith("raw-preview:", StringComparison.Ordinal) != true;
        var styleActive = await _dbContext.Styles.AnyAsync(s => s.IsActive && s.Name == preview.Style, HttpContext.RequestAborted);
        if ((!isPaidCandidate && !styleActive) || !await StorageImageExistsAsync(preview.ProcessedImageUrl))
        {
            return Ok(new { success = true, data = (object?)null, error = (object?)null });
        }

        var rawPreviewExists = !isPaidCandidate && await StorageImageExistsAsync(preview.FailureReason!["raw-preview:".Length..]);
        var sourceExists = await StorageImageExistsAsync(preview.OriginalImageUrl);

        var entitlement = isPaidCandidate || rawPreviewExists
            ? await _dbContext.UserPackageEntitlements
            .Include(e => e.OutcomePackageDefinition)
            .Where(e => e.UserId == userId &&
                        e.Status == PackageEntitlementStatus.Active &&
                        (e.ExpiresAt == null || e.ExpiresAt > DateTime.UtcNow) &&
                        (e.OutcomePackageDefinition.Code == "starter_package" || e.OutcomePackageDefinition.Code == "pro_package") &&
                        (isPaidCandidate
                            ? e.RemainingRefinements > 0 || e.RemainingPremiumAugmentations > 0 || e.PlatformExportKitAvailable
                            : e.RemainingPackageUses > 0 && e.RemainingCandidates > 0))
            .OrderByDescending(e => e.OutcomePackageDefinition.Code == "pro_package")
            .ThenBy(e => e.ExpiresAt ?? DateTime.MaxValue)
            .ThenBy(e => e.CreatedAt)
            .FirstOrDefaultAsync(HttpContext.RequestAborted)
            : null;

        var totalCandidates = entitlement?.OutcomePackageDefinition.IncludedCandidateCount ?? 0;
        var remainingCandidateCount = isPaidCandidate || entitlement == null ? 0 : Math.Max(totalCandidates - 1, 0);
        var dto = new ResumableHeadshotPreviewDto
        {
            ProcessedImageId = preview.Id,
            ImageUrl = preview.ProcessedImageUrl,
            StoragePath = preview.ProcessedImageUrl,
            SourceStoragePath = preview.OriginalImageUrl,
            Style = preview.Style,
            CreatedAt = preview.CreatedAt,
            HasRawPreview = rawPreviewExists,
            IsPaidCandidate = isPaidCandidate,
            CanPromotePreview = !isPaidCandidate && entitlement != null && rawPreviewExists,
            ActivePackageCode = entitlement?.OutcomePackageDefinition.Code,
            RemainingCandidateCount = remainingCandidateCount,
            Message = isPaidCandidate
                ? "Paid photo restored. Use your remaining package tools or download your saved image."
                : entitlement == null
                ? (rawPreviewExists
                    ? "Resume this preview, then unlock Starter or Pro to generate paid candidates."
                    : "This preview can be viewed, but its generation source expired. Start over to create paid candidates.")
                : (sourceExists ? "Resume this preview and generate the remaining paid candidates." : "Original upload expired; we will continue from the protected raw preview.")
        };

        return Ok(new { success = true, data = dto, error = (object?)null });
    }

    private async Task<bool> StorageImageExistsAsync(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return false;
        }

        try
        {
            await using var stream = await _storageService.GetImageAsync(storagePath);
            return stream != null;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to verify resumable preview storage path {StoragePath}", S(storagePath));
            return false;
        }
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] HeadshotGenerationRequestDto request)
    {
        if (!IsOpenAIHeadshotMvpEnabled())
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "FeatureDisabled", message = "Instant headshot generation is not enabled." }
            });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(new
            {
                success = false,
                error = new
                {
                    code = "InvalidRequest",
                    message = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))
                }
            });
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new
            {
                success = false,
                error = new { code = "Unauthorized", message = "User authentication required." }
            });
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || !user.EmailConfirmed)
        {
            return Unauthorized(new
            {
                success = false,
                error = new { code = "EmailNotVerified", message = "Please verify your email address before generating a headshot." }
            });
        }

        var turnstileOk = await _turnstile.VerifyAsync(request.TurnstileToken, HttpContext.Connection.RemoteIpAddress?.ToString());
        if (!turnstileOk)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "BotVerificationFailed", message = "Bot verification failed. Please try again." }
            });
        }

        try
        {
            var result = await _headshotGenerationService.GenerateHeadshotAsync(
                request,
                userId,
                HttpContext.RequestAborted);

            return Ok(new
            {
                success = true,
                data = result,
                error = (object?)null
            });
        }
        catch (HeadshotGenerationException ex)
        {
            _logger.LogWarning(ex, "Headshot generation rejected for user {UserId}: {Code}", Sid(userId), S(ex.Code));
            var status = ex.Code switch
            {
                "InsufficientCredits" => 400,
                "InvalidImageSource" => 400,
                "StyleUnavailable" => 400,
                "PackageEntitlementRequired" => 402,
                "GenerationInProgress" => 409,
                "ProviderAuthenticationFailed" => 503,
                "ProviderTimeout" => 408,
                "ProviderNetworkError" => 502,
                _ => 503
            };

            return StatusCode(status, new
            {
                success = false,
                error = new { code = ex.Code, message = SafeUserMessage(ex.Code) }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected headshot generation error for user {UserId}", Sid(userId));
            return StatusCode(500, new
            {
                success = false,
                error = new { code = "HeadshotGenerationFailed", message = "Failed to generate headshot. Please try again later." }
            });
        }
    }

    private bool IsOpenAIHeadshotMvpEnabled()
    {
        var configured = _configuration.GetValue<bool?>("Features:OpenAIHeadshotMvp");
        return configured ?? !_environment.IsProduction();
    }

    private static string SafeUserMessage(string code) => code switch
    {
        "InsufficientCredits" => "You do not have enough credits to generate a headshot.",
        "InvalidImageSource" => "Please upload or select a valid image before generating a headshot.",
        "StyleUnavailable" => "That portrait style is no longer available. Choose another style.",
        "PackageEntitlementRequired" => "Unlock or select an active profile photo package before generating these candidates.",
        "GenerationInProgress" => "This headshot request is already being generated. Please wait for it to finish.",
        "ProviderAuthenticationFailed" => "Headshot generation is not configured correctly. Please contact support.",
        "ProviderTimeout" => "Headshot generation is taking longer than expected. Please try again.",
        "ProviderNetworkError" => "Headshot generation service is temporarily unreachable. Please try again later.",
        _ => "Headshot generation is temporarily unavailable. Please try again later."
    };
}
