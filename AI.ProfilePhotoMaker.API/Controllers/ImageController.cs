using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using System.Security.Claims;

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
                        Style = ImageConstants.OriginalStyle,
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
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized();

            var profile = await _userProfileRepository.GetByUserIdAsync(userId);
            if (profile == null)
                return NotFound("Profile not found");

            var images = new List<object>();

            foreach (var i in profile.ProcessedImages.OrderByDescending(i => i.CreatedAt))
            {
                string? originalUrl = null;
                string? processedUrl = null;

                if (!string.IsNullOrEmpty(i.OriginalImageUrl))
                {
                    if (i.OriginalImageUrl.StartsWith("http"))
                    {
                        originalUrl = i.OriginalImageUrl;
                    }
                    else
                    {
                        originalUrl = GetAbsoluteUrl(i.OriginalImageUrl);
                    }
                }
                if (!string.IsNullOrEmpty(i.ProcessedImageUrl))
                {
                    processedUrl = i.ProcessedImageUrl.StartsWith("http") ? i.ProcessedImageUrl : GetAbsoluteUrl(i.ProcessedImageUrl);
                }

                images.Add(new
                {
                    i.Id,
                    OriginalImageUrl = originalUrl,
                    ProcessedImageUrl = processedUrl,
                    i.Style,
                    i.CreatedAt,
                    IsOriginalUpload = i.IsOriginalUpload,
                    i.IsGenerated
                });
            }

            var imageList = images.Cast<dynamic>().ToList();
            var summary = new
            {
                TotalImages = images.Count,
                OriginalUploads = imageList.Count(i => i.IsOriginalUpload),
                GeneratedImages = imageList.Count(i => i.IsGenerated && !i.IsOriginalUpload),
                Images = images
            };

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

        /// <summary>
        /// Converts relative path to absolute URL
        /// </summary>
        private string GetAbsoluteUrl(string relativePath)
        {
            try
            {
                // Use configured AppBaseUrl (ngrok) instead of localhost for external access
                var baseUrl = _configuration?["AppBaseUrl"];
                Logger.LogDebug("GetAbsoluteUrl called with: {RelativePath}, AppBaseUrl: {BaseUrl}", relativePath, baseUrl);
                
                if (!string.IsNullOrEmpty(baseUrl))
                {
                    var result = $"{baseUrl.TrimEnd('/')}{relativePath}";
                    Logger.LogDebug("GetAbsoluteUrl result: {Result}", result);
                    return result;
                }
                
                // Fallback to request host for local development
                var scheme = Request?.Scheme ?? "https";
                var host = Request?.Host.ToString() ?? "localhost";
                var fallbackResult = $"{scheme}://{host}{relativePath}";
                Logger.LogDebug("GetAbsoluteUrl fallback result: {Result}", fallbackResult);
                return fallbackResult;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "GetAbsoluteUrl failed for path: {RelativePath}", relativePath);
                // Return a safe fallback instead of null
                return $"https://localhost{relativePath}";
            }
        }

        /// <summary>
        /// Creates a training ZIP file from uploaded images
        /// TODO: Move this to a dedicated service class
        /// </summary>
        private string? CreateTrainingZip(string uploadDir, string userId)
        {
            try
            {
                var zipPath = Path.Combine(_environment.ContentRootPath, "training-zips", $"{userId}.zip");
                Directory.CreateDirectory(Path.GetDirectoryName(zipPath)!);

                // Delete existing ZIP file if it exists to avoid conflicts
                if (System.IO.File.Exists(zipPath))
                {
                    System.IO.File.Delete(zipPath);
                    Logger.LogInformation("Deleted existing training ZIP file before creating new one for user {UserId}", userId);
                }

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

                    if (imageFiles.Length < 10)
                    {
                        Logger.LogWarning("Insufficient images ({Count}) for training ZIP for user {UserId}", imageFiles.Length, userId);
                        return null;
                    }

                    foreach (var file in imageFiles)
                    {
                        archive.CreateEntryFromFile(file, Path.GetFileName(file));
                    }

                    Logger.LogInformation("Created training ZIP for user {UserId} with {FileCount} images", userId, imageFiles.Length);
                }

                return zipPath;
            }
            catch (Exception ex)
            {
                LogError(ex, "Error creating training ZIP", userId);
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

        /// <summary>
        /// Create training ZIP from existing uploaded images
        /// </summary>
        [HttpPost("create-training-zip")]
        public async Task<IActionResult> CreateTrainingZip()
        {
            var authCheck = ValidateAuthentication();
            if (authCheck != null) return authCheck;
            var userId = GetCurrentUserId()!;

            var profile = await _userProfileRepository.GetByUserIdAsync(userId);
            if (profile == null)
                return ErrorResponse("ProfileNotFound", "Profile not found", 404);

            try
            {
                // Get uploaded images count for response message
                var uploadedImages = profile.ProcessedImages.Where(i => i.Style == ImageConstants.OriginalStyle).ToList();
                
                var uploadDir = Path.Combine(_environment.ContentRootPath, "uploads", userId);
                
                // Create training ZIP from existing uploaded images (validation handled inside method)
                var zipPath = CreateTrainingZip(uploadDir, userId);
                
                if (string.IsNullOrEmpty(zipPath))
                {
                    // Check specific reasons for failure
                    if (uploadedImages.Count < 10)
                    {
                        return ErrorResponse("InsufficientImages", 
                            $"Need at least 10 images for training (currently {uploadedImages.Count})");
                    }
                    
                    if (!Directory.Exists(uploadDir))
                    {
                        return ErrorResponse("NoUploadDirectory", 
                            "Upload directory not found. Please upload images first.");
                    }
                    
                    return ErrorResponse("ZipCreationFailed", 
                        "Failed to create training ZIP file. Check that all uploaded images are still available.", 500);
                }

                return SuccessResponse(new { 
                    ZipCreated = true,
                    ZipPath = zipPath,
                    Message = $"Training ZIP created with all {uploadedImages.Count} uploaded original images"
                });
            }
            catch (Exception ex)
            {
                LogError(ex, "Error creating training ZIP", userId);
                return ErrorResponse("InternalError", "Error creating training ZIP", 500);
            }
        }

        /// <summary>
        /// Get list of available training ZIP files for the user with public URLs
        /// </summary>
        [HttpGet("training-zips")]
        public IActionResult GetTrainingZips()
        {
            try
            {
                var authCheck = ValidateAuthentication();
                if (authCheck != null) return authCheck;
                var userId = GetCurrentUserId()!;

                var trainingZipsPath = Path.Combine(_environment.ContentRootPath, "training-zips");
                
                if (!Directory.Exists(trainingZipsPath))
                {
                    return SuccessResponse(new List<object>());
                }

                var zipFilePath = Path.Combine(trainingZipsPath, $"{userId}.zip");
                var userZipFiles = new List<object>();
                
                if (System.IO.File.Exists(zipFilePath))
                {
                    var fileInfo = new FileInfo(zipFilePath);
                    var fileName = Path.GetFileName(zipFilePath);
                    var publicUrl = GetAbsoluteUrl($"/training-zips/{fileName}");
                    
                    userZipFiles.Add(new
                    {
                        fileName = fileName,
                        filePath = zipFilePath,
                        publicUrl = publicUrl,
                        createdAt = fileInfo.CreationTime,
                        sizeBytes = fileInfo.Length
                    });
                }

                return SuccessResponse(userZipFiles);
            }
            catch (Exception ex)
            {
                LogError(ex, "Error getting training ZIP files");
                return ErrorResponse("FileSystemError", "Failed to get training ZIP files.", 500);
            }
        }

        /// <summary>
        /// Get the most recent training ZIP public URL for the user
        /// </summary>
        [HttpGet("latest-training-zip")]
        public IActionResult GetLatestTrainingZip()
        {
            try
            {
                var authCheck = ValidateAuthentication();
                if (authCheck != null) return authCheck;
                var userId = GetCurrentUserId()!;

                var trainingZipsPath = Path.Combine(_environment.ContentRootPath, "training-zips");
                
                if (!Directory.Exists(trainingZipsPath))
                {
                    return ErrorResponse("NoZipFiles", "No training ZIP files found.", 404);
                }

                var zipFilePath = Path.Combine(trainingZipsPath, $"{userId}.zip");

                if (!System.IO.File.Exists(zipFilePath))
                {
                    return ErrorResponse("NoZipFiles", "No training ZIP files found for user.", 404);
                }

                var fileName = Path.GetFileName(zipFilePath);
                var publicUrl = GetAbsoluteUrl($"/training-zips/{fileName}");
                var fileInfo = new FileInfo(zipFilePath);

                return SuccessResponse(new { 
                    fileName = fileName,
                    publicUrl = publicUrl,
                    createdAt = fileInfo.CreationTime,
                    sizeBytes = fileInfo.Length
                });
            }
            catch (Exception ex)
            {
                LogError(ex, "Error getting latest training ZIP file");
                return ErrorResponse("FileSystemError", "Failed to get latest training ZIP file.", 500);
            }
        }

        /// <summary>
        /// Delete a specific training ZIP file by filename
        /// </summary>
        [HttpDelete("training-zips/{fileName}")]
        public IActionResult DeleteTrainingZip(string fileName)
        {
            try
            {
                var authCheck = ValidateAuthentication();
                if (authCheck != null) return authCheck;
                var userId = GetCurrentUserId()!;

                // Validate that the filename belongs to the current user
                if (fileName != $"{userId}.zip")
                {
                    return ErrorResponse("InvalidFileName", "Invalid filename or access denied.");
                }

                var trainingZipsPath = Path.Combine(_environment.ContentRootPath, "training-zips");
                var filePath = Path.Combine(trainingZipsPath, fileName);

                if (!System.IO.File.Exists(filePath))
                {
                    return ErrorResponse("FileNotFound", "Training ZIP file not found.", 404);
                }

                System.IO.File.Delete(filePath);
                
                Logger.LogInformation("Deleted training ZIP file {FileName} for user {UserId}", fileName, userId);

                return SuccessResponse(new { 
                    fileName = fileName,
                    message = "Training ZIP file deleted successfully." 
                });
            }
            catch (Exception ex)
            {
                LogError(ex, "Error deleting training ZIP file {FileName}", fileName);
                return ErrorResponse("FileSystemError", "Failed to delete training ZIP file.", 500);
            }
        }

        /// <summary>
        /// Delete all training ZIP files for the current user
        /// </summary>
        [HttpDelete("training-zips")]
        public IActionResult DeleteAllTrainingZips()
        {
            try
            {
                var authCheck = ValidateAuthentication();
                if (authCheck != null) return authCheck;
                var userId = GetCurrentUserId()!;

                var trainingZipsPath = Path.Combine(_environment.ContentRootPath, "training-zips");
                
                if (!Directory.Exists(trainingZipsPath))
                {
                    return SuccessResponse(new { deletedCount = 0, message = "No training ZIP files found." });
                }

                var zipFilePath = Path.Combine(trainingZipsPath, $"{userId}.zip");
                var deletedCount = 0;

                if (System.IO.File.Exists(zipFilePath))
                {
                    try
                    {
                        System.IO.File.Delete(zipFilePath);
                        deletedCount = 1;
                        Logger.LogInformation("Deleted training ZIP file {FilePath} for user {UserId}", zipFilePath, userId);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Failed to delete training ZIP file {FilePath} for user {UserId}", zipFilePath, userId);
                    }
                }

                return SuccessResponse(new { 
                    deletedCount = deletedCount,
                    message = $"Deleted {deletedCount} training ZIP files successfully." 
                });
            }
            catch (Exception ex)
            {
                LogError(ex, "Error deleting all training ZIP files");
                return ErrorResponse("FileSystemError", "Failed to delete training ZIP files.", 500);
            }
        }

        #endregion
    }

}