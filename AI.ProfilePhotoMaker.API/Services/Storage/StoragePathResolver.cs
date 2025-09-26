using AI.ProfilePhotoMaker.API.Infrastructure.Logging;

namespace AI.ProfilePhotoMaker.API.Services.Storage;

/// <summary>
/// Types of storage for different file categories
/// </summary>
public enum StorageType
{
    Upload,      // Original user uploads
    Enhanced,    // Temporary enhanced photos for Replicate processing
    Generated,   // AI-generated profile photos
    TrainingZip  // ZIP files for model training
}

/// <summary>
/// Resolves storage paths with environment awareness for proper dev/staging/prod separation
/// </summary>
public class StoragePathResolver
{
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StoragePathResolver> _logger;
    private static string S(string? value) => LoggingSanitizer.Sanitize(value);
    private static string Sid(string? value) => LoggingSanitizer.SanitizeId(value);

    public StoragePathResolver(
        IWebHostEnvironment environment,
        IConfiguration configuration,
        ILogger<StoragePathResolver> logger)
    {
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Gets the storage path for a file based on type, user, and environment
    /// </summary>
    /// <param name="type">Type of storage</param>
    /// <param name="userId">User ID</param>
    /// <param name="fileName">File name</param>
    /// <returns>Full storage path with environment prefix</returns>
    public string GetPath(StorageType type, string userId, string fileName)
    {
        var environmentPrefix = GetEnvironmentPrefix();

        var path = type switch
        {
            StorageType.Upload => $"{environmentPrefix}/uploads/{userId}/{fileName}",
            StorageType.Enhanced => $"{environmentPrefix}/enhanced/{userId}/{fileName}",
            StorageType.Generated => $"{environmentPrefix}/generated/{userId}/{fileName}",
            StorageType.TrainingZip => $"{environmentPrefix}/training-zips/{fileName}",
            _ => throw new ArgumentException($"Unknown storage type: {type}")
        };

        _logger.LogDebug("Generated storage path: {Path} for type: {Type}, user: {UserId}",
            S(path), type, Sid(userId));

        return path;
    }

    /// <summary>
    /// Gets the directory prefix for a storage type
    /// </summary>
    /// <param name="type">Type of storage</param>
    /// <param name="userId">User ID (optional, for user-specific directories)</param>
    /// <returns>Directory prefix for listing operations</returns>
    public string GetDirectoryPrefix(StorageType type, string? userId = null)
    {
        var environmentPrefix = GetEnvironmentPrefix();

        return type switch
        {
            StorageType.Upload when userId != null => $"{environmentPrefix}/uploads/{userId}/",
            StorageType.Upload => $"{environmentPrefix}/uploads/",
            StorageType.Enhanced when userId != null => $"{environmentPrefix}/enhanced/{userId}/",
            StorageType.Enhanced => $"{environmentPrefix}/enhanced/",
            StorageType.Generated when userId != null => $"{environmentPrefix}/generated/{userId}/",
            StorageType.Generated => $"{environmentPrefix}/generated/",
            StorageType.TrainingZip => $"{environmentPrefix}/training-zips/",
            _ => throw new ArgumentException($"Unknown storage type: {type}")
        };
    }

    /// <summary>
    /// Gets the environment prefix for path separation
    /// </summary>
    /// <returns>Environment prefix (e.g., "dev", "staging", "prod")</returns>
    private string GetEnvironmentPrefix()
    {
        // Check for explicit configuration override
        var configPrefix = _configuration["Storage:EnvironmentPrefix"];
        if (!string.IsNullOrEmpty(configPrefix))
        {
            return configPrefix;
        }

        // Use environment name, normalized to lowercase
        var environmentName = _environment.EnvironmentName.ToLowerInvariant();

        return environmentName switch
        {
            "development" => "dev",
            "staging" => "staging",
            "production" => "prod",
            _ => environmentName // Use as-is for custom environments
        };
    }

    /// <summary>
    /// Parses a storage path to extract its components
    /// </summary>
    /// <param name="storagePath">Storage path to parse</param>
    /// <returns>Parsed path components</returns>
    public StoragePathInfo ParsePath(string storagePath)
    {
        var parts = storagePath.Trim('/').Split('/');

        if (parts.Length < 2)
        {
            throw new ArgumentException($"Invalid storage path format: {storagePath}");
        }

        var environment = parts[0];
        var typeStr = parts[1];

        if (!Enum.TryParse<StorageType>(typeStr, true, out var storageType))
        {
            throw new ArgumentException($"Unknown storage type in path: {typeStr}");
        }

        string? userId = null;
        string? fileName = null;

        if (storageType == StorageType.TrainingZip)
        {
            // Format: env/training-zips/filename
            fileName = parts.Length > 2 ? parts[2] : null;
        }
        else
        {
            // Format: env/type/userId/filename
            userId = parts.Length > 2 ? parts[2] : null;
            fileName = parts.Length > 3 ? parts[3] : null;
        }

        return new StoragePathInfo
        {
            Environment = environment,
            StorageType = storageType,
            UserId = userId,
            FileName = fileName,
            OriginalPath = storagePath
        };
    }
}

/// <summary>
/// Information parsed from a storage path
/// </summary>
public class StoragePathInfo
{
    public string Environment { get; set; } = string.Empty;
    public StorageType StorageType { get; set; }
    public string? UserId { get; set; }
    public string? FileName { get; set; }
    public string OriginalPath { get; set; } = string.Empty;
}
