using System.Diagnostics;
using System.Text;
using AI.ProfilePhotoMaker.API.Services.Storage;

namespace AI.ProfilePhotoMaker.API.Services.Health;

/// <summary>
/// Storage health check service implementation
/// </summary>
public class StorageHealthService : IStorageHealthService
{
    private readonly IStorageService _storageService;
    private readonly ILogger<StorageHealthService> _logger;
    private readonly IConfiguration _configuration;

    public StorageHealthService(
        IStorageService storageService,
        ILogger<StorageHealthService> logger,
        IConfiguration configuration)
    {
        _storageService = storageService;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<bool> CanConnectAsync()
    {
        try
        {
            // Test basic connectivity by attempting to list or check a simple operation
            // This is a generic test that should work with both Azure Blob Storage and Local Storage
            
            using var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            
            // Try to perform a simple operation that tests connectivity
            // For Azure Blob Storage, this might test container access
            // For Local Storage, this tests file system access
            
            var testFileName = $"health-check-{Guid.NewGuid()}.txt";
            var testContent = Encoding.UTF8.GetBytes("health check test");
            
            try
            {
                // Try to upload a small test file using the correct interface
                using var testStream = new MemoryStream(testContent);
                var uploadResult = await _storageService.SaveImageAsync(testStream, testFileName, "health-check");
                
                if (!string.IsNullOrEmpty(uploadResult))
                {
                    // If upload succeeded, try to delete it to clean up
                    try
                    {
                        await _storageService.DeleteImageAsync(uploadResult);
                    }
                    catch (Exception deleteEx)
                    {
                        _logger.LogWarning(deleteEx, "Could not delete health check test file {FileName}", testFileName);
                    }
                    
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Storage connectivity test failed");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Storage connectivity check failed");
            return false;
        }
    }

    public async Task<Dictionary<string, bool>> PerformOperationTestsAsync()
    {
        var results = new Dictionary<string, bool>
        {
            ["upload"] = false,
            ["download"] = false,
            ["delete"] = false,
            ["exists"] = false
        };

        var testFileName = $"health-check-operations-{Guid.NewGuid()}.txt";
        var testContent = Encoding.UTF8.GetBytes($"Health check test content - {DateTime.UtcNow}");

        try
        {
            using var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            string? uploadedPath = null;

            // Test Upload
            try
            {
                using var testStream = new MemoryStream(testContent);
                uploadedPath = await _storageService.SaveImageAsync(testStream, testFileName, "health-check");
                results["upload"] = !string.IsNullOrEmpty(uploadedPath);
                _logger.LogDebug("Storage upload test: {Result}", results["upload"] ? "SUCCESS" : "FAILED");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Storage upload test failed");
                results["upload"] = false;
            }

            // Test Exists (only if upload succeeded)
            if (results["upload"] && !string.IsNullOrEmpty(uploadedPath))
            {
                try
                {
                    results["exists"] = await _storageService.ExistsAsync(uploadedPath);
                    _logger.LogDebug("Storage exists test: {Result}", results["exists"] ? "SUCCESS" : "FAILED");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Storage exists test failed");
                    results["exists"] = false;
                }
            }

            // Test Download (only if upload succeeded)
            if (results["upload"] && !string.IsNullOrEmpty(uploadedPath))
            {
                try
                {
                    using var downloadedStream = await _storageService.GetImageAsync(uploadedPath);
                    results["download"] = downloadedStream != null && downloadedStream.Length > 0;
                    _logger.LogDebug("Storage download test: {Result}", results["download"] ? "SUCCESS" : "FAILED");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Storage download test failed");
                    results["download"] = false;
                }
            }

            // Test Delete (cleanup, only if upload succeeded)
            if (results["upload"] && !string.IsNullOrEmpty(uploadedPath))
            {
                try
                {
                    var deleteResult = await _storageService.DeleteImageAsync(uploadedPath);
                    results["delete"] = deleteResult && !await _storageService.ExistsAsync(uploadedPath);
                    _logger.LogDebug("Storage delete test: {Result}", results["delete"] ? "SUCCESS" : "FAILED");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Storage delete test failed");
                    results["delete"] = false;
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Storage operations test failed");
            return results; // Return current results even if there was an overall failure
        }
    }

    public async Task<Dictionary<string, object>> GetConfigurationAsync()
    {
        try
        {
            var config = new Dictionary<string, object>();

            // Determine storage type based on configuration
            var azureStorageConnectionString = _configuration.GetConnectionString("AzureStorage") ?? 
                                             _configuration["AzureStorage:ConnectionString"];

            if (!string.IsNullOrEmpty(azureStorageConnectionString))
            {
                config["provider"] = "AzureBlobStorage";
                config["hasConnectionString"] = true;
                
                // Parse connection string to get account name (without exposing sensitive data)
                try
                {
                    if (azureStorageConnectionString.Contains("AccountName="))
                    {
                        var accountNamePart = azureStorageConnectionString.Split("AccountName=")[1].Split(';')[0];
                        config["accountName"] = accountNamePart;
                    }
                    
                    config["isEmulator"] = azureStorageConnectionString.Contains("UseDevelopmentStorage=true");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not parse Azure Storage connection string details");
                }
            }
            else
            {
                config["provider"] = "LocalStorage";
                config["hasConnectionString"] = false;
                
                // Get local storage path information
                try
                {
                    var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
                    config["uploadsPath"] = uploadsPath;
                    config["uploadsPathExists"] = Directory.Exists(uploadsPath);
                    
                    if (Directory.Exists(uploadsPath))
                    {
                        var directoryInfo = new DirectoryInfo(uploadsPath);
                        config["uploadsPathWritable"] = directoryInfo.Exists;
                        
                        // Get disk space information
                        var driveInfo = new DriveInfo(directoryInfo.Root.FullName);
                        config["availableSpaceGB"] = Math.Round(driveInfo.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0, 2);
                        config["totalSpaceGB"] = Math.Round(driveInfo.TotalSize / 1024.0 / 1024.0 / 1024.0, 2);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not get local storage path information");
                }
            }

            // Add test timing
            var stopwatch = Stopwatch.StartNew();
            await Task.Delay(1); // Minimal async operation for timing test
            stopwatch.Stop();
            config["configurationRetrievalTimeMs"] = stopwatch.ElapsedMilliseconds;

            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get storage configuration");
            return new Dictionary<string, object>
            {
                ["error"] = "Failed to get storage configuration",
                ["errorMessage"] = ex.Message
            };
        }
    }
}