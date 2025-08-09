using AI.ProfilePhotoMaker.API.Services;

namespace AI.ProfilePhotoMaker.API.Extensions;

/// <summary>
/// Extension methods for registering async I/O services
/// </summary>
public static class AsyncServiceExtensions
{
    /// <summary>
    /// Registers async I/O services for high-performance file operations
    /// </summary>
    public static IServiceCollection AddAsyncIoServices(this IServiceCollection services)
    {
        // Register async file service as singleton for thread safety and performance
        services.AddSingleton<IAsyncFileService, AsyncFileService>();
        
        // Register async ZIP service as singleton for optimal performance
        services.AddSingleton<IAsyncZipService, AsyncZipService>();
        
        return services;
    }

    /// <summary>
    /// Configures async I/O options for optimal performance
    /// </summary>
    public static IServiceCollection ConfigureAsyncIoOptions(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure default options for async operations
        services.Configure<AsyncIoOptions>(options =>
        {
            // Default buffer size for stream operations (80KB)
            options.DefaultBufferSize = configuration.GetValue<int>("AsyncIo:DefaultBufferSize", 81920);
            
            // Maximum concurrent file operations
            options.MaxConcurrentOperations = configuration.GetValue<int>("AsyncIo:MaxConcurrentOperations", Environment.ProcessorCount * 2);
            
            // Default timeout for file operations
            options.DefaultTimeoutMs = configuration.GetValue<int>("AsyncIo:DefaultTimeoutMs", 30000);
            
            // Enable performance logging
            options.EnablePerformanceLogging = configuration.GetValue<bool>("AsyncIo:EnablePerformanceLogging", false);
            
            // ZIP compression settings
            options.ZipCompressionLevel = configuration.GetValue<System.IO.Compression.CompressionLevel>("AsyncIo:ZipCompressionLevel", System.IO.Compression.CompressionLevel.Optimal);
            
            // File validation settings
            options.MaxFileSizeBytes = configuration.GetValue<long>("AsyncIo:MaxFileSizeBytes", 50 * 1024 * 1024); // 50MB
            
            // Allowed image extensions
            var allowedExtensions = configuration.GetSection("AsyncIo:AllowedImageExtensions").Get<string[]>();
            options.AllowedImageExtensions = allowedExtensions ?? new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp" };
        });
        
        return services;
    }
}

/// <summary>
/// Configuration options for async I/O operations
/// </summary>
public class AsyncIoOptions
{
    /// <summary>
    /// Default buffer size for stream operations
    /// </summary>
    public int DefaultBufferSize { get; set; } = 81920; // 80KB
    
    /// <summary>
    /// Maximum concurrent file operations
    /// </summary>
    public int MaxConcurrentOperations { get; set; } = Environment.ProcessorCount * 2;
    
    /// <summary>
    /// Default timeout for file operations in milliseconds
    /// </summary>
    public int DefaultTimeoutMs { get; set; } = 30000; // 30 seconds
    
    /// <summary>
    /// Whether to enable performance logging
    /// </summary>
    public bool EnablePerformanceLogging { get; set; } = false;
    
    /// <summary>
    /// ZIP compression level
    /// </summary>
    public System.IO.Compression.CompressionLevel ZipCompressionLevel { get; set; } = System.IO.Compression.CompressionLevel.Optimal;
    
    /// <summary>
    /// Maximum file size for processing
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024; // 50MB
    
    /// <summary>
    /// Allowed image file extensions
    /// </summary>
    public string[] AllowedImageExtensions { get; set; } = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp" };
}