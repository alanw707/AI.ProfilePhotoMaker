using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Constants;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
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

    public ProfileController(
        IUserProfileRepository userProfileRepository,
        ApplicationDbContext context,
        IWebHostEnvironment environment,
        ILogger<ProfileController> logger,
        IConfiguration configuration,
        IReplicateApiClient replicateApiClient)
    {
        _userProfileRepository = userProfileRepository;
        _context = context;
        _environment = environment;
        _logger = logger;
        _configuration = configuration;
        _replicateApiClient = replicateApiClient;
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

        // Get model info from ModelCreationRequest
        var latestModel = await _context.ModelCreationRequests
            .Where(m => m.UserId == userId && m.Status == ModelCreationStatus.Ready)
            .OrderByDescending(m => m.CompletedAt)
            .FirstOrDefaultAsync();

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
            dto.UserId = userId;
            dto.TrainedModelVersion = trainedModel.TrainedModelVersion;
            dto.UserInfo = new UserInfo
            {
                Gender = profile.Gender,
                Ethnicity = profile.Ethnicity
            };

            var processedImageUrl = await _replicateApiClient.GenerateImagesAsync(dto);
            
            return Ok(new { ImageUrl = processedImageUrl, Message = "Image generation started" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating images for user {UserId}", userId);
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

        // Get model info from ModelCreationRequest
        var latestModel = await GetLatestTrainedModelAsync(userId);

        return Ok(new
        {
            ProfileId = profile.Id,
            HasTrainedModel = latestModel != null,
            TrainedModelId = latestModel?.ReplicateModelId,
            ModelTrainedAt = latestModel?.CompletedAt,
            TotalUploadedImages = uploadedImages.Count,
            LatestZipFile = zipFiles.OrderByDescending(f => System.IO.File.GetCreationTime(f)).FirstOrDefault(),
            CanStartTraining = uploadedImages.Count >= 10, // Minimum 10 images for training
            Status = uploadedImages.Count switch
            {
                0 => "No images uploaded",
                < 10 => $"Need at least 10 images (currently {uploadedImages.Count})",
                >= 10 when latestModel == null => "Ready for training",
                >= 10 when latestModel != null => "Model trained - ready for generation",
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
            var profile = await _userProfileRepository.GetByUserIdAsync(userId);
            if (profile == null)
                return NotFound("Profile not found");

            var inputPhotos = profile.ProcessedImages.Where(i => i.Style == ImageConstants.OriginalStyle && !i.IsDeleted).Count();
            var generatedPhotos = profile.ProcessedImages.Where(i => i.IsGenerated && !i.IsDeleted).Count();
            var enhancedPhotos = profile.ProcessedImages.Where(i => 
                (i.Style == "Enhanced" || i.Style == "Background Remover" || i.Style == "Social Media" || i.Style == "Cartoon") 
                && !i.IsDeleted).Count();

            // Calculate total data size (approximate)
            var totalImages = profile.ProcessedImages.Where(i => !i.IsDeleted).Count();
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
            _logger.LogError(ex, "Error getting data stats for user {UserId}", userId);
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
            var profile = await _userProfileRepository.GetByUserIdAsync(userId);
            if (profile == null)
                return NotFound("Profile not found");

            // Get only original upload photos (not generated ones)
            var inputPhotos = profile.ProcessedImages
                .Where(i => i.Style == ImageConstants.OriginalStyle && !i.IsDeleted)
                .ToList();

            var deletedCount = 0;
            var uploadDir = Path.Combine(_environment.ContentRootPath, "uploads", userId);

            foreach (var photo in inputPhotos)
            {
                try
                {
                    // Mark as deleted in database
                    photo.IsDeleted = true;
                    photo.DeletedAt = DateTime.UtcNow;
                    photo.UserRequestedDeletionDate = DateTime.UtcNow;

                    // Delete physical file if it exists
                    if (!string.IsNullOrEmpty(photo.OriginalImageUrl))
                    {
                        var fileName = Path.GetFileName(photo.OriginalImageUrl);
                        var filePath = Path.Combine(uploadDir, fileName);
                        if (System.IO.File.Exists(filePath))
                        {
                            System.IO.File.Delete(filePath);
                        }
                    }

                    deletedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete photo {PhotoId} for user {UserId}", photo.Id, userId);
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted {DeletedCount} input photos for user {UserId}", deletedCount, userId);

            return Ok(new 
            { 
                success = true, 
                data = new { 
                    deletedCount = deletedCount, 
                    message = $"Successfully deleted {deletedCount} input photos" 
                }, 
                error = (object?)null 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting input photos for user {UserId}", userId);
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

            // Check ModelCreationRequest table for model availability
        var hasModel = await GetLatestTrainedModelAsync(userId!) != null;
        if (!hasModel)
            {
                return BadRequest(new { success = false, error = new { code = "NoModel", message = "No trained model found to delete." } });
            }

            // Get model from ModelCreationRequest
            var trainedModel = await GetLatestTrainedModelAsync(userId);
            if (trainedModel != null && !string.IsNullOrEmpty(trainedModel.ReplicateModelId))
            {
                var modelId = trainedModel.ReplicateModelId;
                
                // Try to delete model from Replicate (best effort)
                try
                {
                    await _replicateApiClient.DeleteModelAsync(modelId);
                    _logger.LogInformation("Successfully deleted model {ModelId} from Replicate for user {UserId}", modelId, userId);
                }
                catch (Exception replicateEx)
                {
                    _logger.LogWarning(replicateEx, "Failed to delete model {ModelId} from Replicate for user {UserId}, continuing with database cleanup", modelId, userId);
                }
                
                // Mark model as deleted in ModelCreationRequest
                trainedModel.Status = ModelCreationStatus.Failed;
                trainedModel.ErrorMessage = "Model deleted by user";
                await _context.SaveChangesAsync();
            }

            // Clear model information from database
            // Model data is now managed in ModelCreationRequest table
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
                _logger.LogWarning(zipEx, "Failed to delete training ZIP files for user {UserId}", userId);
            }

            _logger.LogInformation("Successfully deleted AI model and related files for user {UserId}", userId);

            return Ok(new 
            { 
                success = true, 
                data = new { 
                    message = "AI model and training files have been successfully deleted" 
                }, 
                error = (object?)null 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting AI model for user {UserId}", userId);
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
            var profile = await _userProfileRepository.GetByUserIdAsync(userId);
            if (profile == null)
                return NotFound("Profile not found");

            var deletionSummary = new
            {
                PhotosDeleted = 0,
                ModelDeleted = false,
                UsageLogsDeleted = 0,
                FilesDeleted = 0
            };

            // Delete all photos (mark as deleted and remove files)
            var allPhotos = profile.ProcessedImages.Where(i => !i.IsDeleted).ToList();
            var photosDeleted = 0;
            var filesDeleted = 0;

            var uploadDir = Path.Combine(_environment.ContentRootPath, "uploads", userId);
            
            foreach (var photo in allPhotos)
            {
                try
                {
                    // Mark as deleted in database
                    photo.IsDeleted = true;
                    photo.DeletedAt = DateTime.UtcNow;
                    photo.UserRequestedDeletionDate = DateTime.UtcNow;

                    // Delete physical file if it exists
                    if (!string.IsNullOrEmpty(photo.OriginalImageUrl))
                    {
                        var fileName = Path.GetFileName(photo.OriginalImageUrl);
                        var filePath = Path.Combine(uploadDir, fileName);
                        if (System.IO.File.Exists(filePath))
                        {
                            System.IO.File.Delete(filePath);
                            filesDeleted++;
                        }
                    }

                    photosDeleted++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete photo {PhotoId} for user {UserId}", photo.Id, userId);
                }
            }

            // Delete entire upload directory if it exists
            try
            {
                if (Directory.Exists(uploadDir))
                {
                    Directory.Delete(uploadDir, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete upload directory for user {UserId}", userId);
            }

            // Delete AI model
            var modelDeleted = false;
            // Check ModelCreationRequest table for model availability
        var hasModel = await GetLatestTrainedModelAsync(userId!) != null;
        if (hasModel)
            {
                try
                {
                    // await _replicateApiClient.DeleteModelAsync(profile.TrainedModelId); // TODO: Use ModelCreationRequest
                    modelDeleted = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete model from Replicate for user {UserId}", userId);
                }

                // Clear model information from database
                // profile.TrainedModelId = null; // TODO: Mark ModelCreationRequest as deleted
                // profile.TrainedModelVersionId = null;
                // profile.ModelTrainedAt = null; // Moved to ModelCreationRequest
            }

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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete training ZIP files for user {UserId}", userId);
            }

            // Delete usage logs (soft delete)
            var usageLogsDeleted = 0;
            foreach (var log in profile.UsageLogs)
            {
                _context.UsageLogs.Remove(log);
                usageLogsDeleted++;
            }

            // Reset profile credits and subscription data (but keep basic profile info)
            profile.Credits = 3; // Reset to default
            profile.PurchasedCredits = 0;
            profile.LastCreditReset = DateTime.UtcNow;
            profile.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var summary = new
            {
                PhotosDeleted = photosDeleted,
                ModelDeleted = modelDeleted,
                UsageLogsDeleted = usageLogsDeleted,
                FilesDeleted = filesDeleted
            };

            _logger.LogInformation("Deleted all data for user {UserId}: {@Summary}", userId, summary);

            return Ok(new 
            { 
                success = true, 
                data = new { 
                    message = "All user data has been successfully deleted",
                    summary = summary
                }, 
                error = (object?)null 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting all user data for user {UserId}", userId);
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

            _logger.LogInformation("Successfully deleted entire account for user {UserId}", userId);

            return Ok(new 
            { 
                success = true, 
                data = new { 
                    message = "Account has been successfully deleted" 
                }, 
                error = (object?)null 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting account for user {UserId}", userId);
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
                    profile.PurchasedCredits,
                    profile.CreatedAt,
                    profile.UpdatedAt,
                    HasTrainedModel = latestModel != null,
                    ModelTrainedAt = latestModel?.CompletedAt
                },
                Images = profile.ProcessedImages.Where(i => !i.IsDeleted).Select(i => new
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
                    TotalImages = profile.ProcessedImages.Count(i => !i.IsDeleted),
                    OriginalUploads = profile.ProcessedImages.Count(i => i.Style == ImageConstants.OriginalStyle && !i.IsDeleted),
                    GeneratedImages = profile.ProcessedImages.Count(i => i.IsGenerated && !i.IsDeleted),
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
            _logger.LogError(ex, "Error exporting data for user {UserId}", userId);
            return StatusCode(500, new { success = false, error = new { code = "ExportError", message = "Failed to export user data." } });
        }
    }

    /// <summary>
    /// Internal helper method for deleting all user data
    /// </summary>
    private async Task DeleteAllUserDataInternal(string userId, UserProfile profile)
    {
        // Delete all photos (mark as deleted and remove files)
        var allPhotos = profile.ProcessedImages.Where(i => !i.IsDeleted).ToList();
        var uploadDir = Path.Combine(_environment.ContentRootPath, "uploads", userId);
        
        foreach (var photo in allPhotos)
        {
            try
            {
                photo.IsDeleted = true;
                photo.DeletedAt = DateTime.UtcNow;
                photo.UserRequestedDeletionDate = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(photo.OriginalImageUrl))
                {
                    var fileName = Path.GetFileName(photo.OriginalImageUrl);
                    var filePath = Path.Combine(uploadDir, fileName);
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete photo {PhotoId} for user {UserId}", photo.Id, userId);
            }
        }

        // Delete upload directory
        try
        {
            if (Directory.Exists(uploadDir))
            {
                Directory.Delete(uploadDir, true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete upload directory for user {UserId}", userId);
        }

        // Delete AI model
        var trainedModel = await GetLatestTrainedModelAsync(userId);
        if (trainedModel != null && !string.IsNullOrEmpty(trainedModel.ReplicateModelId))
        {
            try
            {
                await _replicateApiClient.DeleteModelAsync(trainedModel.ReplicateModelId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete model from Replicate for user {UserId}", userId);
            }
        }

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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete training ZIP files for user {UserId}", userId);
        }

        // Delete usage logs
        _context.UsageLogs.RemoveRange(profile.UsageLogs);
    }
}

