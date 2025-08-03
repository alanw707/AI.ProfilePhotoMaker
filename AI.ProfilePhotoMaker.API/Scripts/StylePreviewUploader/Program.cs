using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace StylePreviewUploader;

class Program
{
    private static readonly Dictionary<string, string> _colorCodes = new()
    {
        ["RED"] = "\u001b[31m",
        ["GREEN"] = "\u001b[32m",
        ["YELLOW"] = "\u001b[33m",
        ["BLUE"] = "\u001b[34m",
        ["CYAN"] = "\u001b[36m",
        ["RESET"] = "\u001b[0m"
    };

    static async Task<int> Main(string[] args)
    {
        // Parse command line arguments
        var options = ParseArguments(args);
        
        PrintHeader();

        // Build configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Setup logging
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(options.Verbose ? LogLevel.Debug : LogLevel.Information));
        var logger = loggerFactory.CreateLogger<Program>();

        try
        {
            // Get Azure Storage connection string
            var connectionString = options.ConnectionString ?? 
                                 configuration.GetConnectionString("AzureStorage") ??
                                 configuration["AzureStorage:ConnectionString"] ??
                                 Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

            if (string.IsNullOrEmpty(connectionString))
            {
                PrintError("Azure Storage connection string is required.");
                PrintUsage();
                return 1;
            }

            // Initialize Azure Blob Storage
            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(options.ContainerName);
            
            PrintSuccess("Connected to Azure Storage successfully");

            // Ensure container exists
            if (options.DryRun)
            {
                PrintWarning($"DRY RUN: Would ensure container '{options.ContainerName}' exists");
            }
            else
            {
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);
                PrintSuccess($"Container '{options.ContainerName}' ready");
            }

            // Find style preview images
            var previewsPath = Path.GetFullPath(options.PreviewsPath);
            if (!Directory.Exists(previewsPath))
            {
                PrintError($"Style previews directory not found: {previewsPath}");
                return 1;
            }

            var imageFiles = Directory.GetFiles(previewsPath, "*.jpg")
                .Where(f => new FileInfo(f).Length > 0) // Skip empty files
                .ToList();

            if (!imageFiles.Any())
            {
                PrintWarning($"No valid .jpg files found in {previewsPath}");
                return 0;
            }

            PrintInfo($"Found {imageFiles.Count} style preview images to upload");

            // Upload statistics
            var stats = new UploadStats();
            
            // Upload each file
            foreach (var filePath in imageFiles)
            {
                var fileName = Path.GetFileName(filePath);
                var blobName = $"style-previews/{fileName}";
                var fileInfo = new FileInfo(filePath);
                var fileSizeKB = Math.Round(fileInfo.Length / 1024.0, 2);

                try
                {
                    var blobClient = containerClient.GetBlobClient(blobName);
                    
                    // Check if blob already exists
                    var exists = await blobClient.ExistsAsync();
                    if (exists.Value && !options.Force)
                    {
                        if (options.Verbose)
                        {
                            PrintWarning($"Skipping {fileName} (already exists, use --force to overwrite)");
                        }
                        stats.Skipped++;
                        continue;
                    }

                    if (options.DryRun)
                    {
                        var action = exists.Value ? "overwrite" : "upload";
                        PrintWarning($"DRY RUN: Would {action} {fileName} ({fileSizeKB} KB) → {blobName}");
                        stats.Uploaded++;
                    }
                    else
                    {
                        // Upload the file
                        using var fileStream = File.OpenRead(filePath);
                        await blobClient.UploadAsync(fileStream, overwrite: options.Force);
                        
                        // Set content type
                        await blobClient.SetHttpHeadersAsync(new BlobHttpHeaders
                        {
                            ContentType = "image/jpeg"
                        });

                        PrintSuccess($"Uploaded {fileName} ({fileSizeKB} KB) → {blobName}");
                        stats.Uploaded++;
                        stats.TotalSize += fileInfo.Length;
                    }
                }
                catch (Exception ex)
                {
                    PrintError($"Failed to upload {fileName}: {ex.Message}");
                    stats.Failed++;
                }
            }

            // Display summary
            PrintSummary(stats, imageFiles.Count, options.DryRun);

            // Generate sample URLs
            if (stats.Uploaded > 0 && !options.DryRun)
            {
                await GenerateSampleUrls(connectionString, options.ContainerName, imageFiles.Take(3));
            }

            // Test API endpoint
            if (!options.DryRun && stats.Uploaded > 0)
            {
                await TestApiEndpoint(logger);
            }

