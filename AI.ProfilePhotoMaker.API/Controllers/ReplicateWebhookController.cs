using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Filters;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.Replicate;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
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

    public ReplicateWebhookController(
        ILogger<ReplicateWebhookController> logger,
        ApplicationDbContext dbContext,
        IReplicateApiClient replicateApiClient,
        IImageDownloadService imageDownloadService,
        IStorageService storageService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _replicateApiClient = replicateApiClient;
        _imageDownloadService = imageDownloadService;
        _storageService = storageService;
    }

    // Training webhook endpoint removed - now using polling mechanism

    /// <summary>
    /// Webhook endpoint for Replicate prediction (image generation) completion
    /// </summary>
    [HttpPost("prediction-complete")]
    [ReplicateSignatureValidation]
    public async Task<IActionResult> PredictionComplete([FromBody] ReplicatePredictionResult payload)
    {
        _logger.LogInformation("Processing prediction completion webhook: {@Payload}", payload);

        try
        {
            // Extract user_id and style from payload.Input safely
            string? userId = null;
            string? style = null;
            if (payload.Input != null)
            {
                if (payload.Input.TryGetValue("user_id", out var userIdObj))
                    userId = userIdObj?.ToString();
                if (payload.Input.TryGetValue("style", out var styleObj))
                    style = styleObj?.ToString();

                // Debug logging for webhook payload
                _logger.LogInformation("Webhook Input contains user_id: {UserId}, style: {Style}", userId ?? "NULL", style ?? "NULL");
                _logger.LogInformation("Webhook Status: {Status}, IsCompleted: {IsCompleted}, HasFailed: {HasFailed}, HasOutput: {HasOutput}",
                    payload.Status, payload.IsCompleted, payload.HasFailed, payload.GeneratedImageUrls.Any());
            }
            else
            {
                _logger.LogWarning("Webhook payload.Input is null");
            }

            // Only process if completed and not failed and has output
            if (payload.IsCompleted && !payload.HasFailed && payload.GeneratedImageUrls.Any() && !string.IsNullOrEmpty(userId))
            {
                // Find the user profile
                var userProfile = await _dbContext.UserProfiles.FirstOrDefaultAsync(up => up.UserId == userId);
                if (userProfile == null)
                {
                    _logger.LogWarning("User profile not found for userId: {UserId}", userId);
                    return Ok(new { success = true, message = "User profile not found" });
                }

                var imageUrls = payload.GeneratedImageUrls.ToList();
                _logger.LogInformation("Downloading {Count} generated images for user {UserId}, style {Style}",
                    imageUrls.Count, userId, style);

                try
                {
                    // Download all generated images to local storage with filename tracking
                    var downloadResults = await _imageDownloadService.DownloadImagesWithDetailsAsync(
                        imageUrls,
                        userId,
                        style ?? "Unknown");

                    var savedImageIds = new List<int>();

                    // Save each downloaded image to the database
                    for (int i = 0; i < imageUrls.Count; i++)
                    {
                        var replicateUrl = imageUrls[i];
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
                            i + 1, downloadResult?.Success, storagePath);

                        var processedImage = new ProcessedImage
                        {
                            UserProfileId = userProfile.Id,
                            OriginalImageUrl = publicUrl ?? replicateUrl, // Use storage URL if download succeeded, fallback to Replicate URL
                            ProcessedImageUrl = publicUrl ?? string.Empty, // Only set if download was successful; otherwise empty
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

                        _logger.LogInformation("Saved generated image {Index} for user {UserId}: ID={ImageId}, StoragePath={StoragePath}, PublicUrl={PublicUrl}",
                            i + 1, userId, processedImage.Id, storagePath ?? "None", publicUrl ?? replicateUrl);
                    }

                    _logger.LogInformation("Successfully processed {Count} generated images for user {UserId}, style {Style}. Image IDs: {ImageIds}",
                        imageUrls.Count, userId, style, string.Join(", ", savedImageIds));

                    return Ok(new
                    {
                        success = true,
                        message = $"Processed {imageUrls.Count} images",
                        imageIds = savedImageIds,
                        downloadedCount = downloadResults.Count(r => r.Success)
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to download and save generated images for user {UserId}, style {Style}", userId, style);
                    return StatusCode(500, new { success = false, error = "Failed to download and save generated images." });
                }
            }
            else
            {
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
