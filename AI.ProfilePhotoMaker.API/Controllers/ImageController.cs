using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.Storage;
using AI.ProfilePhotoMaker.API.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;

namespace AI.ProfilePhotoMaker.API.Controllers
{
    /// <summary>
    /// Controller for handling image upload, retrieval, and management operations with async I/O optimizations
    /// </summary>
    [Authorize]
    public class ImageController : BaseController
    {
        private readonly IUserProfileRepository _userProfileRepository;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly IUserContextService _userContextService;
        private readonly IBasicTierService _basicTierService;
        private readonly IAsyncFileService _asyncFileService;
        private readonly IAsyncZipService _asyncZipService;
        private readonly IStorageService _storageService;
        private readonly StoragePathResolver _pathResolver;

        public ImageController(
            IUserProfileRepository userProfileRepository,
            IWebHostEnvironment environment,
            IConfiguration configuration,
            IUserContextService userContextService,
            IBasicTierService basicTierService,
            ILogger<ImageController> logger,
            ApplicationDbContext context,
            IAsyncFileService asyncFileService,
            IAsyncZipService asyncZipService,
            IStorageService storageService,
            StoragePathResolver pathResolver)
            : base(logger, context)
        {
            _userProfileRepository = userProfileRepository;
            _environment = environment;
            _configuration = configuration;
            _userContextService = userContextService;
            _basicTierService = basicTierService;
            _asyncFileService = asyncFileService;
            _asyncZipService = asyncZipService;
            _storageService = storageService;
            _pathResolver = pathResolver;
        }

