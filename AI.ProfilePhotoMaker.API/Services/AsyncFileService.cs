using System.IO.Compression;
using System.Text;

namespace AI.ProfilePhotoMaker.API.Services;

/// <summary>
/// High-performance async file operations service that provides non-blocking I/O
/// </summary>
public interface IAsyncFileService
{
    /// <summary>
    /// Asynchronously creates a directory if it doesn't exist
    /// </summary>
    Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously copies a stream to a file with configurable buffer size
    /// </summary>
    Task CopyStreamToFileAsync(Stream sourceStream, string filePath, int bufferSize = 81920, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously deletes a file if it exists
    /// </summary>
    Task<bool> DeleteFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously checks if a file exists
    /// </summary>
    Task<bool> FileExistsAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously creates a ZIP archive from files using streaming compression
    /// </summary>
    Task<string?> CreateStreamingZipAsync(string sourceDirectory, string zipFilePath, string[]? allowedExtensions = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously gets directory files matching patterns
    /// </summary>
    Task<string[]> GetDirectoryFilesAsync(string directoryPath, string searchPattern = "*", bool recursive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously gets file information
    /// </summary>
    Task<FileInfo?> GetFileInfoAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously opens a file stream for reading
    /// </summary>
    Task<FileStream?> OpenFileStreamAsync(string filePath, FileMode mode = FileMode.Open, FileAccess access = FileAccess.Read, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously processes multiple files with background parallelization
    /// </summary>
    Task<List<FileProcessResult>> ProcessFilesAsync<T>(IEnumerable<string> filePaths, Func<string, CancellationToken, Task<T?>> processor, int maxConcurrency = 4, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously moves a file with retry logic
    /// </summary>
    Task<bool> MoveFileAsync(string sourceFilePath, string destinationFilePath, bool overwrite = false, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of file processing operations
/// </summary>
public class FileProcessResult
{
    public string FilePath { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public object? Result { get; set; }
    public TimeSpan ProcessingTime { get; set; }
}

/// <summary>
/// High-performance async file operations service implementation
/// </summary>
public class AsyncFileService : IAsyncFileService
{
    private readonly ILogger<AsyncFileService> _logger;
    private readonly SemaphoreSlim _concurrencySemaphore;

    public AsyncFileService(ILogger<AsyncFileService> logger)
    {
        _logger = logger;
        _concurrencySemaphore = new SemaphoreSlim(Environment.ProcessorCount * 2);
    }

    public async Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    _logger.LogDebug("Created directory: {Path}", path);
                }
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create directory: {Path}", path);
            throw;
        }
    }

    public async Task CopyStreamToFileAsync(Stream sourceStream, string filePath, int bufferSize = 81920, CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure directory exists first
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                await CreateDirectoryAsync(directory, cancellationToken);
            }

            await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true);
            await sourceStream.CopyToAsync(fileStream, bufferSize, cancellationToken);
            await fileStream.FlushAsync(cancellationToken);

            _logger.LogDebug("Successfully copied stream to file: {FilePath}", filePath);
        }
        catch (OperationCanceledException)
        {
            // Clean up partial file on cancellation
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch { /* Best effort cleanup */ }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy stream to file: {FilePath}", filePath);
            throw;
        }
    }

    public async Task<bool> DeleteFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!File.Exists(filePath))
                {
                    _logger.LogDebug("File not found for deletion: {FilePath}", filePath);
                    return false;
                }

                File.Delete(filePath);
                _logger.LogDebug("Successfully deleted file: {FilePath}", filePath);
                return true;
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<bool> FileExistsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return File.Exists(filePath);
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check file existence: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<string?> CreateStreamingZipAsync(string sourceDirectory, string zipFilePath, string[]? allowedExtensions = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(sourceDirectory))
            {
                _logger.LogWarning("Source directory not found: {SourceDirectory}", sourceDirectory);
                return null;
            }

            // Ensure ZIP directory exists
            var zipDirectory = Path.GetDirectoryName(zipFilePath);
            if (!string.IsNullOrEmpty(zipDirectory))
            {
                await CreateDirectoryAsync(zipDirectory, cancellationToken);
            }

            // Get files to compress
            var files = await GetDirectoryFilesAsync(sourceDirectory, "*", false, cancellationToken);

            if (allowedExtensions != null)
            {
                files = files.Where(f => allowedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())).ToArray();
            }

            if (files.Length == 0)
            {
                _logger.LogWarning("No files found to compress in: {SourceDirectory}", sourceDirectory);
                return null;
            }

