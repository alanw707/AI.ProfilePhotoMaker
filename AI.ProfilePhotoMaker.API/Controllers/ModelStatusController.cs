using AI.ProfilePhotoMaker.API.Constants;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
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
    private static string S(string? value) => LoggingSanitizer.Sanitize(value);
    private static string Sid(string? value) => LoggingSanitizer.SanitizeId(value);

    public ModelStatusController(ApplicationDbContext context, ILogger<ModelStatusController> logger)
    {
        _context = context;
        _logger = logger;
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
                createdAt = latestRequest.CreatedAt,
                completedAt = latestRequest.CompletedAt,
                errorMessage = latestRequest.ErrorMessage
            }
        };

        // Determine unified status code
        if (latestRequest != null && latestRequest.Status == ModelCreationStatus.Failed)
        {
            response.StatusCode = UnifiedModelStatusCode.Failed;
            response.Reason = latestRequest.ErrorMessage ?? "Latest training request failed";
        }
        else if (latestRequest != null && (latestRequest.Status == ModelCreationStatus.Pending || latestRequest.Status == ModelCreationStatus.Creating))
        {
            response.StatusCode = UnifiedModelStatusCode.Training;
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

        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> Get()
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
                createdAt = latestRequest.CreatedAt,
                completedAt = latestRequest.CompletedAt,
                errorMessage = latestRequest.ErrorMessage
            }
        };

        // Determine unified status code
        if (latestRequest != null && latestRequest.Status == ModelCreationStatus.Failed)
        {
            response.StatusCode = UnifiedModelStatusCode.Failed;
            response.Reason = latestRequest.ErrorMessage ?? "Latest training request failed";
        }
        else if (latestRequest != null && (latestRequest.Status == ModelCreationStatus.Pending || latestRequest.Status == ModelCreationStatus.Creating))
        {
            response.StatusCode = UnifiedModelStatusCode.Training;
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

        return Ok(response);
    }
}
