using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Filters;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.Replicate;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services.Storage;
using AI.ProfilePhotoMaker.API.Hubs;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;

namespace AI.ProfilePhotoMaker.API.Controllers;

[Route("api/webhooks/replicate")]
[ApiController]
[AllowAnonymous] // Webhooks are called by Replicate, not users
public class ReplicateWebhookController : ControllerBase
{
    private readonly ILogger<ReplicateWebhookController> _logger;
    private readonly ApplicationDbContext _dbContext;
    private readonly IReplicateApiClient _replicateApiClient;
    private readonly IImageDownloadService _imageDownloadService;
    private readonly IStorageService _storageService;
    private readonly IHubContext<PredictionHub> _hubContext;
    private readonly IEmailNotificationService _emailNotificationService;

    private static string S(string? value) => LoggingSanitizer.Sanitize(value);
    private static string Sid(string? value) => LoggingSanitizer.SanitizeId(value);

    public ReplicateWebhookController(
        ILogger<ReplicateWebhookController> logger,
        ApplicationDbContext dbContext,
        IReplicateApiClient replicateApiClient,
        IImageDownloadService imageDownloadService,
        IStorageService storageService,
        IHubContext<PredictionHub> hubContext,
        IEmailNotificationService emailNotificationService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _replicateApiClient = replicateApiClient;
        _imageDownloadService = imageDownloadService;
        _storageService = storageService;
        _hubContext = hubContext;
        _emailNotificationService = emailNotificationService;
    }

    // Training webhook endpoint removed - now using polling mechanism

