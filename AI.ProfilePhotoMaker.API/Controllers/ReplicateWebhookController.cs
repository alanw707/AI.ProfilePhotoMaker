using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Filters;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.Replicate;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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
        var userProfile = _dbContext.UserProfiles.FirstOrDefault(u => u.UserId == userId);
        if (userProfile != null && payload.IsCompleted && !payload.HasFailed && payload.GeneratedImageUrls.Any())
        {
            foreach (var imageUrl in payload.GeneratedImageUrls)
            {
                _dbContext.ProcessedImages.Add(new ProcessedImage
                {
                    OriginalImageUrl = string.Empty, // Optionally store original if available
                    ProcessedImageUrl = imageUrl,
                    Style = style ?? "Unknown",
                    UserProfileId = userProfile.Id,
                    CreatedAt = DateTime.UtcNow
                });
            }
            userProfile.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }
        _logger.LogInformation("Handled Replicate prediction-complete webhook: {@Payload}", payload);
        return Ok(new { success = true });
    }

}