            return stats.Failed == 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            PrintError($"Unexpected error: {ex.Message}");
            logger.LogError(ex, "Unexpected error occurred");
            return 1;
        }
    }

    private static UploadOptions ParseArguments(string[] args)
    {
        var options = new UploadOptions();
        
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--connection-string":
                case "-c":
                    if (i + 1 < args.Length) options.ConnectionString = args[++i];
                    break;
                case "--container":
                    if (i + 1 < args.Length) options.ContainerName = args[++i];
                    break;
                case "--path":
                case "-p":
                    if (i + 1 < args.Length) options.PreviewsPath = args[++i];
                    break;
                case "--force":
                case "-f":
                    options.Force = true;
                    break;
                case "--dry-run":
                case "-d":
                    options.DryRun = true;
                    break;
                case "--verbose":
                case "-v":
                    options.Verbose = true;
                    break;
                case "--help":
                case "-h":
                    PrintUsage();
                    Environment.Exit(0);
                    break;
            }
        }
        
        return options;
    }

    private static void PrintHeader()
    {
        Console.WriteLine($"{_colorCodes["CYAN"]}🚀 Azure Blob Storage Upload for Style Previews{_colorCodes["RESET"]}");
        Console.WriteLine("==================================================");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: StylePreviewUploader [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -c, --connection-string <string>  Azure Storage connection string");
        Console.WriteLine("  --container <string>              Container name (default: profile-images-staging)");
        Console.WriteLine("  -p, --path <string>               Path to style previews directory (default: ../../style-previews)");
        Console.WriteLine("  -f, --force                       Overwrite existing files");
        Console.WriteLine("  -d, --dry-run                     Show what would be uploaded without uploading");
        Console.WriteLine("  -v, --verbose                     Enable verbose output");
        Console.WriteLine("  -h, --help                        Show this help message");
        Console.WriteLine();
        Console.WriteLine("Environment Variables:");
        Console.WriteLine("  AZURE_STORAGE_CONNECTION_STRING   Azure Storage connection string");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  StylePreviewUploader");
        Console.WriteLine("  StylePreviewUploader --dry-run");
        Console.WriteLine("  StylePreviewUploader --force --verbose");
        Console.WriteLine("  StylePreviewUploader -c \"DefaultEndpointsProtocol=https;...\"");
    }

    private static void PrintSuccess(string message) => Console.WriteLine($"{_colorCodes["GREEN"]}✅ {message}{_colorCodes["RESET"]}");
    private static void PrintError(string message) => Console.WriteLine($"{_colorCodes["RED"]}❌ {message}{_colorCodes["RESET"]}");
    private static void PrintWarning(string message) => Console.WriteLine($"{_colorCodes["YELLOW"]}⚠️  {message}{_colorCodes["RESET"]}");
    private static void PrintInfo(string message) => Console.WriteLine($"{_colorCodes["BLUE"]}📋 {message}{_colorCodes["RESET"]}");

    private static void PrintSummary(UploadStats stats, int total, bool dryRun)
    {
        Console.WriteLine();
        Console.WriteLine($"{_colorCodes["CYAN"]}📊 Upload Summary:{_colorCodes["RESET"]}");
        Console.WriteLine($"   Total files: {total}");
        Console.WriteLine($"   {_colorCodes["GREEN"]}Uploaded: {stats.Uploaded}{_colorCodes["RESET"]}");
        Console.WriteLine($"   {_colorCodes["YELLOW"]}Skipped: {stats.Skipped}{_colorCodes["RESET"]}");
        Console.WriteLine($"   {_colorCodes["RED"]}Failed: {stats.Failed}{_colorCodes["RESET"]}");

        if (!dryRun && stats.TotalSize > 0)
        {
            var totalSizeMB = Math.Round(stats.TotalSize / (1024.0 * 1024.0), 2);
            Console.WriteLine($"   {_colorCodes["CYAN"]}Total uploaded: {totalSizeMB} MB{_colorCodes["RESET"]}");
        }
    }

    private static async Task GenerateSampleUrls(string connectionString, string containerName, IEnumerable<string> sampleFiles)
    {
        Console.WriteLine();
        Console.WriteLine($"{_colorCodes["CYAN"]}🔗 Sample URLs (for verification):{_colorCodes["RESET"]}");

        // Extract storage account name from connection string
        var accountNameMatch = Regex.Match(connectionString, @"AccountName=([^;]+)");
        if (accountNameMatch.Success)
        {
            var accountName = accountNameMatch.Groups[1].Value;
            var baseUrl = $"https://{accountName}.blob.core.windows.net/{containerName}/style-previews";

            foreach (var filePath in sampleFiles)
            {
                var fileName = Path.GetFileName(filePath);
                Console.WriteLine($"   {baseUrl}/{fileName}");
            }
        }
    }

    private static async Task TestApiEndpoint(ILogger logger)
    {
        Console.WriteLine();
        Console.WriteLine($"{_colorCodes["CYAN"]}🔄 Testing API endpoint...{_colorCodes["RESET"]}");

        try
        {
            using var httpClient = new HttpClient();
            var apiUrl = "https://aiprofilemaker-api-staging.thankfulriver-68674ea3.eastus2.azurecontainerapps.io/api/style-preview/list";
            
            var response = await httpClient.GetStringAsync(apiUrl);
            
            if (response.Contains("\"success\":true"))
            {
                // Extract count if available
                var countMatch = Regex.Match(response, @"""count"":(\d+)");
                var count = countMatch.Success ? countMatch.Groups[1].Value : "unknown";
                PrintSuccess($"API endpoint working! Found {count} style previews");
            }
            else
            {
                PrintWarning("API endpoint returned unexpected response");
            }
        }
        catch (Exception ex)
        {
            PrintWarning("Could not test API endpoint (this is normal if API is not running)");
            logger.LogDebug(ex, "API endpoint test failed");
        }
    }

    private class UploadOptions
    {
        public string? ConnectionString { get; set; }
        public string ContainerName { get; set; } = "profile-images-staging";
        public string PreviewsPath { get; set; } = "../../style-previews";
        public bool Force { get; set; }
        public bool DryRun { get; set; }
        public bool Verbose { get; set; }
    }

    private class UploadStats
    {
        public int Uploaded { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public long TotalSize { get; set; }
    }
}