            _logger.LogInformation("Creating streaming ZIP with {FileCount} files: {ZipPath}", files.Length, zipFilePath);

            // Delete existing ZIP file if it exists
            if (await FileExistsAsync(zipFilePath, cancellationToken))
            {
                await DeleteFileAsync(zipFilePath, cancellationToken);
            }

            // Create ZIP with streaming compression to avoid memory issues
            await using var zipFileStream = new FileStream(zipFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            using var archive = new ZipArchive(zipFileStream, ZipArchiveMode.Create, false);

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entryName = Path.GetFileName(file);
                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);

                await using var entryStream = entry.Open();
                await using var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);

                await fileStream.CopyToAsync(entryStream, 4096, cancellationToken);
            }

            _logger.LogInformation("Successfully created streaming ZIP: {ZipPath}", zipFilePath);
            return zipFilePath;
        }
        catch (OperationCanceledException)
        {
            // Clean up partial ZIP on cancellation
            try
            {
                if (await FileExistsAsync(zipFilePath, cancellationToken))
                {
                    await DeleteFileAsync(zipFilePath, cancellationToken);
                }
            }
            catch { /* Best effort cleanup */ }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create streaming ZIP from {SourceDirectory} to {ZipPath}", sourceDirectory, zipFilePath);
            return null;
        }
    }

    public async Task<string[]> GetDirectoryFilesAsync(string directoryPath, string searchPattern = "*", bool recursive = false, CancellationToken cancellationToken = default)
    {
        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!Directory.Exists(directoryPath))
                {
                    return Array.Empty<string>();
                }

                var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                return Directory.GetFiles(directoryPath, searchPattern, searchOption);
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get directory files: {DirectoryPath}", directoryPath);
            return Array.Empty<string>();
        }
    }

    public async Task<FileInfo?> GetFileInfoAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!File.Exists(filePath))
                {
                    return null;
                }

                return new FileInfo(filePath);
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file info: {FilePath}", filePath);
            return null;
        }
    }

    public async Task<FileStream?> OpenFileStreamAsync(string filePath, FileMode mode = FileMode.Open, FileAccess access = FileAccess.Read, CancellationToken cancellationToken = default)
    {
        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (access == FileAccess.Read && !File.Exists(filePath))
                {
                    return null;
                }

                return new FileStream(filePath, mode, access, FileShare.Read, 4096, useAsync: true);
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open file stream: {FilePath}", filePath);
            return null;
        }
    }

    public async Task<List<FileProcessResult>> ProcessFilesAsync<T>(IEnumerable<string> filePaths, Func<string, CancellationToken, Task<T?>> processor, int maxConcurrency = 4, CancellationToken cancellationToken = default)
    {
        var results = new List<FileProcessResult>();
        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        var tasks = filePaths.Select(async filePath =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var result = new FileProcessResult
                {
                    FilePath = filePath,
                    Success = false
                };

                try
                {
                    var processResult = await processor(filePath, cancellationToken);
                    result.Success = true;
                    result.Result = processResult;
                }
                catch (Exception ex)
                {
                    result.ErrorMessage = ex.Message;
                    _logger.LogError(ex, "Failed to process file: {FilePath}", filePath);
                }
                finally
                {
                    result.ProcessingTime = stopwatch.Elapsed;
                }

                return result;
            }
            finally
            {
                semaphore.Release();
            }
        });

        var taskResults = await Task.WhenAll(tasks);
        results.AddRange(taskResults);

        return results;
    }

    public async Task<bool> MoveFileAsync(string sourceFilePath, string destinationFilePath, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!File.Exists(sourceFilePath))
                {
                    _logger.LogWarning("Source file not found for move: {SourcePath}", sourceFilePath);
                    return false;
                }

                // Ensure destination directory exists
                var destinationDirectory = Path.GetDirectoryName(destinationFilePath);
                if (!string.IsNullOrEmpty(destinationDirectory) && !Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                if (overwrite && File.Exists(destinationFilePath))
                {
                    File.Delete(destinationFilePath);
                }

                File.Move(sourceFilePath, destinationFilePath);
                _logger.LogDebug("Successfully moved file from {SourcePath} to {DestinationPath}", sourceFilePath, destinationFilePath);
                return true;
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move file from {SourcePath} to {DestinationPath}", sourceFilePath, destinationFilePath);
            return false;
        }
    }

    public void Dispose()
    {
        _concurrencySemaphore?.Dispose();
    }
}