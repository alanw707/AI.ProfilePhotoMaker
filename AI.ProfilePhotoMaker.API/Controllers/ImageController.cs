using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Services.Storage;
using AI.ProfilePhotoMaker.API.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using System.Text.Json;

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
        private static new string S(string? value) => LoggingSanitizer.Sanitize(value);
        private static new string Sid(string? value) => LoggingSanitizer.SanitizeId(value);

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
        /// Reconcile database image records with backing storage.
        /// Removes records that reference non-existent files. When dryRun=true, only reports counts.
        /// </summary>
        [HttpPost("reconcile-database")]
        public async Task<IActionResult> ReconcileDatabase([FromQuery] bool dryRun = true)
        {
            // Allow anonymous access in development for database maintenance
            string? userId = null;
            if (_environment.IsProduction())
            {
                var authCheck = ValidateAuthentication();
                if (authCheck != null) return authCheck;
                userId = GetCurrentUserId()!;
            }
            else
            {
                // In development, we'll process all images for cleanup
                LogInfo("Running reconcile-database in development mode (anonymous access)");
            }

            try
            {
                // Safety guard: never allow destructive reconcile in Production environment
                if (_environment.IsProduction() && !dryRun)
                {
                    return ErrorResponse("Forbidden", "Destructive reconciliation is disabled in production", 403);
                }

                List<ProcessedImage> allImages;
                UserProfile? profile = null;

                if (userId != null)
                {
                    // Production mode: process images for specific user
                    profile = await _userProfileRepository.GetByUserIdAsync(userId);
                    if (profile == null)
                        return ErrorResponse("ProfileNotFound", "Profile not found", 404);
                    allImages = profile.ProcessedImages.ToList();
                }
                else
                {
                    // Development mode: process all images for cleanup
                    if (Context == null)
                    {
                        return ErrorResponse("ServerError", "Database context unavailable", 500);
                    }
                    allImages = await Context.ProcessedImages.ToListAsync();
                }

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
                    // Only remove from profile if we have a profile (production mode)
                    if (profile != null)
                        profile.ProcessedImages.Remove(img);
                    // Always remove from context in both modes
                    Context?.ProcessedImages.Remove(img);
                }

                if (orphaned.Count > 0)
                {
                    await Context!.SaveChangesAsync();
                    // Update repo/caches as well for real application behavior (production mode only)
                    if (profile != null && _userProfileRepository != null)
                        await _userProfileRepository.UpdateAsync(profile);
                    if (userId != null && _userContextService != null)
                        await _userContextService.InvalidateUserCacheAsync(userId);
                    LogInfo($"Removed {orphaned.Count} orphaned image records" + (userId != null ? $" for user {Sid(userId)}" : " (development cleanup)"));
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
                LogError(ex, "Error reconciling image database", Sid(userId));
                return ErrorResponse("ReconciliationFailed", "Failed to reconcile image database", 500);
            }
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

                    // For ENHANCEMENT uploads: do NOT create gallery records.
                    // These are temporary inputs for enhancement workflows.
                    if (!dto.IsEnhanced)
                    {
                        // Store storage path in DB for original uploads so they appear in the gallery
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

                        Logger.LogInformation(
                            "Created database record for uploaded image {FileName} for user {UserId}",
                            fileName, userId);
                    }
                    else
                    {
                        Logger.LogInformation(
                            "Stored temporary enhanced upload (no DB record) {FileName} for user {UserId}",
                            fileName, userId);
                    }

                    // Return a public URL for the client while keeping storagePath in DB
                    var publicUrl = _storageService.GetImageUrl(storagePath);
                    uploadResults.Add(new
                    {
                        FileName = fileName,
                        Size = image.Length,
                        Url = publicUrl,
                        StoragePath = storagePath
                    });
                }

                // Note: Do NOT consume credits here for enhanced images.
                // Credits are handled by the enhancement workflow (e.g., OpenAI/Replicate controllers).

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

            string? ResolveImageUrl(string? imagePath)
            {
                if (string.IsNullOrEmpty(imagePath))
                {
                    return null;
                }

                return imagePath.StartsWith("http") ? imagePath : _storageService.GetImageUrl(imagePath);
            }

            foreach (var i in profile.ProcessedImages.OrderByDescending(i => i.CreatedAt))
            {
                images.Add(new
                {
                    id = i.Id,
                    originalImageUrl = ResolveImageUrl(i.OriginalImageUrl),
                    processedImageUrl = ResolveImageUrl(i.ProcessedImageUrl),
                    style = i.Style,
                    createdAt = i.CreatedAt,
                    isOriginalUpload = i.IsOriginalUpload,
                    isGenerated = i.IsGenerated,
                    provider = i.Provider,
                    providerModel = i.ProviderModel,
                    generationMode = i.GenerationMode,
                    promptVersion = i.PromptVersion,
                    correlationId = i.CorrelationId,
                    creditCost = i.CreditCost,
                    generationStatus = i.GenerationStatus,
                    failureReason = i.FailureReason
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
                            Logger.LogInformation("Deleted generated image from storage: {StoragePath}", S(image.ProcessedImageUrl));
                        }
                        else
                        {
                            Logger.LogWarning("Generated image not found in storage: {StoragePath}", S(image.ProcessedImageUrl));
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
                            Logger.LogInformation("Deleted uploaded image from storage: {StoragePath}", S(image.OriginalImageUrl));
                        }
                        else
                        {
                            Logger.LogWarning("Uploaded image not found in storage: {StoragePath}", S(image.OriginalImageUrl));
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
                Logger.LogInformation("Saving profile changes to database for user {UserId}", Sid(userId));
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

                await WriteImageDeletionAuditLogAsync(userId, image);

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


        #region Helper Methods

        private async Task WriteImageDeletionAuditLogAsync(string userId, ProcessedImage image)
        {
            if (Context == null)
            {
                return;
            }

            try
            {
                Context.AdminAuditLogs.Add(new AdminAuditLog
                {
                    AdminUserId = userId,
                    TargetUserId = userId,
                    Action = "ImageDeletedByUser",
                    Details = JsonSerializer.Serialize(new
                    {
                        imageId = image.Id,
                        imageType = image.IsOriginalUpload ? "upload" : image.IsGenerated ? "generated" : "image",
                        imageStyle = image.Style,
                        deletedBy = "user"
                    }),
                    OldValue = image.IsOriginalUpload ? image.OriginalImageUrl : image.ProcessedImageUrl,
                    NewValue = "Deleted",
                    CreatedAt = DateTime.UtcNow
                });

                await Context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to write image deletion audit log for image {ImageId}", image.Id);
            }
        }

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

                var allowedForwardedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var configured in new[]
                         {
                             _configuration?["AppBaseUrl"],
                             _configuration?["ExternalApiBaseUrl"],
                             _configuration?["Webhooks:NgrokTunnelUrl"]
                         })
                {
                    if (string.IsNullOrWhiteSpace(configured)) continue;
                    if (Uri.TryCreate(configured, UriKind.Absolute, out var parsed) && !string.IsNullOrWhiteSpace(parsed.Host))
                    {
                        allowedForwardedHosts.Add(parsed.Host);
                    }
                }

                // Priority 1: Use X-Forwarded-Host header only when it matches configured allowed hosts
                var forwardedHost = Request?.Headers["X-Forwarded-Host"].FirstOrDefault();
                if (!string.IsNullOrEmpty(forwardedHost) && allowedForwardedHosts.Contains(forwardedHost))
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

        private string? RewriteStorageUrlForExternalAccess(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return url;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
            {
                return url;
            }

            // Keep real Azure Blob URLs intact.
            if (parsed.Host.Contains("blob.core.windows.net", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            // Only rewrite when the storage URL points to internal/local endpoints (e.g. docker service name).
            var host = parsed.Host.ToLowerInvariant();
            var isInternalBlobHost = host is "azurite" or "localhost" or "127.0.0.1";
            if (!isInternalBlobHost)
            {
                return url;
            }

            // Prefer ngrok tunnel for external services (Replicate), then fall back.
            var externalApiBaseUrl = _configuration?["ExternalApiBaseUrl"];
            var ngrokBaseUrl = _configuration?["Webhooks:NgrokTunnelUrl"];

            var externalBaseUrl =
                !string.IsNullOrWhiteSpace(externalApiBaseUrl)
                    ? externalApiBaseUrl
                    : !string.IsNullOrWhiteSpace(ngrokBaseUrl)
                        ? ngrokBaseUrl
                        : _configuration?["AppBaseUrl"]
                          ?? $"{Request?.Scheme ?? "https"}://{Request?.Host.ToString() ?? "localhost:5032"}";

            if (string.IsNullOrWhiteSpace(externalBaseUrl))
            {
                return url;
            }

            var rewritten = $"{externalBaseUrl.TrimEnd('/')}{parsed.AbsolutePath}{parsed.Query}";

            // Ngrok sometimes shows an interstitial unless this param is set; easiest for external services.
            if (externalBaseUrl.Contains("ngrok", StringComparison.OrdinalIgnoreCase)
                && !rewritten.Contains("ngrok-skip-browser-warning=", StringComparison.OrdinalIgnoreCase))
            {
                rewritten += (rewritten.Contains('?') ? "&" : "?") + "ngrok-skip-browser-warning=true";
            }

            return rewritten;
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

                if (imageFiles.Count < 5)
                {
                    Logger.LogWarning("User {UserId} has insufficient images for training ZIP ({Count}/5 required)",
                        userId, imageFiles.Count);
                    return null;
                }

                // Filter for valid image extensions
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var validImages = imageFiles.Where(file =>
                    allowedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant())
                ).ToList();

                if (validImages.Count < 5)
                {
                    Logger.LogWarning("User {UserId} has insufficient valid images for training ZIP ({Count}/5 required)",
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
                sasUrl = RewriteStorageUrlForExternalAccess(sasUrl);

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
                    if (uploadedImages.Count < 5)
                    {
                        return ErrorResponse("InsufficientImages",
                            $"Need at least 5 images for training (currently {uploadedImages.Count})");
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
                        downloadUrl = RewriteStorageUrlForExternalAccess(downloadUrl);

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
                publicUrl = RewriteStorageUrlForExternalAccess(publicUrl);

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

                Logger.LogInformation("Deleted training ZIP file {FileName} for user {UserId}", S(fileName), Sid(userId));

                return SuccessResponse(new
                {
                    fileName = fileName,
                    message = "Training ZIP file deleted successfully."
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error deleting training ZIP file {FileName} for user {UserId}", S(fileName), Sid(GetCurrentUserId()));
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
                    Logger.LogInformation("Deleted training ZIP file at {StoragePath} for user {UserId}", S(trainingZipPath), Sid(userId));
                }
                else
                {
                    Logger.LogInformation("No training ZIP file found at {StoragePath} for user {UserId}", S(trainingZipPath), Sid(userId));
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
        /// Diagnostic endpoint to show database records vs storage files for generated photos
        /// </summary>
        [HttpGet("diagnostic")]
        [AllowAnonymous] // Allow unauthenticated access for debugging
        public async Task<IActionResult> DiagnosticInfo([FromQuery] string? userId = null)
        {
            // For debugging - allow bypassing auth if userId provided, otherwise require auth
            if (string.IsNullOrEmpty(userId))
            {
                var authCheck = ValidateAuthentication();
                if (authCheck != null) return authCheck;
                userId = GetCurrentUserId()!;
            }

            try
            {
                var profile = await _userProfileRepository.GetByUserIdAsync(userId);
                if (profile == null)
                    return ErrorResponse("ProfileNotFound", "Profile not found", 404);

                var allImages = profile.ProcessedImages.OrderByDescending(i => i.CreatedAt).ToList();
                var generatedImages = allImages.Where(i => i.IsGenerated).ToList();

                var diagnosticInfo = new
                {
                    totalImages = allImages.Count,
                    generatedImages = generatedImages.Count,
                    uploadedImages = allImages.Count(i => i.IsOriginalUpload),
                    styleBreakdown = allImages.GroupBy(i => i.Style).Select(g => new
                    {
                        style = g.Key,
                        count = g.Count(),
                        generatedCount = g.Count(i => i.IsGenerated)
                    }).ToList(),
                    recentImages = allImages.Take(20).Select(img => new
                    {
                        id = img.Id,
                        style = img.Style,
                        isGenerated = img.IsGenerated,
                        isOriginalUpload = img.IsOriginalUpload,
                        createdAt = img.CreatedAt,
                        originalImageUrl = img.OriginalImageUrl,
                        processedImageUrl = img.ProcessedImageUrl,
                        hasOriginalUrl = !string.IsNullOrEmpty(img.OriginalImageUrl),
                        hasProcessedUrl = !string.IsNullOrEmpty(img.ProcessedImageUrl),
                        classification = GetImageClassification(img)
                    }).ToList()
                };

                return SuccessResponse(diagnosticInfo);
            }
            catch (Exception ex)
            {
                LogError(ex, "Error getting diagnostic info");
                return ErrorResponse("DiagnosticError", "Failed to get diagnostic info", 500);
            }
        }



        /// <summary>
        /// Saves enhanced image from base64 data to storage and creates database record
        /// </summary>
        [HttpPost("save-enhanced")]
        public async Task<IActionResult> SaveEnhancedImage([FromBody] SaveEnhancedImageDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate authentication
            var authCheck = ValidateAuthentication();
            if (authCheck != null) return authCheck;
            var userId = GetCurrentUserId()!;

            // Use a fresh, lightweight profile read to avoid cached credit data.
            var profile = await _userProfileRepository.GetByUserIdLightAsync(userId);
            if (profile == null)
            {
                return ErrorResponse("ProfileNotFound", "User profile not found");
            }

            try
            {
                // Validate and parse base64 data
                if (string.IsNullOrEmpty(dto.Base64ImageData))
                {
                    return ErrorResponse("InvalidData", "Base64 image data is required");
                }

                // Remove data URL prefix if present (e.g., "data:image/png;base64,")
                var base64Data = dto.Base64ImageData;
                if (base64Data.StartsWith("data:image/"))
                {
                    var commaIndex = base64Data.IndexOf(',');
                    if (commaIndex >= 0)
                    {
                        base64Data = base64Data.Substring(commaIndex + 1);
                    }
                }

                // Convert base64 to byte array
                byte[] imageBytes;
                try
                {
                    imageBytes = Convert.FromBase64String(base64Data);
                }
                catch (FormatException)
                {
                    return ErrorResponse("InvalidBase64", "Invalid base64 image data format");
                }

                // Generate filename with appropriate extension
                var fileExtension = ".png"; // Default to PNG for enhanced images
                var fileName = $"{Guid.NewGuid()}_enhanced{fileExtension}";

                // Get storage path using path resolver
                var storagePath = _pathResolver.GetPath(StorageType.Enhanced, userId, fileName);

                // Save to blob storage
                using var imageStream = new MemoryStream(imageBytes);
                await _storageService.SaveImageToPathAsync(imageStream, storagePath);

                // Create database record
                var processedImage = new ProcessedImage
                {
                    OriginalImageUrl = string.Empty,
                    ProcessedImageUrl = storagePath,
                    Style = $"Enhanced-{dto.EnhancementType ?? "professional"}",
                    UserProfileId = profile.Id,
                    CreatedAt = DateTime.UtcNow,
                    IsOriginalUpload = false,
                    IsGenerated = true, // Enhanced images are AI-generated content
                };

                // Set scheduled deletion date based on retention policy
                processedImage.SetScheduledDeletionDate();

                if (Context == null)
                {
                    return ErrorResponse("ServerError", "Database context unavailable", 500);
                }

                Context.ProcessedImages.Add(processedImage);
                await Context.SaveChangesAsync();

                Logger.LogInformation("Saved enhanced image {FileName} for user {UserId} with type {EnhancementType}",
                    fileName, userId, dto.EnhancementType);

                // Return the saved image URL
                var publicUrl = _storageService.GetImageUrl(storagePath);

                return SuccessResponse(new
                {
                    Id = processedImage.Id,
                    FileName = fileName,
                    Url = publicUrl,
                    EnhancementType = dto.EnhancementType,
                    CreatedAt = processedImage.CreatedAt,
                    Message = "Enhanced image saved successfully to gallery"
                });
            }
            catch (Exception ex)
            {
                LogError(ex, "Error saving enhanced image", userId);
                return ErrorResponse("SaveFailed", "Failed to save enhanced image", 500);
            }
        }

    }

}
