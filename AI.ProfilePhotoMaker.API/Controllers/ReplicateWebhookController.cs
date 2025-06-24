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

    public ReplicateWebhookController(
        ILogger<ReplicateWebhookController> logger,
        ApplicationDbContext dbContext,
        IReplicateApiClient replicateApiClient)
    {
        _logger = logger;
        _dbContext = dbContext;
        _replicateApiClient = replicateApiClient;
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
            
            if (!string.IsNullOrEmpty(payload.Version))
            {
                // Extract base model name from version (before the colon)
                var baseModelName = payload.Version.Contains(':')
                    ? payload.Version.Split(':')[0]
                    : payload.Version;
                
                modelRequest = await _dbContext.ModelCreationRequests
                    .FirstOrDefaultAsync(r => r.ReplicateModelId == baseModelName);
            }

            if (modelRequest != null && payload.IsCompleted && !payload.HasFailed && !string.IsNullOrEmpty(payload.Version))
            {
                // Store the trained model version (the full version string with hash)
                modelRequest.TrainedModelVersion = payload.Version;
                _logger.LogInformation("Training completed for model {ModelId}, version: {Version}",
                    modelRequest.ReplicateModelId, payload.Version);

                // Also update the user profile if it exists
                var userProfile = await _dbContext.UserProfiles.FirstOrDefaultAsync(u => u.UserId == modelRequest.UserId);
                if (userProfile != null)
                {
                    userProfile.TrainedModelId = payload.Version;
                    userProfile.ModelTrainedAt = DateTime.UtcNow;
                    userProfile.UpdatedAt = DateTime.UtcNow;

                    // If user has a selected style, start generation automatically
                    if (userProfile.StyleId.HasValue)
                    {
                        var style = await _dbContext.Styles.FindAsync(userProfile.StyleId.Value);
                        if (style != null && style.IsActive)
                        {
                            _logger.LogInformation("Starting automatic image generation for user {UserId} with style {StyleName}",
                                modelRequest.UserId, style.Name);
                            await _replicateApiClient.GenerateImagesAsync(payload.Version, userProfile.UserId, style.Name, null);
                        }
                    }
                }

                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Successfully processed training completion for model {ModelId}", modelRequest.ReplicateModelId);
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
                _logger.LogWarning("Could not find matching ModelCreationRequest for training completion: {@Payload}", payload);
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

                var imageUrl = payload.GeneratedImageUrls.First();
                using var httpClient = new HttpClient();
                
                try
                {
                    var response = await httpClient.GetAsync(imageUrl);
                    response.EnsureSuccessStatusCode();
                    var contentType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? "image/jpeg";
                    var imageBytes = await response.Content.ReadAsByteArrayAsync();
                    var base64 = Convert.ToBase64String(imageBytes);
                    var dataUrl = $"data:{contentType};base64,{base64}";

                    // Save the generated image to the database
                    var processedImage = new ProcessedImage
                    {
                        UserProfileId = userProfile.Id,
                        OriginalImageUrl = imageUrl, // Store the Replicate URL as original
                        ProcessedImageUrl = imageUrl, // For generated images, both URLs are the same
                        Style = style ?? "Unknown",
                        IsGenerated = true,
                        IsOriginalUpload = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    _dbContext.ProcessedImages.Add(processedImage);
                    await _dbContext.SaveChangesAsync();

                    _logger.LogInformation("Successfully saved generated image for user {UserId} with style {Style}, image ID: {ImageId}", 
                        userId, style, processedImage.Id);

                    return Ok(new { success = true, dataUrl, imageId = processedImage.Id });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch or convert image for data URL: {ImageUrl}", imageUrl);
                    return StatusCode(500, new { success = false, error = "Failed to fetch or convert image." });
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
