using AI.ProfilePhotoMaker.API.Services.Storage;

namespace AI.ProfilePhotoMaker.API.Services;

/// <summary>
/// Command-line service for upload operations
/// </summary>
public static class UploadCommandService
{
    /// <summary>
    /// Handle upload command-line operations
    /// </summary>
    public static async Task<int> HandleUploadCommand(string[] args, IServiceProvider serviceProvider)
    {
        if (args.Length == 0)
            return 0; // No upload command

        var command = args[0];
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            return command switch
            {
                "upload-previews" => await HandleStylePreviewsUpload(args, serviceProvider, logger),
                "list-previews" => await HandleStylePreviewsList(serviceProvider, logger),
                _ => 0 // Not an upload command
            };
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Upload command failed: {Command}", command);
            return 1;
        }
    }

    private static async Task<int> HandleStylePreviewsUpload(string[] args, IServiceProvider serviceProvider, ILogger logger)
    {
        // Parse flags
        var dryRun = args.Contains("--dry-run");
        var force = args.Contains("--force");
        var help = args.Contains("--help");

        if (help)
        {
            UploadStylePreviewsService.ShowHelp();
            return 0;
        }

        // Validate Azure Storage configuration
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var azureStorageConnectionString = configuration.GetConnectionString("AzureStorage") ?? 
                                          configuration["AzureStorage:ConnectionString"];

        if (string.IsNullOrEmpty(azureStorageConnectionString) || 
            azureStorageConnectionString.StartsWith("REPLACE_WITH_"))
        {
            if (dryRun)
            {
                // Allow dry-run without Azure configuration for testing
                return await HandleDemoUpload(serviceProvider, logger, true);
            }
            logger.LogError("Azure Storage connection string is not configured for upload operation");
            return 1;
        }

        try
        {
            // Use a scope to resolve scoped services
            using var scope = serviceProvider.CreateScope();
            var scopedProvider = scope.ServiceProvider;
            
            // Ensure we're using Azure Blob Storage
            var storageService = scopedProvider.GetService<IStorageService>();
            if (storageService is not AzureBlobStorageService)
            {
                logger.LogError("Upload command attempted with non-Azure storage service: {ServiceType}", 
                    storageService?.GetType().Name ?? "null");
                return 1;
            }
            
            logger.LogInformation("Starting style previews upload. DryRun: {DryRun}, Force: {Force}", dryRun, force);

            var uploadService = scopedProvider.GetRequiredService<UploadStylePreviewsService>();
            return await uploadService.UploadStylePreviewsAsync(dryRun, force);
        }
        catch (Exception ex) when (ex.Message.Contains("Settings must be of the form"))
        {
            logger.LogError(ex, "Invalid Azure Storage connection string format");
            return 1;
        }


    }

    private static async Task<int> HandleStylePreviewsList(IServiceProvider serviceProvider, ILogger logger)
    {
        // Validate Azure Storage configuration
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var azureStorageConnectionString = configuration.GetConnectionString("AzureStorage") ?? 
                                          configuration["AzureStorage:ConnectionString"];

        if (string.IsNullOrEmpty(azureStorageConnectionString) || 
            azureStorageConnectionString.StartsWith("REPLACE_WITH_"))
        {
            Console.WriteLine("ERROR: Azure Storage connection string is not configured.");
            Console.WriteLine("The connection string contains placeholder values.");
            logger.LogError("Azure Storage connection string is not configured for list operation");
            return 1;
        }

        // Use a scope to resolve scoped services
        using var scope = serviceProvider.CreateScope();
        var scopedProvider = scope.ServiceProvider;
        
        // Ensure we're using Azure Blob Storage
        var storageService = scopedProvider.GetService<IStorageService>();
        if (storageService is not AzureBlobStorageService)
        {
            Console.WriteLine("ERROR: List command requires Azure Blob Storage configuration.");
            logger.LogError("List command attempted with non-Azure storage service: {ServiceType}", 
                storageService?.GetType().Name ?? "null");
            return 1;
        }

        logger.LogInformation("Starting style previews list operation");

        var uploadService = scopedProvider.GetRequiredService<UploadStylePreviewsService>();
        return await uploadService.ListStylePreviewsAsync();
    }

    private static async Task<int> HandleDemoUpload(IServiceProvider serviceProvider, ILogger logger, bool dryRun)
    {
        var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
        var stylePreviewsPath = Path.Combine(environment.ContentRootPath, "style-previews");
        
        Console.WriteLine("=== DEMO MODE: Style Previews Upload ===\n");
        Console.WriteLine("⚠️  Running in demo mode without Azure Storage configuration\n");
        Console.WriteLine($"Source Directory: {stylePreviewsPath}");
        Console.WriteLine($"Target Container: style-previews");
        Console.WriteLine($"Dry Run: Yes (Demo Mode)");
        Console.WriteLine();

        // Validate local directory exists
        if (!Directory.Exists(stylePreviewsPath))
        {
            Console.WriteLine($"ERROR: Style previews directory not found: {stylePreviewsPath}");
            logger.LogError("Style previews directory not found: {Path}", stylePreviewsPath);
            return 1;
        }

        // Get all .jpg files
        var imageFiles = Directory.GetFiles(stylePreviewsPath, "*.jpg", SearchOption.TopDirectoryOnly);
        
        if (!imageFiles.Any())
        {
            Console.WriteLine("No .jpg files found in style-previews directory");
            return 0;
        }

        Console.WriteLine($"Found {imageFiles.Length} image files to process:");
        foreach (var file in imageFiles)
        {
            var fileName = Path.GetFileName(file);
            var fileInfo = new FileInfo(file);
            Console.WriteLine($"  • {fileName} ({fileInfo.Length:N0} bytes)");
        }
        Console.WriteLine();

        Console.WriteLine("Demo Upload Simulation:");
        Console.WriteLine("STATUS   SIZE        FILE");
        Console.WriteLine("------   ---------   ----");
        
        foreach (var filePath in imageFiles)
        {
            var fileName = Path.GetFileName(filePath);
            var fileInfo = new FileInfo(filePath);
            
            await Task.Delay(100); // Simulate upload time
            Console.WriteLine($"🔍 DEMO  {fileInfo.Length,9:N0}   {fileName}");
        }
        
        Console.WriteLine();
        Console.WriteLine("=== Demo Summary ===");
        Console.WriteLine($"Total Files: {imageFiles.Length}");
        Console.WriteLine($"Total Size: {imageFiles.Sum(f => new FileInfo(f).Length):N0} bytes");
        Console.WriteLine();
        Console.WriteLine("ℹ️  This was a demo simulation. Configure Azure Storage to perform actual uploads.");
        
        return 0;
    }
}