        /// <summary>
        /// Gets available image styles
        /// </summary>
        [HttpGet("styles")]
        public async Task<IActionResult> GetStyles()
        {
            try
            {
                if (Context == null)
                {
                    return ErrorResponse("ServerError", "Database context unavailable", 500);
                }
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

                // Determine storage type and file prefix based on image type
                var storageType = dto.IsEnhanced ? StorageType.Enhanced : StorageType.Upload;
                var filePrefix = dto.IsEnhanced ? "enhanced" : "selfie";

                foreach (var image in dto.Images)
                {
                    if (!IsValidImageFile(image))
                    {
                        return ErrorResponse("InvalidImage", $"Invalid image file: {image.FileName}");
                    }

                    // Generate clean filename based on image type
                    var extension = Path.GetExtension(image.FileName);
                    var fileName = $"{Guid.NewGuid()}_{filePrefix}{extension}";

                    // Get storage path using path resolver
                    var storagePath = _pathResolver.GetPath(storageType, userId, fileName);

                    // Save to blob storage
                    await using var imageStream = image.OpenReadStream();
                    await _storageService.SaveImageToPathAsync(imageStream, storagePath);

                    // Store storage path in DB; URLs are generated per-request
                    // This keeps deletion/storage operations reliable across environments

                    // Only create database records for non-enhanced images
                    // Enhanced images are temporary files and should NOT be counted in dashboard
                    if (!dto.IsEnhanced)
                    {
                        var processedImage = new ProcessedImage
                        {
                            OriginalImageUrl = storagePath,
                            ProcessedImageUrl = storagePath,
                            Style = ImageConstants.OriginalStyle,
                            UserProfileId = profile.Id,
                            CreatedAt = DateTime.UtcNow,
                            IsOriginalUpload = true,
                            IsGenerated = false,
                        };

                        // Set scheduled deletion date based on retention policy
                        processedImage.SetScheduledDeletionDate();

                        profile.ProcessedImages.Add(processedImage);
                        uploadedImages.Add(processedImage);

                        Logger.LogInformation("Created database record for uploaded image {FileName} for user {UserId}",
                            fileName, userId);
                    }
                    else
                    {
                        Logger.LogInformation("Skipped database record for enhanced image {FileName} for user {UserId} - temporary file only",
                            fileName, userId);
                    }

                    // Return a public URL for the client while keeping storagePath in DB
                    var publicUrl = _storageService.GetImageUrl(storagePath);
                    uploadResults.Add(new
                    {
                        FileName = fileName,
                        Size = image.Length,
                        Url = publicUrl
                    });
                }

                // For enhanced images, deduct credits (weekly credits first, then purchased)
                if (dto.IsEnhanced && uploadedImages.Count > 0)
                {
                    int creditsNeeded = uploadedImages.Count; // 1 credit per enhanced image
                    bool hasCredits = await _basicTierService.ConsumeCreditsAsync(userId, creditsNeeded, "enhanced_image_upload");

                    if (!hasCredits)
                    {
                        // Cleanup uploaded files if credit deduction failed - use storage service for cleanup
                        foreach (var uploadedImage in uploadedImages)
                        {
                            await _storageService.DeleteImageAsync(uploadedImage.OriginalImageUrl);
                        }

                        return ErrorResponse("InsufficientCredits",
                            $"Insufficient credits for enhanced image upload. Required: {creditsNeeded} credit(s). " +
                            "Enhanced images consume weekly credits first, then purchased credits if available.");
                    }
                }

                // Save uploaded image records to database (only if non-enhanced images were uploaded)
                if (uploadedImages.Any())
                {
                    await _userProfileRepository.UpdateAsync(profile);
                }

                string? zipPath = null;
                if (dto.ForTraining)
                {
                    zipPath = await CreateTrainingZipAsync(userId);
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
        /// Gets user's processed images with URLs using blob storage service
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

                // Handle OriginalImageUrl - use storage service for URL generation
                if (!string.IsNullOrEmpty(i.OriginalImageUrl))
                {
                    if (i.OriginalImageUrl.StartsWith("http"))
                    {
                        // Already a full URL (external or SAS URL)
                        originalUrl = i.OriginalImageUrl;
                    }
                    else
                    {
                        // Storage path - get environment-appropriate URL for frontend access
                        originalUrl = _storageService.GetImageUrl(i.OriginalImageUrl);
                    }
                }

                // Handle ProcessedImageUrl - use storage service for URL generation
                if (!string.IsNullOrEmpty(i.ProcessedImageUrl))
                {
                    if (i.ProcessedImageUrl.StartsWith("http"))
                    {
                        // Already a full URL (external or SAS URL)
                        processedUrl = i.ProcessedImageUrl;
                    }
                    else
                    {
                        // Storage path - get environment-appropriate URL for frontend access
                        processedUrl = _storageService.GetImageUrl(i.ProcessedImageUrl);
                    }
                }

                images.Add(new
                {
                    id = i.Id,
                    originalImageUrl = originalUrl,
                    processedImageUrl = processedUrl,
                    style = i.Style,
                    createdAt = i.CreatedAt,
                    isOriginalUpload = i.IsOriginalUpload,
                    isGenerated = i.IsGenerated
                });
            }

            var imageList = images.Cast<dynamic>().ToList();
            var summary = new
            {
                totalImages = images.Count,
                originalUploads = imageList.Count(i => i.isOriginalUpload),
                generatedImages = imageList.Count(i => i.isGenerated && !i.isOriginalUpload),
                images = images
            };

            return SuccessResponse(summary);
        }

        /// <summary>
        /// Deletes a specific image using blob storage service
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

            try
            {
                var physicalFileDeleted = false;
                var imageCountBefore = profile.ProcessedImages.Count;

                Logger.LogInformation("Starting deletion of image {ImageId} for user {UserId}. Profile has {ImageCount} images",
                    imageId, userId, imageCountBefore);

                // Delete physical files using storage service
                if (image.IsGenerated && !string.IsNullOrEmpty(image.ProcessedImageUrl))
                {
                    try
                    {
                        Logger.LogDebug("Attempting to delete generated image from storage: {StoragePath}", image.ProcessedImageUrl);

                        physicalFileDeleted = await _storageService.DeleteImageAsync(image.ProcessedImageUrl);
                        if (physicalFileDeleted)
                        {
                            Logger.LogInformation("Deleted generated image from storage: {StoragePath}", image.ProcessedImageUrl);
                        }
                        else
                        {
                            Logger.LogWarning("Generated image not found in storage: {StoragePath}", image.ProcessedImageUrl);
                        }
                    }
                    catch (Exception fileEx)
                    {
                        Logger.LogError(fileEx, "Error deleting generated image from storage for image {ImageId}. URL: {ProcessedUrl}, UserId: {UserId}",
                            imageId, image.ProcessedImageUrl, userId);
                        // Continue with database deletion even if storage deletion fails
                    }
                }
                else if (image.IsOriginalUpload && !string.IsNullOrEmpty(image.OriginalImageUrl))
                {
                    try
                    {
                        Logger.LogDebug("Attempting to delete uploaded image from storage: {StoragePath}", image.OriginalImageUrl);

                        physicalFileDeleted = await _storageService.DeleteImageAsync(image.OriginalImageUrl);
                        if (physicalFileDeleted)
                        {
                            Logger.LogInformation("Deleted uploaded image from storage: {StoragePath}", image.OriginalImageUrl);
                        }
                        else
                        {
                            Logger.LogWarning("Uploaded image not found in storage: {StoragePath}", image.OriginalImageUrl);
                        }
                    }
                    catch (Exception fileEx)
                    {
                        Logger.LogError(fileEx, "Error deleting uploaded image from storage for image {ImageId}. URL: {OriginalUrl}, UserId: {UserId}",
                            imageId, image.OriginalImageUrl, userId);
                        // Continue with database deletion even if storage deletion fails
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
                if (Context == null)
                {
                    return ErrorResponse("ServerError", "Database context unavailable", 500);
                }
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

                return SuccessResponse(new
                {
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

        /// <summary>
        /// Delete enhanced image file from temporary storage
        /// </summary>
        [HttpDelete("enhanced/{fileName}")]
        public async Task<IActionResult> DeleteEnhancedImage(string fileName)
        {
            var authCheck = ValidateAuthentication();
            if (authCheck != null) return authCheck;
            var userId = GetCurrentUserId()!;

            try
            {
                Logger.LogInformation("Attempting to delete enhanced image file {FileName} for user {UserId}", fileName, userId);

                // Validate fileName to prevent path traversal attacks
                if (string.IsNullOrEmpty(fileName) ||
                    fileName.Contains("..") ||
                    fileName.Contains("/") ||
                    fileName.Contains("\\") ||
                    Path.GetDirectoryName(fileName) != "")
                {
                    Logger.LogWarning("Invalid file name provided for enhanced image deletion: {FileName}", fileName);
                    return ErrorResponse("InvalidFileName", "Invalid file name", 400);
                }

                // Get storage path for enhanced image
                var storagePath = _pathResolver.GetPath(StorageType.Enhanced, userId, fileName);

                Logger.LogDebug("Checking for enhanced image file at storage path: {StoragePath}", storagePath);

                // Check if file exists and delete using blob storage
                var exists = await _storageService.ExistsAsync(storagePath);
                if (!exists)
                {
                    Logger.LogInformation("Enhanced image file already cleaned up: {StoragePath}", storagePath);
                    return SuccessResponse(new
                    {
                        fileName = fileName,
                        message = "Enhanced image file already cleaned up (idempotent delete)"
                    });
                }

                var deleted = await _storageService.DeleteImageAsync(storagePath);
                if (!deleted)
                {
                    Logger.LogWarning("Failed to delete enhanced image file: {StoragePath}", storagePath);
                    return ErrorResponse("DeletionFailed", "Failed to delete enhanced image file", 500);
                }

                Logger.LogInformation("Successfully deleted enhanced image file {FileName} for user {UserId}", fileName, userId);

                return SuccessResponse(new
                {
                    fileName = fileName,
                    message = "Enhanced image file deleted successfully"
                });
            }
            catch (Exception ex)
            {
                LogError(ex, $"Error deleting enhanced image file {fileName} for user {userId}");
                return ErrorResponse("DeletionFailed", "Failed to delete enhanced image file", 500);
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
                var host = Request?.Host.ToString() ?? "localhost:5032";
                var fallbackResult = $"{scheme}://{host}{relativePath}";
                Logger.LogDebug("GetAbsoluteUrl using request host fallback: {Result}", fallbackResult);
                return fallbackResult;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "GetAbsoluteUrl failed for path: {RelativePath}", relativePath);
                // Return a safe fallback that works with proxy
                var safeBaseUrl = _configuration?["AppBaseUrl"] ?? "https://localhost:5032";
                return $"{safeBaseUrl.TrimEnd('/')}{relativePath}";
            }
        }

        /// <summary>
        /// Creates a training ZIP file from uploaded images using blob storage
        /// </summary>
        private async Task<string?> CreateTrainingZipAsync(string userId)
        {
            try
            {
                // Get list of uploaded images for this user from blob storage
                var uploadsPrefix = _pathResolver.GetDirectoryPrefix(StorageType.Upload, userId);
                var imageFiles = await _storageService.ListFilesAsync(uploadsPrefix);

                if (imageFiles.Count < 10)
                {
                    Logger.LogWarning("User {UserId} has insufficient images for training ZIP ({Count}/10 required)",
                        userId, imageFiles.Count);
                    return null;
                }

                // Filter for valid image extensions
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var validImages = imageFiles.Where(file =>
                    allowedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant())
                ).ToList();

                if (validImages.Count < 10)
                {
                    Logger.LogWarning("User {UserId} has insufficient valid images for training ZIP ({Count}/10 required)",
                        userId, validImages.Count);
                    return null;
                }

                Logger.LogInformation("Creating training ZIP for user {UserId} with {Count} images", userId, validImages.Count);

                // Create ZIP in memory
                using var memoryStream = new MemoryStream();
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    foreach (var imagePath in validImages)
                    {
                        var fileName = Path.GetFileName(imagePath);
                        var imageStream = await _storageService.GetImageAsync(imagePath);

                        if (imageStream != null)
                        {
                            using (imageStream)
                            {
                                var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);
                                using var entryStream = entry.Open();
                                await imageStream.CopyToAsync(entryStream);
                            }
                        }
                    }
                }

                memoryStream.Position = 0;

                // Save ZIP to blob storage
                var zipFileName = $"{userId}.zip";
                var zipStoragePath = _pathResolver.GetPath(StorageType.TrainingZip, userId, zipFileName);
                await _storageService.SaveZipAsync(memoryStream, zipStoragePath);

                // Generate SAS URL for Replicate API access
                var sasUrl = await _storageService.GenerateSasUrlAsync(zipStoragePath, TimeSpan.FromHours(2));

                // For development, replace localhost with ngrok domain to allow external access
                if (sasUrl != null && sasUrl.Contains("127.0.0.1:10000"))
                {
                    var ngrokDomain = _configuration["Webhooks:NgrokTunnelUrl"] ?? "https://clear-anteater-usually.ngrok-free.app";
                    sasUrl = sasUrl.Replace("http://127.0.0.1:10000", ngrokDomain);

                    // Add ngrok-skip-browser-warning parameter for API access
                    var separator = sasUrl.Contains("?") ? "&" : "?";
                    sasUrl += $"{separator}ngrok-skip-browser-warning=true";

                    Logger.LogInformation("Converted localhost URL to ngrok domain: {NgrokUrl}", sasUrl);
                }

                Logger.LogInformation("Created training ZIP for user {UserId} with {FileCount} images at {StoragePath}",
                    userId, validImages.Count, zipStoragePath);

                return sasUrl;
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

                // Create training ZIP from existing uploaded images using blob storage
                var zipPath = await CreateTrainingZipAsync(userId);

                if (string.IsNullOrEmpty(zipPath))
                {
                    // Check specific reasons for failure
                    if (uploadedImages.Count < 10)
                    {
                        return ErrorResponse("InsufficientImages",
                            $"Need at least 10 images for training (currently {uploadedImages.Count})");
                    }

                    return ErrorResponse("ZipCreationFailed",
                        "Failed to create training ZIP file. Check that all uploaded images are still available.", 500);
                }

                return SuccessResponse(new
                {
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
        public async Task<IActionResult> GetTrainingZips()
        {
            try
            {
                var authCheck = ValidateAuthentication();
                if (authCheck != null) return authCheck;
                var userId = GetCurrentUserId()!;

                var userZipFiles = new List<object>();

                // Check if user's training ZIP exists in blob storage
                var zipFileName = $"{userId}.zip";
                var zipStoragePath = _pathResolver.GetPath(StorageType.TrainingZip, userId, zipFileName);

                if (await _storageService.ExistsAsync(zipStoragePath))
                {
                    var fileInfo = await _storageService.GetFileInfoAsync(zipStoragePath);
                    if (fileInfo != null)
                    {
                        // Generate SAS URL for download (valid for 1 hour)
                        var downloadUrl = await _storageService.GenerateSasUrlAsync(zipStoragePath, TimeSpan.FromHours(1));

                        userZipFiles.Add(new
                        {
                            fileName = zipFileName,
                            storagePath = zipStoragePath,
                            downloadUrl = downloadUrl,
                            createdAt = fileInfo.CreatedAt,
                            sizeBytes = fileInfo.Size
                        });
                    }
                }

                return SuccessResponse(userZipFiles);
            }
            catch (Exception ex)
            {
                LogError(ex, "Error getting training ZIP files");
                return ErrorResponse("ErrorGettingZips", "Failed to get training ZIP files", 500);
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
                var authCheck = ValidateAuthentication();
                if (authCheck != null) return authCheck;
                var userId = GetCurrentUserId()!;

                // Check if user's training ZIP exists in blob storage
                var zipFileName = $"{userId}.zip";
                var zipStoragePath = _pathResolver.GetPath(StorageType.TrainingZip, userId, zipFileName);

                var fileInfo = await _storageService.GetFileInfoAsync(zipStoragePath);
                if (fileInfo == null)
                {
                    return ErrorResponse("NoZipFiles", "No training ZIP files found for user.", 404);
                }

                // Generate SAS URL for access (valid for 2 hours for Replicate)
                var publicUrl = await _storageService.GenerateSasUrlAsync(zipStoragePath, TimeSpan.FromHours(2));

                // For development, replace localhost with ngrok domain to allow external access
                if (publicUrl != null && publicUrl.Contains("127.0.0.1:10000"))
                {
                    var ngrokDomain = _configuration["Webhooks:NgrokTunnelUrl"] ?? "https://clear-anteater-usually.ngrok-free.app";
                    publicUrl = publicUrl.Replace("http://127.0.0.1:10000", ngrokDomain);

                    // Add ngrok-skip-browser-warning parameter for API access
                    var separator = publicUrl.Contains("?") ? "&" : "?";
                    publicUrl += $"{separator}ngrok-skip-browser-warning=true";
                }

                return SuccessResponse(new
                {
                    fileName = zipFileName,
                    publicUrl = publicUrl,
                    createdAt = fileInfo.CreatedAt,
                    sizeBytes = fileInfo.Size
                });
            }
            catch (Exception ex)
            {
                LogError(ex, "Error getting latest training ZIP file");
                return ErrorResponse("ErrorGettingZip", "Failed to get latest training ZIP file", 500);
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
                var authCheck = ValidateAuthentication();
                if (authCheck != null) return authCheck;
                var userId = GetCurrentUserId()!;

                // Validate that the filename belongs to the current user
                if (fileName != $"{userId}.zip")
                {
                    return ErrorResponse("InvalidFileName", "Invalid filename or access denied.");
                }

                var zipStoragePath = _pathResolver.GetPath(StorageType.TrainingZip, userId, fileName);

                var deleted = await _storageService.DeleteImageAsync(zipStoragePath);
                if (!deleted)
                {
                    return ErrorResponse("FileNotFound", "Training ZIP file not found.", 404);
                }

                Logger.LogInformation("Deleted training ZIP file {FileName} for user {UserId}", fileName, userId);

                return SuccessResponse(new
                {
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
        public async Task<IActionResult> DeleteAllTrainingZips()
        {
            try
            {
                var authCheck = ValidateAuthentication();
                if (authCheck != null) return authCheck;
                var userId = GetCurrentUserId()!;

                // Use StoragePathResolver to get the correct storage path for training ZIPs
                var trainingZipPath = _pathResolver.GetPath(StorageType.TrainingZip, userId, $"{userId}.zip");

                var deleted = await _storageService.DeleteImageAsync(trainingZipPath);
                var deletedCount = deleted ? 1 : 0;

                if (deleted)
                {
                    Logger.LogInformation("Deleted training ZIP file at {StoragePath} for user {UserId}", trainingZipPath, userId);
                }
                else
                {
                    Logger.LogInformation("No training ZIP file found at {StoragePath} for user {UserId}", trainingZipPath, userId);
                }

                return SuccessResponse(new
                {
                    deletedCount = deletedCount,
                    message = $"Deleted {deletedCount} training ZIP files successfully."
                });
            }
            catch (Exception ex)
            {
                LogError(ex, "Error deleting all training ZIP files");
                return ErrorResponse("StorageError", "Failed to delete training ZIP files.", 500);
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

















        #endregion

        /// <summary>
        /// Reconcile database image records with backing storage.
        /// Removes records that reference non-existent files. When dryRun=true, only reports counts.
        /// </summary>
        [HttpPost("reconcile-database")]
        public async Task<IActionResult> ReconcileDatabase([FromQuery] bool dryRun = true)
        {
            var authCheck = ValidateAuthentication();
            if (authCheck != null) return authCheck;
            var userId = GetCurrentUserId()!;

            try
            {
                // Safety guard: never allow destructive reconcile in Production environment
                if (_environment.IsProduction() && !dryRun)
                {
                    return ErrorResponse("Forbidden", "Destructive reconciliation is disabled in production", 403);
                }

                var profile = await _userProfileRepository.GetByUserIdAsync(userId);
                if (profile == null)
                    return ErrorResponse("ProfileNotFound", "Profile not found", 404);

                var allImages = profile.ProcessedImages.ToList();
                var orphaned = new List<ProcessedImage>();
                var skippedAmbiguous = 0;

                async Task<bool> PathExistsAsync(string? path)
                {
                    if (string.IsNullOrEmpty(path)) return false;
                    if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return true; // cannot verify external URLs; do not mark missing
                    return await _storageService.ExistsAsync(path);
                }

                foreach (var img in allImages)
                {
                    // If both URLs are missing entirely, this is clearly orphaned
                    if (string.IsNullOrEmpty(img.OriginalImageUrl) && string.IsNullOrEmpty(img.ProcessedImageUrl))
                    {
                        orphaned.Add(img);
                        continue;
                    }

                    var isClearlyOriginal = img.IsOriginalUpload && !img.IsGenerated;
                    var isClearlyGenerated = img.IsGenerated && !img.IsOriginalUpload;

                    // Resolve ambiguous flags using style when possible
                    if (!isClearlyOriginal && !isClearlyGenerated)
                    {
                        if (string.Equals(img.Style, ImageConstants.OriginalStyle, StringComparison.OrdinalIgnoreCase))
                        {
                            isClearlyOriginal = true;
                        }
                        else if (!string.IsNullOrEmpty(img.Style) && !string.Equals(img.Style, ImageConstants.OriginalStyle, StringComparison.OrdinalIgnoreCase))
                        {
                            isClearlyGenerated = true;
                        }
                    }

                    // Original uploads must have the original file present; processed file is not required
                    if (isClearlyOriginal)
                    {
                        var originalExists = await PathExistsAsync(img.OriginalImageUrl);
                        if (!originalExists)
                        {
                            orphaned.Add(img);
                        }
                        continue;
                    }

                    // Generated images must have the processed file present; original file is not required
                    if (isClearlyGenerated)
                    {
                        var processedExists = await PathExistsAsync(img.ProcessedImageUrl);
                        if (!processedExists)
                        {
                            orphaned.Add(img);
                        }
                        continue;
                    }

                    // Ambiguous records (cannot confidently classify) — do not delete to avoid data loss
                    skippedAmbiguous++;
                }

                if (dryRun)
                {
                    // Include fields used by tests/clients even in dry-run
                    var totalUsersWithImages = allImages.Any() ? 1 : 0; // this endpoint reconciles only current user
                    return SuccessResponse(new
                    {
                        TotalImages = allImages.Count,
                        OrphanedRecords = orphaned.Count,
                        OrphanedRecordsRemoved = 0,
                        TotalUsers = totalUsersWithImages,
                        SkippedAmbiguousRecords = skippedAmbiguous,
                        Message = orphaned.Count > 0
                            ? $"Found {orphaned.Count} orphaned image record(s)"
                            : "No orphaned image records found",
                        Timestamp = DateTime.UtcNow
                    });
                }

                foreach (var img in orphaned)
                {
                    profile.ProcessedImages.Remove(img);
                    // Ensure persistence even if repository mock isn't configured in tests
                    Context?.ProcessedImages.Remove(img);
                }

                if (orphaned.Count > 0)
                {
                    await Context!.SaveChangesAsync();
                    // Update repo/caches as well for real application behavior
                    if (_userProfileRepository != null)
                        await _userProfileRepository.UpdateAsync(profile);
                    if (_userContextService != null)
                        await _userContextService.InvalidateUserCacheAsync(userId);
                    LogInfo($"Removed {orphaned.Count} orphaned image records for user {userId}");
                }

                return SuccessResponse(new
                {
                    TotalImagesChecked = allImages.Count,
                    OrphanedRecordsRemoved = orphaned.Count,
                    SkippedAmbiguousRecords = skippedAmbiguous,
                    Message = orphaned.Count > 0
                        ? $"Successfully removed {orphaned.Count} orphaned image record(s)"
                        : "No orphaned image records to remove",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                LogError(ex, "Error reconciling image database", userId);
                return ErrorResponse("ReconciliationFailed", "Failed to reconcile image database", 500);
            }
        }

    }

}
