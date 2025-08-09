using Microsoft.Extensions.Configuration;

namespace AI.ProfilePhotoMaker.API.Services.Storage;

/// <summary>
/// Local filesystem implementation of storage service with async I/O optimizations
/// </summary>
public class LocalStorageService : IStorageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LocalStorageService> _logger;
    private readonly IAsyncFileService _asyncFileService;

    public LocalStorageService(
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<LocalStorageService> logger,
        IAsyncFileService asyncFileService)
    {
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
        _asyncFileService = asyncFileService;
    }

    public async Task<string> SaveImageAsync(Stream imageStream, string fileName, string userId)
    {
        try
        {
            // Ensure the user's generated directory exists
            var userDirectory = Path.Combine(_environment.ContentRootPath, "generated", userId);
            await _asyncFileService.CreateDirectoryAsync(userDirectory);

            var filePath = Path.Combine(userDirectory, fileName);

            // Save the image to the local filesystem using async streaming with optimal buffer size
            await _asyncFileService.CopyStreamToFileAsync(imageStream, filePath, 81920);

            // Return the relative path that can be served by the web server
            var storagePath = $"/generated/{userId}/{fileName}";

            _logger.LogInformation("Saved image to local storage: {StoragePath}", storagePath);
            return storagePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save image {FileName} for user {UserId}", fileName, userId);
            throw;
        }
    }

    public async Task<Stream?> GetImageAsync(string storagePath)
    {
        try
        {
            var fullPath = GetFullPath(storagePath);
            if (!await _asyncFileService.FileExistsAsync(fullPath))
            {
                _logger.LogWarning("Image not found at path: {StoragePath}", storagePath);
                return null;
            }

            var fileStream = await _asyncFileService.OpenFileStreamAsync(fullPath, FileMode.Open, FileAccess.Read);
            return fileStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get image from storage: {StoragePath}", storagePath);
            return null;
        }
    }

    public async Task<bool> DeleteImageAsync(string storagePath)
    {
        try
        {
            var fullPath = GetFullPath(storagePath);
            var deleted = await _asyncFileService.DeleteFileAsync(fullPath);
            
            if (deleted)
            {
                _logger.LogInformation("Deleted image from local storage: {StoragePath}", storagePath);
            }
            else
            {
                _logger.LogWarning("Attempted to delete non-existent image: {StoragePath}", storagePath);
            }
            
            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete image from storage: {StoragePath}", storagePath);
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string storagePath)
    {
        try
        {
            var fullPath = GetFullPath(storagePath);
            return await _asyncFileService.FileExistsAsync(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if image exists: {StoragePath}", storagePath);
            return false;
        }
    }

    public string GetImageUrl(string storagePath)
    {
        // For local storage, convert relative path to absolute URL
        var baseUrl = _configuration["AppBaseUrl"] ?? "https://localhost:5001";

        // Ensure storagePath starts with /
        if (!storagePath.StartsWith('/'))
        {
            storagePath = "/" + storagePath;
        }

        return $"{baseUrl.TrimEnd('/')}{storagePath}";
    }

    public async Task<List<string>> ListUserImagesAsync(string userId)
    {
        try
        {
            var userDirectory = Path.Combine(_environment.ContentRootPath, "generated", userId);
            if (!await _asyncFileService.FileExistsAsync(userDirectory))
            {
                return new List<string>();
            }

            var allFiles = await _asyncFileService.GetDirectoryFilesAsync(userDirectory, "*", false);
            var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif" };
            
            var imageFiles = allFiles
                .Where(file => imageExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                .Select(file =>
                {
                    var fileName = Path.GetFileName(file);
                    return $"/generated/{userId}/{fileName}";
                })
                .ToList();

            return imageFiles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list images for user {UserId}", userId);
            return new List<string>();
        }
    }

    public async Task<StorageFileInfo?> GetFileInfoAsync(string storagePath)
    {
        try
        {
            var fullPath = GetFullPath(storagePath);
            var fileInfo = await _asyncFileService.GetFileInfoAsync(fullPath);
            
            if (fileInfo == null)
            {
                return null;
            }

            var fileName = Path.GetFileName(storagePath);
            var contentType = GetContentType(Path.GetExtension(fileName));

            return new StorageFileInfo
            {
                FileName = fileName,
                Size = fileInfo.Length,
                CreatedAt = fileInfo.CreationTimeUtc,
                ModifiedAt = fileInfo.LastWriteTimeUtc,
                ContentType = contentType
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file info for: {StoragePath}", storagePath);
            return null;
        }
    }

    /// <summary>
    /// Converts a storage path to a full filesystem path
    /// </summary>
    private string GetFullPath(string storagePath)
    {
        // Remove leading slash and convert to filesystem path
        var relativePath = storagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(_environment.ContentRootPath, relativePath);
    }

    /// <summary>
    /// Gets the MIME content type for a file extension
    /// </summary>
    private static string GetContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".tiff" or ".tif" => "image/tiff",
            _ => "application/octet-stream"
        };
    }
}