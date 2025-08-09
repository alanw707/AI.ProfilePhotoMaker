namespace AI.ProfilePhotoMaker.API.Services.ImageProcessing;

/// <summary>
/// Service for downloading and storing images from external URLs with async I/O optimizations
/// </summary>
public class ImageDownloadService : IImageDownloadService
{
    private readonly HttpClient _httpClient;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ImageDownloadService> _logger;
    private readonly IAsyncFileService _asyncFileService;

    public ImageDownloadService(
        HttpClient httpClient,
        IWebHostEnvironment environment,
        ILogger<ImageDownloadService> logger,
        IAsyncFileService asyncFileService)
    {
        _httpClient = httpClient;
        _environment = environment;
        _logger = logger;
        _asyncFileService = asyncFileService;

        // Configure HTTP client with reasonable timeouts
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
    }


    public async Task<List<ImageDownloadResult>> DownloadImagesWithDetailsAsync(List<string> imageUrls, string userId, string style)
    {
        var downloadResults = new List<ImageDownloadResult>();

        if (imageUrls == null || imageUrls.Count == 0)
        {
            _logger.LogWarning("No image URLs provided for download for user {UserId}", userId);
            return downloadResults;
        }

        _logger.LogInformation("Starting download of {Count} images for user {UserId}, style {Style}",
            imageUrls.Count, userId, style);

        for (int i = 0; i < imageUrls.Count; i++)
        {
            var imageUrl = imageUrls[i];
            // Let DownloadImageAsync generate unique filename to prevent overwrites
            var fileName = (string?)null;

            try
            {
                var localPath = await DownloadImageAsync(imageUrl, userId, style, fileName);
                if (!string.IsNullOrEmpty(localPath))
                {
                    // Extract the actual filename from the path
                    var actualFileName = Path.GetFileName(localPath);
                    downloadResults.Add(new ImageDownloadResult
                    {
                        LocalPath = localPath,
                        FileName = actualFileName,
                        Success = true
                    });
                    _logger.LogInformation("Successfully downloaded image {Index} for user {UserId}: {LocalPath} as {FileName}",
                        i + 1, userId, localPath, actualFileName);
                }
                else
                {
                    downloadResults.Add(new ImageDownloadResult
                    {
                        LocalPath = string.Empty,
                        FileName = string.Empty,
                        Success = false
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download image {Index} from {Url} for user {UserId}",
                    i + 1, imageUrl, userId);
                downloadResults.Add(new ImageDownloadResult
                {
                    LocalPath = string.Empty,
                    FileName = string.Empty,
                    Success = false
                });
            }
        }

        var successCount = downloadResults.Count(r => r.Success);
        _logger.LogInformation("Downloaded {SuccessCount}/{TotalCount} images for user {UserId}, style {Style}",
            successCount, imageUrls.Count, userId, style);

        return downloadResults;
    }

    public async Task<string?> DownloadImageAsync(string imageUrl, string userId, string style, string? fileName = null)
    {
        try
        {
            if (string.IsNullOrEmpty(imageUrl))
            {
                _logger.LogWarning("Empty image URL provided for user {UserId}", userId);
                return null;
            }

            // Validate URL accessibility first
            if (!await ValidateImageUrlAsync(imageUrl))
            {
                _logger.LogWarning("Image URL not accessible: {Url} for user {UserId}", imageUrl, userId);
                return null;
            }

            // Ensure directory exists asynchronously
            var generatedDir = await EnsureGeneratedImagesDirectoryAsync(userId);

            // Generate filename if not provided
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = $"{style}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
            }

            // Download image
            var response = await _httpClient.GetAsync(imageUrl);
            response.EnsureSuccessStatusCode();

            // Determine file extension from content type or URL
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/png";
            var extension = GetExtensionFromContentType(contentType);

            if (string.IsNullOrEmpty(extension))
            {
                // Fallback to getting extension from URL
                var uri = new Uri(imageUrl);
                extension = Path.GetExtension(uri.LocalPath);
                if (string.IsNullOrEmpty(extension))
                {
                    extension = ".png"; // Default fallback
                }
            }

            var fullFileName = $"{fileName}{extension}";
            var localPath = Path.Combine(generatedDir, fullFileName);

            // Save image to local storage using async streaming with optimal buffer size
            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await _asyncFileService.CopyStreamToFileAsync(contentStream, localPath, 81920);

            _logger.LogInformation("Successfully downloaded image from {Url} to {LocalPath} for user {UserId}",
                imageUrl, localPath, userId);

            return localPath;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error downloading image from {Url} for user {UserId}: {Message}",
                imageUrl, userId, ex.Message);
            return null;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Timeout downloading image from {Url} for user {UserId}", imageUrl, userId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error downloading image from {Url} for user {UserId}: {Message}",
                imageUrl, userId, ex.Message);
            return null;
        }
    }

    public async Task<string> EnsureGeneratedImagesDirectoryAsync(string userId)
    {
        var generatedDir = Path.Combine(_environment.ContentRootPath, "generated", userId);

        await _asyncFileService.CreateDirectoryAsync(generatedDir);
        _logger.LogDebug("Ensured generated images directory exists: {Directory}", generatedDir);

        return generatedDir;
    }

    [Obsolete("Use EnsureGeneratedImagesDirectoryAsync instead")]
    public string EnsureGeneratedImagesDirectory(string userId)
    {
        return EnsureGeneratedImagesDirectoryAsync(userId).GetAwaiter().GetResult();
    }

    public async Task<bool> ValidateImageUrlAsync(string imageUrl)
    {
        try
        {
            if (string.IsNullOrEmpty(imageUrl))
                return false;

            using var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, imageUrl));

            if (!response.IsSuccessStatusCode)
                return false;

            // Check if content type is an image
            var contentType = response.Content.Headers.ContentType?.MediaType;
            return !string.IsNullOrEmpty(contentType) && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to validate image URL: {Url}", imageUrl);
            return false;
        }
    }

    private static string GetExtensionFromContentType(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/bmp" => ".bmp",
            "image/tiff" => ".tiff",
            _ => string.Empty
        };
    }
}