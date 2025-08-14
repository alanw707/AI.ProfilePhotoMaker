using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

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

    public async Task<string> SaveImageToPathAsync(Stream imageStream, string storagePath)
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
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            var blobClient = containerClient.GetBlobClient(blobPath.TrimStart('/'));

            await blobClient.UploadAsync(imageStream, overwrite: true);

            _logger.LogInformation("Saved image to Azure Blob Storage: {BlobPath}", blobPath);
            return storagePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save image to storage path {StoragePath}", storagePath);
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

        if (forExternalApi)
        {
            // For external APIs (like Replicate), use ExternalApiBaseUrl to route through ngrok tunnel
            var externalApiBaseUrl = _configuration["ExternalApiBaseUrl"];
            if (!string.IsNullOrEmpty(externalApiBaseUrl))
            {
                var extContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                var extBlobClient = extContainerClient.GetBlobClient(blobPath.TrimStart('/'));
                var azureStoragePath = extBlobClient.Uri.AbsolutePath; // Gets the path part: /devstoreaccount1/container/blob
                var externalUrl = $"{externalApiBaseUrl.TrimEnd('/')}{azureStoragePath}";
                
                _logger.LogDebug("GetImageUrl for external API (Azure Blob via ngrok): {Url}", externalUrl);
                return externalUrl;
            }
            
            // Fallback warning if no ExternalApiBaseUrl configured
            _logger.LogWarning("No ExternalApiBaseUrl configured - external APIs may not be able to access Azure blob URLs");
        }

        // For frontend/internal use or fallback, return direct Azure Blob Storage URL
        var cleanPath = blobPath.TrimStart('/');
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(cleanPath);
        var url = blobClient.Uri.ToString();

        _logger.LogDebug("GetImageUrl for frontend (Azure Blob): {Url}", url);
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

    public async Task<string> GenerateSasUrlAsync(string storagePath, TimeSpan expiry, BlobSasPermissions permissions = BlobSasPermissions.Read)
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

            // Check if the blob client can generate SAS
            if (!blobClient.CanGenerateSasUri)
            {
                _logger.LogError("Cannot generate SAS URL for blob: {StoragePath}. Storage account key required.", storagePath);
                throw new InvalidOperationException("Cannot generate SAS URL. Storage account key required.");
            }

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerName,
                BlobName = blobPath.TrimStart('/'),
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.Add(expiry)
            };

            sasBuilder.SetPermissions(permissions);

            var sasUri = blobClient.GenerateSasUri(sasBuilder);
            
            _logger.LogDebug("Generated SAS URL for blob: {StoragePath}, expires: {ExpiresOn}", 
                storagePath, sasBuilder.ExpiresOn);
            
            return sasUri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate SAS URL for blob: {StoragePath}", storagePath);
            throw;
        }
    }

    public async Task<string> SaveZipAsync(Stream zipStream, string storagePath)
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
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            var blobClient = containerClient.GetBlobClient(blobPath.TrimStart('/'));

            // Set content type for ZIP files
            var blobUploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/zip"
                }
            };

            await blobClient.UploadAsync(zipStream, blobUploadOptions);

            _logger.LogInformation("Saved ZIP file to Azure Blob Storage: {BlobPath}", blobPath);
            return storagePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save ZIP file {StoragePath} to Azure Blob Storage", storagePath);
            throw;
        }
    }

    public async Task<bool> DeleteDirectoryAsync(string directoryPath)
    {
        try
        {
            string containerName;
            string prefix;
            
            if (directoryPath.StartsWith("style-previews/"))
            {
                containerName = "style-previews";
                prefix = directoryPath.Substring("style-previews/".Length);
            }
            else
            {
                containerName = _containerName;
                prefix = directoryPath;
            }

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var cleanPrefix = prefix.TrimStart('/').TrimEnd('/') + '/';
            
            var deletedCount = 0;
            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: cleanPrefix))
            {
                var blobClient = containerClient.GetBlobClient(blobItem.Name);
                await blobClient.DeleteIfExistsAsync();
                deletedCount++;
            }

            _logger.LogInformation("Deleted {Count} blobs from directory: {DirectoryPath}", deletedCount, directoryPath);
            return deletedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete directory from Azure Storage: {DirectoryPath}", directoryPath);
            return false;
        }
    }

    public async Task<List<string>> ListFilesAsync(string prefix)
    {
        try
        {
            string containerName;
            string searchPrefix;
            
            if (prefix.StartsWith("style-previews/"))
            {
                containerName = "style-previews";
                searchPrefix = prefix.Substring("style-previews/".Length);
            }
            else
            {
                containerName = _containerName;
                searchPrefix = prefix;
            }

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var cleanPrefix = searchPrefix.TrimStart('/');
            var files = new List<string>();

            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: cleanPrefix))
            {
                // Return full storage path (including container prefix if applicable)
                var fullPath = prefix.StartsWith("style-previews/") 
                    ? $"style-previews/{blobItem.Name}"
                    : blobItem.Name;
                files.Add(fullPath);
            }

            _logger.LogDebug("Listed {Count} files with prefix: {Prefix}", files.Count, prefix);
            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list files with prefix: {Prefix}", prefix);
            return new List<string>();
        }
    }
}