    /// <summary>
    /// Webhook endpoint for Replicate prediction (image generation) completion
    /// </summary>
    [HttpPost("prediction-complete")]
    [ReplicateSignatureValidation]
    public async Task<IActionResult> PredictionComplete([FromBody] ReplicatePredictionResult payload)
    {
        if (payload == null)
        {
            _logger.LogWarning("Prediction webhook received null payload");
            return BadRequest(new { success = false, error = "Invalid payload" });
        }

        var safePayload = payload;

        _logger.LogInformation(
            "Processing prediction completion webhook: id={PredictionId}, status={Status}, hasOutput={HasOutput}",
            Sid(safePayload.Id),
            S(safePayload.Status),
            safePayload.GeneratedImageUrls?.Any() == true);

        try
        {
            // Extract user_id and style from payload.Input safely
            string? userId = null;
            string? style = null;
            if (safePayload.Input is null)
            {
                _logger.LogWarning("Webhook payload.Input is null");
            }
            else
            {
                var dictionary = safePayload.Input!;
                if (dictionary.TryGetValue("user_id", out var userIdObj))
                    userId = userIdObj?.ToString();
                if (dictionary.TryGetValue("style", out var styleObj))
                    style = styleObj?.ToString();

                // Debug logging for webhook payload
                _logger.LogInformation("Webhook input contains user_id: {UserId}, style: {Style}", Sid(userId), S(style));
                _logger.LogInformation("Webhook Status: {Status}, IsCompleted: {IsCompleted}, HasFailed: {HasFailed}, HasOutput: {HasOutput}",
                    S(safePayload.Status), safePayload.IsCompleted, safePayload.HasFailed, safePayload.GeneratedImageUrls?.Any() == true);
            }

            // Only process if completed and not failed and has output
            if (safePayload.IsCompleted && !safePayload.HasFailed && safePayload.GeneratedImageUrls?.Any() == true && !string.IsNullOrEmpty(userId))
            {
                // Find the user profile
                var userProfile = await _dbContext.UserProfiles.FirstOrDefaultAsync(up => up.UserId == userId);
                if (userProfile == null)
                {
                    _logger.LogWarning("User profile not found for userId: {UserId}", Sid(userId));
                    return Ok(new { success = true, message = "User profile not found" });
                }

                var imageUrls = safePayload.GeneratedImageUrls.ToList();

                // Filter out invalid URLs (like "predict on this model is a no-op")
                var validImageUrls = imageUrls
                    .Where(url => !string.IsNullOrEmpty(url) &&
                                  Uri.IsWellFormedUriString(url, UriKind.Absolute) &&
                                  !url.Contains("no-op"))
                    .ToList();

                if (!validImageUrls.Any())
                {
                    _logger.LogWarning(
                        "No valid image URLs found in webhook payload for user {UserId}. URLs: {Urls}",
                        Sid(userId),
                        S(string.Join(", ", imageUrls)));
                    return Ok(new { success = true, message = "No valid image URLs to process" });
                }

                _logger.LogInformation("Downloading {Count} valid generated images for user {UserId}, style {Style}",
                    validImageUrls.Count, Sid(userId), S(style));

                try
                {
                    // Download all generated images to local storage with filename tracking
                    var downloadResults = await _imageDownloadService.DownloadImagesWithDetailsAsync(
                        validImageUrls,
                        userId,
                        style ?? "Unknown");

                    var savedImageIds = new List<int>();

                    // Save each downloaded image to the database
                    for (int i = 0; i < validImageUrls.Count; i++)
                    {
                        var replicateUrl = validImageUrls[i];
                        var downloadResult = i < downloadResults.Count ? downloadResults[i] : null;
                        var storagePath = downloadResult?.Success == true ? downloadResult.StoragePath : null;
                        var actualFileName = downloadResult?.Success == true ? downloadResult.FileName : null;

                        // Generate public URL using storage service
                        string? publicUrl = null;
                        if (!string.IsNullOrEmpty(storagePath))
                        {
                            publicUrl = _storageService.GetImageUrl(storagePath);
                        }

                        _logger.LogInformation("Downloaded image {Index}, success: {DownloadSuccess}, storage path: {StoragePath}",
                            i + 1, downloadResult?.Success, S(storagePath));

                        // Skip saving to database if we don't have a valid URL (prevents unique constraint violations)
                        if (string.IsNullOrEmpty(publicUrl) && string.IsNullOrEmpty(replicateUrl))
                        {
                            _logger.LogWarning("Skipping database save for image {Index} - no valid URLs available", i + 1);
                            continue;
                        }

                        var processedImage = new ProcessedImage
                        {
                            UserProfileId = userProfile.Id,
                            OriginalImageUrl = publicUrl ?? replicateUrl, // Use storage URL if download succeeded, fallback to Replicate URL
                            ProcessedImageUrl = publicUrl ?? replicateUrl, // Use same logic for both URLs to prevent empty strings
                            Style = style ?? "Unknown",
                            IsGenerated = true,
                            IsOriginalUpload = false,
                            CreatedAt = DateTime.UtcNow
                            // Both URLs will use local path when download succeeds, ensuring consistent local serving
                        };

                        // Set scheduled deletion date based on retention policy (7 days for generated images)
                        processedImage.SetScheduledDeletionDate();

                        _dbContext.ProcessedImages.Add(processedImage);
                        await _dbContext.SaveChangesAsync();

                        savedImageIds.Add(processedImage.Id);

                        _logger.LogInformation(
                            "Saved generated image {Index} for user {UserId}: ID={ImageId}, StoragePath={StoragePath}, PublicUrl={PublicUrl}",
                            i + 1,
                            Sid(userId),
                            processedImage.Id,
                            S(storagePath ?? "None"),
                            S(publicUrl ?? replicateUrl));
                    }

                    _logger.LogInformation(
                        "Successfully processed {Count} generated images for user {UserId}, style {Style}. Image IDs: {ImageIds}",
                        validImageUrls.Count,
                        Sid(userId),
                        S(style),
                        S(string.Join(", ", savedImageIds)));

                    // Update Predictions table with completion status (enables local status checking)
                    try
                    {
                        var prediction = await _dbContext.Predictions.FirstOrDefaultAsync(p => p.Id == safePayload.Id);
                        if (prediction != null)
                        {
                            _logger.LogInformation("Prediction {PredictionId} completed successfully for user {UserId}", Sid(safePayload.Id), Sid(userId));
                            // Note: Full completion tracking would require adding CompletedAt, Status fields via migration
                        }
                        else
                        {
                            _logger.LogWarning("Prediction {PredictionId} not found in local database for completion update", Sid(safePayload.Id));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to update prediction completion status for {PredictionId}", Sid(safePayload.Id));
                    }

                    // Send real-time notification to user (eliminates need for polling)
                    try
                    {
                        await _hubContext.Clients.Group($"user_{userId}")
                            .SendAsync("PredictionCompleted", new
                            {
                                predictionId = safePayload.Id,
                                status = "succeeded",
                                imageCount = savedImageIds.Count,
                                imageIds = savedImageIds,
                                style = style
                            });

                        await _hubContext.Clients.Group($"prediction_{safePayload.Id}")
                            .SendAsync("PredictionUpdated", new
                            {
                                predictionId = safePayload.Id,
                                status = "succeeded",
                                completedAt = DateTime.UtcNow
                            });

                        _logger.LogDebug("Sent real-time completion notification for prediction {PredictionId} to user {UserId}", Sid(safePayload.Id), Sid(userId));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send real-time notification for prediction {PredictionId}", Sid(safePayload.Id));
                    }

                    // Best-effort email notification
                    try
                    {
                        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
                        await _emailNotificationService.SendGenerationCompletedAsync(userId, user?.Email, style, savedImageIds.Count);
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogWarning(emailEx, "Failed to send generation completion email for user {UserId}", Sid(userId));
                    }

                    return Ok(new
                    {
                        success = true,
                        message = $"Processed {savedImageIds.Count} images",
                        imageIds = savedImageIds,
                        downloadedCount = downloadResults.Count(r => r.Success)
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to download and save generated images for user {UserId}, style {Style}", Sid(userId), S(style));
                    return StatusCode(500, new { success = false, error = "Failed to download and save generated images." });
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(userId) && safePayload.HasFailed)
                {
                    try
                    {
                        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
                        await _emailNotificationService.SendGenerationFailedAsync(userId, user?.Email, style, safePayload.Error ?? safePayload.Status);
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogWarning(emailEx, "Failed to send generation failure email for user {UserId}", Sid(userId));
                    }
                }

                _logger.LogWarning("Prediction webhook ignored - not completed, failed, no output, or missing userId: {@Payload}", payload);
            }

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing prediction completion webhook");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

}
