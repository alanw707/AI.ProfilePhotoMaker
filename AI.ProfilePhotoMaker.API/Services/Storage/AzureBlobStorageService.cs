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

    public async Task<string> SaveImageAsync(Stream imageStream, string fileName, string userId, string folderType = "generated")
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            var blobPath = $"{folderType}/{userId}/{fileName}";
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

        var cleanPath = blobPath.TrimStart('/');
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(cleanPath);
        var fullUrl = blobClient.Uri.ToString();

        // Check if using Azurite (development storage emulator)
        if (IsUsingAzurite(fullUrl))
        {
            // For Azurite, generate relative URLs that work with ngrok proxy
            // Format: /devstoreaccount1/{containerName}/{blobPath}
            var relativeUrl = $"/devstoreaccount1/{containerName}/{cleanPath}";
            
            if (forExternalApi)
            {
                _logger.LogDebug("GetImageUrl for external API (Azurite relative): {Url}", relativeUrl);
            }
            else
            {
                _logger.LogDebug("GetImageUrl for frontend (Azurite relative): {Url}", relativeUrl);
            }
            
            return relativeUrl;
        }

        // For Azure Blob Storage (production), use full URLs
        if (forExternalApi)
        {
            _logger.LogDebug("GetImageUrl for external API (Azure Blob): {Url}", fullUrl);
        }
        else
        {
            _logger.LogDebug("GetImageUrl for frontend (Azure Blob): {Url}", fullUrl);
        }

        return fullUrl;
    }

    /// <summary>
    /// Determines if the current configuration is using Azurite (development storage emulator)
    /// </summary>
    private bool IsUsingAzurite(string blobUrl)
    {
        // Azurite URLs contain localhost or 127.0.0.1 and port 10000
        return blobUrl.Contains("127.0.0.1:10000") || blobUrl.Contains("localhost:10000");
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