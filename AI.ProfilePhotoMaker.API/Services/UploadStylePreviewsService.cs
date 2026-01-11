using AI.ProfilePhotoMaker.API.Services.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace AI.ProfilePhotoMaker.API.Services;

/// <summary>
/// Service for uploading local style preview images to Azure Blob Storage
/// </summary>
public class UploadStylePreviewsService
{
    private readonly IStorageService _storageService;
    private readonly ILogger<UploadStylePreviewsService> _logger;
    private readonly IHostEnvironment _environment;

    public UploadStylePreviewsService(
        IStorageService storageService,
        ILogger<UploadStylePreviewsService> logger,
        IHostEnvironment environment)
    {
        _storageService = storageService;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Upload style preview images to Azure Blob Storage
    /// </summary>
    /// <param name="dryRun">If true, only simulate the upload without actually uploading</param>
    /// <param name="force">If true, overwrite existing files in Azure</param>
    /// <returns>Exit code: 0 for success, 1 for failure</returns>
    public async Task<int> UploadStylePreviewsAsync(bool dryRun = false, bool force = false)
    {
        try
        {
            var stylePreviewsPath = Path.Combine(_environment.ContentRootPath, "style-previews");

            Console.WriteLine("=== Style Previews Upload ===");
            Console.WriteLine($"Source Directory: {stylePreviewsPath}");
            Console.WriteLine($"Target Container: style-previews");
            Console.WriteLine($"Dry Run: {(dryRun ? "Yes" : "No")}");
            Console.WriteLine($"Force Overwrite: {(force ? "Yes" : "No")}");
            Console.WriteLine();

            // Validate local directory exists
            if (!Directory.Exists(stylePreviewsPath))
            {
                Console.WriteLine($"ERROR: Style previews directory not found: {stylePreviewsPath}");
                _logger.LogError("Style previews directory not found: {Path}", stylePreviewsPath);
                return 1;
            }

            // Get all .jpg and .png files
            var imageFiles = Directory.GetFiles(stylePreviewsPath, "*.jpg", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(stylePreviewsPath, "*.png", SearchOption.TopDirectoryOnly))
                .ToArray();

            if (!imageFiles.Any())
            {
                Console.WriteLine("No .jpg or .png files found in style-previews directory");
                return 0;
            }

            Console.WriteLine($"Found {imageFiles.Length} image files to process:");
            foreach (var file in imageFiles)
            {
                var fileName = Path.GetFileName(file);
                Console.WriteLine($"  • {fileName}");
            }
            Console.WriteLine();

            var uploadedCount = 0;
            var skippedCount = 0;
            var failedCount = 0;
            var failedFiles = new List<string>();

            // Process each file
            foreach (var filePath in imageFiles)
            {
                var fileName = Path.GetFileName(filePath);
                var targetFileName = GetTargetFileName(fileName);
                var targetPath = $"style-previews/{targetFileName}";

                try
                {
                    // Check if file already exists in Azure (unless force mode)
                    if (!force)
                    {
                        var exists = await _storageService.ExistsAsync(targetPath);
                        if (exists)
                        {
                            Console.WriteLine($"⏩ SKIP: {fileName} (already exists, use --force to overwrite)");
                            skippedCount++;
                            continue;
                        }
                    }

                    if (dryRun)
                    {
                        Console.WriteLine($"🔍 DRY RUN: Would upload {fileName} as {targetFileName}");
                        uploadedCount++;
                        continue;
                    }

                    // Upload the file
                    Console.Write($"📤 Uploading {fileName} as {targetFileName}... ");

                    using var uploadStream = OpenUploadStream(filePath);
                    var storagePath = await _storageService.SaveImageToPathAsync(uploadStream, targetPath);

                    // Verify the upload
                    var uploadExists = await _storageService.ExistsAsync(storagePath);
                    if (uploadExists)
                    {
                        var fileInfo = await _storageService.GetFileInfoAsync(storagePath);
                        Console.WriteLine($"✅ SUCCESS ({fileInfo?.Size ?? 0:N0} bytes)");

                        _logger.LogInformation("Successfully uploaded style preview: {SourceFile} -> {StoragePath}",
                            fileName, storagePath);
                        uploadedCount++;
                    }
                    else
                    {
                        Console.WriteLine("❌ FAILED (upload verification failed)");
                        _logger.LogError("Upload verification failed for: {FileName}", fileName);
                        failedCount++;
                        failedFiles.Add(fileName);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ ERROR: {ex.Message}");
                    _logger.LogError(ex, "Failed to upload style preview: {FileName}", fileName);
                    failedCount++;
                    failedFiles.Add(fileName);
                }
            }

            // Summary
            Console.WriteLine();
            Console.WriteLine("=== Upload Summary ===");
            Console.WriteLine($"Total Files: {imageFiles.Length}");
            Console.WriteLine($"Uploaded: {uploadedCount}");
            Console.WriteLine($"Skipped: {skippedCount}");
            Console.WriteLine($"Failed: {failedCount}");

            if (failedFiles.Any())
            {
                Console.WriteLine();
                Console.WriteLine("Failed Files:");
                foreach (var fileName in failedFiles)
                {
                    Console.WriteLine($"  • {fileName}");
                }
            }

            if (dryRun)
            {
                Console.WriteLine();
                Console.WriteLine("ℹ️  This was a dry run. Use without --dry-run to perform actual upload.");
            }

            // Return appropriate exit code
            if (failedCount > 0)
            {
                _logger.LogWarning("Upload completed with {FailedCount} failures", failedCount);
                return 1;
            }

            _logger.LogInformation("Style previews upload completed successfully. Uploaded: {UploadedCount}, Skipped: {SkippedCount}",
                uploadedCount, skippedCount);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CRITICAL ERROR: {ex.Message}");
            _logger.LogCritical(ex, "Critical error during style previews upload");
            return 1;
        }
    }

    /// <summary>
    /// Display help information for the upload command
    /// </summary>
    public static void ShowHelp()
    {
        Console.WriteLine("Style Previews Upload Command");
        Console.WriteLine();
        Console.WriteLine("USAGE:");
        Console.WriteLine("  upload-previews                    Upload all style preview images");
        Console.WriteLine("  upload-previews --dry-run          Simulate upload without actually uploading");
        Console.WriteLine("  upload-previews --force             Overwrite existing files in Azure");
        Console.WriteLine("  upload-previews --dry-run --force   Simulate upload with force overwrite");
        Console.WriteLine();
        Console.WriteLine("DESCRIPTION:");
        Console.WriteLine("  Uploads .jpg/.png files from the local 'style-previews' directory to Azure Blob Storage");
        Console.WriteLine("  in the 'style-previews' container. Files are uploaded with public read access.");
        Console.WriteLine();
        Console.WriteLine("FLAGS:");
        Console.WriteLine("  --dry-run      Simulate the operation without actually uploading files");
        Console.WriteLine("  --force        Overwrite existing files in Azure (default: skip existing files)");
        Console.WriteLine("  --help         Show this help information");
        Console.WriteLine();
        Console.WriteLine("EXAMPLES:");
        Console.WriteLine("  dotnet run upload-previews");
        Console.WriteLine("  dotnet run upload-previews --dry-run");
        Console.WriteLine("  dotnet run upload-previews --force");
        Console.WriteLine();
    }

    /// <summary>
    /// List all style preview files in Azure Blob Storage
    /// </summary>
    public async Task<int> ListStylePreviewsAsync()
    {
        try
        {
            Console.WriteLine("=== Azure Style Previews Inventory ===");

            // Get local files for comparison
            var stylePreviewsPath = Path.Combine(_environment.ContentRootPath, "style-previews");
            var localFiles = new HashSet<string>();

            if (Directory.Exists(stylePreviewsPath))
            {
                var files = Directory.GetFiles(stylePreviewsPath, "*.jpg", SearchOption.TopDirectoryOnly)
                    .Concat(Directory.GetFiles(stylePreviewsPath, "*.png", SearchOption.TopDirectoryOnly));
                localFiles = files.Select(Path.GetFileName).Where(f => f != null).ToHashSet(StringComparer.OrdinalIgnoreCase)!;
            }

            Console.WriteLine($"Local Files Found: {localFiles.Count}");
            Console.WriteLine();

            // Check each local file in Azure
            var existingCount = 0;
            var missingCount = 0;
            var totalSize = 0L;

            if (localFiles.Any())
            {
                Console.WriteLine("File Status:");
                Console.WriteLine("STATUS   SIZE        FILE");
                Console.WriteLine("------   ---------   ----");

                foreach (var fileName in localFiles.OrderBy(f => f))
                {
                    var targetFileName = GetTargetFileName(fileName);
                    var targetPath = $"style-previews/{targetFileName}";

                    try
                    {
                        var exists = await _storageService.ExistsAsync(targetPath);

                        if (exists)
                        {
                            var fileInfo = await _storageService.GetFileInfoAsync(targetPath);
                            var size = fileInfo?.Size ?? 0;
                            totalSize += size;

                            Console.WriteLine($"✅ EXIST  {size,9:N0}   {targetFileName}");
                            existingCount++;
                        }
                        else
                        {
                            Console.WriteLine($"❌ MISS  {0,9:N0}   {targetFileName}");
                            missingCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️  ERROR {0,9:N0}   {targetFileName} ({ex.Message})");
                        missingCount++;
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("=== Summary ===");
            Console.WriteLine($"Local Files: {localFiles.Count}");
            Console.WriteLine($"In Azure: {existingCount}");
            Console.WriteLine($"Missing: {missingCount}");
            Console.WriteLine($"Total Size: {totalSize:N0} bytes ({totalSize / (1024.0 * 1024.0):F2} MB)");

            if (missingCount > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"Run 'upload-previews' to upload missing files.");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Failed to list style previews: {ex.Message}");
            _logger.LogError(ex, "Failed to list style previews");
            return 1;
        }
    }

    private static string GetTargetFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            return Path.ChangeExtension(fileName, ".jpg");
        }

        return fileName;
    }

    private static Stream OpenUploadStream(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        if (!extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            return File.OpenRead(filePath);
        }

        using var image = Image.Load(filePath);
        var jpegStream = new MemoryStream();
        image.SaveAsJpeg(jpegStream, new JpegEncoder { Quality = 90 });
        jpegStream.Position = 0;
        return jpegStream;
    }
}
