using System.IO.Compression;

namespace AI.ProfilePhotoMaker.API.Services;

/// <summary>
/// High-performance async ZIP service for creating training archives without memory loading
/// </summary>
public interface IAsyncZipService
{
    /// <summary>
    /// Creates a ZIP archive from files using streaming compression to avoid memory issues
    /// </summary>
    Task<AsyncZipResult> CreateStreamingZipAsync(string sourceDirectory, string zipFilePath, AsyncZipOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a ZIP archive from specific files using streaming compression
    /// </summary>
    Task<AsyncZipResult> CreateStreamingZipFromFilesAsync(IEnumerable<string> filePaths, string zipFilePath, AsyncZipOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that files meet requirements for ZIP creation
    /// </summary>
    Task<ZipValidationResult> ValidateFilesForZipAsync(string sourceDirectory, AsyncZipOptions? options = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for async ZIP operations
/// </summary>
public class AsyncZipOptions
{
    /// <summary>
    /// Allowed file extensions (e.g., [".jpg", ".png"])
    /// </summary>
    public string[]? AllowedExtensions { get; set; }

    /// <summary>
    /// Minimum number of files required
    /// </summary>
    public int MinimumFiles { get; set; } = 1;

    /// <summary>
    /// Maximum file size in bytes (0 = no limit)
    /// </summary>
    public long MaxFileSize { get; set; } = 0;

    /// <summary>
    /// Compression level
    /// </summary>
    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;

    /// <summary>
    /// Buffer size for streaming operations
    /// </summary>
    public int BufferSize { get; set; } = 81920; // 80KB

    /// <summary>
    /// Whether to overwrite existing ZIP files
    /// </summary>
    public bool OverwriteExisting { get; set; } = true;

    /// <summary>
    /// Maximum concurrent file processing
    /// </summary>
    public int MaxConcurrency { get; set; } = 4;
}

/// <summary>
/// Result of async ZIP creation
/// </summary>
public class AsyncZipResult
{
    public bool Success { get; set; }
    public string? ZipFilePath { get; set; }
    public int FilesProcessed { get; set; }
    public long CompressedSize { get; set; }
    public long UncompressedSize { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> ProcessedFiles { get; set; } = new();
    public List<string> SkippedFiles { get; set; } = new();
}

/// <summary>
/// Result of ZIP file validation
/// </summary>
public class ZipValidationResult
{
    public bool IsValid { get; set; }
    public int ValidFiles { get; set; }
    public int InvalidFiles { get; set; }
    public long TotalSize { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public List<string> ValidFilePaths { get; set; } = new();
    public List<string> InvalidFilePaths { get; set; } = new();
}

/// <summary>
/// High-performance async ZIP service implementation
/// </summary>
public class AsyncZipService : IAsyncZipService
{
    private readonly IAsyncFileService _asyncFileService;
    private readonly ILogger<AsyncZipService> _logger;

    public AsyncZipService(IAsyncFileService asyncFileService, ILogger<AsyncZipService> logger)
    {
        _asyncFileService = asyncFileService;
        _logger = logger;
    }

    public async Task<AsyncZipResult> CreateStreamingZipAsync(string sourceDirectory, string zipFilePath, AsyncZipOptions? options = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        options ??= new AsyncZipOptions();

        var result = new AsyncZipResult();

        try
        {
            _logger.LogInformation("Starting streaming ZIP creation from {SourceDirectory} to {ZipPath}", sourceDirectory, zipFilePath);

            // Validate source directory exists
            if (!await _asyncFileService.FileExistsAsync(sourceDirectory))
            {
                result.ErrorMessage = $"Source directory not found: {sourceDirectory}";
                return result;
            }

            // Get and validate files
            var validation = await ValidateFilesForZipAsync(sourceDirectory, options, cancellationToken);
            if (!validation.IsValid)
            {
                result.ErrorMessage = $"File validation failed: {string.Join(", ", validation.ValidationErrors)}";
                return result;
            }

            // Create ZIP from validated files
            return await CreateStreamingZipFromFilesAsync(validation.ValidFilePaths, zipFilePath, options, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            result.ErrorMessage = "ZIP creation was cancelled";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create streaming ZIP from {SourceDirectory}", sourceDirectory);
            result.ErrorMessage = ex.Message;
            return result;
        }
        finally
        {
            result.ProcessingTime = stopwatch.Elapsed;
        }
    }

    public async Task<AsyncZipResult> CreateStreamingZipFromFilesAsync(IEnumerable<string> filePaths, string zipFilePath, AsyncZipOptions? options = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        options ??= new AsyncZipOptions();

        var result = new AsyncZipResult
        {
            ZipFilePath = zipFilePath
        };

        try
        {
            var files = filePaths.ToArray();
            _logger.LogInformation("Creating streaming ZIP with {FileCount} files: {ZipPath}", files.Length, zipFilePath);

            if (files.Length == 0)
            {
                result.ErrorMessage = "No files provided for ZIP creation";
                return result;
            }

            // Ensure ZIP directory exists
            var zipDirectory = Path.GetDirectoryName(zipFilePath);
            if (!string.IsNullOrEmpty(zipDirectory))
            {
                await _asyncFileService.CreateDirectoryAsync(zipDirectory, cancellationToken);
            }

            // Delete existing ZIP file if it exists and overwrite is enabled
            if (options.OverwriteExisting && await _asyncFileService.FileExistsAsync(zipFilePath, cancellationToken))
            {
                await _asyncFileService.DeleteFileAsync(zipFilePath, cancellationToken);
                _logger.LogDebug("Deleted existing ZIP file before creating new one: {ZipPath}", zipFilePath);
            }

            // Create ZIP with streaming compression to avoid memory issues
            await using var zipFileStream = new FileStream(zipFilePath, FileMode.Create, FileAccess.Write, FileShare.None, options.BufferSize, useAsync: true);
            using var archive = new ZipArchive(zipFileStream, ZipArchiveMode.Create, false);

            // Process files with controlled concurrency
            var semaphore = new SemaphoreSlim(options.MaxConcurrency, options.MaxConcurrency);
            var tasks = files.Select(async file =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    return await ProcessFileForZipAsync(archive, file, options, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var fileResults = await Task.WhenAll(tasks);

            // Aggregate results
            foreach (var fileResult in fileResults)
            {
                if (fileResult.Success)
                {
                    result.ProcessedFiles.Add(fileResult.FilePath);
                    result.UncompressedSize += fileResult.UncompressedSize;
                }
                else
                {
                    result.SkippedFiles.Add(fileResult.FilePath);
                    _logger.LogWarning("Skipped file {FilePath}: {Error}", fileResult.FilePath, fileResult.Error);
                }
            }

            result.FilesProcessed = result.ProcessedFiles.Count;

            // Get compressed size
            await zipFileStream.FlushAsync(cancellationToken);
            result.CompressedSize = zipFileStream.Length;

            result.Success = result.FilesProcessed > 0;

            if (result.Success)
            {
                _logger.LogInformation("Successfully created streaming ZIP: {ZipPath} ({CompressedSize} bytes, {FileCount} files, {ProcessingTime}ms)", 
                    zipFilePath, result.CompressedSize, result.FilesProcessed, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                result.ErrorMessage = "No files were successfully processed";
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            // Clean up partial ZIP on cancellation
            try
            {
                if (await _asyncFileService.FileExistsAsync(zipFilePath, CancellationToken.None))
                {
                    await _asyncFileService.DeleteFileAsync(zipFilePath, CancellationToken.None);
                }
            }
            catch { /* Best effort cleanup */ }

            result.ErrorMessage = "ZIP creation was cancelled";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create streaming ZIP: {ZipPath}", zipFilePath);
            result.ErrorMessage = ex.Message;
            return result;
        }
        finally
        {
            result.ProcessingTime = stopwatch.Elapsed;
        }
    }

    public async Task<ZipValidationResult> ValidateFilesForZipAsync(string sourceDirectory, AsyncZipOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new AsyncZipOptions();
        var result = new ZipValidationResult();

        try
        {
            // Get all files from directory
            var allFiles = await _asyncFileService.GetDirectoryFilesAsync(sourceDirectory, "*", false, cancellationToken);

            // Filter by allowed extensions if specified
            var filteredFiles = allFiles.AsEnumerable();
            if (options.AllowedExtensions != null && options.AllowedExtensions.Length > 0)
            {
                var allowedExtLower = options.AllowedExtensions.Select(ext => ext.ToLowerInvariant()).ToArray();
                filteredFiles = filteredFiles.Where(f => allowedExtLower.Contains(Path.GetExtension(f).ToLowerInvariant()));
            }

            var filesToValidate = filteredFiles.ToArray();

            // Validate each file
            foreach (var file in filesToValidate)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var fileInfo = await _asyncFileService.GetFileInfoAsync(file, cancellationToken);
                    if (fileInfo == null)
                    {
                        result.InvalidFiles++;
                        result.InvalidFilePaths.Add(file);
                        result.ValidationErrors.Add($"File not found: {Path.GetFileName(file)}");
                        continue;
                    }

                    // Check file size if limit is set
                    if (options.MaxFileSize > 0 && fileInfo.Length > options.MaxFileSize)
                    {
                        result.InvalidFiles++;
                        result.InvalidFilePaths.Add(file);
                        result.ValidationErrors.Add($"File too large: {Path.GetFileName(file)} ({fileInfo.Length} bytes)");
                        continue;
                    }

                    result.ValidFiles++;
                    result.ValidFilePaths.Add(file);
                    result.TotalSize += fileInfo.Length;
                }
                catch (Exception ex)
                {
                    result.InvalidFiles++;
                    result.InvalidFilePaths.Add(file);
                    result.ValidationErrors.Add($"Error validating {Path.GetFileName(file)}: {ex.Message}");
                }
            }

            // Check minimum file requirement
            if (result.ValidFiles < options.MinimumFiles)
            {
                result.ValidationErrors.Add($"Insufficient files: {result.ValidFiles} found, {options.MinimumFiles} required");
                result.IsValid = false;
            }
            else
            {
                result.IsValid = true;
            }

            _logger.LogInformation("File validation completed: {ValidFiles} valid, {InvalidFiles} invalid files", 
                result.ValidFiles, result.InvalidFiles);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate files for ZIP creation in {SourceDirectory}", sourceDirectory);
            result.ValidationErrors.Add($"Validation failed: {ex.Message}");
            return result;
        }
    }

    private async Task<FileProcessResult> ProcessFileForZipAsync(ZipArchive archive, string filePath, AsyncZipOptions options, CancellationToken cancellationToken)
    {
        var result = new FileProcessResult
        {
            FilePath = filePath
        };

        try
        {
            var entryName = Path.GetFileName(filePath);
            var entry = archive.CreateEntry(entryName, options.CompressionLevel);

            await using var entryStream = entry.Open();
            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, options.BufferSize, useAsync: true);

            var fileInfo = await _asyncFileService.GetFileInfoAsync(filePath, cancellationToken);
            result.Result = new { UncompressedSize = fileInfo?.Length ?? 0 };

            await fileStream.CopyToAsync(entryStream, options.BufferSize, cancellationToken);

            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Failed to process file for ZIP: {FilePath}", filePath);
            return result;
        }
    }

    private class FileProcessResult
    {
        public string FilePath { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public object? Result { get; set; }

        public string Error => ErrorMessage ?? "Unknown error";
        public long UncompressedSize => Result is { } r && r.GetType().GetProperty("UncompressedSize")?.GetValue(r) is long size ? size : 0;
    }
}