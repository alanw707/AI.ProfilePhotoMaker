using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Constants;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AI.ProfilePhotoMaker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly ApplicationDbContext _context; // Keep for now for other operations
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ProfileController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IReplicateApiClient _replicateApiClient;
    private readonly IBasicTierService _basicTierService;
    private readonly IStorageService _storageService;
    private readonly StoragePathResolver _pathResolver;

    private static string S(string? value) => LoggingSanitizer.Sanitize(value);
    private static string Sid(string? value) => LoggingSanitizer.SanitizeId(value);

    public ProfileController(
        IUserProfileRepository userProfileRepository,
        ApplicationDbContext context,
        IWebHostEnvironment environment,
        ILogger<ProfileController> logger,
        IConfiguration configuration,
        IReplicateApiClient replicateApiClient,
        IBasicTierService basicTierService,
        IStorageService storageService,
        StoragePathResolver pathResolver)
    {
        _userProfileRepository = userProfileRepository;
        _context = context;
        _environment = environment;
        _logger = logger;
        _configuration = configuration;
        _replicateApiClient = replicateApiClient;
        _basicTierService = basicTierService;
        _storageService = storageService;
        _pathResolver = pathResolver;
    }

    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var profile = await _userProfileRepository.GetByUserIdAsync(userId);

        if (profile == null)
            return NotFound("Profile not found");

        // Get model info from ModelCreationRequest (include Ready models for UI display)
        var latestModel = await _context.ModelCreationRequests
            .Where(m => m.UserId == userId && m.Status == ModelCreationStatus.Ready)
            .OrderByDescending(m => m.CompletedAt)
            .FirstOrDefaultAsync();

        // Check if there are any models (including Failed) that might exist on Replicate for deletion purposes
        var anyDeletableModel = await _context.ModelCreationRequests
            .Where(m => m.UserId == userId &&
                   (m.Status == ModelCreationStatus.Ready || m.Status == ModelCreationStatus.Failed) &&
                   !string.IsNullOrEmpty(m.ReplicateModelId))
            .OrderByDescending(m => m.CompletedAt ?? m.CreatedAt)
            .FirstOrDefaultAsync();

        var profileDto = new UserProfileDto
        {
            Id = profile.Id,
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            Gender = profile.Gender,
            Ethnicity = profile.Ethnicity,
            // Show Ready model info for UI, but ensure deletion can find Failed models too
            TrainedModelId = latestModel?.ReplicateModelId,
            TrainedModelVersionId = latestModel?.TrainedModelVersion,
            ModelTrainedAt = latestModel?.CompletedAt,
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt,
            TotalProcessedImages = profile.ProcessedImages.Count
        };

        return Ok(profileDto);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProfile([FromBody] CreateUserProfileDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var existingProfile = await _userProfileRepository.GetByUserIdAsync(userId);

        if (existingProfile != null)
            return BadRequest("Profile already exists");

        var profile = new UserProfile
        {
            UserId = userId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Gender = dto.Gender,
            Ethnicity = dto.Ethnicity
        };

        await _userProfileRepository.AddAsync(profile);

        var profileDto = new UserProfileDto
        {
            Id = profile.Id,
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            Gender = profile.Gender,
            Ethnicity = profile.Ethnicity,
            TrainedModelId = null, // New profile won't have trained model
            TrainedModelVersionId = null,
            ModelTrainedAt = null,
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt,
            TotalProcessedImages = 0
        };

        return CreatedAtAction(nameof(GetProfile), profileDto);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateUserProfileDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var profile = await _context.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile == null)
            return NotFound("Profile not found");

        profile.FirstName = dto.FirstName;
        profile.LastName = dto.LastName;
        profile.Gender = dto.Gender;
        profile.Ethnicity = dto.Ethnicity;
        profile.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Get model info from ModelCreationRequest
        var latestModel = await GetLatestTrainedModelAsync(userId);

        var profileDto = new UserProfileDto
        {
            Id = profile.Id,
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            Gender = profile.Gender,
            Ethnicity = profile.Ethnicity,
            TrainedModelId = latestModel?.ReplicateModelId,
            TrainedModelVersionId = latestModel?.TrainedModelVersion,
            ModelTrainedAt = latestModel?.CompletedAt,
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt,
            TotalProcessedImages = profile.ProcessedImages.Count
        };

        return Ok(profileDto);
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteProfile()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var profile = await _userProfileRepository.GetByUserIdAsync(userId);

        if (profile == null)
            return NotFound("Profile not found");

        await _userProfileRepository.DeleteAsync(profile);

        return Ok(new { success = true, message = "Profile deleted" });
    }

    [HttpGet("styles")]
    public async Task<IActionResult> GetStyles()
    {
        var styles = await _context.Styles
            .Where(s => s.IsActive)
            .Select(s => s.Name)
            .ToListAsync();
        return Ok(styles);
    }


    [HttpPost("generate")]
    public async Task<IActionResult> GenerateImages([FromBody] GenerateImagesRequestDto dto)
    {
        CreditConsumptionResult? creditConsumptionResult = null;
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var profile = await _userProfileRepository.GetByUserIdAsync(userId);

        if (profile == null)
            return NotFound("Profile not found");

        // Get trained model from ModelCreationRequest
        var trainedModel = await GetLatestTrainedModelAsync(userId);
        if (trainedModel == null || string.IsNullOrEmpty(trainedModel.TrainedModelVersion))
            return BadRequest("No trained model available. Please upload training images first.");

        try
        {
            if (dto.NumOutputs < 1 || dto.NumOutputs > 4)
            {
                return BadRequest(new
                {
                    success = false,
                    error = new
                    {
                        code = "InvalidNumOutputs",
                        message = "NumOutputs must be between 1 and 4."
                    }
                });
            }

            var requiredCredits = dto.NumOutputs * CreditCostConfig.GetCreditCost("image_generation");
            var availableCredits = await _basicTierService.GetAvailableCreditsAsync(userId);
            if (availableCredits < requiredCredits)
            {
                return BadRequest(new
                {
                    success = false,
                    error = new
                    {
                        code = "InsufficientCredits",
                        message = $"Image generation requires {requiredCredits} credits. You have {availableCredits} credits."
                    }
                });
            }

            var correlationId = $"image_generation:{Guid.NewGuid()}";
            creditConsumptionResult = await _basicTierService.ConsumeCreditsAsync(
                userId,
                requiredCredits,
                "image_generation",
                correlationId,
                HttpContext?.RequestAborted ?? CancellationToken.None);
            if (creditConsumptionResult == null || !creditConsumptionResult.Success)
            {
                if (creditConsumptionResult?.Error == "insufficient_credits")
                {
                    _logger.LogWarning("Insufficient credits detected during image generation for user {UserId}", Sid(userId));
                    return BadRequest(new
                    {
                        success = false,
                        error = new
                        {
                            code = "InsufficientCredits",
                            message = $"Image generation requires {requiredCredits} credits. You have {availableCredits} credits."
                        }
                    });
                }

                _logger.LogError("Failed to consume credits for user {UserId} before image generation", Sid(userId));
                return StatusCode(500, "Failed to process credit deduction. Please try again.");
            }

            dto.UserId = userId;
            dto.TrainedModelVersion = trainedModel.TrainedModelVersion;
            dto.UserInfo = new UserInfo
            {
                Gender = profile.Gender,
                Ethnicity = profile.Ethnicity
            };

            var processedImageUrl = await _replicateApiClient.GenerateImagesAsync(dto);
            var remainingCredits = await _basicTierService.GetAvailableCreditsAsync(userId);

            var response = new GenerateProfileImagesResponseDto
            {
                ImageUrl = processedImageUrl,
                Message = "Image generation started",
                CreditsRemaining = remainingCredits,
                CreditsCost = requiredCredits
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            if (creditConsumptionResult?.Success == true)
            {
                await _basicTierService.RefundCreditsAsync(userId, creditConsumptionResult);
            }
            _logger.LogError(ex, "Error generating images for user {UserId}", Sid(userId));
            return StatusCode(500, "Error generating images");
        }
    }

    [HttpGet("training-status")]
    public async Task<IActionResult> GetTrainingStatus()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var profile = await _userProfileRepository.GetByUserIdAsync(userId);

        if (profile == null)
            return NotFound("Profile not found");

        var uploadedImages = profile.ProcessedImages.Where(i => i.Style == ImageConstants.OriginalStyle).ToList();
        var zipPath = Path.Combine(_environment.ContentRootPath, "training-zips", $"{userId}.zip");
        var zipFiles = System.IO.File.Exists(zipPath) ? new[] { zipPath } : Array.Empty<string>();

        // Get model info from ModelCreationRequest - consistent with GetProfile() logic
        var latestModel = await GetLatestTrainedModelAsync(userId);

        // Check if there are any models (including Failed) that might exist on Replicate for deletion purposes
        var anyDeletableModel = await GetAnyDeletableModelAsync(userId);

        // Use Ready-only status for hasTrainedModel; deletable Failed models are handled via deletion flows
        var hasTrainedModel = latestModel != null;
        var trainedModelId = latestModel?.ReplicateModelId;

        return Ok(new
        {
            ProfileId = profile.Id,
            HasTrainedModel = hasTrainedModel,
            TrainedModelId = trainedModelId,
            ModelTrainedAt = latestModel?.CompletedAt,
            TotalUploadedImages = uploadedImages.Count,
            LatestZipFile = zipFiles.OrderByDescending(f => System.IO.File.GetCreationTime(f)).FirstOrDefault(),
            CanStartTraining = uploadedImages.Count >= 10, // Minimum 10 images for training
            Status = uploadedImages.Count switch
            {
                0 => "No images uploaded",
                < 10 => $"Need at least 10 images (currently {uploadedImages.Count})",
                >= 10 when !hasTrainedModel => "Ready for training",
                >= 10 when hasTrainedModel => "Model trained - ready for generation",
                _ => "Unknown status"
            }
        });
    }



    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private async Task<ModelCreationRequest?> GetLatestTrainedModelAsync(string userId)
    {
        return await _context.ModelCreationRequests
            .Where(m => m.UserId == userId && m.Status == ModelCreationStatus.Ready)
            .OrderByDescending(m => m.CompletedAt)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Get any model that might exist on Replicate for deletion purposes (Ready or Failed status)
    /// </summary>
    private async Task<ModelCreationRequest?> GetAnyDeletableModelAsync(string userId)
    {
        return await _context.ModelCreationRequests
            .Where(m => m.UserId == userId &&
                   (m.Status == ModelCreationStatus.Ready || m.Status == ModelCreationStatus.Failed) &&
                   !string.IsNullOrEmpty(m.ReplicateModelId))
            .OrderByDescending(m => m.CompletedAt ?? m.CreatedAt)
            .FirstOrDefaultAsync();
    }






    /// <summary>
    /// Get user data statistics for account settings
    /// </summary>
    [HttpGet("data-stats")]
    public async Task<IActionResult> GetDataStats()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        try
        {
            var profile = await _userProfileRepository.GetByUserIdLightAsync(userId);
            if (profile == null)
                return NotFound("Profile not found");

            var inputPhotos = profile.ProcessedImages.Where(i => i.Style == ImageConstants.OriginalStyle).Count();
            var generatedPhotos = profile.ProcessedImages.Where(i => i.IsGenerated).Count();
            var enhancedPhotos = profile.ProcessedImages.Where(i =>
                (i.Style == "Enhanced" || i.Style == "Background Remover" || i.Style == "Social Media" || i.Style == "Cartoon")
               ).Count();

            // Calculate total data size (approximate)
            var totalImages = profile.ProcessedImages.Where(i => true).Count();
            var estimatedDataSize = totalImages * 2.5; // Approximate MB per image

            var stats = new
            {
                InputPhotos = inputPhotos,
                GeneratedPhotos = generatedPhotos,
                EnhancedPhotos = enhancedPhotos,
                HasTrainedModel = false, // Use ModelCreationRequest table for model status
                TotalDataSize = estimatedDataSize,
                AccountAge = (DateTime.UtcNow - profile.CreatedAt).Days,
                UsageLogCount = profile.UsageLogs.Count
            };

            return Ok(new { success = true, data = stats, error = (object?)null });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting data stats for user {UserId}", Sid(userId));
            return StatusCode(500, new { success = false, error = new { code = "DataStatsError", message = "Failed to get data statistics." } });
        }
    }

    /// <summary>
    /// Delete only input photos (original uploads) for the user
    /// </summary>
    [HttpDelete("data/photos")]
    public async Task<IActionResult> DeleteInputPhotos()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        try
        {
            var profile = await _userProfileRepository.GetByUserIdLightAsync(userId);
            if (profile == null)
                return NotFound("Profile not found");

            // Get only original upload photos (not generated ones)
            var inputPhotos = await _context.ProcessedImages
                .Where(i => i.UserProfileId == profile.Id && (i.IsOriginalUpload || i.Style == ImageConstants.OriginalStyle))
                .ToListAsync();

            var deletedCount = 0;
            var filesDeleted = 0;

            // Best-effort delete any remaining upload files (new + legacy prefixes)
            try
            {
                var uploadPrefix = _pathResolver.GetDirectoryPrefix(StorageType.Upload, userId);
                if (await _storageService.DeleteDirectoryAsync(uploadPrefix))
                {
                    filesDeleted++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete upload directory prefix for user {UserId}", Sid(userId));
            }

            try
            {
                var legacyUploadPrefix = $"uploads/{userId}/";
                if (await _storageService.DeleteDirectoryAsync(legacyUploadPrefix))
                {
                    filesDeleted++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete legacy upload directory prefix for user {UserId}", Sid(userId));
            }

            foreach (var photo in inputPhotos)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(photo.OriginalImageUrl))
                    {
                        if (await _storageService.DeleteImageAsync(photo.OriginalImageUrl))
                        {
                            filesDeleted++;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(photo.ProcessedImageUrl) &&
                        !string.Equals(photo.ProcessedImageUrl, photo.OriginalImageUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        if (await _storageService.DeleteImageAsync(photo.ProcessedImageUrl))
                        {
                            filesDeleted++;
                        }
                    }

                    deletedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete photo {PhotoId} for user {UserId}", photo.Id, Sid(userId));
                }
            }

            if (inputPhotos.Count > 0)
            {
                _context.ProcessedImages.RemoveRange(inputPhotos);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Deleted {DeletedCount} input photos for user {UserId} (filesDeleted: {FilesDeleted})",
                deletedCount,
                Sid(userId),
                filesDeleted);

            return Ok(new
            {
                success = true,
                data = new
                {
                    deletedCount = deletedCount,
                    filesDeleted = filesDeleted,
                    message = $"Successfully deleted {deletedCount} input photos"
                },
                error = (object?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting input photos for user {UserId}", Sid(userId));
            return StatusCode(500, new { success = false, error = new { code = "PhotoDeletionError", message = "Failed to delete input photos." } });
        }
    }

    /// <summary>
    /// Delete the user's trained AI model
    /// </summary>
    [HttpDelete("data/model")]
    public async Task<IActionResult> DeleteAIModel()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        try
        {
            var profile = await _userProfileRepository.GetByUserIdAsync(userId);
            if (profile == null)
                return NotFound("Profile not found");

            // Gather ALL model creation requests for user to ensure complete cleanup
            var allModelRequests = await _context.ModelCreationRequests
                .Where(m => m.UserId == userId)
                .OrderByDescending(m => m.CompletedAt ?? m.CreatedAt)
                .ToListAsync();

            _logger.LogInformation("Found {Count} total model requests for user {UserId} - will delete all regardless of status",
                allModelRequests.Count, Sid(userId));

            // Clean up any legacy "deleted" records that were incorrectly marked as Failed
            var legacyDeletedRecords = allModelRequests
                .Where(r => r.Status == ModelCreationStatus.Failed &&
                           !string.IsNullOrEmpty(r.ErrorMessage) &&
                           r.ErrorMessage.ToLower().Contains("deleted"))
                .ToList();

            if (legacyDeletedRecords.Any())
            {
                _logger.LogWarning("Found {Count} legacy 'deleted' records incorrectly marked as Failed - these will be properly deleted",
                    legacyDeletedRecords.Count);
            }

            if (allModelRequests == null || allModelRequests.Count == 0)
            {
                return BadRequest(new { success = false, error = new { code = "NoModel", message = "No trained model found to delete." } });
            }

            int deletedModels = 0;
            foreach (var trainedModel in allModelRequests)
            {
                // Only attempt Replicate deletion for Ready/Failed models with ReplicateModelId
                if (string.IsNullOrEmpty(trainedModel.ReplicateModelId) ||
                    (trainedModel.Status != ModelCreationStatus.Ready && trainedModel.Status != ModelCreationStatus.Failed))
                {
                    // No remote artifact to delete (Pending/Creating records) — just remove DB entry later
                    _logger.LogInformation("Skipping Replicate deletion for model request {Id} (Status: {Status}, ModelId: {ModelId})",
                        trainedModel.Id, trainedModel.Status, S(trainedModel.ReplicateModelId ?? "null"));
                    continue;
                }

                var modelId = trainedModel.ReplicateModelId;

                // Pre-validate model exists on Replicate before attempting deletion
                bool modelExistsOnReplicate;
                try
                {
                    modelExistsOnReplicate = await _replicateApiClient.CheckModelExistsAsync(modelId);
                    _logger.LogInformation("Pre-deletion validation for model {ModelId}: exists = {Exists}", S(modelId), modelExistsOnReplicate);
                }
                catch (Exception validationEx)
                {
                    _logger.LogWarning(validationEx, "Unable to validate model existence for {ModelId}, proceeding with deletion attempt", S(modelId));
                    modelExistsOnReplicate = true; // Assume exists if we can't check
                }

                if (modelExistsOnReplicate)
                {
                    try
                    {
                        // Ensure proper tuple destructuring with trainedModel reference (regression check)
                        var (success, errorMessage) = await _replicateApiClient.DeleteModelAsync(trainedModel.ReplicateModelId);
                        if (!success)
                        {
                            _logger.LogError("Failed to delete model {ModelId} from Replicate for user {UserId} - {ErrorMessage}", S(modelId), Sid(userId), S(errorMessage));
                            return StatusCode(400, new { success = false, error = new { code = "ReplicateDeletionFailed", message = errorMessage ?? "Failed to delete AI model from Replicate service." } });
                        }
                    }
                    catch (Exception replicateEx)
                    {
                        _logger.LogError(replicateEx, "Failed to delete model {ModelId} from Replicate for user {UserId} - exception occurred", S(modelId), Sid(userId));
                        return StatusCode(500, new { success = false, error = new { code = "ReplicateDeletionError", message = $"Error communicating with Replicate service: {replicateEx.Message}" } });
                    }
                }
                else
                {
                    _logger.LogInformation("Model {ModelId} not found on Replicate, treating as already deleted for user {UserId}", S(modelId), Sid(userId));
                }

                // We will hard-delete the record below; keep a count for response
                deletedModels++;
            }

            // Hard delete all model creation request rows to avoid stale Ready records
            _logger.LogInformation("Deleting {Count} model creation requests from database for user {UserId}",
                allModelRequests.Count, Sid(userId));

            foreach (var request in allModelRequests)
            {
                _logger.LogInformation("Deleting model request {Id}: Status={Status}, ModelId={ModelId}, Created={Created}",
                    request.Id, request.Status, S(request.ReplicateModelId ?? "null"), request.CreatedAt);
            }

            _context.ModelCreationRequests.RemoveRange(allModelRequests);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully removed all {Count} model creation requests from database", allModelRequests.Count);

            // Clear model information from database (updated timestamp only)
            profile.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Delete training ZIP files
            try
            {
                var trainingZipsPath = Path.Combine(_environment.ContentRootPath, "training-zips");
                if (Directory.Exists(trainingZipsPath))
                {
                    var userZipFile = Path.Combine(trainingZipsPath, $"{userId}.zip");
                    if (System.IO.File.Exists(userZipFile))
                    {
                        System.IO.File.Delete(userZipFile);
                    }
                }
            }
            catch (Exception zipEx)
            {
                _logger.LogWarning(zipEx, "Failed to delete training ZIP files for user {UserId}", Sid(userId));
            }

            _logger.LogInformation("Successfully deleted {Count} AI model(s) and related files for user {UserId}", deletedModels, Sid(userId));

            return Ok(new
            {
                success = true,
                data = new
                {
                    message = deletedModels <= 1 ? "AI model and training files have been successfully deleted" : $"{deletedModels} AI models and training files have been successfully deleted"
                },
                error = (object?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting AI model for user {UserId}", Sid(userId));
            return StatusCode(500, new { success = false, error = new { code = "ModelDeletionError", message = "Failed to delete AI model." } });
        }
    }


    /// <summary>
    /// Delete all user data (photos, models, usage logs) but keep the profile
    /// </summary>
    [HttpDelete("data/all")]
    public async Task<IActionResult> DeleteAllUserData()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        try
        {
            var profile = await _userProfileRepository.GetByUserIdLightAsync(userId);
            if (profile == null)
                return NotFound("Profile not found");

            var summary = await PrepareUserDataDeletionAsync(
                userId,
                profile.Id,
                includeFeedbackSubmissions: false);

            profile.UpdatedAt = DateTime.UtcNow;
            profile.LastModelSyncCheck = null;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted all data for user {UserId}: {@Summary}", Sid(userId), summary);

            return Ok(new
            {
                success = true,
                data = new
                {
                    message = "All user data has been successfully deleted",
                    summary = summary
                },
                error = (object?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting all user data for user {UserId}", Sid(userId));
            return StatusCode(500, new { success = false, error = new { code = "DataDeletionError", message = "Failed to delete all user data." } });
        }
    }

    /// <summary>
    /// Delete the entire user account and all associated data
    /// </summary>
    [HttpDelete("account")]
    public async Task<IActionResult> DeleteUserAccount()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        try
        {
            var profile = await _userProfileRepository.GetByUserIdAsync(userId);
            if (profile == null)
                return NotFound("Profile not found");

            // First delete all user data using the existing method logic
            await DeleteAllUserDataInternal(userId, profile);

            // Then delete the profile itself
            await _userProfileRepository.DeleteAsync(profile);

            // Delete the ApplicationUser record
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null)
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Successfully deleted entire account for user {UserId}", Sid(userId));

            return Ok(new
            {
                success = true,
                data = new
                {
                    message = "Account has been successfully deleted"
                },
                error = (object?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting account for user {UserId}", Sid(userId));
            return StatusCode(500, new { success = false, error = new { code = "AccountDeletionError", message = "Failed to delete account." } });
        }
    }

    /// <summary>
    /// Generate and download user data export
    /// </summary>
    [HttpGet("data/export")]
    public async Task<IActionResult> ExportUserData()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        try
        {
            var profile = await _userProfileRepository.GetByUserIdAsync(userId);
            if (profile == null)
                return NotFound("Profile not found");

            // Get model info from ModelCreationRequest
            var latestModel = await GetLatestTrainedModelAsync(userId);

            var exportData = new
            {
                Profile = new
                {
                    profile.Id,
                    profile.FirstName,
                    profile.LastName,
                    profile.Gender,
                    profile.Ethnicity,
                    profile.SubscriptionTier,
                    profile.Credits,
                    profile.CreatedAt,
                    profile.UpdatedAt,
                    HasTrainedModel = latestModel != null,
                    ModelTrainedAt = latestModel?.CompletedAt
                },
                Images = profile.ProcessedImages.Where(i => true).Select(i => new
                {
                    i.Id,
                    i.Style,
                    i.IsGenerated,
                    i.IsOriginalUpload,
                    i.CreatedAt,
                    HasOriginalFile = !string.IsNullOrEmpty(i.OriginalImageUrl),
                    HasProcessedFile = !string.IsNullOrEmpty(i.ProcessedImageUrl)
                }),
                UsageLogs = profile.UsageLogs.Select(log => new
                {
                    log.Id,
                    log.Action,
                    CreditsUsed = log.CreditsCost,
                    Timestamp = log.CreatedAt,
                    log.Details
                }),
                Statistics = new
                {
                    TotalImages = profile.ProcessedImages.Count(i => true),
                    OriginalUploads = profile.ProcessedImages.Count(i => i.Style == ImageConstants.OriginalStyle),
                    GeneratedImages = profile.ProcessedImages.Count(i => i.IsGenerated),
                    TotalCreditsUsed = profile.UsageLogs.Sum(log => log.CreditsCost ?? 0),
                    AccountAge = (DateTime.UtcNow - profile.CreatedAt).Days
                },
                ExportInfo = new
                {
                    ExportedAt = DateTime.UtcNow,
                    UserId = userId,
                    Version = "1.0"
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            var fileName = $"profile-data-export-{userId}-{DateTime.UtcNow:yyyyMMddHHmmss}.json";

            return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting data for user {UserId}", Sid(userId));
            return StatusCode(500, new { success = false, error = new { code = "ExportError", message = "Failed to export user data." } });
        }
    }

    /// <summary>
    /// Internal helper method for deleting all user data
    /// </summary>
    private async Task DeleteAllUserDataInternal(string userId, UserProfile profile)
    {
        await PrepareUserDataDeletionAsync(
            userId,
            profile.Id,
            includeFeedbackSubmissions: true);
    }

    private sealed record UserDataDeletionSummary(
        int InputPhotosDeleted,
        int GeneratedPhotosDeleted,
        int ProcessedImagesDeleted,
        int UsageLogsDeleted,
        int ModelRequestsDeleted,
        int PredictionsDeleted,
        int PendingGenerationRequestsDeleted,
        int UserStyleSelectionsDeleted,
        int FeedbackSubmissionsDeleted,
        int StorageDirectoriesDeleted,
        int StorageFilesDeleted,
        int StorageDeleteTimeouts,
        int ReplicateModelDeleteAttempts,
        int ReplicateModelDeleteTimeouts);

    private async Task<UserDataDeletionSummary> PrepareUserDataDeletionAsync(
        string userId,
        int userProfileId,
        bool includeFeedbackSubmissions)
    {
        // Fetch DB records to delete
        var processedImages = await _context.ProcessedImages
            .Where(i => i.UserProfileId == userProfileId)
            .ToListAsync();

        var usageLogs = await _context.UsageLogs
            .Where(l => l.UserId == userId)
            .ToListAsync();

        var modelRequests = await _context.ModelCreationRequests
            .Where(m => m.UserId == userId)
            .ToListAsync();

        var predictions = await _context.Predictions
            .Where(p => p.UserId == userId)
            .ToListAsync();

        var pendingGenerationRequests = await _context.PendingGenerationRequests
            .Where(p => p.UserId == userId)
            .ToListAsync();

        var styleSelections = await _context.UserStyleSelections
            .Where(s => s.UserProfileId == userProfileId)
            .ToListAsync();

        var feedbackSubmissions = includeFeedbackSubmissions
            ? await _context.FeedbackSubmissions.Where(fs => fs.UserId == userId).ToListAsync()
            : new List<FeedbackSubmission>();

        // Delete storage content (best effort)
        var storageDirectoriesDeleted = 0;
        var storageFilesDeleted = 0;
        var storageDeleteTimeouts = 0;

        var directoryPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            _pathResolver.GetDirectoryPrefix(StorageType.Upload, userId),
            _pathResolver.GetDirectoryPrefix(StorageType.Generated, userId),
            _pathResolver.GetDirectoryPrefix(StorageType.Enhanced, userId),

            // Legacy/non-environment-prefixed paths (older versions)
            $"uploads/{userId}/",
            $"generated/{userId}/",
            $"enhanced/{userId}/"
        };

        foreach (var prefix in directoryPrefixes)
        {
            var (completed, success) = await TryDeleteStorageDirectoryAsync(prefix, TimeSpan.FromSeconds(5));
            if (!completed)
            {
                storageDeleteTimeouts++;
            }
            if (success)
            {
                storageDirectoriesDeleted++;
            }
        }

        var filePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Training ZIP (current path resolver + legacy)
        var zipFileName = $"{userId}.zip";
        filePaths.Add(_pathResolver.GetPath(StorageType.TrainingZip, userId, zipFileName));
        filePaths.Add($"training-zips/{zipFileName}");

        foreach (var modelRequest in modelRequests)
        {
            if (!string.IsNullOrWhiteSpace(modelRequest.TrainingImageZipUrl))
            {
                filePaths.Add(modelRequest.TrainingImageZipUrl);
            }
        }

        foreach (var img in processedImages)
        {
            if (!string.IsNullOrWhiteSpace(img.OriginalImageUrl))
            {
                filePaths.Add(img.OriginalImageUrl);
            }

            if (!string.IsNullOrWhiteSpace(img.ProcessedImageUrl))
            {
                filePaths.Add(img.ProcessedImageUrl);
            }
        }

        foreach (var path in filePaths)
        {
            var (completed, success) = await TryDeleteStorageFileAsync(path, TimeSpan.FromSeconds(5));
            if (!completed)
            {
                storageDeleteTimeouts++;
            }
            if (success)
            {
                storageFilesDeleted++;
            }
        }

        // Delete Replicate models (best effort, with timeout)
        var replicateModelDeleteAttempts = 0;
        var replicateModelDeleteTimeouts = 0;
        var replicateModelIds = modelRequests
            .Select(m => m.ReplicateModelId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var modelId in replicateModelIds)
        {
            replicateModelDeleteAttempts++;
            var (completed, success) = await TryDeleteReplicateModelAsync(modelId!, TimeSpan.FromSeconds(20));
            if (!completed)
            {
                replicateModelDeleteTimeouts++;
            }

            if (!success)
            {
                _logger.LogWarning("Replicate model delete failed for user {UserId}, model {ModelId}", Sid(userId), S(modelId));
            }
        }

        // Remove DB records
        _context.ProcessedImages.RemoveRange(processedImages);
        _context.UsageLogs.RemoveRange(usageLogs);
        _context.ModelCreationRequests.RemoveRange(modelRequests);
        _context.Predictions.RemoveRange(predictions);
        _context.PendingGenerationRequests.RemoveRange(pendingGenerationRequests);
        _context.UserStyleSelections.RemoveRange(styleSelections);
        if (includeFeedbackSubmissions && feedbackSubmissions.Count > 0)
        {
            _context.FeedbackSubmissions.RemoveRange(feedbackSubmissions);
        }

        var inputPhotosDeleted = processedImages.Count(i => i.IsOriginalUpload || i.Style == ImageConstants.OriginalStyle);
        var generatedPhotosDeleted = processedImages.Count(i => i.IsGenerated);

        return new UserDataDeletionSummary(
            InputPhotosDeleted: inputPhotosDeleted,
            GeneratedPhotosDeleted: generatedPhotosDeleted,
            ProcessedImagesDeleted: processedImages.Count,
            UsageLogsDeleted: usageLogs.Count,
            ModelRequestsDeleted: modelRequests.Count,
            PredictionsDeleted: predictions.Count,
            PendingGenerationRequestsDeleted: pendingGenerationRequests.Count,
            UserStyleSelectionsDeleted: styleSelections.Count,
            FeedbackSubmissionsDeleted: feedbackSubmissions.Count,
            StorageDirectoriesDeleted: storageDirectoriesDeleted,
            StorageFilesDeleted: storageFilesDeleted,
            StorageDeleteTimeouts: storageDeleteTimeouts,
            ReplicateModelDeleteAttempts: replicateModelDeleteAttempts,
            ReplicateModelDeleteTimeouts: replicateModelDeleteTimeouts);
    }

    private async Task<(bool Completed, bool Success)> TryDeleteReplicateModelAsync(string modelId, TimeSpan timeout)
    {
        try
        {
            var deleteTask = _replicateApiClient.DeleteModelAsync(modelId);
            var completed = await Task.WhenAny(deleteTask, Task.Delay(timeout));
            if (completed != deleteTask)
            {
                return (Completed: false, Success: false);
            }

            var (success, _) = await deleteTask;
            return (Completed: true, Success: success);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Replicate model delete threw for model {ModelId}", S(modelId));
            return (Completed: true, Success: false);
        }
    }

    private async Task<(bool Completed, bool Success)> TryDeleteStorageDirectoryAsync(string directoryPrefix, TimeSpan timeout)
    {
        try
        {
            var deleteTask = _storageService.DeleteDirectoryAsync(directoryPrefix);
            var completed = await Task.WhenAny(deleteTask, Task.Delay(timeout));
            if (completed != deleteTask)
            {
                _logger.LogWarning(
                    "Storage directory deletion timed out for user directory {Prefix}",
                    S(directoryPrefix));
                return (Completed: false, Success: false);
            }

            return (Completed: true, Success: await deleteTask);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete storage directory {Prefix}", S(directoryPrefix));
            return (Completed: true, Success: false);
        }
    }

    private async Task<(bool Completed, bool Success)> TryDeleteStorageFileAsync(string storagePath, TimeSpan timeout)
    {
        try
        {
            var deleteTask = _storageService.DeleteImageAsync(storagePath);
            var completed = await Task.WhenAny(deleteTask, Task.Delay(timeout));
            if (completed != deleteTask)
            {
                _logger.LogWarning(
                    "Storage file deletion timed out for path {Path}",
                    S(storagePath));
                return (Completed: false, Success: false);
            }

            return (Completed: true, Success: await deleteTask);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete storage file {Path}", S(storagePath));
            return (Completed: true, Success: false);
        }
    }
}
