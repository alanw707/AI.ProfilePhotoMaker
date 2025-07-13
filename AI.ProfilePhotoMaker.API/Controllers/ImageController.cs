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

                    // Generate clean filename for uploaded selfies
                    var extension = Path.GetExtension(image.FileName);
                    var fileName = $"{Guid.NewGuid()}_selfie{extension}";
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

            // No need to check IsDeleted since we're using hard delete

            try
            {
                var physicalFileDeleted = false;
                var imageCountBefore = profile.ProcessedImages.Count;
                
                Logger.LogInformation("Starting deletion of image {ImageId} for user {UserId}. Profile has {ImageCount} images", 
                    imageId, userId, imageCountBefore);
                
                // Delete physical file based on image type and storage location
                if (image.IsGenerated && !string.IsNullOrEmpty(image.ProcessedImageUrl))
                {
                    try
                    {
                        // Generated images are stored in /generated/{userId}/ directory
                        var fileName = Path.GetFileName(image.ProcessedImageUrl);
                        var generatedFilePath = Path.Combine(_environment.ContentRootPath, "generated", userId, fileName);
                        
                        Logger.LogDebug("Attempting to delete generated image file: {FilePath}", generatedFilePath);
                        Logger.LogDebug("Processed URL: {ProcessedUrl}, Extracted filename: {FileName}, UserId: {UserId}", 
                            image.ProcessedImageUrl, fileName, userId);
                        
                        // Validate path length and characters
                        if (generatedFilePath.Length > 260)
                        {
                            Logger.LogWarning("Generated file path too long ({Length} chars): {FilePath}", generatedFilePath.Length, generatedFilePath);
                        }
                        
                        if (System.IO.File.Exists(generatedFilePath))
                        {
                            System.IO.File.Delete(generatedFilePath);
                            physicalFileDeleted = true;
                            Logger.LogInformation("Deleted generated image file: {FilePath}", generatedFilePath);
                        }
                        else
                        {
                            Logger.LogWarning("Generated image file not found: {FilePath}", generatedFilePath);
                        }
                    }
                    catch (Exception fileEx)
                    {
                        Logger.LogError(fileEx, "Error deleting generated image file for image {ImageId}. URL: {ProcessedUrl}, UserId: {UserId}", 
                            imageId, image.ProcessedImageUrl, userId);
                        // Continue with database deletion even if file deletion fails
                    }
                }
                else if (image.IsOriginalUpload && !string.IsNullOrEmpty(image.OriginalImageUrl))
                {
                    try
                    {
                        // Original uploads are stored in /uploads/{userId}/ directory
                        var fileName = Path.GetFileName(image.OriginalImageUrl);
                        var uploadFilePath = Path.Combine(_environment.ContentRootPath, "uploads", userId, fileName);
                        
                        Logger.LogDebug("Attempting to delete uploaded image file: {FilePath}", uploadFilePath);
                        Logger.LogDebug("Original URL: {OriginalUrl}, Extracted filename: {FileName}, UserId: {UserId}", 
                            image.OriginalImageUrl, fileName, userId);
                        
                        // Validate path length and characters
                        if (uploadFilePath.Length > 260)
                        {
                            Logger.LogWarning("File path too long ({Length} chars): {FilePath}", uploadFilePath.Length, uploadFilePath);
                        }
                        
                        if (System.IO.File.Exists(uploadFilePath))
                        {
                            System.IO.File.Delete(uploadFilePath);
                            physicalFileDeleted = true;
                            Logger.LogInformation("Deleted uploaded image file: {FilePath}", uploadFilePath);
                        }
                        else
                        {
                            Logger.LogWarning("Uploaded image file not found: {FilePath}", uploadFilePath);
                        }
                    }
                    catch (Exception fileEx)
                    {
                        Logger.LogError(fileEx, "Error deleting uploaded image file for image {ImageId}. URL: {OriginalUrl}, UserId: {UserId}", 
                            imageId, image.OriginalImageUrl, userId);
                        // Continue with database deletion even if file deletion fails
                    }
                }

                // Hard delete: Remove from database immediately (no soft delete)
                Logger.LogInformation("Removing image {ImageId} from profile collection", imageId);
                var removeResult = profile.ProcessedImages.Remove(image);
                
                if (!removeResult)
                {
                    Logger.LogError("Failed to remove image {ImageId} from profile collection - image not found in collection", imageId);
                    return ErrorResponse("RemoveFromCollectionFailed", "Failed to remove image from profile collection", 500);
                }
                
                var imageCountAfterRemove = profile.ProcessedImages.Count;
                Logger.LogInformation("Image removed from collection. Count changed from {Before} to {After}", 
                    imageCountBefore, imageCountAfterRemove);

                // Save changes to database with explicit transaction handling
                Logger.LogInformation("Saving profile changes to database for user {UserId}", userId);
                await _userProfileRepository.UpdateAsync(profile);
                
                // Verify deletion worked by querying directly from database context (bypasses EF tracking)
                var imageStillExists = await Context.ProcessedImages
                    .AsNoTracking()
                    .AnyAsync(i => i.Id == imageId);
                
                if (imageStillExists)
                {
                    Logger.LogError("Database deletion verification failed - image {ImageId} still exists after save", imageId);
                    return ErrorResponse("DeletionVerificationFailed", "Image deletion could not be verified in database", 500);
                }
                
                Logger.LogInformation("Database deletion verified - image {ImageId} successfully removed", imageId);

                // Invalidate user cache
                await _userContextService.InvalidateUserCacheAsync(userId);

                return SuccessResponse(new { 
                    Message = "Image deleted successfully",
                    PhysicalFileDeleted = physicalFileDeleted,
                    DatabaseVerified = true,
                    ImageCountBefore = imageCountBefore,
                    ImageCountAfter = imageCountAfterRemove
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
        /// Handles ngrok proxy setup and production environments
        /// </summary>
        private string GetAbsoluteUrl(string relativePath)
        {
            try
            {
                // Ensure relative path starts with /
                if (!relativePath.StartsWith("/"))
                {
                    relativePath = "/" + relativePath;
                }
                
                // Priority 1: Use X-Forwarded-Host header (ngrok proxy context)
                var forwardedHost = Request?.Headers["X-Forwarded-Host"].FirstOrDefault();
                if (!string.IsNullOrEmpty(forwardedHost))
                {
                    var forwardedScheme = Request?.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? "https";
                    var result = $"{forwardedScheme}://{forwardedHost}{relativePath}";
                    Logger.LogDebug("GetAbsoluteUrl using forwarded headers: {Result}", result);
                    return result;
                }
                
                // Priority 2: Use configured AppBaseUrl (for development/production)
                var baseUrl = _configuration?["AppBaseUrl"];
                if (!string.IsNullOrEmpty(baseUrl))
                {
                    var result = $"{baseUrl.TrimEnd('/')}{relativePath}";
                    Logger.LogDebug("GetAbsoluteUrl using AppBaseUrl: {Result}", result);
                    return result;
                }
                
                // Priority 3: Fallback to request host (localhost development)
                var scheme = Request?.Scheme ?? "https";
                var host = Request?.Host.ToString() ?? "localhost:5035";
                var fallbackResult = $"{scheme}://{host}{relativePath}";
                Logger.LogDebug("GetAbsoluteUrl using request host fallback: {Result}", fallbackResult);
                return fallbackResult;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "GetAbsoluteUrl failed for path: {RelativePath}", relativePath);
                // Return a safe fallback that works with proxy
                var safeBaseUrl = _configuration?["AppBaseUrl"] ?? "https://localhost:5035";
                return $"{safeBaseUrl.TrimEnd('/')}{relativePath}";
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

        /// <summary>
        /// Debug endpoint to test URL generation in different environments
        /// </summary>
        [HttpGet("debug/url-test")]
        [AllowAnonymous]
        public IActionResult TestUrlGeneration()
        {
            try
            {
                var testPath = "/uploads/test/sample.jpg";
                var generatedUrl = GetAbsoluteUrl(testPath);
                
                var debugInfo = new
                {
                    RequestScheme = Request?.Scheme,
                    RequestHost = Request?.Host.ToString(),
                    ForwardedHost = Request?.Headers["X-Forwarded-Host"].FirstOrDefault(),
                    ForwardedProto = Request?.Headers["X-Forwarded-Proto"].FirstOrDefault(),
                    ConfiguredAppBaseUrl = _configuration?["AppBaseUrl"],
                    GeneratedUrl = generatedUrl,
                    TestPath = testPath,
                    Environment = _environment.EnvironmentName,
                    Timestamp = DateTime.UtcNow
                };
                
                return SuccessResponse(debugInfo);
            }
            catch (Exception ex)
            {
                LogError(ex, "Error in URL generation test");
                return ErrorResponse("TestFailed", "URL generation test failed", 500);
            }
        }

        /// <summary>
        /// Debug endpoint to diagnose database flag corruption for image classification
        /// </summary>
        [HttpGet("debug/database-flags")]
        public async Task<IActionResult> DiagnoseDatabaseFlags()
        {
            var authCheck = ValidateAuthentication();
            if (authCheck != null) return authCheck;
            var userId = GetCurrentUserId()!;

            try
            {
                var profile = await _userProfileRepository.GetByUserIdAsync(userId);
                if (profile == null)
                    return ErrorResponse("ProfileNotFound", "Profile not found", 404);

                var allImages = profile.ProcessedImages.OrderByDescending(i => i.CreatedAt).ToList();
                
                var diagnosticData = new
                {
                    UserId = userId,
                    TotalImages = allImages.Count,
                    ImageBreakdown = new
                    {
                        TotalOriginalUploads = allImages.Count(i => i.IsOriginalUpload),
                        TotalGenerated = allImages.Count(i => i.IsGenerated),
                        CorruptedImages = allImages.Count(i => i.IsGenerated && i.IsOriginalUpload), // Should be 0
                        OrphanedImages = allImages.Count(i => !i.IsGenerated && !i.IsOriginalUpload), // Should be 0
                        ImagesWithOriginalStyle = allImages.Count(i => i.Style == ImageConstants.OriginalStyle),
                        ImagesWithGeneratedStyles = allImages.Count(i => i.Style != ImageConstants.OriginalStyle)
                    },
                    FileSystemCheck = new
                    {
                        UploadDirectoryExists = Directory.Exists(Path.Combine(_environment.ContentRootPath, "uploads", userId)),
                        GeneratedDirectoryExists = Directory.Exists(Path.Combine(_environment.ContentRootPath, "generated", userId)),
                        OrphanedOriginalUploads = GetOrphanedImages(userId, true),
                        OrphanedGeneratedImages = GetOrphanedImages(userId, false)
                    },
                    DetailedImageAnalysis = allImages.Select(img => new
                    {
                        img.Id,
                        img.Style,
                        img.IsGenerated,
                        img.IsOriginalUpload,
                        img.CreatedAt,
                        HasOriginalUrl = !string.IsNullOrEmpty(img.OriginalImageUrl),
                        HasProcessedUrl = !string.IsNullOrEmpty(img.ProcessedImageUrl),
                        OriginalUrl = img.OriginalImageUrl,
                        ProcessedUrl = img.ProcessedImageUrl,
                        OriginalFileExists = CheckFileExists(img.OriginalImageUrl, userId),
                        ProcessedFileExists = CheckFileExists(img.ProcessedImageUrl, userId),
                        Classification = GetImageClassification(img),
                        PotentialIssues = GetImageIssues(img)
                    }).ToList(),
                    Timestamp = DateTime.UtcNow
                };

                return SuccessResponse(diagnosticData);
            }
            catch (Exception ex)
            {
                LogError(ex, "Error diagnosing database flags", userId);
                return ErrorResponse("DiagnosticFailed", "Failed to diagnose database flags", 500);
            }
        }

        /// <summary>
        /// Helper method to classify images based on their flags and properties
        /// </summary>
        private string GetImageClassification(ProcessedImage img)
        {
            if (img.IsOriginalUpload && !img.IsGenerated && img.Style == ImageConstants.OriginalStyle)
                return "VALID_ORIGINAL_UPLOAD";
            
            if (!img.IsOriginalUpload && img.IsGenerated && img.Style != ImageConstants.OriginalStyle)
                return "VALID_GENERATED_IMAGE";
            
            if (img.IsOriginalUpload && img.IsGenerated)
                return "CORRUPTED_BOTH_FLAGS_TRUE";
            
            if (!img.IsOriginalUpload && !img.IsGenerated)
                return "CORRUPTED_BOTH_FLAGS_FALSE";
            
            if (img.IsOriginalUpload && img.Style != ImageConstants.OriginalStyle)
                return "SUSPICIOUS_ORIGINAL_WITH_GENERATED_STYLE";
            
            if (img.IsGenerated && img.Style == ImageConstants.OriginalStyle)
                return "SUSPICIOUS_GENERATED_WITH_ORIGINAL_STYLE";
            
            return "UNKNOWN_CLASSIFICATION";
        }

        /// <summary>
        /// Helper method to identify potential issues with image records
        /// </summary>
        private List<string> GetImageIssues(ProcessedImage img)
        {
            var issues = new List<string>();
            
            if (img.IsOriginalUpload && img.IsGenerated)
                issues.Add("Both IsOriginalUpload and IsGenerated are true");
            
            if (!img.IsOriginalUpload && !img.IsGenerated)
                issues.Add("Both IsOriginalUpload and IsGenerated are false");
            
            if (img.IsOriginalUpload && img.Style != ImageConstants.OriginalStyle)
                issues.Add($"Original upload has non-original style: {img.Style}");
            
            if (img.IsGenerated && img.Style == ImageConstants.OriginalStyle)
                issues.Add("Generated image has original style");
            
            if (string.IsNullOrEmpty(img.OriginalImageUrl) && string.IsNullOrEmpty(img.ProcessedImageUrl))
                issues.Add("No image URLs available");
            
            if (img.IsOriginalUpload && string.IsNullOrEmpty(img.OriginalImageUrl))
                issues.Add("Original upload missing OriginalImageUrl");
            
            if (img.IsGenerated && string.IsNullOrEmpty(img.ProcessedImageUrl))
                issues.Add("Generated image missing ProcessedImageUrl");
            
            return issues;
        }

        /// <summary>
        /// Check if an image file exists on the filesystem
        /// </summary>
        private bool CheckFileExists(string? imageUrl, string userId)
        {
            if (string.IsNullOrEmpty(imageUrl)) return false;
            
            try
            {
                // Handle relative URLs
                if (imageUrl.StartsWith("/uploads/") || imageUrl.StartsWith("/generated/"))
                {
                    var relativePath = imageUrl.TrimStart('/');
                    var fullPath = Path.Combine(_environment.ContentRootPath, relativePath);
                    return System.IO.File.Exists(fullPath);
                }
                
                // Handle full URLs - can't check filesystem for external URLs
                if (imageUrl.StartsWith("http"))
                {
                    return false; // Indicate we can't verify external URLs
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get count of orphaned images (database records without corresponding files)
        /// </summary>
        private int GetOrphanedImages(string userId, bool isOriginalUploads)
        {
            try
            {
                var profile = Context.UserProfiles.Include(p => p.ProcessedImages)
                    .FirstOrDefault(p => p.UserId == userId);
                
                if (profile == null) return 0;
                
                var images = isOriginalUploads 
                    ? profile.ProcessedImages.Where(i => i.IsOriginalUpload).ToList()
                    : profile.ProcessedImages.Where(i => i.IsGenerated).ToList();
                
                int orphanedCount = 0;
                
                foreach (var img in images)
                {
                    var urlToCheck = isOriginalUploads ? img.OriginalImageUrl : img.ProcessedImageUrl;
                    if (!CheckFileExists(urlToCheck, userId))
                    {
                        orphanedCount++;
                    }
                }
                
                return orphanedCount;
            }
            catch
            {
                return -1; // Error indicator
            }
        }

        /// <summary>
        /// Repair corrupted database flags for image classification
        /// </summary>
        [HttpPost("debug/repair-database-flags")]
        public async Task<IActionResult> RepairDatabaseFlags()
        {
            var authCheck = ValidateAuthentication();
            if (authCheck != null) return authCheck;
            var userId = GetCurrentUserId()!;

            try
            {
                var profile = await _userProfileRepository.GetByUserIdAsync(userId);
                if (profile == null)
                    return ErrorResponse("ProfileNotFound", "Profile not found", 404);

                var allImages = profile.ProcessedImages.ToList();
                var repairedImages = new List<object>();
                int repairedCount = 0;

                foreach (var img in allImages)
                {
                    var originalState = new { img.Id, img.IsGenerated, img.IsOriginalUpload, img.Style };
                    bool needsRepair = false;
                    var issues = new List<string>();

                    // Rule 1: Images with "Original" style should be original uploads
                    if (img.Style == ImageConstants.OriginalStyle)
                    {
                        if (!img.IsOriginalUpload || img.IsGenerated)
                        {
                            img.IsOriginalUpload = true;
                            img.IsGenerated = false;
                            needsRepair = true;
                            issues.Add("Fixed: Original style image corrected to IsOriginalUpload=true, IsGenerated=false");
                        }
                    }
                    // Rule 2: Images with non-"Original" styles should be generated
                    else if (img.Style != ImageConstants.OriginalStyle)
                    {
                        if (img.IsOriginalUpload || !img.IsGenerated)
                        {
                            img.IsOriginalUpload = false;
                            img.IsGenerated = true;
                            needsRepair = true;
                            issues.Add($"Fixed: Generated style '{img.Style}' image corrected to IsOriginalUpload=false, IsGenerated=true");
                        }
                    }

                    // Rule 3: Both flags can't be true or both false
                    if (img.IsOriginalUpload && img.IsGenerated)
                    {
                        // Determine correct state based on style
                        if (img.Style == ImageConstants.OriginalStyle)
                        {
                            img.IsGenerated = false;
                            issues.Add("Fixed: Removed IsGenerated=true from original style image");
                        }
                        else
                        {
                            img.IsOriginalUpload = false;
                            issues.Add("Fixed: Removed IsOriginalUpload=true from generated style image");
                        }
                        needsRepair = true;
                    }
                    else if (!img.IsOriginalUpload && !img.IsGenerated)
                    {
                        // Determine correct state based on style
                        if (img.Style == ImageConstants.OriginalStyle)
                        {
                            img.IsOriginalUpload = true;
                            issues.Add("Fixed: Set IsOriginalUpload=true for original style image");
                        }
                        else
                        {
                            img.IsGenerated = true;
                            issues.Add("Fixed: Set IsGenerated=true for generated style image");
                        }
                        needsRepair = true;
                    }

                    if (needsRepair)
                    {
                        repairedCount++;
                        repairedImages.Add(new
                        {
                            img.Id,
                            OriginalState = originalState,
                            NewState = new { img.IsGenerated, img.IsOriginalUpload, img.Style },
                            Issues = issues
                        });
                    }
                }

                if (repairedCount > 0)
                {
                    await _userProfileRepository.UpdateAsync(profile);
                    LogInfo($"Repaired {repairedCount} corrupted image flags for user {userId}");
                }

                return SuccessResponse(new
                {
                    TotalImagesChecked = allImages.Count,
                    RepairedCount = repairedCount,
                    RepairedImages = repairedImages,
                    Message = repairedCount > 0 
                        ? $"Successfully repaired {repairedCount} corrupted image records"
                        : "No corrupted image records found",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                LogError(ex, "Error repairing database flags", userId);
                return ErrorResponse("RepairFailed", "Failed to repair database flags", 500);
            }
        }

        /// <summary>
        /// Fix corrupted Style column entries that contain timestamps
        /// </summary>
        [HttpPost("debug/repair-style-corruption")]
        public async Task<IActionResult> RepairStyleCorruption()
        {
            var authCheck = ValidateAuthentication();
            if (authCheck != null) return authCheck;
            var userId = GetCurrentUserId()!;

            try
            {
                var profile = await _userProfileRepository.GetByUserIdAsync(userId);
                if (profile == null)
                    return ErrorResponse("ProfileNotFound", "Profile not found", 404);

                var allImages = profile.ProcessedImages.ToList();
                var repairedStyles = new List<object>();
                int repairedCount = 0;

                // Regex pattern to detect corrupted styles: style_timestamp or style_timestamp_guid
                var timestampPattern = new System.Text.RegularExpressions.Regex(@"^(.+?)_\d{14}(_.*)?$");

                foreach (var img in allImages)
                {
                    if (string.IsNullOrEmpty(img.Style)) continue;

                    var match = timestampPattern.Match(img.Style);
                    if (match.Success)
                    {
                        var originalStyle = img.Style;
                        var cleanStyle = match.Groups[1].Value; // Extract just the style part

                        // Validate that the clean style is reasonable
                        if (!string.IsNullOrEmpty(cleanStyle) && cleanStyle.Length > 0 && !cleanStyle.Contains("_20"))
                        {
                            img.Style = cleanStyle;
                            repairedCount++;

                            repairedStyles.Add(new
                            {
                                img.Id,
                                OriginalStyle = originalStyle,
                                CleanedStyle = cleanStyle,
                                img.IsGenerated,
                                img.CreatedAt
                            });

                            LogInfo($"Repaired style corruption: '{originalStyle}' → '{cleanStyle}' for image {img.Id}");
                        }
                    }
                }

                if (repairedCount > 0)
                {
                    await _userProfileRepository.UpdateAsync(profile);
                    LogInfo($"Repaired {repairedCount} corrupted style entries for user {userId}");
                }

                return SuccessResponse(new
                {
                    TotalImagesChecked = allImages.Count,
                    CorruptedStylesFound = repairedCount,
                    RepairedStyles = repairedStyles,
                    Message = repairedCount > 0 
                        ? $"Successfully repaired {repairedCount} corrupted style entries"
                        : "No corrupted style entries found",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                LogError(ex, "Error repairing style corruption", userId);
                return ErrorResponse("RepairFailed", "Failed to repair style corruption", 500);
            }
        }

        /// <summary>
        /// Remove orphaned database records that point to non-existent files
        /// </summary>
        [HttpPost("debug/cleanup-orphaned-records")]
        public async Task<IActionResult> CleanupOrphanedRecords()
        {
            var authCheck = ValidateAuthentication();
            if (authCheck != null) return authCheck;
            var userId = GetCurrentUserId()!;

            try
            {
                var profile = await _userProfileRepository.GetByUserIdAsync(userId);
                if (profile == null)
                    return ErrorResponse("ProfileNotFound", "Profile not found", 404);

                var allImages = profile.ProcessedImages.ToList();
                var orphanedImages = new List<ProcessedImage>();
                var removedDetails = new List<object>();

                foreach (var img in allImages)
                {
                    bool shouldRemove = false;
                    var issues = new List<string>();

                    // Check if original upload files exist
                    if (img.IsOriginalUpload && !string.IsNullOrEmpty(img.OriginalImageUrl))
                    {
                        if (!CheckFileExists(img.OriginalImageUrl, userId))
                        {
                            shouldRemove = true;
                            issues.Add($"Original upload file not found: {img.OriginalImageUrl}");
                        }
                    }

                    // Check if generated image files exist
                    if (img.IsGenerated && !string.IsNullOrEmpty(img.ProcessedImageUrl))
                    {
                        if (!CheckFileExists(img.ProcessedImageUrl, userId))
                        {
                            shouldRemove = true;
                            issues.Add($"Generated image file not found: {img.ProcessedImageUrl}");
                        }
                    }

                    // Remove records with no valid URLs
                    if (string.IsNullOrEmpty(img.OriginalImageUrl) && string.IsNullOrEmpty(img.ProcessedImageUrl))
                    {
                        shouldRemove = true;
                        issues.Add("No image URLs available");
                    }

                    if (shouldRemove)
                    {
                        orphanedImages.Add(img);
                        removedDetails.Add(new
                        {
                            img.Id,
                            img.Style,
                            img.IsGenerated,
                            img.IsOriginalUpload,
                            img.CreatedAt,
                            img.OriginalImageUrl,
                            img.ProcessedImageUrl,
                            Issues = issues
                        });
                    }
                }

                // Remove orphaned images from the profile
                foreach (var orphanedImage in orphanedImages)
                {
                    profile.ProcessedImages.Remove(orphanedImage);
                }

                if (orphanedImages.Count > 0)
                {
                    await _userProfileRepository.UpdateAsync(profile);
                    LogInfo($"Removed {orphanedImages.Count} orphaned image records for user {userId}");
                }

                return SuccessResponse(new
                {
                    TotalImagesChecked = allImages.Count,
                    OrphanedRecordsRemoved = orphanedImages.Count,
                    RemovedImageDetails = removedDetails,
                    Message = orphanedImages.Count > 0 
                        ? $"Successfully removed {orphanedImages.Count} orphaned image records"
                        : "No orphaned image records found",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                LogError(ex, "Error cleaning up orphaned records", userId);
                return ErrorResponse("CleanupFailed", "Failed to cleanup orphaned records", 500);
            }
        }

        /// <summary>
        /// Complete repair solution - runs all repairs and invalidates UI cache
        /// </summary>
        [HttpPost("debug/complete-repair")]
        public async Task<IActionResult> CompleteRepair()
        {
            var authCheck = ValidateAuthentication();
            if (authCheck != null) return authCheck;
            var userId = GetCurrentUserId()!;

            try
            {
                var repairResults = new
                {
                    StyleRepair = new { },
                    OrphanedCleanup = new { },
                    CacheInvalidation = new { }
                };

                // Step 1: Style corruption repair
                LogInfo("Starting complete repair process - Step 1: Style corruption");
                var styleRepairResult = await RepairStyleCorruption();
                
                // Step 2: Orphaned records cleanup  
                LogInfo("Complete repair process - Step 2: Orphaned records cleanup");
                var orphanedCleanupResult = await CleanupOrphanedRecords();

                // Step 3: Invalidate user cache to force UI refresh
                LogInfo("Complete repair process - Step 3: Cache invalidation");
                await _userContextService.InvalidateUserCacheAsync(userId);

                LogInfo($"Complete repair process finished for user {userId}");

                return SuccessResponse(new
                {
                    Message = "Complete repair process finished successfully",
                    Steps = new[]
                    {
                        "✅ Style corruption repair completed",
                        "✅ Orphaned records cleanup completed", 
                        "✅ UI cache invalidated for fresh data load"
                    },
                    Instructions = new[]
                    {
                        "1. Refresh your browser page (F5 or Ctrl+R)",
                        "2. Or call forceRefresh() in browser console",
                        "3. All 404 errors should now be resolved"
                    },
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                LogError(ex, "Error during complete repair process", userId);
                return ErrorResponse("CompleteRepairFailed", "Complete repair process failed", 500);
            }
        }

        /// <summary>
        /// Repopulate ProcessedImage table from filesystem images
        /// </summary>
        [HttpPost("repopulate-from-filesystem")]
        public async Task<IActionResult> RepopulateFromFilesystem([FromQuery] bool dryRun = true)
        {
            try
            {
                var repopulationResult = await RepopulateImagesFromFilesystemAsync(dryRun);
                
                return SuccessResponse(new 
                { 
                    dryRun = dryRun,
                    data = repopulationResult,
                    message = dryRun ? "Dry run completed - no changes made to database" : "Filesystem repopulation completed"
                });
            }
            catch (Exception ex)
            {
                LogError(ex, "Error during filesystem repopulation");
                return ErrorResponse("RepopulationFailed", "Failed to repopulate from filesystem", 500);
            }
        }

        private async Task<object> RepopulateImagesFromFilesystemAsync(bool dryRun)
        {
            var baseDirectory = _environment.ContentRootPath;
            var uploadsPath = Path.Combine(baseDirectory, "uploads");
            var generatedPath = Path.Combine(baseDirectory, "generated");
            
            var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp" };
            var processedUsers = new List<object>();
            var summary = new RepopulationSummary();

            // Get all user profiles with their GUIDs
            var userProfiles = await Context.UserProfiles
                .Include(u => u.ProcessedImages)
                .Select(u => new { u.Id, u.UserId, u.FirstName, u.LastName })
                .ToListAsync();

            Logger.LogInformation("Found {Count} user profiles for repopulation", userProfiles.Count);

            // Process uploads directory
            if (Directory.Exists(uploadsPath))
            {
                var uploadUsers = Directory.GetDirectories(uploadsPath);
                foreach (var userDir in uploadUsers)
                {
                    var guidUserId = Path.GetFileName(userDir);
                    var userProfile = userProfiles.FirstOrDefault(u => u.UserId == guidUserId);
                    
                    if (userProfile == null)
                    {
                        summary.Errors.Add($"No profile found for GUID: {guidUserId}");
                        summary.FailedMappings++;
                        continue;
                    }

                    var userResult = await ProcessUserDirectoryAsync(userDir, userProfile.Id, true, imageExtensions, dryRun);
                    processedUsers.Add(new
                    {
                        UserId = guidUserId,
                        UserProfileId = userProfile.Id,
                        UserName = $"{userProfile.FirstName} {userProfile.LastName}",
                        DirectoryType = "uploads",
                        userResult.ImagesFound,
                        userResult.ImagesProcessed,
                        userResult.Errors
                    });
                    
                    summary.TotalUploads += userResult.ImagesFound;
                    summary.SuccessfulMappings++;
                }
            }

            // Process generated directory
            if (Directory.Exists(generatedPath))
            {
                var generatedUsers = Directory.GetDirectories(generatedPath);
                foreach (var userDir in generatedUsers)
                {
                    var guidUserId = Path.GetFileName(userDir);
                    var userProfile = userProfiles.FirstOrDefault(u => u.UserId == guidUserId);
                    
                    if (userProfile == null)
                    {
                        summary.Errors.Add($"No profile found for GUID: {guidUserId}");
                        summary.FailedMappings++;
                        continue;
                    }

                    var userResult = await ProcessUserDirectoryAsync(userDir, userProfile.Id, false, imageExtensions, dryRun);
                    var existingUserIndex = processedUsers.FindIndex(u => 
                        ((dynamic)u).UserId == guidUserId);
                    
                    if (existingUserIndex >= 0)
                    {
                        // Update existing entry
                        var existingUser = (dynamic)processedUsers[existingUserIndex];
                        processedUsers[existingUserIndex] = new
                        {
                            UserId = guidUserId,
                            UserProfileId = userProfile.Id,
                            UserName = $"{userProfile.FirstName} {userProfile.LastName}",
                            DirectoryType = "both",
                            ImagesFound = existingUser.ImagesFound + userResult.ImagesFound,
                            ImagesProcessed = existingUser.ImagesProcessed + userResult.ImagesProcessed,
                            Errors = ((IEnumerable<string>)existingUser.Errors).Concat(userResult.Errors).ToList()
                        };
                    }
                    else
                    {
                        processedUsers.Add(new
                        {
                            UserId = guidUserId,
                            UserProfileId = userProfile.Id,
                            UserName = $"{userProfile.FirstName} {userProfile.LastName}",
                            DirectoryType = "generated",
                            userResult.ImagesFound,
                            userResult.ImagesProcessed,
                            userResult.Errors
                        });
                        summary.SuccessfulMappings++;
                    }
                    
                    summary.TotalGenerated += userResult.ImagesFound;
                }
            }

            summary.TotalUsers = processedUsers.Count;

            return new
            {
                ProcessedUsers = processedUsers,
                Summary = new
                {
                    summary.TotalUsers,
                    summary.TotalUploads,
                    summary.TotalGenerated,
                    TotalImages = summary.TotalUploads + summary.TotalGenerated,
                    summary.SuccessfulMappings,
                    summary.FailedMappings,
                    summary.Errors
                }
            };
        }

        private async Task<(int ImagesFound, int ImagesProcessed, List<string> Errors)> ProcessUserDirectoryAsync(
            string userDirectoryPath, int userProfileId, bool isUploadDirectory, string[] imageExtensions, bool dryRun)
        {
            var errors = new List<string>();
            var imagesFound = 0;
            var imagesProcessed = 0;

            try
            {
                var imageFiles = Directory.GetFiles(userDirectoryPath)
                    .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .ToArray();

                imagesFound = imageFiles.Length;
                Logger.LogInformation("Found {Count} images in {Directory}", imagesFound, userDirectoryPath);

                if (!dryRun && imagesFound > 0)
                {
                    var imagesToCreate = new List<ProcessedImage>();

                    foreach (var imageFile in imageFiles)
                    {
                        try
                        {
                            var fileName = Path.GetFileName(imageFile);
                            var fileInfo = new FileInfo(imageFile);
                            var style = ExtractStyleFromFilename(fileName, isUploadDirectory);
                            var userGuid = Path.GetFileName(Path.GetDirectoryName(imageFile));
                            
                            var processedImage = new ProcessedImage
                            {
                                UserProfileId = userProfileId,
                                Style = style,
                                IsOriginalUpload = isUploadDirectory,
                                IsGenerated = !isUploadDirectory,
                                CreatedAt = fileInfo.CreationTimeUtc,
                                // Set correct URLs based on image type
                                OriginalImageUrl = isUploadDirectory 
                                    ? $"/uploads/{userGuid}/{fileName}"  // Uploaded images: source is uploads
                                    : $"/generated/{userGuid}/{fileName}", // Generated images: source is generated (fallback for filesystem-found files)
                                ProcessedImageUrl = isUploadDirectory 
                                    ? $"/uploads/{userGuid}/{fileName}"   // Uploaded images: processed same as original
                                    : $"/generated/{userGuid}/{fileName}" // Generated images: processed path
                            };

                            // Set retention policy
                            processedImage.SetScheduledDeletionDate();
                            
                            imagesToCreate.Add(processedImage);
                            imagesProcessed++;
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Failed to process {Path.GetFileName(imageFile)}: {ex.Message}");
                            Logger.LogWarning(ex, "Failed to process image file {ImageFile}", imageFile);
                        }
                    }

                    if (imagesToCreate.Any())
                    {
                        Context.ProcessedImages.AddRange(imagesToCreate);
                        await Context.SaveChangesAsync();
                        Logger.LogInformation("Created {Count} ProcessedImage records for user {UserProfileId}", imagesToCreate.Count, userProfileId);
                    }
                }
                else if (dryRun)
                {
                    imagesProcessed = imagesFound; // In dry run, assume all would be processed
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to process directory {userDirectoryPath}: {ex.Message}");
                Logger.LogError(ex, "Failed to process user directory {Directory}", userDirectoryPath);
            }

            return (imagesFound, imagesProcessed, errors);
        }

        private string ExtractStyleFromFilename(string fileName, bool isUploadDirectory)
        {
            if (isUploadDirectory)
            {
                return ImageConstants.OriginalStyle;
            }

            // Extract style from generated image filename patterns
            var fileNameLower = fileName.ToLowerInvariant();
            
            if (fileNameLower.Contains("professional"))
                return "Professional";
            if (fileNameLower.Contains("casual"))
                return "Casual";
            if (fileNameLower.Contains("business"))
                return "Business";
            if (fileNameLower.Contains("headshot"))
                return "Headshot";
            if (fileNameLower.Contains("portrait"))
                return "Portrait";
            if (fileNameLower.Contains("linkedin"))
                return "LinkedIn";
            if (fileNameLower.Contains("corporate"))
                return "Corporate";
            
            // Default for generated images
            return "Generated";
        }

        /// <summary>
        /// Repair generated images with incorrect OriginalImageUrl paths
        /// </summary>
        [HttpPost("repair-generated-image-urls")]
        public async Task<IActionResult> RepairGeneratedImageUrls([FromQuery] bool dryRun = true)
        {
            try
            {
                var repairResult = await RepairGeneratedImageUrlsAsync(dryRun);
                
                return SuccessResponse(new 
                { 
                    dryRun = dryRun,
                    data = repairResult,
                    message = dryRun ? "Dry run completed - no changes made to database" : "Generated image URL repair completed"
                });
            }
            catch (Exception ex)
            {
                LogError(ex, "Error during generated image URL repair");
                return ErrorResponse("RepairFailed", "Failed to repair generated image URLs", 500);
            }
        }

        private async Task<object> RepairGeneratedImageUrlsAsync(bool dryRun)
        {
            // Find all generated images with incorrect /uploads/ URLs in OriginalImageUrl
            var corruptedImages = await Context.ProcessedImages
                .Where(img => img.IsGenerated && 
                             img.OriginalImageUrl != null && 
                             img.OriginalImageUrl.Contains("/uploads/"))
                .ToListAsync();

            Logger.LogInformation("Found {Count} generated images with incorrect /uploads/ URLs in OriginalImageUrl", 
                corruptedImages.Count);

            var repairedCount = 0;
            var errors = new List<string>();

            foreach (var image in corruptedImages)
            {
                try
                {
                    var oldUrl = image.OriginalImageUrl;
                    
                    // Strategy: Use ProcessedImageUrl as the source for generated images
                    // This makes sense because for generated images, the "original" and "processed" are the same file
                    var newUrl = image.ProcessedImageUrl ?? oldUrl;
                    
                    // Ensure the new URL doesn't also have /uploads/ (double corruption)
                    if (newUrl?.Contains("/uploads/") == true)
                    {
                        // Fallback: Try to convert /uploads/ to /generated/
                        newUrl = newUrl.Replace("/uploads/", "/generated/");
                    }

                    if (!dryRun)
                    {
                        image.OriginalImageUrl = newUrl;
                        repairedCount++;
                        
                        Logger.LogInformation("Repaired image {ImageId}: '{OldUrl}' -> '{NewUrl}'", 
                            image.Id, oldUrl, newUrl);
                    }
                    else
                    {
                        repairedCount++;
                        Logger.LogInformation("Would repair image {ImageId}: '{OldUrl}' -> '{NewUrl}'", 
                            image.Id, oldUrl, newUrl);
                    }
                }
                catch (Exception ex)
                {
                    var error = $"Failed to repair image {image.Id}: {ex.Message}";
                    errors.Add(error);
                    Logger.LogWarning(ex, error);
                }
            }

            if (!dryRun && repairedCount > 0)
            {
                await Context.SaveChangesAsync();
                Logger.LogInformation("Successfully repaired {Count} generated image URLs", repairedCount);
            }

            return new
            {
                TotalCorruptedFound = corruptedImages.Count,
                RepairedCount = repairedCount,
                Errors = errors,
                Summary = new
                {
                    Message = dryRun 
                        ? $"Found {corruptedImages.Count} corrupted URLs, would repair {repairedCount}"
                        : $"Repaired {repairedCount} of {corruptedImages.Count} corrupted URLs",
                    CorruptedImages = corruptedImages.Take(10).Select(img => new 
                    {
                        img.Id,
                        img.Style,
                        OriginalImageUrl = img.OriginalImageUrl,
                        ProcessedImageUrl = img.ProcessedImageUrl,
                        WouldBecomeUrl = img.ProcessedImageUrl ?? img.OriginalImageUrl?.Replace("/uploads/", "/generated/")
                    }).ToList()
                }
            };
        }

        /// <summary>
        /// Reconcile database with filesystem - filesystem as source of truth
        /// </summary>
        [AllowAnonymous] // Temporary for testing
        [HttpPost("reconcile-database")]
        public async Task<IActionResult> ReconcileDatabase([FromQuery] bool dryRun = true)
        {
            try
            {
                var reconcileResult = await ReconcileDatabaseWithFilesystemAsync(dryRun);
                
                return SuccessResponse(new 
                { 
                    dryRun = dryRun,
                    data = reconcileResult,
                    message = dryRun ? "Dry run completed - no changes made to database" : "Database reconciliation completed"
                });
            }
            catch (Exception ex)
            {
                LogError(ex, "Error during database reconciliation");
                return ErrorResponse("ReconciliationFailed", "Failed to reconcile database with filesystem", 500);
            }
        }

        private async Task<object> ReconcileDatabaseWithFilesystemAsync(bool dryRun)
        {
            var baseDirectory = _environment.ContentRootPath;
            var uploadsPath = Path.Combine(baseDirectory, "uploads");
            var generatedPath = Path.Combine(baseDirectory, "generated");
            
            var reconciliationSummary = new ReconciliationSummary();
            var detailedResults = new List<object>();

            // Get all user profiles
            var userProfiles = await Context.UserProfiles
                .Include(u => u.ProcessedImages)
                .ToListAsync();

            Logger.LogInformation("Starting database reconciliation for {UserCount} users", userProfiles.Count);

            foreach (var userProfile in userProfiles)
            {
                try 
                {
                    var userResult = await ReconcileUserImagesAsync(userProfile, uploadsPath, generatedPath, dryRun);
                    detailedResults.Add(userResult);
                    
                    reconciliationSummary.TotalUsers++;
                    reconciliationSummary.OrphanedRecordsRemoved += userResult.OrphanedRecordsRemoved;
                    reconciliationSummary.MissingRecordsCreated += userResult.MissingRecordsCreated;
                    reconciliationSummary.TotalFilesProcessed += userResult.FilesProcessed;
                    reconciliationSummary.Errors.AddRange(userResult.Errors);
                }
                catch (Exception ex)
                {
                    var error = $"Failed to process user {userProfile.UserId}: {ex.Message}";
                    reconciliationSummary.Errors.Add(error);
                    Logger.LogError(ex, error);
                }
            }

            if (!dryRun && (reconciliationSummary.OrphanedRecordsRemoved > 0 || reconciliationSummary.MissingRecordsCreated > 0))
            {
                await Context.SaveChangesAsync();
                Logger.LogInformation("Database reconciliation completed. Removed {Orphaned} orphaned records, created {Missing} missing records", 
                    reconciliationSummary.OrphanedRecordsRemoved, reconciliationSummary.MissingRecordsCreated);
            }

            return new
            {
                Summary = reconciliationSummary,
                DetailedResults = detailedResults.Take(10), // Limit output for performance
                Message = dryRun 
                    ? $"Would remove {reconciliationSummary.OrphanedRecordsRemoved} orphaned records and create {reconciliationSummary.MissingRecordsCreated} missing records"
                    : $"Removed {reconciliationSummary.OrphanedRecordsRemoved} orphaned records and created {reconciliationSummary.MissingRecordsCreated} missing records"
            };
        }

        private async Task<UserReconciliationResult> ReconcileUserImagesAsync(
            UserProfile userProfile, string uploadsPath, string generatedPath, bool dryRun)
        {
            var result = new UserReconciliationResult
            {
                UserId = userProfile.UserId,
                UserName = $"{userProfile.FirstName} {userProfile.LastName}".Trim()
            };

            var userUploadsDir = Path.Combine(uploadsPath, userProfile.UserId);
            var userGeneratedDir = Path.Combine(generatedPath, userProfile.UserId);

            // Get current database records
            var currentImages = userProfile.ProcessedImages.ToList();
            var uploadedImages = currentImages.Where(img => img.IsOriginalUpload).ToList();
            var generatedImages = currentImages.Where(img => img.IsGenerated).ToList();

            // Check uploaded images
            if (Directory.Exists(userUploadsDir))
            {
                await ReconcileUserDirectoryAsync(userProfile, userUploadsDir, uploadedImages, true, dryRun, result);
            }
            else if (uploadedImages.Any())
            {
                // No uploads directory but database has uploaded images - mark for removal
                foreach (var orphanedImage in uploadedImages)
                {
                    if (!dryRun)
                    {
                        Context.ProcessedImages.Remove(orphanedImage);
                    }
                    result.OrphanedRecordsRemoved++;
                    result.RemovedImages.Add($"ID {orphanedImage.Id}: {orphanedImage.OriginalImageUrl} (no uploads directory)");
                }
            }

            // Check generated images
            if (Directory.Exists(userGeneratedDir))
            {
                await ReconcileUserDirectoryAsync(userProfile, userGeneratedDir, generatedImages, false, dryRun, result);
            }
            else if (generatedImages.Any())
            {
                // No generated directory but database has generated images - mark for removal
                foreach (var orphanedImage in generatedImages)
                {
                    if (!dryRun)
                    {
                        Context.ProcessedImages.Remove(orphanedImage);
                    }
                    result.OrphanedRecordsRemoved++;
                    result.RemovedImages.Add($"ID {orphanedImage.Id}: {orphanedImage.ProcessedImageUrl} (no generated directory)");
                }
            }

            return result;
        }

        private async Task ReconcileUserDirectoryAsync(
            UserProfile userProfile, string directoryPath, List<ProcessedImage> databaseImages, 
            bool isUploadDirectory, bool dryRun, UserReconciliationResult result)
        {
            var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp" };
            var filesOnDisk = Directory.GetFiles(directoryPath)
                .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            result.FilesProcessed += filesOnDisk.Count;

            // Check for orphaned database records (file doesn't exist)
            foreach (var dbImage in databaseImages)
            {
                var expectedPath = isUploadDirectory 
                    ? Path.Combine(directoryPath, Path.GetFileName(dbImage.OriginalImageUrl ?? ""))
                    : Path.Combine(directoryPath, Path.GetFileName(dbImage.ProcessedImageUrl ?? ""));

                if (!System.IO.File.Exists(expectedPath))
                {
                    // Orphaned database record - file doesn't exist
                    if (!dryRun)
                    {
                        Context.ProcessedImages.Remove(dbImage);
                    }
                    result.OrphanedRecordsRemoved++;
                    result.RemovedImages.Add($"ID {dbImage.Id}: {dbImage.OriginalImageUrl ?? dbImage.ProcessedImageUrl} (file not found)");
                    
                    Logger.LogInformation("Removing orphaned database record for missing file: {FilePath}", expectedPath);
                }
            }

            // Check for orphaned files (no database record) - optional: create missing records
            foreach (var filePath in filesOnDisk)
            {
                var fileName = Path.GetFileName(filePath);
                var hasDbRecord = databaseImages.Any(img => 
                    (isUploadDirectory && img.OriginalImageUrl?.EndsWith(fileName) == true) ||
                    (!isUploadDirectory && img.ProcessedImageUrl?.EndsWith(fileName) == true));

                if (!hasDbRecord)
                {
                    // Orphaned file - could create database record, but for now just log
                    result.OrphanedFiles.Add($"{fileName} (no database record)");
                    Logger.LogInformation("Found orphaned file with no database record: {FilePath}", filePath);
                    // Note: Not auto-creating records as we don't have enough metadata
                }
            }
        }

        private class ReconciliationSummary
        {
            public int TotalUsers { get; set; } = 0;
            public int OrphanedRecordsRemoved { get; set; } = 0;
            public int MissingRecordsCreated { get; set; } = 0;
            public int TotalFilesProcessed { get; set; } = 0;
            public List<string> Errors { get; set; } = new List<string>();
        }

        private class UserReconciliationResult
        {
            public string UserId { get; set; } = "";
            public string UserName { get; set; } = "";
            public int FilesProcessed { get; set; } = 0;
            public int OrphanedRecordsRemoved { get; set; } = 0;
            public int MissingRecordsCreated { get; set; } = 0;
            public List<string> RemovedImages { get; set; } = new List<string>();
            public List<string> CreatedImages { get; set; } = new List<string>();
            public List<string> OrphanedFiles { get; set; } = new List<string>();
            public List<string> Errors { get; set; } = new List<string>();
        }

        private class RepopulationSummary
        {
            public int TotalUsers { get; set; } = 0;
            public int TotalUploads { get; set; } = 0;
            public int TotalGenerated { get; set; } = 0;
            public int SuccessfulMappings { get; set; } = 0;
            public int FailedMappings { get; set; } = 0;
            public List<string> Errors { get; set; } = new List<string>();
        }

        #endregion
    }

}