using System.Security.Claims;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AI.ProfilePhotoMaker.API.Controllers;

[ApiController]
[Authorize]
[Route("api/headshots")]
public class HeadshotsController : ControllerBase
{
    private readonly IHeadshotGenerationService _headshotGenerationService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITurnstileVerificationService _turnstile;
    private readonly IOutcomePackageService _outcomePackageService;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<HeadshotsController> _logger;

    private static int _temporaryFailureTelemetryCaptured;

    private static string S(string? value) => LoggingSanitizer.Sanitize(value);
    private static string Sid(string? value) => LoggingSanitizer.SanitizeId(value);

    public HeadshotsController(
        IHeadshotGenerationService headshotGenerationService,
        UserManager<ApplicationUser> userManager,
        ITurnstileVerificationService turnstile,
        IOutcomePackageService outcomePackageService,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<HeadshotsController> logger)
    {
        _headshotGenerationService = headshotGenerationService;
        _userManager = userManager;
        _turnstile = turnstile;
        _outcomePackageService = outcomePackageService;
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

        var preview = await _outcomePackageService.GetResumablePreviewAsync(
            userId,
            previewId,
            HttpContext.RequestAborted);
        return Ok(new { success = true, data = preview, error = (object?)null });
    }

    [HttpDelete("resumable-preview/{previewId:int}")]
    public async Task<IActionResult> AbandonPreview(int previewId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var abandoned = await _outcomePackageService.AbandonPreviewAsync(userId, previewId, HttpContext.RequestAborted);
        return abandoned ? Ok(new { success = true }) : NotFound(new { success = false });
    }

    [HttpGet("images/{imageId:int}/original")]
    public async Task<IActionResult> DownloadOriginal(int imageId)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var download = await _outcomePackageService.GetPromotedPreviewDownloadAsync(
            userId,
            imageId,
            HttpContext.RequestAborted);
        return download == null
            ? NotFound()
            : File(download.Content, "image/png", $"profile-photo-{download.ImageId}.png");
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
                "ProviderAuthenticationFailed" => 503,
                "ProviderTimeout" => 408,
                "ProviderNetworkError" => 502,
                _ => 503
            };

            await CaptureTemporaryFailureTelemetryAsync(request, status, ex.Code, userId);
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

    private async Task CaptureTemporaryFailureTelemetryAsync(
        HeadshotGenerationRequestDto request,
        int status,
        string failureCode,
        string userId)
    {
        if (status != StatusCodes.Status400BadRequest ||
            !_configuration.GetValue<bool>("Diagnostics:CaptureHeadshotFailure") ||
            Interlocked.Exchange(ref _temporaryFailureTelemetryCaptured, 1) != 0)
        {
            return;
        }

        var packageCode = request.PackageCode?.Trim().ToLowerInvariant() is "starter_package" or "pro_package" or "free_preview"
            ? request.PackageCode.Trim().ToLowerInvariant()
            : "other";
        UserPackageEntitlement? entitlement = null;
        try
        {
            if (packageCode is "starter_package" or "pro_package")
            {
                entitlement = await _outcomePackageService.GetActiveEntitlementAsync(
                    userId,
                    packageCode,
                    HttpContext.RequestAborted);
            }
        }
        catch (Exception telemetryException)
        {
            _logger.LogWarning(telemetryException, "Temporary headshot failure telemetry could not read entitlement state");
        }

        var correlationId = Guid.NewGuid().ToString("N");
        HttpContext.Response.Headers.Append("X-Headshot-Failure-Correlation", correlationId);
        _logger.LogWarning(
            "SAFE_HEADSHOT_FAILURE Correlation={Correlation} Status={Status} Code={Code} Package={Package} Outputs={Outputs} EntitlementPresent={EntitlementPresent} RemainingCandidates={RemainingCandidates} SourcePathSupplied={SourcePathSupplied} IsRegeneration={IsRegeneration}",
            correlationId,
            status,
            S(failureCode),
            packageCode,
            Math.Clamp(request.NumOutputs, 1, 9),
            entitlement != null,
            entitlement?.RemainingCandidates,
            !string.IsNullOrWhiteSpace(request.ImageStoragePath),
            request.IsRegeneration);
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
        "ProviderAuthenticationFailed" => "Headshot generation is not configured correctly. Please contact support.",
        "ProviderTimeout" => "Headshot generation is taking longer than expected. Please try again.",
        "ProviderNetworkError" => "Headshot generation service is temporarily unreachable. Please try again later.",
        _ => "Headshot generation is temporarily unavailable. Please try again later."
    };
}
