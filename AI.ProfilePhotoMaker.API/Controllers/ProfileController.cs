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

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IImageProcessingService _imageProcessingService;
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        IImageProcessingService imageProcessingService,
        ApplicationDbContext context,
        IWebHostEnvironment environment,
        ILogger<ProfileController> logger)
    {
        _imageProcessingService = imageProcessingService;
        _context = context;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var profile = await _context.UserProfiles
            .Include(p => p.ProcessedImages)
            .FirstOrDefaultAsync(p => p.UserId == userId);

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

        var existingProfile = await _context.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);

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

        _context.UserProfiles.Add(profile);
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

        var profile = await _context.UserProfiles
            .Include(p => p.ProcessedImages)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile == null)
            return NotFound("Profile not found");

        _context.ProcessedImages.RemoveRange(profile.ProcessedImages);
        _context.UserProfiles.Remove(profile);
        await _context.SaveChangesAsync();

        return NoContent();
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
            _context.UserProfiles.Add(profile);
            await _context.SaveChangesAsync();
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

                // Create database record for uploaded image
                var processedImage = new ProcessedImage
                {
                    OriginalImageUrl = relativeUrl,
                    ProcessedImageUrl = "", // Will be updated when AI processing completes
                    Style = "Original", // Mark as original upload
                    UserProfileId = profile.Id,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ProcessedImages.Add(processedImage);
                uploadedImages.Add(processedImage);

                uploadResults.Add(new { 
                    FileName = fileName, 
                    Size = image.Length,
                    Url = relativeUrl
                });
            }

            // Save all uploaded image records to database
            await _context.SaveChangesAsync();

            var zipPath = await CreateTrainingZip(uploadDir, userId);

            return Ok(new
            {
                ProfileId = profile.Id,
                UploadedFiles = uploadResults,
                UploadedImageIds = uploadedImages.Select(img => img.Id).ToList(),
                ZipCreated = !string.IsNullOrEmpty(zipPath),
                ZipPath = zipPath,
                Message = "Images uploaded successfully. Ready for training."
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

        var profile = await _context.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);

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

        var profile = await _context.UserProfiles
            .Include(p => p.ProcessedImages)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile == null)
            return NotFound("Profile not found");

        var images = profile.ProcessedImages
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new
            {
                i.Id,
                i.OriginalImageUrl,
                i.ProcessedImageUrl,
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

        var profile = await _context.UserProfiles
            .Include(p => p.ProcessedImages)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile == null)
            return NotFound("Profile not found");

        var uploadedImages = profile.ProcessedImages.Where(i => i.Style == "Original").ToList();
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

    [HttpDelete("images/{imageId}")]
    public async Task<IActionResult> DeleteImage(int imageId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var profile = await _context.UserProfiles
            .Include(p => p.ProcessedImages)
            .FirstOrDefaultAsync(p => p.UserId == userId);

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

            // Delete database record
            _context.ProcessedImages.Remove(image);
            await _context.SaveChangesAsync();

            return NoContent();
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
        
        return allowedExtensions.Contains(extension);
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
                    .Where(f => IsValidImageExtension(Path.GetExtension(f)))
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

    private static bool IsValidImageExtension(string extension)
    {
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        return allowedExtensions.Contains(extension.ToLowerInvariant());
    }
}