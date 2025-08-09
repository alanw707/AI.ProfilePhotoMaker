using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

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

    public AzureBlobStorageService(
        BlobServiceClient blobServiceClient,
        IConfiguration configuration,
        ILogger<AzureBlobStorageService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _configuration = configuration;
        _logger = logger;
        _containerName = configuration["AzureStorage:ContainerName"] ?? "profile-images";
    }

    public async Task<string> SaveImageAsync(Stream imageStream, string fileName, string userId)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            var blobPath = $"generated/{userId}/{fileName}";
            var blobClient = containerClient.GetBlobClient(blobPath);

            await blobClient.UploadAsync(imageStream, overwrite: true);

            _logger.LogInformation("Saved image to Azure Blob Storage: {BlobPath}", blobPath);
            return blobPath;
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
            string containerName;
            string blobPath;
            
            if (storagePath.StartsWith("style-previews/"))
            {
                containerName = "style-previews";
                blobPath = storagePath.Substring("style-previews/".Length);
            }
            else
            {
                containerName = _containerName;
                blobPath = storagePath;
            }

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobPath.TrimStart('/'));

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
            string containerName;
            string blobPath;
            
            if (storagePath.StartsWith("style-previews/"))
            {
                containerName = "style-previews";
                blobPath = storagePath.Substring("style-previews/".Length);
            }
            else
            {
                containerName = _containerName;
                blobPath = storagePath;
            }

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobPath.TrimStart('/'));

            var response = await blobClient.DeleteIfExistsAsync();
            
            if (response.Value)
            {
                _logger.LogInformation("Deleted blob from Azure Storage: {StoragePath}", storagePath);
            }
            else
            {
                _logger.LogWarning("Attempted to delete non-existent blob: {StoragePath}", storagePath);
            }
            
            return response.Value;
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
            string containerName;
            string blobPath;
            
            if (storagePath.StartsWith("style-previews/"))
            {
                containerName = "style-previews";
                blobPath = storagePath.Substring("style-previews/".Length);
            }
            else
            {
                containerName = _containerName;
                blobPath = storagePath;
            }

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobPath.TrimStart('/'));

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
        // Default to frontend/internal use (for Azure, both internal and external use the same public blob URLs)
        return GetImageUrl(storagePath, forExternalApi: false);
    }

    public string GetImageUrl(string storagePath, bool forExternalApi)
    {
        // Handle style-previews paths by using correct container
        string containerName;
        string blobPath;
        
        if (storagePath.StartsWith("style-previews/"))
        {
            containerName = "style-previews";
            blobPath = storagePath.Substring("style-previews/".Length);
        }
        else
        {
            containerName = _containerName;
            blobPath = storagePath;
        }

        // Azure Blob Storage provides public URLs that work for both internal and external access
        // No need for different URLs based on context since blobs are publicly accessible
        var cleanPath = blobPath.TrimStart('/');
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(cleanPath);
        var url = blobClient.Uri.ToString();

        if (forExternalApi)
        {
            _logger.LogDebug("GetImageUrl for external API (Azure Blob): {Url}", url);
        }
        else
        {
            _logger.LogDebug("GetImageUrl for frontend (Azure Blob): {Url}", url);
        }

        return url;
    }

    public async Task<List<string>> ListUserImagesAsync(string userId)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var prefix = $"generated/{userId}/";
            var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif" };
            var imageFiles = new List<string>();

            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix))
            {
                var extension = Path.GetExtension(blobItem.Name).ToLowerInvariant();
                if (imageExtensions.Contains(extension))
                {
                    imageFiles.Add(blobItem.Name);
                }
            }

            return imageFiles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list images for user {UserId} from Azure Blob Storage", userId);
            return new List<string>();
        }
    }

    public async Task<StorageFileInfo?> GetFileInfoAsync(string storagePath)
    {
        try
        {
            string containerName;
            string blobPath;
            
            if (storagePath.StartsWith("style-previews/"))
            {
                containerName = "style-previews";
                blobPath = storagePath.Substring("style-previews/".Length);
            }
            else
            {
                containerName = _containerName;
                blobPath = storagePath;
            }

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobPath.TrimStart('/'));

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
                CreatedAt = properties.Value.CreatedOn.DateTime,
                ModifiedAt = properties.Value.LastModified.DateTime,
                ContentType = properties.Value.ContentType
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file info for blob: {StoragePath}", storagePath);
            return null;
        }
    }
}