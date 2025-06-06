using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.Replicate;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace AI.ProfilePhotoMaker.API.Controllers;

[Route("api/webhooks/replicate")]
[ApiController]
[AllowAnonymous] // Webhooks are called by Replicate, not users
public class ReplicateWebhookController : ControllerBase
{
    private readonly ILogger<ReplicateWebhookController> _logger;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _dbContext;
    private readonly IReplicateApiClient _replicateApiClient;

    public ReplicateWebhookController(
        ILogger<ReplicateWebhookController> logger,
        IConfiguration configuration,
        ApplicationDbContext dbContext,
        IReplicateApiClient replicateApiClient)
    {
        _logger = logger;
        _configuration = configuration;
        _dbContext = dbContext;
        _replicateApiClient = replicateApiClient;
    }

    /// <summary>
    /// Webhook endpoint for Replicate training completion
    /// </summary>
    [HttpPost("training-complete")]
    public async Task<IActionResult> TrainingComplete([FromBody] ReplicateTrainingResult payload)
    {
        if (!VerifySignature(Request))
        {
            _logger.LogWarning("Invalid Replicate webhook signature for training-complete");
            return Unauthorized();
        }
        // Update model status in DB (pseudo-code, adjust as needed)
        // Extract user_id from payload.Input safely
        string? userId = null;
        if (payload.Input != null && payload.Input.TryGetValue("user_id", out var userIdObj))
        {
            userId = userIdObj?.ToString();
        }
        var userProfile = _dbContext.UserProfiles.FirstOrDefault(u => u.UserId == userId);
        if (userProfile != null && payload.IsCompleted && !payload.HasFailed && !string.IsNullOrEmpty(payload.Version))
        {
            // For each style the user selected (pseudo-code, replace with your actual logic)
            var selectedStyles = new List<string> { "Professional" }; // TODO: fetch from user profile or related table
            foreach (var style in selectedStyles)
            {
                await _replicateApiClient.GenerateImagesAsync(payload.Version, userProfile.UserId, style, null);
            }
            userProfile.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }
        _logger.LogInformation("Handled Replicate training-complete webhook: {@Payload}", payload);
        return Ok(new { success = true });
    }

    /// <summary>
    /// Webhook endpoint for Replicate prediction (image generation) completion
    /// </summary>
    [HttpPost("prediction-complete")]
    public async Task<IActionResult> PredictionComplete([FromBody] ReplicatePredictionResult payload)
    {
        if (!VerifySignature(Request))
        {
            _logger.LogWarning("Invalid Replicate webhook signature for prediction-complete");
            return Unauthorized();
        }
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

    // Example signature verification (customize as needed for Replicate's webhook security)
    private bool VerifySignature(HttpRequest request)
    {
        // Example: Replicate may send a signature header (e.g., X-Replicate-Signature)
        var signatureHeader = request.Headers["X-Replicate-Signature"].FirstOrDefault();
        var secret = _configuration["Replicate:WebhookSecret"];
        if (string.IsNullOrEmpty(signatureHeader) || string.IsNullOrEmpty(secret))
            return false;
        // Compute HMAC of the request body
        request.Body.Position = 0;
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = reader.ReadToEnd();
        var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        var computedSignature = BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();
        return string.Equals(signatureHeader, computedSignature, StringComparison.OrdinalIgnoreCase);
    }
}
