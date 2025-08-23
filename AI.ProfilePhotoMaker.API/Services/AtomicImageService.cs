using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services.Storage;
using AI.ProfilePhotoMaker.API.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AI.ProfilePhotoMaker.API.Services;

/// <summary>
/// Service for atomic image operations ensuring database and storage consistency
/// </summary>
public interface IAtomicImageService
{
    /// <summary>
    /// Atomically uploads images with database transaction rollback on failure
    /// </summary>
    Task<AtomicUploadResult> AtomicUploadAsync(
        string userId,
        List<IFormFile> images,
        StorageType storageType,
        string filePrefix,
        bool createDatabaseRecords = true);

    /// <summary>
    /// Atomically deletes an image with database transaction rollback on failure
    /// </summary>
    Task<AtomicDeleteResult> AtomicDeleteAsync(string userId, int imageId);

    /// <summary>
    /// Atomically deletes multiple images with database transaction rollback on failure
    /// </summary>
    Task<AtomicDeleteResult> AtomicBulkDeleteAsync(string userId, List<int> imageIds);
}

/// <summary>
/// Result of atomic upload operation
/// </summary>
public class AtomicUploadResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ProcessedImage> UploadedImages { get; set; } = new();
    public List<string> StoragePaths { get; set; } = new();
    public int FilesUploaded { get; set; }
    public int DatabaseRecordsCreated { get; set; }
}

/// <summary>
/// Result of atomic delete operation
/// </summary>
public class AtomicDeleteResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int FilesDeleted { get; set; }
    public int DatabaseRecordsDeleted { get; set; }
    public List<string> DeletedStoragePaths { get; set; } = new();
}

/// <summary>
/// Atomic image service implementation with transaction safety
/// </summary>
public class AtomicImageService : IAtomicImageService
{
    private readonly ApplicationDbContext _context;
    private readonly IStorageService _storageService;
    private readonly StoragePathResolver _pathResolver;
    private readonly ILogger<AtomicImageService> _logger;

    public AtomicImageService(
        ApplicationDbContext context,
        IStorageService storageService,
        StoragePathResolver pathResolver,
        ILogger<AtomicImageService> logger)
    {
        _context = context;
        _storageService = storageService;
        _pathResolver = pathResolver;
        _logger = logger;
    }

