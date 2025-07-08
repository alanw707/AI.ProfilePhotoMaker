using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;

namespace AI.ProfilePhotoMaker.API.Controllers
{
    /// <summary>
    /// Controller for handling image upload, retrieval, and management operations
    /// </summary>
    [Authorize]
    public class ImageController : BaseController
    {
        private readonly IUserProfileRepository _userProfileRepository;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly IUserContextService _userContextService;

        public ImageController(
            IUserProfileRepository userProfileRepository,
            IWebHostEnvironment environment,
            IConfiguration configuration,
            IUserContextService userContextService,
            ILogger<ImageController> logger,
            ApplicationDbContext context) 
            : base(logger, context)
        {
            _userProfileRepository = userProfileRepository;
            _environment = environment;
            _configuration = configuration;
            _userContextService = userContextService;
        }

        /// <summary>
        /// Gets available image styles
        /// </summary>
        [HttpGet("styles")]
        public async Task<IActionResult> GetStyles()
        {
            try
            {
                var styles = await Context.Styles
                    .Where(s => s.IsActive)
                    .Select(s => s.Name)
                    .ToListAsync();
                
                return SuccessResponse(styles);
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to get styles");
                return ErrorResponse("InternalError", "Failed to retrieve styles", 500);
            }
        }

        /// <summary>
        /// Uploads images with optional profile creation
        /// </summary>
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadImages([FromForm] UploadImagesDto dto)
        {
            // Validate input
            if (dto.Images == null || !dto.Images.Any())
                return ErrorResponse("NoImages", "No images provided");

            if (dto.Images.Count > 20)
                return ErrorResponse("TooManyImages", "Maximum 20 images allowed");

            // Validate authentication
            var authCheck = ValidateAuthentication();
            if (authCheck != null) return authCheck;
            var userId = GetCurrentUserId()!;

            var profile = await _userContextService.GetUserProfileAsync(userId);

            // Create profile if it doesn't exist
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
                        return ErrorResponse("InvalidImage", $"Invalid image file: {image.FileName}");
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
                        ProcessedImageUrl = relativeUrl,
                        Style = ProfileControllerConstants.OriginalStyle,
                        UserProfileId = profile.Id,
                        CreatedAt = DateTime.UtcNow,
                        IsOriginalUpload = true,
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

                return SuccessResponse(new
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
                LogError(ex, "Error uploading images", userId);
                return ErrorResponse("UploadFailed", "Failed to upload images", 500);
            }
        }

