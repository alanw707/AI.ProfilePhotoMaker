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
    private readonly IImageProcessingService _imageProcessingService;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly ApplicationDbContext _context; // Keep for now for other operations
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ProfileController> _logger;
    private readonly IConfiguration _configuration;

    public ProfileController(
        IImageProcessingService imageProcessingService,
        IUserProfileRepository userProfileRepository,
        ApplicationDbContext context,
        IWebHostEnvironment environment,
        ILogger<ProfileController> logger,
        IConfiguration configuration)
    {
        _imageProcessingService = imageProcessingService;
        _userProfileRepository = userProfileRepository;
        _context = context;
        _environment = environment;
        _logger = logger;
        _configuration = configuration;
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
        var styles = await _imageProcessingService.GetAvailableStylesAsync();
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
                    CreatedAt = DateTime.UtcNow
                };

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
                zipPath = await CreateTrainingZip(uploadDir, userId);
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

            var processedImageUrl = await _imageProcessingService.GenerateImageAsync(dto);
            
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

        var images = profile.ProcessedImages
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new
            {
                i.Id,
                OriginalImageUrl = !string.IsNullOrEmpty(i.OriginalImageUrl) ? GetAbsoluteUrl(i.OriginalImageUrl) : i.OriginalImageUrl,
                ProcessedImageUrl = !string.IsNullOrEmpty(i.ProcessedImageUrl) ? GetAbsoluteUrl(i.ProcessedImageUrl) : i.ProcessedImageUrl,
                i.Style,
                i.CreatedAt,
                IsOriginalUpload = i.Style == "Original",
                IsGenerated = !string.IsNullOrEmpty(i.ProcessedImageUrl),
                FileExists = !string.IsNullOrEmpty(i.OriginalImageUrl) && 
                            System.IO.File.Exists(Path.Combine(_environment.ContentRootPath, i.OriginalImageUrl.TrimStart('/')))
            })
            .ToList();

        var summary = new
        {
            TotalImages = images.Count,
            OriginalUploads = images.Count(i => i.IsOriginalUpload),
            GeneratedImages = images.Count(i => i.IsGenerated && !i.IsOriginalUpload),
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
            // Get uploaded images (original style)
            var uploadedImages = profile.ProcessedImages.Where(i => i.Style == ProfileControllerConstants.OriginalStyle).ToList();
            
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

            var uploadDir = Path.Combine(_environment.ContentRootPath, "uploads", userId);
            if (!Directory.Exists(uploadDir))
            {
                return BadRequest(new { 
                    success = false, 
                    error = new { 
                        code = "NoUploadDirectory", 
                        message = "No uploaded images found" 
                    } 
                });
            }

            // Create training ZIP from existing uploaded images
            var zipPath = await CreateTrainingZip(uploadDir, userId);
            
            if (string.IsNullOrEmpty(zipPath))
            {
                return StatusCode(500, new { 
                    success = false, 
                    error = new { 
                        code = "ZipCreationFailed", 
                        message = "Failed to create training ZIP file" 
                    } 
                });
            }

            return Ok(new { 
                success = true, 
                zipCreated = true,
                zipPath = zipPath,
                message = $"Training ZIP created with {uploadedImages.Count} images"
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

    private async Task<string?> CreateTrainingZip(string uploadDir, string userId)
    {
        try
        {
            var zipPath = Path.Combine(_environment.ContentRootPath, "training-zips", $"{userId}_{DateTime.UtcNow:yyyyMMddHHmmss}.zip");
            Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);

            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var imageFiles = Directory.GetFiles(uploadDir, "*.*")
                    .Where(f =>
                    {
                        var extension = Path.GetExtension(f);
                        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                        return allowedExtensions.Contains(extension.ToLowerInvariant());
                    })
                    .ToArray();

                foreach (var file in imageFiles)
                {
                    archive.CreateEntryFromFile(file, Path.GetFileName(file));
                }
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
    /// Set the user's preferred style for image generation
    /// </summary>
    [HttpPost("set-style")]
    public async Task<IActionResult> SetUserStyle([FromBody] SetStyleRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            // Validate that the style exists and is active
            var style = await _context.Styles
                .FirstOrDefaultAsync(s => s.Id == request.StyleId && s.IsActive);

            if (style == null)
                return BadRequest(new { success = false, error = new { code = "StyleNotFound", message = "Style not found or inactive." } });

            // Get or create user profile
            var profile = await _userProfileRepository.GetByUserIdAsync(userId);

            if (profile == null)
                return BadRequest(new { success = false, error = new { code = "ProfileNotFound", message = "User profile not found. Please create a profile first." } });

            // Update user's style preference
            profile.StyleId = request.StyleId;
            profile.UpdatedAt = DateTime.UtcNow;

            await _userProfileRepository.UpdateAsync(profile);

            return Ok(new { 
                success = true, 
                data = new { 
                    styleId = style.Id, 
                    styleName = style.Name, 
                    styleDescription = style.Description 
                }, 
                error = (object?)null 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting user style");
            return StatusCode(500, new { success = false, error = new { code = "DatabaseError", message = "Failed to set user style." } });
        }
    }

    /// <summary>
    /// Get the user's current style preference
    /// </summary>
    [HttpGet("style")]
    public async Task<IActionResult> GetUserStyle()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var profile = await _context.UserProfiles
                .Include(p => p.Style)
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile?.Style == null)
                return Ok(new { success = true, data = (object?)null, error = (object?)null });

            return Ok(new { 
                success = true, 
                data = new { 
                    styleId = profile.Style.Id, 
                    styleName = profile.Style.Name, 
                    styleDescription = profile.Style.Description 
                }, 
                error = (object?)null 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user style");
            return StatusCode(500, new { success = false, error = new { code = "DatabaseError", message = "Failed to get user style." } });
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
}

/// <summary>
/// DTO for setting user style preference
/// </summary>
public record SetStyleRequest(int StyleId);