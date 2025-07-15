using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Filters;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.Replicate;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
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

    public ReplicateWebhookController(
        ILogger<ReplicateWebhookController> logger,
        ApplicationDbContext dbContext,
        IReplicateApiClient replicateApiClient,
        IImageDownloadService imageDownloadService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _replicateApiClient = replicateApiClient;
        _imageDownloadService = imageDownloadService;
    }

    /// <summary>
    /// Webhook endpoint for Replicate training completion
    /// </summary>
    [HttpPost("training-complete")]
    [ReplicateSignatureValidation]
    public async Task<IActionResult> TrainingComplete([FromBody] ReplicateTrainingResult payload)
    {

        _logger.LogInformation("Processing training completion webhook: {@Payload}", payload);

        try
        {
            // Find the ModelCreationRequest by looking for the model that was trained
            ModelCreationRequest? modelRequest = null;
            UserProfile? userProfile = null;

            if (!string.IsNullOrEmpty(payload.Version))
            {
                // Extract base model name from version (before the colon)
                var baseModelName = payload.Version.Contains(':')
                    ? payload.Version.Split(':')[0]
                    : payload.Version;

                modelRequest = await _dbContext.ModelCreationRequests
                    .FirstOrDefaultAsync(r => r.ReplicateModelId == baseModelName);

                // If no ModelCreationRequest found, try to extract user ID from model name
                if (modelRequest == null && baseModelName.StartsWith("user-"))
                {
                    // Extract user ID from model name pattern: user-{userId}-{timestamp}
                    var parts = baseModelName.Split('-');
                    if (parts.Length >= 2)
                    {
                        var userId = parts[1]; // Extract userId from "user-{userId}-timestamp"
                        userProfile = await _dbContext.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);
                        _logger.LogInformation("Found user profile by extracted userId {UserId} from model name {ModelName}", userId, baseModelName);
                    }
                }
                else if (modelRequest != null)
                {
                    userProfile = await _dbContext.UserProfiles.FirstOrDefaultAsync(u => u.UserId == modelRequest.UserId);
                }
            }

            if (payload.IsCompleted && !payload.HasFailed && !string.IsNullOrEmpty(payload.Version))
            {
                bool updatedSuccessfully = false;

                // Update ModelCreationRequest with training completion data
                if (modelRequest != null)
                {
                    // Extract model name and version ID from payload.Version
                    string modelName;
                    string versionId;

                    if (payload.Version.Contains(':'))
                    {
                        var parts = payload.Version.Split(':', 2);
                        modelName = parts[0]; // e.g., "alanw707/user-b99678bd-cb87-40c1-a7bf-b889f1e00c08-20250624130213"
                        versionId = parts[1]; // e.g., "787e9b51e9a943dca35ea5be25d62c10db35af6d43e0b15336a36682c75bc024"
                    }
                    else
                    {
                        // If no colon, assume payload.Version is just the version ID
                        modelName = payload.Version;
                        versionId = payload.Version;
                    }

                    // Update ModelCreationRequest as single source of truth
                    modelRequest.TrainedModelVersion = versionId;
                    modelRequest.ReplicateModelId = modelName; // Ensure model ID is set correctly
                    modelRequest.Status = ModelCreationStatus.Ready;
                    modelRequest.CompletedAt = DateTime.UtcNow;

                    _logger.LogInformation("Training completed for model {ModelId}, version: {Version}",
                        modelName, versionId);

                    // If user has selected styles, start generation automatically for all selected styles
                    if (userProfile != null)
                    {
                        var selectedStyles = await _dbContext.UserStyleSelections
                            .Include(uss => uss.Style)
                            .Where(uss => uss.UserProfileId == userProfile.Id && uss.Style.IsActive)
                            .ToListAsync();

                        if (selectedStyles.Any())
                        {
                            _logger.LogInformation("Starting automatic image generation for user {UserId} with {StyleCount} selected styles",
                                userProfile.UserId, selectedStyles.Count);

                            foreach (var selectedStyle in selectedStyles)
                            {
                                await _replicateApiClient.GenerateImagesAsync(versionId, userProfile.UserId, selectedStyle.Style.Name, null);
                            }
                        }

                        // Update UserProfile timestamp to track activity
                        userProfile.UpdatedAt = DateTime.UtcNow;
                    }

                    updatedSuccessfully = true;
                }

                if (updatedSuccessfully)
                {
                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation("Successfully processed training completion for version {Version}", payload.Version);
                }
                else
                {
                    _logger.LogError("Could not find user or model request for training completion: {@Payload}", payload);
                }
            }
            else if (payload.HasFailed)
            {
                _logger.LogError("Training failed for payload: {@Payload}", payload);
                // Optionally update the model request status to failed
                if (modelRequest != null)
                {
                    modelRequest.ErrorMessage = $"Training failed: {payload.Error}";
                    await _dbContext.SaveChangesAsync();
                }
            }
            else
            {
                _logger.LogWarning("Training webhook received but not completed or failed: {@Payload}", payload);
            }

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing training completion webhook");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

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
                        var localPath = downloadResult?.Success == true ? downloadResult.LocalPath : null;
                        var actualFileName = downloadResult?.Success == true ? downloadResult.FileName : null;

                        // Convert local path to public URL path
                        string? publicUrl = null;
                        if (!string.IsNullOrEmpty(localPath))
                        {
                            // Convert local path to relative URL path for serving
                            var relativePath = Path.GetRelativePath(Path.Combine(Directory.GetCurrentDirectory(), "generated"), localPath);
                            publicUrl = $"/generated/{relativePath.Replace('\\', '/')}";
                        }

                        _logger.LogInformation("Downloaded image {Index}, success: {DownloadSuccess}, local path: {LocalPath}",
                            i + 1, downloadResult?.Success, localPath);

                        var processedImage = new ProcessedImage
                        {
                            UserProfileId = userProfile.Id,
                            OriginalImageUrl = publicUrl ?? replicateUrl, // Use local URL if download succeeded, fallback to Replicate URL
                            ProcessedImageUrl = publicUrl, // Only set if download was successful (null if failed)
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

                        _logger.LogInformation("Saved generated image {Index} for user {UserId}: ID={ImageId}, LocalPath={LocalPath}, PublicUrl={PublicUrl}",
                            i + 1, userId, processedImage.Id, localPath ?? "None", publicUrl ?? replicateUrl);
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
