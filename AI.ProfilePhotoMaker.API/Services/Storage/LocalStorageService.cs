using Microsoft.Extensions.Configuration;
using Azure.Storage.Sas;

namespace AI.ProfilePhotoMaker.API.Services.Storage;

/// <summary>
/// Local filesystem implementation of storage service with async I/O optimizations
/// </summary>
public class LocalStorageService : BaseStorageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly IAsyncFileService _asyncFileService;

    public LocalStorageService(
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<LocalStorageService> logger,
        IAsyncFileService asyncFileService)
        : base(configuration, logger)
    {
        _environment = environment;
        _asyncFileService = asyncFileService;
    }

    public override async Task<string> SaveImageAsync(Stream imageStream, string fileName, string userId, string folderType = "generated")
    {
        ValidateUserId(userId);
        ValidateFileName(fileName);

        try
        {
            // Ensure the user's folder directory exists  
            var userDirectory = Path.Combine(_environment.ContentRootPath, folderType, userId);
            await _asyncFileService.CreateDirectoryAsync(userDirectory);

            var filePath = Path.Combine(userDirectory, fileName);

            // Save the image to the local filesystem using async streaming with optimal buffer size
            await _asyncFileService.CopyStreamToFileAsync(imageStream, filePath, 81920);

            // Return the relative path that can be served by the web server
            var storagePath = GenerateUserStoragePath(userId, fileName, folderType);

            LogOperation(LogLevel.Information, "SaveImageAsync", storagePath);
            return storagePath;
        }
        catch (Exception ex)
        {
            LogError(ex, "SaveImageAsync", $"userId: {userId}, fileName: {fileName}");
            throw;
        }
    }

    public override async Task<string> SaveImageToPathAsync(Stream imageStream, string storagePath)
    {
        ValidateStoragePath(storagePath);

        try
        {
            var fullPath = GetFullPath(storagePath);
            var directory = Path.GetDirectoryName(fullPath);

            if (directory != null)
            {
                await _asyncFileService.CreateDirectoryAsync(directory);
            }

            await _asyncFileService.CopyStreamToFileAsync(imageStream, fullPath, 81920);

            LogOperation(LogLevel.Information, "SaveImageToPathAsync", storagePath);
            return storagePath;
        }
        catch (Exception ex)
        {
            LogError(ex, "SaveImageToPathAsync", storagePath);
            throw;
        }
    }

    public override async Task<Stream?> GetImageAsync(string storagePath)
    {
        ValidateStoragePath(storagePath);

        try
        {
            var fullPath = GetFullPath(storagePath);
            if (!await _asyncFileService.FileExistsAsync(fullPath))
            {
                LogOperation(LogLevel.Warning, "GetImageAsync - not found", storagePath);
                return null;
            }

            var fileStream = await _asyncFileService.OpenFileStreamAsync(fullPath, FileMode.Open, FileAccess.Read);
            return fileStream;
        }
        catch (Exception ex)
        {
            LogError(ex, "GetImageAsync", storagePath);
            return null;
        }
    }

    public override async Task<bool> DeleteImageAsync(string storagePath)
    {
        ValidateStoragePath(storagePath);

        try
        {
            // Normalize if a full URL was provided
            if (Uri.TryCreate(storagePath, UriKind.Absolute, out var uri))
            {
                storagePath = uri.AbsolutePath; // keep leading '/'
            }
            var fullPath = GetFullPath(storagePath);
            var deleted = await _asyncFileService.DeleteFileAsync(fullPath);

            if (deleted)
            {
                LogOperation(LogLevel.Information, "DeleteImageAsync - success", storagePath);
            }
            else
            {
                LogOperation(LogLevel.Warning, "DeleteImageAsync - not found", storagePath);
            }

            return deleted;
        }
        catch (Exception ex)
        {
            LogError(ex, "DeleteImageAsync", storagePath);
            return false;
        }
    }

    public override async Task<bool> ExistsAsync(string storagePath)
    {
        ValidateStoragePath(storagePath);

        try
        {
            var fullPath = GetFullPath(storagePath);
            return await _asyncFileService.FileExistsAsync(fullPath);
        }
        catch (Exception ex)
        {
            LogError(ex, "ExistsAsync", storagePath);
            return false;
        }
    }

    public override string GetImageUrl(string storagePath)
    {
        ValidateStoragePath(storagePath);

        // Ensure storagePath starts with /
        if (!storagePath.StartsWith('/'))
        {
            storagePath = "/" + storagePath;
        }

        // For local development, prioritize ExternalApiBaseUrl (ngrok) if available,
        // otherwise fall back to AppBaseUrl for both frontend and external API access
        var externalApiBaseUrl = Configuration["ExternalApiBaseUrl"];
        if (!string.IsNullOrEmpty(externalApiBaseUrl))
        {
            Logger.LogDebug("GetImageUrl using ExternalApiBaseUrl: {BaseUrl}{Path}", externalApiBaseUrl, storagePath);
            return $"{externalApiBaseUrl.TrimEnd('/')}{storagePath}";
        }

        var appBaseUrl = Configuration["AppBaseUrl"] ?? "https://localhost:5001";
        Logger.LogDebug("GetImageUrl using AppBaseUrl: {BaseUrl}{Path}", appBaseUrl, storagePath);
        return $"{appBaseUrl.TrimEnd('/')}{storagePath}";
    }

    public override async Task<List<string>> ListUserImagesAsync(string userId)
    {
        ValidateUserId(userId);

        try
        {
            var userDirectory = Path.Combine(_environment.ContentRootPath, "generated", userId);
            if (!await _asyncFileService.FileExistsAsync(userDirectory))
            {
                return new List<string>();
            }

            var allFiles = await _asyncFileService.GetDirectoryFilesAsync(userDirectory, "*", false);

            var imageFiles = allFiles
                .Where(file => IsImageExtension(Path.GetExtension(file)))
                .Select(file =>
                {
                    var fileName = Path.GetFileName(file);
                    return GenerateUserStoragePath(userId, fileName);
                })
                .ToList();

            return imageFiles;
        }
        catch (Exception ex)
        {
            LogError(ex, "ListUserImagesAsync", $"userId: {userId}");
            return new List<string>();
        }
    }

    public override async Task<StorageFileInfo?> GetFileInfoAsync(string storagePath)
    {
        ValidateStoragePath(storagePath);

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
            LogError(ex, "GetFileInfoAsync", storagePath);
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


    public override Task<string> GenerateSasUrlAsync(string storagePath, TimeSpan expiry, BlobSasPermissions permissions = BlobSasPermissions.Read)
    {
        ValidateStoragePath(storagePath);

        // For local storage, we can't generate true SAS URLs, so return the regular URL
        // This is used in development where we're not actually using external APIs
        Logger.LogWarning("GenerateSasUrlAsync called on LocalStorageService - returning regular URL for development");
        return Task.FromResult(GetImageUrl(storagePath));
    }

    public override async Task<string> SaveZipAsync(Stream zipStream, string storagePath)
    {
        ValidateStoragePath(storagePath);

        try
        {
            var fullPath = GetFullPath(storagePath);
            var directory = Path.GetDirectoryName(fullPath);

            if (directory != null)
            {
                await _asyncFileService.CreateDirectoryAsync(directory);
            }

            await _asyncFileService.CopyStreamToFileAsync(zipStream, fullPath, 81920);

            LogOperation(LogLevel.Information, "SaveZipAsync", storagePath);
            return storagePath;
        }
        catch (Exception ex)
        {
            LogError(ex, "SaveZipAsync", storagePath);
            throw;
        }
    }

    public override Task<bool> DeleteDirectoryAsync(string directoryPath)
    {
        ValidateStoragePath(directoryPath);

        try
        {
            var fullPath = GetFullPath(directoryPath);

            if (!Directory.Exists(fullPath))
            {
                LogOperation(LogLevel.Warning, "DeleteDirectoryAsync - not found", directoryPath);
                return Task.FromResult(false);
            }

            Directory.Delete(fullPath, recursive: true);

            LogOperation(LogLevel.Information, "DeleteDirectoryAsync - success", directoryPath);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            LogError(ex, "DeleteDirectoryAsync", directoryPath);
            return Task.FromResult(false);
        }
    }

    public override async Task<List<string>> ListFilesAsync(string prefix)
    {
        ValidateStoragePath(prefix);

        try
        {
            var fullPrefix = GetFullPath(prefix);
            var directory = Path.GetDirectoryName(fullPrefix);
            var pattern = Path.GetFileName(fullPrefix);

            if (string.IsNullOrEmpty(pattern))
            {
                pattern = "*";
            }

            if (directory == null || !Directory.Exists(directory))
            {
                return new List<string>();
            }

            var files = await _asyncFileService.GetDirectoryFilesAsync(directory, pattern, true);

            return files.Select(file =>
            {
                // Convert back to storage path format
                var relativePath = Path.GetRelativePath(_environment.ContentRootPath, file);
                return "/" + relativePath.Replace(Path.DirectorySeparatorChar, '/');
            }).ToList();
        }
        catch (Exception ex)
        {
            LogError(ex, "ListFilesAsync", prefix);
            return new List<string>();
        }
    }
}
