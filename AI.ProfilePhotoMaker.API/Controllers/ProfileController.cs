using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using System.Security.Claims;

namespace AI.ProfilePhotoMaker.API.Controllers;

public static class ProfileControllerConstants
{
    public const string OriginalStyle = "Original";
}

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

        var profileDto = new UserProfileDto
        {
            Id = profile.Id,
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            Gender = profile.Gender,
            Ethnicity = profile.Ethnicity,
            TrainedModelId = profile.TrainedModelId,
            TrainedModelVersionId = profile.TrainedModelVersionId,
            ModelTrainedAt = profile.ModelTrainedAt,
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

        var profileDto = new UserProfileDto
        {
            Id = profile.Id,
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            Gender = profile.Gender,
            Ethnicity = profile.Ethnicity,
            TrainedModelId = profile.TrainedModelId,
            TrainedModelVersionId = profile.TrainedModelVersionId,
            ModelTrainedAt = profile.ModelTrainedAt,
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

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImages([FromForm] UploadImagesDto dto)
    {
        if (dto.Images == null || !dto.Images.Any())
            return BadRequest("No images provided");

        if (dto.Images.Count > 10)
            return BadRequest("Maximum 10 images allowed");

        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var profile = await _context.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile == null)
        {
            profile = new UserProfile
            {
                UserId = userId,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Gender = dto.Gender,
                Ethnicity = dto.Ethnicity
            };
            await _userProfileRepository.AddAsync(profile);
        }

        try
        {
            var uploadResults = new List<object>();
            var uploadedImages = new List<ProcessedImage>();
            var uploadDir = Path.Combine(_environment.ContentRootPath, "uploads", userId);
            Directory.CreateDirectory(uploadDir);

            foreach (var image in dto.Images)
            {
                if (!IsValidImageFile(image))
                {
                    return BadRequest($"Invalid image file: {image.FileName}");
                }

                var fileName = $"{Guid.NewGuid()}_{image.FileName}";
                var filePath = Path.Combine(uploadDir, fileName);
                var relativeUrl = $"/uploads/{userId}/{fileName}";

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                // Create database record for uploaded image - store relative path
                var processedImage = new ProcessedImage
                {
                    OriginalImageUrl = relativeUrl,  // Store relative path instead of absolute URL
                    ProcessedImageUrl = "", // Will be updated when AI processing completes
                    Style = ProfileControllerConstants.OriginalStyle, // Mark as original upload
                    UserProfileId = profile.Id,
                    CreatedAt = DateTime.UtcNow,
                    IsOriginalUpload = true, // Mark as original upload for retention policy
                    IsGenerated = false
                };
                
                // Set scheduled deletion date based on retention policy
                processedImage.SetScheduledDeletionDate();

                profile.ProcessedImages.Add(processedImage);
                uploadedImages.Add(processedImage);

                uploadResults.Add(new { 
                    FileName = fileName, 
                    Size = image.Length,
                    Url = GetAbsoluteUrl(relativeUrl)
                });
            }

            // Save all uploaded image records to database
            await _userProfileRepository.UpdateAsync(profile);

            string? zipPath = null;
            if (dto.ForTraining)
            {
                zipPath = CreateTrainingZip(uploadDir, userId);
            }

            return Ok(new
            {
                ProfileId = profile.Id,
                UploadedFiles = uploadResults,
                UploadedImageIds = uploadedImages.Select(img => img.Id).ToList(),
                ZipCreated = !string.IsNullOrEmpty(zipPath),
                ZipPath = zipPath,
                Message = dto.ForTraining ? "Images uploaded and zipped for training." : "Images uploaded successfully."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading images for user {UserId}", userId);
            return StatusCode(500, "Error processing images");
        }
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

        if (string.IsNullOrEmpty(profile.TrainedModelId))
            return BadRequest("No trained model available. Please upload training images first.");

        try
        {
            dto.UserId = userId;
            dto.TrainedModelVersion = profile.TrainedModelId;
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

    [HttpGet("images")]
    public async Task<IActionResult> GetImages()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var profile = await _userProfileRepository.GetByUserIdAsync(userId);

        if (profile == null)
            return NotFound("Profile not found");

        var images = new List<object>();
        var expiredImages = new List<ProcessedImage>();

        foreach (var i in profile.ProcessedImages.OrderByDescending(i => i.CreatedAt))
        {
            var originalUrl = !string.IsNullOrEmpty(i.OriginalImageUrl) ? (i.OriginalImageUrl.StartsWith("http") ? i.OriginalImageUrl : GetAbsoluteUrl(i.OriginalImageUrl)) : i.OriginalImageUrl;
            var processedUrl = !string.IsNullOrEmpty(i.ProcessedImageUrl) ? (i.ProcessedImageUrl.StartsWith("http") ? i.ProcessedImageUrl : GetAbsoluteUrl(i.ProcessedImageUrl)) : i.ProcessedImageUrl;
            
            // Check if local files exist
            var localFileExists = !string.IsNullOrEmpty(i.OriginalImageUrl) && 
                                 !i.OriginalImageUrl.StartsWith("http") &&
                                 System.IO.File.Exists(Path.Combine(_environment.ContentRootPath, i.OriginalImageUrl.TrimStart('/')));
            
            // Check if external URLs are still valid (for generated images)
            var urlValid = true;
            if (i.IsGenerated && !string.IsNullOrEmpty(processedUrl) && processedUrl.StartsWith("http"))
            {
                urlValid = await IsUrlValidAsync(processedUrl);
                if (!urlValid)
                {
                    // Mark for deletion if URL is expired
                    expiredImages.Add(i);
                    continue; // Skip adding to results
                }
            }
            
            images.Add(new
            {
                i.Id,
                OriginalImageUrl = originalUrl,
                ProcessedImageUrl = processedUrl,
                i.Style,
                i.CreatedAt,
                IsOriginalUpload = i.Style == "Original",
                IsGenerated = i.IsGenerated,
                FileExists = localFileExists || urlValid
            });
        }
        
        // Clean up expired images from database
        if (expiredImages.Any())
        {
            _context.ProcessedImages.RemoveRange(expiredImages);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Cleaned up {expiredImages.Count} expired images for user {userId}");
        }

        var imageList = images.Cast<dynamic>().ToList();
        var summary = new
        {
            TotalImages = images.Count,
            OriginalUploads = imageList.Count(i => i.IsOriginalUpload),
            GeneratedImages = imageList.Count(i => i.IsGenerated && !i.IsOriginalUpload),
            Images = images
        };

        return Ok(summary);
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

        var uploadedImages = profile.ProcessedImages.Where(i => i.Style == ProfileControllerConstants.OriginalStyle).ToList();
        var zipPath = Path.Combine(_environment.ContentRootPath, "training-zips", $"{userId}_*.zip");
        var zipFiles = Directory.GetFiles(Path.GetDirectoryName(zipPath)!, Path.GetFileName(zipPath));

        return Ok(new
        {
            ProfileId = profile.Id,
            HasTrainedModel = !string.IsNullOrEmpty(profile.TrainedModelId),
            TrainedModelId = profile.TrainedModelId,
            ModelTrainedAt = profile.ModelTrainedAt,
            TotalUploadedImages = uploadedImages.Count,
            LatestZipFile = zipFiles.OrderByDescending(f => System.IO.File.GetCreationTime(f)).FirstOrDefault(),
            CanStartTraining = uploadedImages.Count >= 4, // Minimum 4 images for training
            Status = uploadedImages.Count switch
            {
                0 => "No images uploaded",
                < 4 => $"Need at least 4 images (currently {uploadedImages.Count})",
                >= 4 when string.IsNullOrEmpty(profile.TrainedModelId) => "Ready for training",
                >= 4 when !string.IsNullOrEmpty(profile.TrainedModelId) => "Model trained - ready for generation",
                _ => "Unknown status"
            }
        });
    }

    /// <summary>
    /// Create training ZIP from existing uploaded images
    /// </summary>
    [HttpPost("create-training-zip")]
    public async Task<IActionResult> CreateTrainingZip()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var profile = await _userProfileRepository.GetByUserIdAsync(userId);
        if (profile == null)
            return NotFound("Profile not found");

        try
        {
            // Get uploaded images count for response message
            var uploadedImages = profile.ProcessedImages.Where(i => i.Style == ProfileControllerConstants.OriginalStyle).ToList();
            
            var uploadDir = Path.Combine(_environment.ContentRootPath, "uploads", userId);
            
            // Create training ZIP from existing uploaded images (validation handled inside method)
            var zipPath = CreateTrainingZip(uploadDir, userId);
            
            if (string.IsNullOrEmpty(zipPath))
            {
                // Check specific reasons for failure
                if (uploadedImages.Count < 4)
                {
                    return BadRequest(new { 
                        success = false, 
                        error = new { 
                            code = "InsufficientImages", 
                            message = $"Need at least 4 images for training (currently {uploadedImages.Count})" 
                        } 
                    });
                }
                
                if (!Directory.Exists(uploadDir))
                {
                    return BadRequest(new { 
                        success = false, 
                        error = new { 
                            code = "NoUploadDirectory", 
                            message = "Upload directory not found. Please upload images first." 
                        } 
                    });
                }
                
                return StatusCode(500, new { 
                    success = false, 
                    error = new { 
                        code = "ZipCreationFailed", 
                        message = "Failed to create training ZIP file. Check that all uploaded images are still available." 
                    } 
                });
            }

            return Ok(new { 
                success = true, 
                zipCreated = true,
                zipPath = zipPath,
                message = $"Training ZIP created with all {uploadedImages.Count} uploaded original images"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating training ZIP for user {UserId}", userId);
            return StatusCode(500, new { 
                success = false, 
                error = new { 
                    code = "InternalError", 
                    message = "Error creating training ZIP" 
                } 
            });
        }
    }

    [HttpDelete("images/{imageId}")]
    public async Task<IActionResult> DeleteImage(int imageId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var profile = await _userProfileRepository.GetByUserIdAsync(userId);

        if (profile == null)
            return NotFound("Profile not found");

        var image = profile.ProcessedImages.FirstOrDefault(i => i.Id == imageId);
        if (image == null)
            return NotFound("Image not found");

        try
        {
            // Delete physical file if it exists
            if (!string.IsNullOrEmpty(image.OriginalImageUrl))
            {
                var filePath = Path.Combine(_environment.ContentRootPath, "uploads", userId, 
                    Path.GetFileName(image.OriginalImageUrl));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            // Delete database record - remove from profile and update
            profile.ProcessedImages.Remove(image);
            await _userProfileRepository.UpdateAsync(profile);

            return Ok(new { success = true, message = "Image deleted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting image {ImageId} for user {UserId}", imageId, userId);
            return StatusCode(500, "Error deleting image");
        }
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private static bool IsValidImageFile(IFormFile file)
    {
        if (file.Length == 0 || file.Length > 10 * 1024 * 1024) // 10MB limit
            return false;

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        
        if (!allowedExtensions.Contains(extension))
            return false;

        // File signature validation for all types
        using (var reader = new BinaryReader(file.OpenReadStream()))
        {
            var signatures = new Dictionary<string, List<byte[]>>
            {
                { ".jpg", new List<byte[]> { 
                    new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, // JPEG JFIF
                    new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 }, // JPEG EXIF
                    new byte[] { 0xFF, 0xD8, 0xFF, 0xE8 }, // JPEG SPIFF
                    new byte[] { 0xFF, 0xD8, 0xFF, 0xDB }  // JPEG raw
                }},
                { ".jpeg", new List<byte[]> { 
                    new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, // JPEG JFIF
                    new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 }, // JPEG EXIF
                    new byte[] { 0xFF, 0xD8, 0xFF, 0xE8 }, // JPEG SPIFF
                    new byte[] { 0xFF, 0xD8, 0xFF, 0xDB }  // JPEG raw
                }},
                { ".png", new List<byte[]> { new byte[] { 0x89, 0x50, 0x4E, 0x47 } }},
                { ".webp", new List<byte[]> { new byte[] { 0x52, 0x49, 0x46, 0x46 } }}
            };

            var headerBytes = reader.ReadBytes(signatures.Values.SelectMany(list => list).Max(sig => sig.Length));

            return signatures.Any(kvp => 
                kvp.Key == extension && 
                kvp.Value.Any(sig => headerBytes.Take(sig.Length).SequenceEqual(sig)));
        }
    }

    private string? CreateTrainingZip(string uploadDir, string userId)
    {
        try
        {
            var zipPath = Path.Combine(_environment.ContentRootPath, "training-zips", $"{userId}_{DateTime.UtcNow:yyyyMMddHHmmss}.zip");
            Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);

            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                // Get all image files from the upload directory (only contains original uploads)
                var imageFiles = Directory.GetFiles(uploadDir, "*.*")
                    .Where(f =>
                    {
                        var extension = Path.GetExtension(f);
                        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                        return allowedExtensions.Contains(extension.ToLowerInvariant());
                    })
                    .ToArray();

                if (imageFiles.Length < 4)
                {
                    _logger.LogWarning("Insufficient images ({Count}) for training ZIP for user {UserId}", imageFiles.Length, userId);
                    return null;
                }

                foreach (var file in imageFiles)
                {
                    archive.CreateEntryFromFile(file, Path.GetFileName(file));
                }

                _logger.LogInformation("Created training ZIP for user {UserId} with {FileCount} images", userId, imageFiles.Length);
            }

            return zipPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating training ZIP for user {UserId}", userId);
            return null;
        }
    }


    /// <summary>
    /// Get list of available training ZIP files for the user with public URLs
    /// </summary>
    [HttpGet("training-zips")]
    public async Task<IActionResult> GetTrainingZips()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var trainingZipsPath = Path.Combine(_environment.ContentRootPath, "training-zips");
            
            if (!Directory.Exists(trainingZipsPath))
            {
                return Ok(new { success = true, data = new List<object>(), error = (object?)null });
            }

            var userZipFiles = Directory.GetFiles(trainingZipsPath, $"{userId}_*.zip")
                .Select(filePath => 
                {
                    var fileName = Path.GetFileName(filePath);
                    var fileInfo = new FileInfo(filePath);
                    var publicUrl = GetAbsoluteUrl($"/training-zips/{fileName}");
                    
                    return new
                    {
                        fileName = fileName,
                        filePath = filePath,
                        publicUrl = publicUrl,
                        createdAt = fileInfo.CreationTime,
                        sizeBytes = fileInfo.Length
                    };
                })
                .OrderByDescending(f => f.createdAt)
                .ToList();

            return Ok(new { 
                success = true, 
                data = userZipFiles, 
                error = (object?)null 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting training ZIP files for user");
            return StatusCode(500, new { success = false, error = new { code = "FileSystemError", message = "Failed to get training ZIP files." } });
        }
    }

    /// <summary>
    /// Get the most recent training ZIP public URL for the user
    /// </summary>
    [HttpGet("latest-training-zip")]
    public async Task<IActionResult> GetLatestTrainingZip()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var trainingZipsPath = Path.Combine(_environment.ContentRootPath, "training-zips");
            
            if (!Directory.Exists(trainingZipsPath))
            {
                return NotFound(new { success = false, error = new { code = "NoZipFiles", message = "No training ZIP files found." } });
            }

            var latestZipFile = Directory.GetFiles(trainingZipsPath, $"{userId}_*.zip")
                .Select(filePath => new { filePath, createdAt = new FileInfo(filePath).CreationTime })
                .OrderByDescending(f => f.createdAt)
                .FirstOrDefault();

            if (latestZipFile == null)
            {
                return NotFound(new { success = false, error = new { code = "NoZipFiles", message = "No training ZIP files found for user." } });
            }

            var fileName = Path.GetFileName(latestZipFile.filePath);
            var publicUrl = GetAbsoluteUrl($"/training-zips/{fileName}");
            var fileInfo = new FileInfo(latestZipFile.filePath);

            return Ok(new { 
                success = true, 
                data = new { 
                    fileName = fileName,
                    publicUrl = publicUrl,
                    createdAt = fileInfo.CreationTime,
                    sizeBytes = fileInfo.Length
                }, 
                error = (object?)null 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest training ZIP file for user");
            return StatusCode(500, new { success = false, error = new { code = "FileSystemError", message = "Failed to get latest training ZIP file." } });
        }
    }

    /// <summary>
    /// Delete a specific training ZIP file by filename
    /// </summary>
    [HttpDelete("training-zips/{fileName}")]
    public async Task<IActionResult> DeleteTrainingZip(string fileName)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            // Validate that the filename belongs to the current user
            if (!fileName.StartsWith($"{userId}_") || !fileName.EndsWith(".zip"))
            {
                return BadRequest(new { success = false, error = new { code = "InvalidFileName", message = "Invalid filename or access denied." } });
            }

            var trainingZipsPath = Path.Combine(_environment.ContentRootPath, "training-zips");
            var filePath = Path.Combine(trainingZipsPath, fileName);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { success = false, error = new { code = "FileNotFound", message = "Training ZIP file not found." } });
            }

            System.IO.File.Delete(filePath);
            
            _logger.LogInformation("Deleted training ZIP file {FileName} for user {UserId}", fileName, userId);

            return Ok(new { 
                success = true, 
                data = new { 
                    fileName = fileName,
                    message = "Training ZIP file deleted successfully." 
                }, 
                error = (object?)null 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting training ZIP file {FileName} for user", fileName);
            return StatusCode(500, new { success = false, error = new { code = "FileSystemError", message = "Failed to delete training ZIP file." } });
        }
    }

    /// <summary>
    /// Delete all training ZIP files for the current user
    /// </summary>
    [HttpDelete("training-zips")]
    public async Task<IActionResult> DeleteAllTrainingZips()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var trainingZipsPath = Path.Combine(_environment.ContentRootPath, "training-zips");
            
            if (!Directory.Exists(trainingZipsPath))
            {
                return Ok(new { success = true, data = new { deletedCount = 0, message = "No training ZIP files found." }, error = (object?)null });
            }

            var userZipFiles = Directory.GetFiles(trainingZipsPath, $"{userId}_*.zip");
            var deletedCount = 0;

            foreach (var filePath in userZipFiles)
            {
                try
                {
                    System.IO.File.Delete(filePath);
                    deletedCount++;
                    _logger.LogInformation("Deleted training ZIP file {FilePath} for user {UserId}", filePath, userId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete training ZIP file {FilePath} for user {UserId}", filePath, userId);
                }
            }

            return Ok(new { 
                success = true, 
                data = new { 
                    deletedCount = deletedCount,
                    message = $"Deleted {deletedCount} training ZIP files successfully." 
                }, 
                error = (object?)null 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting all training ZIP files for user");
            return StatusCode(500, new { success = false, error = new { code = "FileSystemError", message = "Failed to delete training ZIP files." } });
        }
    }

    /// <summary>
    /// Checks and updates the user's model status by verifying if the model still exists on Replicate
    /// </summary>
    [HttpPost("check-model-status")]
    public async Task<IActionResult> CheckModelStatus()
    {
        try
        {
            var userId = GetCurrentUserId();
            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(u => u.UserId == userId);
            
            if (userProfile == null)
                return NotFound(new { success = false, error = new { code = "UserNotFound", message = "User profile not found." } });

            // If user doesn't have a trained model, nothing to check
            if (string.IsNullOrEmpty(userProfile.TrainedModelId))
            {
                return Ok(new { 
                    success = true, 
                    data = new { 
                        modelExists = false, 
                        modelStatus = "no_model",
                        message = "No trained model found." 
                    }, 
                    error = (object?)null 
                });
            }

            // Check if model exists on Replicate
            bool modelExists = await _replicateApiClient.CheckModelExistsAsync(userProfile.TrainedModelId);
            
            if (!modelExists)
            {
                // Model was deleted from Replicate, clear it from our database
                _logger.LogWarning("Model {ModelId} for user {UserId} no longer exists on Replicate, clearing from database", 
                    userProfile.TrainedModelId, userId);
                
                userProfile.TrainedModelId = null;
                userProfile.TrainedModelVersionId = null;
                userProfile.ModelTrainedAt = null;
                userProfile.UpdatedAt = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();
                
                return Ok(new { 
                    success = true, 
                    data = new { 
                        modelExists = false, 
                        modelStatus = "deleted",
                        message = "Model was deleted from Replicate and cleared from database." 
                    }, 
                    error = (object?)null 
                });
            }
            
            return Ok(new { 
                success = true, 
                data = new { 
                    modelExists = true, 
                    modelStatus = "active",
                    modelId = userProfile.TrainedModelId,
                    message = "Model exists and is accessible." 
                }, 
                error = (object?)null 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking model status for user");
            return StatusCode(500, new { success = false, error = new { code = "ModelStatusCheckFailed", message = "Failed to check model status." } });
        }
    }

    private string GetAbsoluteUrl(string relativePath)
    {
        // Use configured AppBaseUrl (ngrok) instead of localhost for external access
        var baseUrl = _configuration["AppBaseUrl"];
        if (!string.IsNullOrEmpty(baseUrl))
        {
            return $"{baseUrl.TrimEnd('/')}{relativePath}";
        }
        
        // Fallback to request host for local development
        return $"{Request.Scheme}://{Request.Host}{relativePath}";
    }
    
    private async Task<bool> IsUrlValidAsync(string url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith("http"))
            return false;
            
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            
            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "URL validation failed for {Url}", url);
            return false;
        }
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

            var inputPhotos = profile.ProcessedImages.Where(i => i.Style == ProfileControllerConstants.OriginalStyle && !i.IsDeleted).Count();
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
                HasTrainedModel = !string.IsNullOrEmpty(profile.TrainedModelId),
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
                .Where(i => i.Style == ProfileControllerConstants.OriginalStyle && !i.IsDeleted)
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

            if (string.IsNullOrEmpty(profile.TrainedModelId))
            {
                return BadRequest(new { success = false, error = new { code = "NoModel", message = "No trained model found to delete." } });
            }

            var modelId = profile.TrainedModelId;

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

            // Clear model information from database
            profile.TrainedModelId = null;
            profile.TrainedModelVersionId = null;
            profile.ModelTrainedAt = null;
            profile.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Delete training ZIP files
            try
            {
                var trainingZipsPath = Path.Combine(_environment.ContentRootPath, "training-zips");
                if (Directory.Exists(trainingZipsPath))
                {
                    var userZipFiles = Directory.GetFiles(trainingZipsPath, $"{userId}_*.zip");
                    foreach (var zipFile in userZipFiles)
                    {
                        System.IO.File.Delete(zipFile);
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

            // Delete AI model if exists
            var modelDeleted = false;
            if (!string.IsNullOrEmpty(profile.TrainedModelId))
            {
                try
                {
                    await _replicateApiClient.DeleteModelAsync(profile.TrainedModelId);
                    modelDeleted = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete model from Replicate for user {UserId}", userId);
                }

                // Clear model information from database
                profile.TrainedModelId = null;
                profile.TrainedModelVersionId = null;
                profile.ModelTrainedAt = null;
            }

            // Delete training ZIP files
            try
            {
                var trainingZipsPath = Path.Combine(_environment.ContentRootPath, "training-zips");
                if (Directory.Exists(trainingZipsPath))
                {
                    var userZipFiles = Directory.GetFiles(trainingZipsPath, $"{userId}_*.zip");
                    foreach (var zipFile in userZipFiles)
                    {
                        System.IO.File.Delete(zipFile);
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
                    HasTrainedModel = !string.IsNullOrEmpty(profile.TrainedModelId),
                    ModelTrainedAt = profile.ModelTrainedAt
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
                    OriginalUploads = profile.ProcessedImages.Count(i => i.Style == ProfileControllerConstants.OriginalStyle && !i.IsDeleted),
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
        if (!string.IsNullOrEmpty(profile.TrainedModelId))
        {
            try
            {
                await _replicateApiClient.DeleteModelAsync(profile.TrainedModelId);
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
                var userZipFiles = Directory.GetFiles(trainingZipsPath, $"{userId}_*.zip");
                foreach (var zipFile in userZipFiles)
                {
                    System.IO.File.Delete(zipFile);
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