        /// <summary>
        /// Gets user's processed images with URLs
        /// </summary>
        [HttpGet("images")]
        public async Task<IActionResult> GetImages()
        {
            var authCheck = ValidateAuthentication();
            if (authCheck != null) return authCheck;
            var userId = GetCurrentUserId()!;

            // Direct query to ensure we get fresh data with FileExists
            var processedImages = await Context.ProcessedImages
                .Where(pi => pi.UserProfile.UserId == userId)
                .OrderByDescending(i => i.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            Logger.LogInformation("🔍 Direct query found {Count} images for user {UserId}", processedImages.Count, userId);

            var images = new List<object>();

            foreach (var i in processedImages)
            {
                // For generated images, construct local URL from generated folder
                // For uploaded images, use the stored paths
                string? originalUrl = null;
                string? processedUrl = null;
                
                if (i.IsGenerated && !string.IsNullOrEmpty(i.Style) && i.Style != "Original")
                {
                    // For generated images, use ProcessedImageUrl which now contains the full path
                    processedUrl = !string.IsNullOrEmpty(i.ProcessedImageUrl) ? 
                        (i.ProcessedImageUrl.StartsWith("http") ? i.ProcessedImageUrl : GetAbsoluteUrl(i.ProcessedImageUrl)) : 
                        null;
                }
                else
                {
                    // For uploaded images or other cases, use stored URLs
                    originalUrl = !string.IsNullOrEmpty(i.OriginalImageUrl) ? 
                        (i.OriginalImageUrl.StartsWith("http") ? i.OriginalImageUrl : GetAbsoluteUrl(i.OriginalImageUrl)) : 
                        null;
                        
                    processedUrl = !string.IsNullOrEmpty(i.ProcessedImageUrl) ? 
                        (i.ProcessedImageUrl.StartsWith("http") ? i.ProcessedImageUrl : GetAbsoluteUrl(i.ProcessedImageUrl)) : 
                        null;
                }
                
                var imageResponse = new
                {
                    i.Id,
                    OriginalImageUrl = originalUrl,
                    ProcessedImageUrl = processedUrl,
                    i.Style,
                    i.CreatedAt,
                    IsOriginalUpload = i.Style == "Original",
                    IsGenerated = i.IsGenerated
                    // Removed FileExists - we now use ProcessedImageUrl presence to determine if image is ready
                };

                images.Add(imageResponse);
            }

            var imageList = images.Cast<dynamic>().ToList();
            var summary = new
            {
                TotalImages = images.Count,
                OriginalUploads = imageList.Count(i => i.IsOriginalUpload),
                GeneratedImages = imageList.Count(i => i.IsGenerated && !i.IsOriginalUpload),
                Images = images
            };

            Logger.LogInformation("🔍 Returning {TotalImages} images, {GeneratedImages} generated images", 
                summary.TotalImages, summary.GeneratedImages);

            return SuccessResponse(summary);
        }

        /// <summary>
        /// Deletes a specific image
        /// </summary>
        [HttpDelete("images/{imageId}")]
        public async Task<IActionResult> DeleteImage(int imageId)
        {
            var authCheck = ValidateAuthentication();
            if (authCheck != null) return authCheck;
            var userId = GetCurrentUserId()!;

            var profile = await _userProfileRepository.GetByUserIdAsync(userId);

            if (profile == null)
            {
                LogInfo($"Profile not found for user {userId}");
                return ErrorResponse("ProfileNotFound", "Profile not found", 404);
            }
            
            var image = profile.ProcessedImages.FirstOrDefault(i => i.Id == imageId);
            if (image == null)
            {
                LogInfo($"Image {imageId} not found for user {userId}");
                return ErrorResponse("ImageNotFound", "Image not found", 404);
            }

            // Check if image is already deleted
            if (image.IsDeleted)
            {
                LogInfo($"Image {imageId} for user {userId} is already marked as deleted");
                return ErrorResponse("AlreadyDeleted", "Image is already deleted");
            }

            try
            {
                var physicalFileDeleted = false;
                
                // Delete physical file based on image type and storage location
                if (image.IsGenerated && !string.IsNullOrEmpty(image.ProcessedImageUrl))
                {
                    // Generated images are stored in /generated/{userId}/ directory
                    var fileName = Path.GetFileName(image.ProcessedImageUrl);
                    var generatedFilePath = Path.Combine(_environment.ContentRootPath, "generated", userId, fileName);
                    
                    Logger.LogDebug("Attempting to delete generated image file: {FilePath}", generatedFilePath);
                    
                    if (System.IO.File.Exists(generatedFilePath))
                    {
                        System.IO.File.Delete(generatedFilePath);
                        physicalFileDeleted = true;
                        Logger.LogInformation("Deleted generated image file: {FilePath}", generatedFilePath);
                    }
                }
                else if (image.IsOriginalUpload && !string.IsNullOrEmpty(image.OriginalImageUrl))
                {
                    // Original uploads are stored in /uploads/{userId}/ directory
                    var fileName = Path.GetFileName(image.OriginalImageUrl);
                    var uploadFilePath = Path.Combine(_environment.ContentRootPath, "uploads", userId, fileName);
                    
                    Logger.LogDebug("Attempting to delete uploaded image file: {FilePath}", uploadFilePath);
                    
                    if (System.IO.File.Exists(uploadFilePath))
                    {
                        System.IO.File.Delete(uploadFilePath);
                        physicalFileDeleted = true;
                        Logger.LogInformation("Deleted uploaded image file: {FilePath}", uploadFilePath);
                    }
                }

                // Mark image as deleted (soft delete)
                image.IsDeleted = true;
                image.DeletedAt = DateTime.UtcNow;
                image.UserRequestedDeletionDate = DateTime.UtcNow;

                await _userProfileRepository.UpdateAsync(profile);

                // Invalidate user cache
                await _userContextService.InvalidateUserCacheAsync(userId);

                return SuccessResponse(new { 
                    Message = "Image deleted successfully",
                    PhysicalFileDeleted = physicalFileDeleted
                });
            }
            catch (Exception ex)
            {
                LogError(ex, $"Error deleting image {imageId}", userId);
                return ErrorResponse("DeletionFailed", "Failed to delete image", 500);
            }
        }

        #region Helper Methods

        /// <summary>
        /// Validates if the uploaded file is a valid image
        /// </summary>
        private static bool IsValidImageFile(IFormFile file)
        {
            if (file.Length == 0 || file.Length > 10 * 1024 * 1024) // 10MB limit
                return false;

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(extension))
                return false;

            // Basic content type validation
            var allowedContentTypes = new[] { 
                "image/jpeg", "image/png", "image/webp", 
                "image/jpg", "image/pjpeg" 
            };
            
            return allowedContentTypes.Contains(file.ContentType.ToLowerInvariant());
        }

        /// <summary>
        /// Converts relative path to absolute URL
        /// </summary>
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

        /// <summary>
        /// Creates a training ZIP file from uploaded images
        /// TODO: Move this to a dedicated service class
        /// </summary>
        private string? CreateTrainingZip(string uploadDir, string userId)
        {
            try
            {
                var zipDir = Path.Combine(_environment.ContentRootPath, "training-zips");
                Directory.CreateDirectory(zipDir);

                var zipFileName = $"training_{userId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip";
                var zipPath = Path.Combine(zipDir, zipFileName);

                using (var zip = new System.IO.Compression.ZipArchive(
                    new FileStream(zipPath, FileMode.Create), 
                    ZipArchiveMode.Create))
                {
                    var files = Directory.GetFiles(uploadDir, "*.*")
                        .Where(f => IsImageFile(f))
                        .ToList();

                    foreach (var file in files)
                    {
                        var entryName = Path.GetFileName(file);
                        zip.CreateEntryFromFile(file, entryName);
                    }
                }

                return $"/training-zips/{zipFileName}";
            }
            catch (Exception ex)
            {
                LogError(ex, "Failed to create training ZIP", userId);
                return null;
            }
        }

        /// <summary>
        /// Checks if file is an image based on extension
        /// </summary>
        private static bool IsImageFile(string filePath)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return allowedExtensions.Contains(extension);
        }

        #endregion
    }

}