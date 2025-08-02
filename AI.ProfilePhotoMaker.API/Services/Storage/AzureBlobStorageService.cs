using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Text.RegularExpressions;

namespace AI.ProfilePhotoMaker.API.Services.Storage;

/// <summary>
/// Azure Blob Storage implementation of storage service
/// </summary>
public class AzureBlobStorageService : IStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AzureBlobStorageService> _logger;
    private readonly string _containerName;
    private readonly string _storageAccountUrl;

    public AzureBlobStorageService(
        IConfiguration configuration,
        ILogger<AzureBlobStorageService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        var connectionString = _configuration.GetConnectionString("AzureStorage") ??
                              _configuration["AzureStorage:ConnectionString"];

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Azure Storage connection string is not configured");
        }

        _blobServiceClient = new BlobServiceClient(connectionString);
        _containerName = _configuration["AzureStorage:ContainerName"] ?? "profile-images";

        // Extract storage account URL from connection string for public URLs
        var accountNameMatch = Regex.Match(connectionString, @"AccountName=([^;]+)");
        if (accountNameMatch.Success)
        {
            var accountName = accountNameMatch.Groups[1].Value;
            _storageAccountUrl = $"https://{accountName}.blob.core.windows.net";
        }
        else
        {
            _storageAccountUrl = "https://unknown.blob.core.windows.net";
        }

        _logger.LogInformation("Azure Blob Storage Service initialized with container: {ContainerName}", _containerName);
    }

    public async Task<string> SaveImageAsync(Stream imageStream, string fileName, string userId)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            
            // Ensure container exists
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            // Create blob path: generated/{userId}/{fileName} or style-previews/{fileName}
            var blobName = IsStylePreview(fileName) 
                ? $"style-previews/{fileName}"
                : $"generated/{userId}/{fileName}";

            var blobClient = containerClient.GetBlobClient(blobName);

            // Set content type based on file extension
            var contentType = GetContentType(Path.GetExtension(fileName));

            // Upload the blob with overwrite
            await blobClient.UploadAsync(imageStream, overwrite: true);
            
            // Set content type
            await blobClient.SetHttpHeadersAsync(new BlobHttpHeaders
            {
                ContentType = contentType
            });

            _logger.LogInformation("Saved image to Azure Blob Storage: {BlobName}", blobName);
            return blobName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save image {FileName} for user {UserId} to Azure Blob Storage", fileName, userId);
            throw;
        }
    }

    public async Task<Stream?> GetImageAsync(string storagePath)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(storagePath);

            if (!await blobClient.ExistsAsync())
            {
                _logger.LogWarning("Blob not found: {StoragePath}", storagePath);
                return null;
            }

            var response = await blobClient.DownloadStreamingAsync();
            return response.Value.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get image from Azure Blob Storage: {StoragePath}", storagePath);
            return null;
        }
    }

    public async Task<bool> DeleteImageAsync(string storagePath)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(storagePath);

            var result = await blobClient.DeleteIfExistsAsync();
            
            if (result.Value)
            {
                _logger.LogInformation("Deleted blob from Azure Storage: {StoragePath}", storagePath);
            }
            else
            {
                _logger.LogWarning("Attempted to delete non-existent blob: {StoragePath}", storagePath);
            }

            return result.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete blob from Azure Storage: {StoragePath}", storagePath);
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string storagePath)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(storagePath);

            var response = await blobClient.ExistsAsync();
            return response.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if blob exists: {StoragePath}", storagePath);
            return false;
        }
    }

    public string GetImageUrl(string storagePath)
    {
        // Return the public blob URL
        var cleanPath = storagePath.TrimStart('/');
        return $"{_storageAccountUrl}/{_containerName}/{cleanPath}";
    }

    public async Task<List<string>> ListUserImagesAsync(string userId)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var prefix = $"generated/{userId}/";
            var blobs = new List<string>();

            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix))
            {
                blobs.Add(blobItem.Name);
            }

            return blobs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list blobs for user {UserId}", userId);
            return new List<string>();
        }
    }

    public async Task<StorageFileInfo?> GetFileInfoAsync(string storagePath)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(storagePath);

            if (!await blobClient.ExistsAsync())
            {
                return null;
            }

            var properties = await blobClient.GetPropertiesAsync();
            var fileName = Path.GetFileName(storagePath);

            return new StorageFileInfo
            {
                FileName = fileName,
                Size = properties.Value.ContentLength,
                CreatedAt = properties.Value.CreatedOn.UtcDateTime,
                ModifiedAt = properties.Value.LastModified.UtcDateTime,
                ContentType = properties.Value.ContentType
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get blob info for: {StoragePath}", storagePath);
            return null;
        }
    }

    /// <summary>
    /// Checks if the filename indicates it's a style preview image
    /// </summary>
    private static bool IsStylePreview(string fileName)
    {
        return fileName.Contains("-preview.jpg") || fileName.EndsWith("-preview.jpg");
    }

    /// <summary>
    /// Gets the MIME content type for a file extension
    /// </summary>
    private static string GetContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".tiff" or ".tif" => "image/tiff",
            _ => "application/octet-stream"
        };
    }
}