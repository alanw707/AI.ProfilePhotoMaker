using AI.ProfilePhotoMaker.API.Constants;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AI.ProfilePhotoMaker.API.Controllers;

[ApiController]
[Route("api/model-status")]
[Authorize]
public class ModelStatusController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ModelStatusController> _logger;
    private readonly IReplicateApiClient _replicateApiClient;
    private static string S(string? value) => LoggingSanitizer.Sanitize(value);
    private static string Sid(string? value) => LoggingSanitizer.SanitizeId(value);

    public ModelStatusController(
        ApplicationDbContext context,
        ILogger<ModelStatusController> logger,
        IReplicateApiClient replicateApiClient)
    {
        _context = context;
        _logger = logger;
        _replicateApiClient = replicateApiClient;
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    /// <summary>
    /// Debug endpoint to test model status without authentication
    /// </summary>
    [HttpGet("debug/{userId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDebug(string userId)
    {
        // Load user images count for readiness
        var profile = await _context.UserProfiles
            .Include(p => p.ProcessedImages)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile == null)
        {
            return NotFound("Profile not found");
        }

        var totalUploadedImages = profile.ProcessedImages
            .Where(i => i.Style == ImageConstants.OriginalStyle)
            .Count();

        var canStartTraining = totalUploadedImages >= 10;

        // Model creation requests
        var latestTrainedModel = await _context.ModelCreationRequests
            .Where(m => m.UserId == userId && m.Status == ModelCreationStatus.Ready)
            .OrderByDescending(m => m.CompletedAt)
            .FirstOrDefaultAsync();

        var latestRequest = await _context.ModelCreationRequests
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();

        var generationStatus = await GetLatestGenerationStatusAsync(userId);

        var response = new ModelStatusResponse
        {
            HasTrainedModel = latestTrainedModel != null && !string.IsNullOrEmpty(latestTrainedModel.TrainedModelVersion),
            TrainedModelId = latestTrainedModel?.ReplicateModelId,
            TrainedModelVersion = latestTrainedModel?.TrainedModelVersion,
            TotalUploadedImages = totalUploadedImages,
            CanStartTraining = canStartTraining,
            CurrentRequest = latestRequest == null ? null : new
            {
                id = latestRequest.Id,
                status = latestRequest.Status.ToString().ToLower(),
                trainingRequestId = latestRequest.PendingTrainingRequestId,
                createdAt = latestRequest.CreatedAt,
                completedAt = latestRequest.CompletedAt,
                errorMessage = latestRequest.ErrorMessage
            },
            GenerationStatus = generationStatus
        };

        // Determine unified status code
        var isGenerationInProgress = generationStatus != null &&
            (string.Equals(generationStatus.Status, "queued", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(generationStatus.Status, "processing", StringComparison.OrdinalIgnoreCase));

        if (latestRequest != null && latestRequest.Status == ModelCreationStatus.Failed)
        {
            response.StatusCode = UnifiedModelStatusCode.Failed;
            response.Reason = latestRequest.ErrorMessage ?? "Latest training request failed";
            response.CreditImpact = await GetTrainingCreditImpactAsync(userId, latestRequest);
        }
        else if (latestRequest != null && (latestRequest.Status == ModelCreationStatus.Pending || latestRequest.Status == ModelCreationStatus.Creating))
        {
            response.StatusCode = UnifiedModelStatusCode.Training;
        }
        else if (isGenerationInProgress)
        {
            response.StatusCode = UnifiedModelStatusCode.Generating;
        }
        else if (response.HasTrainedModel)
        {
            response.StatusCode = UnifiedModelStatusCode.ModelReady;
        }
        else if (!canStartTraining)
        {
            response.StatusCode = UnifiedModelStatusCode.NotStarted;
            response.Reason = totalUploadedImages == 0
                ? "No images uploaded"
                : $"Need at least 10 images (currently {totalUploadedImages})";
        }
        else
        {
            response.StatusCode = UnifiedModelStatusCode.ReadyForTraining;
        }

        if (response.GenerationStatus != null &&
            (string.Equals(response.GenerationStatus.Status, "failed", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(response.GenerationStatus.Status, "canceled", StringComparison.OrdinalIgnoreCase)))
        {
            response.CreditImpact = await GetGenerationCreditImpactAsync(userId, response.GenerationStatus);
        }

        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // Load user images count for readiness
            var profile = await _context.UserProfiles
                .Include(p => p.ProcessedImages)
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                return NotFound("Profile not found");
            }

            var totalUploadedImages = profile.ProcessedImages
                .Where(i => i.Style == ImageConstants.OriginalStyle)
                .Count();

            var canStartTraining = totalUploadedImages >= 10;

            // Model creation requests
            var latestTrainedModel = await _context.ModelCreationRequests
                .Where(m => m.UserId == userId && m.Status == ModelCreationStatus.Ready)
                .OrderByDescending(m => m.CompletedAt)
                .FirstOrDefaultAsync();

            var latestRequest = await _context.ModelCreationRequests
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync();

            var generationStatus = await GetLatestGenerationStatusAsync(userId);

            var response = new ModelStatusResponse
            {
                HasTrainedModel = latestTrainedModel != null && !string.IsNullOrEmpty(latestTrainedModel.TrainedModelVersion),
                TrainedModelId = latestTrainedModel?.ReplicateModelId,
                TrainedModelVersion = latestTrainedModel?.TrainedModelVersion,
                TotalUploadedImages = totalUploadedImages,
                CanStartTraining = canStartTraining,
                CurrentRequest = latestRequest == null ? null : new
                {
                    id = latestRequest.Id,
                    status = latestRequest.Status.ToString().ToLower(),
                    trainingRequestId = latestRequest.PendingTrainingRequestId,
                    createdAt = latestRequest.CreatedAt,
                    completedAt = latestRequest.CompletedAt,
                    errorMessage = latestRequest.ErrorMessage
                },
                GenerationStatus = generationStatus
            };

            // Determine unified status code
            var isGenerationInProgress = generationStatus != null &&
                (string.Equals(generationStatus.Status, "queued", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(generationStatus.Status, "processing", StringComparison.OrdinalIgnoreCase));

            if (latestRequest != null && latestRequest.Status == ModelCreationStatus.Failed)
            {
                response.StatusCode = UnifiedModelStatusCode.Failed;
                response.Reason = latestRequest.ErrorMessage ?? "Latest training request failed";
                response.CreditImpact = await GetTrainingCreditImpactAsync(userId, latestRequest);
            }
            else if (latestRequest != null && (latestRequest.Status == ModelCreationStatus.Pending || latestRequest.Status == ModelCreationStatus.Creating))
            {
                response.StatusCode = UnifiedModelStatusCode.Training;
            }
            else if (isGenerationInProgress)
            {
                response.StatusCode = UnifiedModelStatusCode.Generating;
            }
            else if (response.HasTrainedModel)
            {
                response.StatusCode = UnifiedModelStatusCode.ModelReady;
            }
            else if (!canStartTraining)
            {
                response.StatusCode = UnifiedModelStatusCode.NotStarted;
                response.Reason = totalUploadedImages == 0
                    ? "No images uploaded"
                    : $"Need at least 10 images (currently {totalUploadedImages})";
            }
            else
            {
                response.StatusCode = UnifiedModelStatusCode.ReadyForTraining;
            }

            if (response.GenerationStatus != null &&
                (string.Equals(response.GenerationStatus.Status, "failed", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(response.GenerationStatus.Status, "canceled", StringComparison.OrdinalIgnoreCase)))
            {
                response.CreditImpact = await GetGenerationCreditImpactAsync(userId, response.GenerationStatus);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            var userId = LoggingSanitizer.SanitizeId(GetCurrentUserId());
            _logger.LogError(ex, "Failed to compute model status for user {UserId}", userId);
            return StatusCode(500, new
            {
                success = false,
                error = new
                {
                    code = "ModelStatusFailed",
                    message = ex.Message,
                    details = ex.ToString()
                }
            });
        }
    }

    private async Task<GenerationStatusDto?> GetLatestGenerationStatusAsync(string userId)
    {
        var latestPrediction = await _context.Predictions
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();

        PendingGenerationRequest? pendingGeneration = null;
        try
        {
            pendingGeneration = await _context.PendingGenerationRequests
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            // Local/dev environments may run with AutoMigrateOnStartup disabled; avoid 500s if the table isn't present yet.
            _logger.LogWarning(ex, "PendingGenerationRequests table not available; falling back to Predictions only");
            pendingGeneration = null;
        }

        // Prefer a queued background generation job when it is newer than the latest prediction record.
        // This supports the "Continue in Background" flow where generation is queued before predictions exist.
        if (pendingGeneration != null &&
            (latestPrediction == null || pendingGeneration.CreatedAt > latestPrediction.CreatedAt))
        {
            return new GenerationStatusDto
            {
                PredictionId = pendingGeneration.LastPredictionId,
                Status = pendingGeneration.Status switch
                {
                    PendingGenerationStatus.Pending => "queued",
                    PendingGenerationStatus.Started => "processing",
                    PendingGenerationStatus.Failed => "failed",
                    PendingGenerationStatus.Succeeded => "succeeded",
                    _ => "unknown"
                },
                StartedAt = pendingGeneration.StartedAt ?? pendingGeneration.CreatedAt,
                CompletedAt = pendingGeneration.CompletedAt,
                Error = pendingGeneration.ErrorMessage
            };
        }

        if (latestPrediction == null)
        {
            if (pendingGeneration != null)
            {
                return new GenerationStatusDto
                {
                    PredictionId = pendingGeneration.LastPredictionId,
                    Status = pendingGeneration.Status switch
                    {
                        PendingGenerationStatus.Pending => "queued",
                        PendingGenerationStatus.Started => "processing",
                        PendingGenerationStatus.Failed => "failed",
                        PendingGenerationStatus.Succeeded => "succeeded",
                        _ => "unknown"
                    },
                    StartedAt = pendingGeneration.StartedAt ?? pendingGeneration.CreatedAt,
                    CompletedAt = pendingGeneration.CompletedAt,
                    Error = pendingGeneration.ErrorMessage
                };
            }

            return null;
        }

        try
        {
            var predictionStatus = await _replicateApiClient.GetPredictionStatusAsync(latestPrediction.Id);
            return new GenerationStatusDto
            {
                PredictionId = predictionStatus.Id ?? latestPrediction.Id,
                Status = NormalizePredictionStatus(predictionStatus.Status),
                Style = latestPrediction.Style,
                StartedAt = predictionStatus.StartedAt ?? predictionStatus.CreatedAt,
                CompletedAt = predictionStatus.CompletedAt,
                OutputCount = predictionStatus.GeneratedImageUrls.Count(),
                Error = predictionStatus.Error
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve prediction status for {PredictionId}", Sid(latestPrediction.Id));
            return new GenerationStatusDto
            {
                PredictionId = latestPrediction.Id,
                Status = "unknown",
                Style = latestPrediction.Style,
                StartedAt = latestPrediction.CreatedAt
            };
        }
    }

    private static string NormalizePredictionStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "unknown";
        }

        var normalized = status.Trim().ToLowerInvariant();
        return normalized switch
        {
            "starting" => "queued",
            "processing" => "processing",
            "succeeded" => "succeeded",
            "failed" => "failed",
            "canceled" => "canceled",
            _ => "unknown"
        };
    }

    private async Task<CreditImpactDto?> GetTrainingCreditImpactAsync(string userId, ModelCreationRequest request)
    {
        var windowStart = request.CreatedAt.AddMinutes(-1);
        var windowEnd = (request.CompletedAt ?? DateTime.UtcNow).AddMinutes(1);

        var charged = await _context.UsageLogs
            .Where(l => l.UserId == userId &&
                        l.Action == "model_training" &&
                        l.CreatedAt >= windowStart &&
                        l.CreatedAt <= windowEnd &&
                        l.CreditsCost.HasValue)
            .SumAsync(l => l.CreditsCost ?? 0);

        var refundedRaw = await _context.UsageLogs
            .Where(l => l.UserId == userId &&
                        l.Action == "model_training_refund" &&
                        l.CreatedAt >= windowStart &&
                        l.CreatedAt <= windowEnd &&
                        l.CreditsCost.HasValue)
            .SumAsync(l => l.CreditsCost ?? 0);

        var refunded = Math.Abs(refundedRaw);
        if (charged == 0 && refunded == 0)
        {
            return null;
        }

        return new CreditImpactDto
        {
            ChargedCredits = charged,
            RefundedCredits = refunded,
            NetCredits = charged - refunded
        };
    }

    private async Task<CreditImpactDto?> GetGenerationCreditImpactAsync(string userId, GenerationStatusDto generationStatus)
    {
        PendingGenerationRequest? pending = null;
        try
        {
            var pendingQuery = _context.PendingGenerationRequests.Where(p => p.UserId == userId);
            if (!string.IsNullOrWhiteSpace(generationStatus.PredictionId))
            {
                pendingQuery = pendingQuery.Where(p => p.LastPredictionId == generationStatus.PredictionId);
            }

            pending = await pendingQuery
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            _logger.LogWarning(ex, "PendingGenerationRequests table not available; skipping generation credit impact calculation");
            return null;
        }

        var start = pending?.StartedAt ?? pending?.CreatedAt ?? generationStatus.StartedAt ?? DateTime.UtcNow;
        var end = pending?.CompletedAt ?? generationStatus.CompletedAt ?? DateTime.UtcNow;

        var windowStart = start.AddMinutes(-1);
        var windowEnd = end.AddMinutes(1);

        var charged = await _context.UsageLogs
            .Where(l => l.UserId == userId &&
                        l.Action == "styled_generation" &&
                        l.CreatedAt >= windowStart &&
                        l.CreatedAt <= windowEnd &&
                        l.CreditsCost.HasValue)
            .SumAsync(l => l.CreditsCost ?? 0);

        var refundedRaw = await _context.UsageLogs
            .Where(l => l.UserId == userId &&
                        l.Action == "styled_generation_refund" &&
                        l.CreatedAt >= windowStart &&
                        l.CreatedAt <= windowEnd &&
                        l.CreditsCost.HasValue)
            .SumAsync(l => l.CreditsCost ?? 0);

        var refunded = Math.Abs(refundedRaw);
        if (charged == 0 && refunded == 0)
        {
            return null;
        }

        return new CreditImpactDto
        {
            ChargedCredits = charged,
            RefundedCredits = refunded,
            NetCredits = charged - refunded
        };
    }
}
