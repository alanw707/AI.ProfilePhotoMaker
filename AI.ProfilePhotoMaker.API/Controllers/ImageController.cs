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

                    // Get the URL for the uploaded image
                    var imageUrl = _storageService.GetImageUrl(storagePath);

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

                    uploadResults.Add(new
                    {
                        FileName = fileName,
                        Size = image.Length,
                        Url = imageUrl
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
        /// Debug endpoint to test URL generation in different environments
        /// </summary>
        [HttpGet("debug/url-test")]
        [AllowAnonymous]
        public IActionResult TestUrlGeneration()
        {
            if (!_environment.IsDevelopment())
            {
                return ErrorResponse("DebugEndpointDisabled", "Debug endpoints are only available in development environment", 404);
            }
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
            if (!_environment.IsDevelopment())
            {
                return ErrorResponse("DebugEndpointDisabled", "Debug endpoints are only available in development environment", 404);
            }
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
                    StorageCheck = new
                    {
                        Note = "Storage checks now use blob storage service instead of filesystem"
                    },
                    DetailedImageAnalysis = await GetDetailedImageAnalysisAsync(allImages, userId),
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
        /// Get detailed image analysis with storage existence checks
        /// </summary>
        private async Task<List<object>> GetDetailedImageAnalysisAsync(List<ProcessedImage> images, string userId)
        {
            var analysisResults = new List<object>();
            
            foreach (var img in images)
            {
                var originalExists = await CheckStorageExistsAsync(img.OriginalImageUrl);
                var processedExists = await CheckStorageExistsAsync(img.ProcessedImageUrl);
                
                analysisResults.Add(new
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
                    OriginalFileExists = originalExists,
                    ProcessedFileExists = processedExists,
                    Classification = GetImageClassification(img),
                    PotentialIssues = GetImageIssues(img)
                });
            }
            
            return analysisResults;
        }

        /// <summary>
        /// Check if an image file exists in storage
        /// </summary>
        private async Task<bool> CheckStorageExistsAsync(string? imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl)) return false;

            try
            {
                // For external URLs, we can't check storage
                if (imageUrl.StartsWith("http"))
                {
                    return false; // Indicate we can't verify external URLs
                }

                // Check storage using storage service
                return await _storageService.ExistsAsync(imageUrl);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get count of orphaned images (database records without corresponding storage files)
        /// </summary>
        private async Task<int> GetOrphanedImages(string userId, bool isOriginalUploads)
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
                    if (!await CheckStorageExistsAsync(urlToCheck))
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
        [Authorize(Roles = "Admin")]
        [HttpPost("debug/repair-database-flags")]
        public async Task<IActionResult> RepairDatabaseFlags()
        {
            if (!_environment.IsDevelopment())
            {
                return ErrorResponse("DebugEndpointDisabled", "Debug endpoints are only available in development environment", 404);
            }
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
        [Authorize(Roles = "Admin")]
        [HttpPost("debug/repair-style-corruption")]
        public async Task<IActionResult> RepairStyleCorruption()
        {
            if (!_environment.IsDevelopment())
            {
                return ErrorResponse("DebugEndpointDisabled", "Debug endpoints are only available in development environment", 404);
            }
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
        [Authorize(Roles = "Admin")]
        [HttpPost("debug/cleanup-orphaned-records")]
        public async Task<IActionResult> CleanupOrphanedRecords()
        {
            if (!_environment.IsDevelopment())
            {
                return ErrorResponse("DebugEndpointDisabled", "Debug endpoints are only available in development environment", 404);
            }
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

                    // Check if original upload files exist in storage
                    if (img.IsOriginalUpload && !string.IsNullOrEmpty(img.OriginalImageUrl))
                    {
                        if (!await CheckStorageExistsAsync(img.OriginalImageUrl))
                        {
                            shouldRemove = true;
                            issues.Add($"Original upload file not found in storage: {img.OriginalImageUrl}");
                        }
                    }

                    // Check if generated image files exist in storage
                    if (img.IsGenerated && !string.IsNullOrEmpty(img.ProcessedImageUrl))
                    {
                        if (!await CheckStorageExistsAsync(img.ProcessedImageUrl))
                        {
                            shouldRemove = true;
                            issues.Add($"Generated image file not found in storage: {img.ProcessedImageUrl}");
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
        /// Clean up enhanced images that shouldn't be in the database
        /// Enhanced images are temporary files and should never be recorded as uploads
        /// </summary>
        [HttpPost("debug/cleanup-enhanced-images")]
        public async Task<IActionResult> CleanupEnhancedImages()
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
                var enhancedImages = new List<ProcessedImage>();
                var removedDetails = new List<object>();

                foreach (var img in allImages)
                {
                    bool isEnhancedImage = false;
                    var issues = new List<string>();

                    // Check if this is an enhanced image (temporary file that shouldn't be in database)
                    if (img.OriginalImageUrl?.Contains("/enhanced/") == true ||
                        img.ProcessedImageUrl?.Contains("/enhanced/") == true ||
                        img.OriginalImageUrl?.Contains("enhanced_") == true ||
                        img.ProcessedImageUrl?.Contains("enhanced_") == true)
                    {
                        isEnhancedImage = true;
                        issues.Add($"Enhanced image found in database: {img.OriginalImageUrl ?? img.ProcessedImageUrl}");
                    }

                    if (isEnhancedImage)
                    {
                        enhancedImages.Add(img);
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

                // Remove enhanced images from the profile
                foreach (var enhancedImage in enhancedImages)
                {
                    profile.ProcessedImages.Remove(enhancedImage);
                }

                if (enhancedImages.Count > 0)
                {
                    await _userProfileRepository.UpdateAsync(profile);
                    LogInfo($"Removed {enhancedImages.Count} enhanced image records for user {userId}");
                }

                return SuccessResponse(new
                {
                    TotalImagesChecked = allImages.Count,
                    EnhancedImagesRemoved = enhancedImages.Count,
                    RemovedImageDetails = removedDetails,
                    Message = enhancedImages.Count > 0
                        ? $"Successfully removed {enhancedImages.Count} enhanced image records that shouldn't be in database"
                        : "No enhanced image records found in database",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                LogError(ex, "Error cleaning up enhanced images", userId);
                return ErrorResponse("CleanupFailed", "Failed to cleanup enhanced images", 500);
            }
        }

        /// <summary>
        /// Complete repair solution - runs all repairs and invalidates UI cache
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPost("debug/complete-repair")]
        public async Task<IActionResult> CompleteRepair()
        {
            if (!_environment.IsDevelopment())
            {
                return ErrorResponse("DebugEndpointDisabled", "Debug endpoints are only available in development environment", 404);
            }
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

                // Step 1: Enhanced images cleanup (new step)
                LogInfo("Starting complete repair process - Step 1: Enhanced images cleanup");
                var enhancedCleanupResult = await CleanupEnhancedImages();

                // Step 2: Style corruption repair
                LogInfo("Complete repair process - Step 2: Style corruption repair");
                var styleRepairResult = await RepairStyleCorruption();

                // Step 3: Orphaned records cleanup  
                LogInfo("Complete repair process - Step 3: Orphaned records cleanup");
                var orphanedCleanupResult = await CleanupOrphanedRecords();

                // Step 4: Invalidate user cache to force UI refresh
                LogInfo("Complete repair process - Step 4: Cache invalidation");
                await _userContextService.InvalidateUserCacheAsync(userId);

                LogInfo($"Complete repair process finished for user {userId}");

                return SuccessResponse(new
                {
                    Message = "Complete repair process finished successfully",
                    Steps = new[]
                    {
                        "✅ Enhanced images cleanup completed",
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
                    return SuccessResponse(new
                    {
                        TotalImages = allImages.Count,
                        OrphanedRecords = orphaned.Count,
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
                }

                if (orphaned.Count > 0)
                {
                    await _userProfileRepository.UpdateAsync(profile);
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