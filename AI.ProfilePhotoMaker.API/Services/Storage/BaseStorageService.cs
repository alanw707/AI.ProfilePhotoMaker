using Azure.Storage.Sas;

namespace AI.ProfilePhotoMaker.API.Services.Storage;

/// <summary>
/// Abstract base class providing common functionality for storage service implementations
/// </summary>
public abstract class BaseStorageService : IStorageService
{
    protected readonly IConfiguration Configuration;
    protected readonly ILogger Logger;

    protected BaseStorageService(IConfiguration configuration, ILogger logger)
    {
        Configuration = configuration;
        Logger = logger;
    }

    // Abstract methods that must be implemented by derived classes
    public abstract Task<string> SaveImageAsync(Stream imageStream, string fileName, string userId, string folderType = "generated");
    public abstract Task<string> SaveImageToPathAsync(Stream imageStream, string storagePath);
    public abstract Task<Stream?> GetImageAsync(string storagePath);
    public abstract Task<bool> DeleteImageAsync(string storagePath);
    public abstract Task<bool> ExistsAsync(string storagePath);
    public abstract Task<List<string>> ListUserImagesAsync(string userId);
    public abstract Task<StorageFileInfo?> GetFileInfoAsync(string storagePath);
    public abstract Task<string> GenerateSasUrlAsync(string storagePath, TimeSpan expiry, BlobSasPermissions permissions = BlobSasPermissions.Read);
    public abstract Task<string> SaveZipAsync(Stream zipStream, string storagePath);
    public abstract Task<bool> DeleteDirectoryAsync(string directoryPath);
    public abstract Task<List<string>> ListFilesAsync(string prefix);

    /// <summary>
    /// Gets the public URL for accessing an image - environment-specific implementation
    /// </summary>
    /// <param name="storagePath">The storage path</param>
    /// <returns>Public URL for the image</returns>
    public abstract string GetImageUrl(string storagePath);

    /// <summary>
    /// Determines the MIME content type based on file extension
    /// </summary>
    /// <param name="extension">File extension (with or without leading dot)</param>
    /// <returns>MIME content type</returns>
    protected static string GetContentType(string extension)
    {
        var ext = extension.StartsWith('.') ? extension : $".{extension}";
        return ext.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".tiff" or ".tif" => "image/tiff",
            ".svg" => "image/svg+xml",
            ".ico" => "image/x-icon",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// Determines if a file extension represents an image
    /// </summary>
    /// <param name="extension">File extension</param>
    /// <returns>True if the extension represents an image file</returns>
    protected static bool IsImageExtension(string extension)
    {
        var ext = extension.ToLowerInvariant();
        if (!ext.StartsWith('.'))
            ext = $".{ext}";

        return ext is ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" or ".bmp" or ".tiff" or ".tif" or ".svg" or ".ico";
    }

    /// <summary>
    /// Validates that a storage path is not null or empty
    /// </summary>
    /// <param name="storagePath">The storage path to validate</param>
    /// <param name="parameterName">Name of the parameter for exception messages</param>
    /// <exception cref="ArgumentException">Thrown when storage path is null or empty</exception>
    protected static void ValidateStoragePath(string storagePath, string parameterName = "storagePath")
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            throw new ArgumentException("Storage path cannot be null or empty", parameterName);
    }

    /// <summary>
    /// Validates that a file name is not null or empty and doesn't contain invalid characters
    /// </summary>
    /// <param name="fileName">The file name to validate</param>
    /// <param name="parameterName">Name of the parameter for exception messages</param>
    /// <exception cref="ArgumentException">Thrown when file name is invalid</exception>
    protected static void ValidateFileName(string fileName, string parameterName = "fileName")
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be null or empty", parameterName);

        var invalidChars = Path.GetInvalidFileNameChars();
        if (fileName.IndexOfAny(invalidChars) >= 0)
            throw new ArgumentException($"File name contains invalid characters: {fileName}", parameterName);
    }

    /// <summary>
    /// Validates that a user ID is not null or empty
    /// </summary>
    /// <param name="userId">The user ID to validate</param>
    /// <param name="parameterName">Name of the parameter for exception messages</param>
    /// <exception cref="ArgumentException">Thrown when user ID is null or empty</exception>
    protected static void ValidateUserId(string userId, string parameterName = "userId")
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID cannot be null or empty", parameterName);
    }

    /// <summary>
    /// Safely logs an operation with consistent formatting
    /// </summary>
    /// <param name="level">Log level</param>
    /// <param name="operation">Operation being performed</param>
    /// <param name="storagePath">Storage path involved</param>
    /// <param name="additionalData">Additional data to include</param>
    protected void LogOperation(LogLevel level, string operation, string storagePath, object? additionalData = null)
    {
        if (additionalData != null)
        {
            Logger.Log(level, "{Operation}: {StoragePath}, Data: {@AdditionalData}", operation, storagePath, additionalData);
        }
        else
        {
            Logger.Log(level, "{Operation}: {StoragePath}", operation, storagePath);
        }
    }

    /// <summary>
    /// Safely logs an exception with operation context
    /// </summary>
    /// <param name="ex">Exception to log</param>
    /// <param name="operation">Operation that failed</param>
    /// <param name="storagePath">Storage path involved</param>
    /// <param name="additionalContext">Additional context information</param>
    protected void LogError(Exception ex, string operation, string storagePath, object? additionalContext = null)
    {
        if (additionalContext != null)
        {
            Logger.LogError(ex, "Failed {Operation}: {StoragePath}, Context: {@AdditionalContext}", operation, storagePath, additionalContext);
        }
        else
        {
            Logger.LogError(ex, "Failed {Operation}: {StoragePath}", operation, storagePath);
        }
    }

    /// <summary>
    /// Generates a storage path for user-specific content
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="fileName">File name</param>
    /// <param name="subdirectory">Optional subdirectory (e.g., "generated", "uploads")</param>
    /// <returns>Formatted storage path</returns>
    protected static string GenerateUserStoragePath(string userId, string fileName, string folderType = "generated")
    {
        ValidateUserId(userId);
        ValidateFileName(fileName);

        return $"{folderType}/{userId}/{fileName}";
    }
}