    /// <summary>
    /// Atomically uploads images with full rollback on any failure
    /// </summary>
    public async Task<AtomicUploadResult> AtomicUploadAsync(
        string userId,
        List<IFormFile> images,
        StorageType storageType,
        string filePrefix,
        bool createDatabaseRecords = true)
    {
        var result = new AtomicUploadResult();
        var uploadedStoragePaths = new List<string>();
        var createdImages = new List<ProcessedImage>();

        IDbContextTransaction? transaction = null;
        var providerName = _context.Database.ProviderName ?? string.Empty;
        var isInMemory = providerName.Contains("InMemory", StringComparison.OrdinalIgnoreCase);
        if (!isInMemory)
        {
            transaction = await _context.Database.BeginTransactionAsync();
        }

        try
        {
            _logger.LogInformation("Starting atomic upload for user {UserId} with {Count} images",
                userId, images.Count);

            // Step 1: Get user profile within transaction
            var profile = await _context.UserProfiles
                .Include(p => p.ProcessedImages)
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                result.ErrorMessage = "User profile not found";
                return result;
            }

            // Step 2: Upload files to storage first (no database changes yet)
            foreach (var image in images)
            {
                try
                {
                    // Generate clean filename
                    var extension = Path.GetExtension(image.FileName);
                    var fileName = $"{Guid.NewGuid()}_{filePrefix}{extension}";

                    // Get storage path
                    var storagePath = _pathResolver.GetPath(storageType, userId, fileName);

                    // Save to storage
                    await using var imageStream = image.OpenReadStream();
                    await _storageService.SaveImageToPathAsync(imageStream, storagePath);

                    uploadedStoragePaths.Add(storagePath);
                    result.FilesUploaded++;

                    _logger.LogDebug("Uploaded file {FileName} to storage path {StoragePath}",
                        fileName, storagePath);

                    // Prepare database record if needed
                    if (createDatabaseRecords)
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

                        // Set retention policy
                        processedImage.SetScheduledDeletionDate();

                        createdImages.Add(processedImage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upload file {FileName} for user {UserId}",
                        image.FileName, userId);

                    // Cleanup already uploaded files
                    await CleanupStorageFilesAsync(uploadedStoragePaths);

                    result.ErrorMessage = $"Failed to upload file {image.FileName}: {ex.Message}";
                    return result;
                }
            }

            // Step 3: Create database records within transaction
            if (createDatabaseRecords && createdImages.Any())
            {
                try
                {
                    foreach (var image in createdImages)
                    {
                        profile.ProcessedImages.Add(image);
                    }

                    await _context.SaveChangesAsync();
                    result.DatabaseRecordsCreated = createdImages.Count;

                    _logger.LogInformation("Created {Count} database records for user {UserId}",
                        createdImages.Count, userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create database records for user {UserId}", userId);

                    // Cleanup uploaded files since database failed
                    await CleanupStorageFilesAsync(uploadedStoragePaths);

                    result.ErrorMessage = $"Failed to create database records: {ex.Message}";
                    return result;
                }
            }

            // Step 4: Commit transaction
            if (transaction != null)
            {
                await transaction.CommitAsync();
            }

            // Success!
            result.Success = true;
            result.UploadedImages = createdImages;
            result.StoragePaths = uploadedStoragePaths;

            _logger.LogInformation("Atomic upload completed successfully for user {UserId}. " +
                "Files: {FileCount}, DB Records: {DbCount}",
                userId, result.FilesUploaded, result.DatabaseRecordsCreated);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Atomic upload failed for user {UserId}", userId);

            // Rollback transaction (this also happens automatically on dispose)
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }

            // Cleanup storage files
            await CleanupStorageFilesAsync(uploadedStoragePaths);

            result.ErrorMessage = $"Atomic upload failed: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Atomically deletes a single image with full rollback on failure
    /// </summary>
    public async Task<AtomicDeleteResult> AtomicDeleteAsync(string userId, int imageId)
    {
        return await AtomicBulkDeleteAsync(userId, new List<int> { imageId });
    }

    /// <summary>
    /// Atomically deletes multiple images with full rollback on failure
    /// </summary>
    public async Task<AtomicDeleteResult> AtomicBulkDeleteAsync(string userId, List<int> imageIds)
    {
        var result = new AtomicDeleteResult();
        var imagesToDelete = new List<ProcessedImage>();
        var storagePathsToDelete = new List<string>();

        IDbContextTransaction? transaction = null;
        var providerName = _context.Database.ProviderName ?? string.Empty;
        var isInMemory = providerName.Contains("InMemory", StringComparison.OrdinalIgnoreCase);
        if (!isInMemory)
        {
            transaction = await _context.Database.BeginTransactionAsync();
        }

        try
        {
            _logger.LogInformation("Starting atomic delete for user {UserId} with {Count} images",
                userId, imageIds.Count);

            // Step 1: Find and validate images within transaction
            var profile = await _context.UserProfiles
                .Include(p => p.ProcessedImages)
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
            {
                result.ErrorMessage = "User profile not found";
                return result;
            }

            foreach (var imageId in imageIds)
            {
                var image = profile.ProcessedImages.FirstOrDefault(i => i.Id == imageId);
                if (image == null)
                {
                    result.ErrorMessage = $"Image {imageId} not found for user {userId}";
                    return result;
                }

                imagesToDelete.Add(image);

                // Collect storage paths to delete
                if (!string.IsNullOrEmpty(image.OriginalImageUrl))
                {
                    storagePathsToDelete.Add(image.OriginalImageUrl);
                }
                if (!string.IsNullOrEmpty(image.ProcessedImageUrl) &&
                    image.ProcessedImageUrl != image.OriginalImageUrl)
                {
                    storagePathsToDelete.Add(image.ProcessedImageUrl);
                }
            }

            // Step 2: Remove database records first (within transaction)
            try
            {
                foreach (var image in imagesToDelete)
                {
                    profile.ProcessedImages.Remove(image);
                }

                await _context.SaveChangesAsync();
                result.DatabaseRecordsDeleted = imagesToDelete.Count;

                _logger.LogInformation("Removed {Count} database records for user {UserId}",
                    imagesToDelete.Count, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove database records for user {UserId}", userId);
                result.ErrorMessage = $"Failed to remove database records: {ex.Message}";
                return result;
            }

            // Step 3: Delete storage files
            var deletedPaths = new List<string>();
            foreach (var storagePath in storagePathsToDelete.Distinct())
            {
                try
                {
                    var deleted = await _storageService.DeleteImageAsync(storagePath);
                    if (deleted)
                    {
                        deletedPaths.Add(storagePath);
                        result.FilesDeleted++;
                        _logger.LogDebug("Deleted storage file: {StoragePath}", storagePath);
                    }
                    else
                    {
                        _logger.LogWarning("Storage file not found (already deleted?): {StoragePath}", storagePath);
                        // Don't fail the operation if file was already deleted
                        deletedPaths.Add(storagePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete storage file {StoragePath} for user {UserId}",
                        storagePath, userId);

                    // Storage deletion failed - rollback database changes
                    result.ErrorMessage = $"Failed to delete storage file {storagePath}: {ex.Message}";
                    // If no real transaction (e.g., InMemory provider), manually restore DB state
                    if (transaction == null)
                    {
                        foreach (var img in imagesToDelete)
                        {
                            profile.ProcessedImages.Add(img);
                        }
                        await _context.SaveChangesAsync();
                        result.DatabaseRecordsDeleted = 0;
                    }
                    return result;
                }
            }

            // Step 4: Commit transaction
            if (transaction != null)
            {
                await transaction.CommitAsync();
            }

            // Success!
            result.Success = true;
            result.DeletedStoragePaths = deletedPaths;

            _logger.LogInformation("Atomic delete completed successfully for user {UserId}. " +
                "DB Records: {DbCount}, Files: {FileCount}",
                userId, result.DatabaseRecordsDeleted, result.FilesDeleted);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Atomic delete failed for user {UserId}", userId);

            // Rollback transaction
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }

            result.ErrorMessage = $"Atomic delete failed: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Cleanup uploaded storage files on failure
    /// </summary>
    private async Task CleanupStorageFilesAsync(List<string> storagePaths)
    {
        foreach (var storagePath in storagePaths)
        {
            try
            {
                await _storageService.DeleteImageAsync(storagePath);
                _logger.LogDebug("Cleaned up storage file: {StoragePath}", storagePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup storage file during rollback: {StoragePath}", storagePath);
                // Don't throw - this is cleanup, we don't want to mask the original error
            }
        }
    }
}
