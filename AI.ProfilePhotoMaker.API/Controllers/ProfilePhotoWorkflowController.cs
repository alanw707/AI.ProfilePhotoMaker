using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Controllers;

[Authorize]
public class ProfilePhotoWorkflowController : BaseController
{
    private readonly IOutcomePackageService _outcomePackageService;
    private readonly IProfilePhotoScoreService _scoreService;
    private readonly IPlatformExportService _platformExportService;
    private readonly IStorageService _storageService;
    private readonly ApplicationDbContext _context;

    public ProfilePhotoWorkflowController(
        IOutcomePackageService outcomePackageService,
        IProfilePhotoScoreService scoreService,
        IPlatformExportService platformExportService,
        IStorageService storageService,
        ApplicationDbContext context,
        ILogger<ProfilePhotoWorkflowController> logger)
        : base(logger, context)
    {
        _outcomePackageService = outcomePackageService;
        _scoreService = scoreService;
        _platformExportService = platformExportService;
        _storageService = storageService;
        _context = context;
    }

    [HttpGet("packages")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPackages(CancellationToken cancellationToken)
    {
        var packages = await _outcomePackageService.GetActivePackageDefinitionsAsync(cancellationToken);
        return SuccessResponse(packages);
    }

    [HttpGet("entitlements")]
    public async Task<IActionResult> GetEntitlements(CancellationToken cancellationToken)
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        var entitlements = await _outcomePackageService.GetUserEntitlementsAsync(GetCurrentUserId()!, cancellationToken);
        return SuccessResponse(entitlements);
    }

    [HttpGet("export-options")]
    [AllowAnonymous]
    public IActionResult GetExportOptions()
    {
        return SuccessResponse(_platformExportService.GetExportOptions());
    }

    [HttpPost("score")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Score([FromForm] IFormFile image, CancellationToken cancellationToken)
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        if (image == null || image.Length == 0)
        {
            return ErrorResponse("ImageRequired", "Upload an image to score.");
        }

        if (image.Length > 7 * 1024 * 1024)
        {
            return ErrorResponse("ImageTooLarge", "Image must be 7MB or smaller.");
        }

        await using var stream = image.OpenReadStream();
        var score = await _scoreService.ScoreAsync(stream, image.FileName, cancellationToken);
        return SuccessResponse(score);
    }

    [HttpGet("score-image/{processedImageId:int}")]
    public async Task<IActionResult> ScoreProcessedImage(int processedImageId, CancellationToken cancellationToken)
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        var userId = GetCurrentUserId()!;
        var image = await _context.ProcessedImages
            .Include(i => i.UserProfile)
            .FirstOrDefaultAsync(i => i.Id == processedImageId && i.UserProfile.UserId == userId, cancellationToken);

        if (image == null)
        {
            return ErrorResponse("ImageNotFound", "The selected image was not found.", 404);
        }

        var storagePath = !string.IsNullOrWhiteSpace(image.ProcessedImageUrl)
            ? image.ProcessedImageUrl
            : image.OriginalImageUrl;

        await using var sourceStream = await _storageService.GetImageAsync(storagePath);
        if (sourceStream == null)
        {
            return ErrorResponse("ImageMissing", "The selected image file is no longer available.", 404);
        }

        var score = await _scoreService.ScoreAsync(sourceStream, $"processed-image-{processedImageId}.jpg", cancellationToken);
        return SuccessResponse(score);
    }

    [HttpPost("export-package")]
    public async Task<IActionResult> CreateExportPackage([FromBody] CreatePlatformExportPackageRequestDto request, CancellationToken cancellationToken)
    {
        var authCheck = ValidateAuthentication();
        if (authCheck != null) return authCheck;

        if (request.ProcessedImageId <= 0)
        {
            return ErrorResponse("ImageRequired", "Choose a processed image to export.");
        }

        var userId = GetCurrentUserId()!;
        var image = await _context.ProcessedImages
            .Include(i => i.UserProfile)
            .FirstOrDefaultAsync(i => i.Id == request.ProcessedImageId && i.UserProfile.UserId == userId, cancellationToken);

        if (image == null)
        {
            return ErrorResponse("ImageNotFound", "The selected image was not found.", 404);
        }

        var storagePath = !string.IsNullOrWhiteSpace(image.ProcessedImageUrl)
            ? image.ProcessedImageUrl
            : image.OriginalImageUrl;

        await using var sourceStream = await _storageService.GetImageAsync(storagePath);
        if (sourceStream == null)
        {
            return ErrorResponse("ImageMissing", "The selected image file is no longer available.", 404);
        }

        if (!await _outcomePackageService.ConsumeExportKitAsync(userId, cancellationToken))
        {
            return ErrorResponse("ExportKitEntitlementRequired", "Unlock a Starter or Pro Package to download a platform export kit.", 402);
        }

        var baseName = $"profile-photo-{image.Id}";
        var adjustments = new PlatformExportAdjustmentOptions
        {
            ZoomPercent = request.ZoomPercent,
            RotateDegrees = request.RotateDegrees,
            BrightnessPercent = request.BrightnessPercent,
            ContrastPercent = request.ContrastPercent,
            SharpnessPercent = request.SharpnessPercent,
            CropOffsetXPercent = request.CropOffsetXPercent,
            CropOffsetYPercent = request.CropOffsetYPercent
        };
        var zipBytes = await _platformExportService.CreateExportPackageAsync(sourceStream, baseName, request.ExportCodes, adjustments, cancellationToken);
        var fileName = $"{baseName}-platform-export-kit.zip";

        return File(zipBytes, "application/zip", fileName);
    }
}
