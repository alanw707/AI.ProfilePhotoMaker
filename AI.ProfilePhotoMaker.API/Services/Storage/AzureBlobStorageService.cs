using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace AI.ProfilePhotoMaker.API.Services.Storage;

/// <summary>
/// Azure Blob Storage implementation of storage service
/// </summary>
public class AzureBlobStorageService : BaseStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;

    public AzureBlobStorageService(
        BlobServiceClient blobServiceClient,
        IConfiguration configuration,
        ILogger<AzureBlobStorageService> logger)
        : base(configuration, logger)
    {
        _blobServiceClient = blobServiceClient;
        _containerName = configuration["AzureStorage:ContainerName"] ?? "profile-images";
    }

    public override async Task<string> SaveImageAsync(Stream imageStream, string fileName, string userId, string folderType = "generated")
    {
        ValidateUserId(userId);
        ValidateFileName(fileName);
        
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            var blobPath = GenerateUserStoragePath(userId, fileName, folderType);
            var blobClient = containerClient.GetBlobClient(blobPath);

            var contentType = GetContentType(Path.GetExtension(fileName));
            var uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType
                }
            };

            await blobClient.DeleteIfExistsAsync();
            await blobClient.UploadAsync(imageStream, uploadOptions);

            LogOperation(LogLevel.Information, "SaveImageAsync", blobPath);
            return blobPath;
        }
        catch (Exception ex)
        {
            LogError(ex, "SaveImageAsync", $"userId: {userId}, fileName: {fileName}");
            throw;
        }
    }

    public override async Task<string> SaveImageToPathAsync(Stream imageStream, string storagePath)
    {
        ValidateStoragePath(storagePath);
        
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

            var contentType = GetContentType(Path.GetExtension(blobPath));
            var uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType
                }
            };

            await blobClient.DeleteIfExistsAsync();
            await blobClient.UploadAsync(imageStream, uploadOptions);

            LogOperation(LogLevel.Information, "SaveImageToPathAsync", storagePath);
            return storagePath;
        }
        catch (Exception ex)
        {
            LogError(ex, "SaveImageToPathAsync", storagePath);
            throw;
        }
    }

    public override async Task<Stream?> GetImageAsync(string storagePath)
    {
        ValidateStoragePath(storagePath);
        
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
                LogOperation(LogLevel.Warning, "GetImageAsync - not found", storagePath);
                return null;
            }

            var response = await blobClient.DownloadStreamingAsync();
            return response.Value.Content;
        }
        catch (Exception ex)
        {
            LogError(ex, "GetImageAsync", storagePath);
            return null;
        }
    }

    public override async Task<bool> DeleteImageAsync(string storagePath)
    {
        ValidateStoragePath(storagePath);
        
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
                LogOperation(LogLevel.Information, "DeleteImageAsync - success", storagePath);
            }
            else
            {
                LogOperation(LogLevel.Warning, "DeleteImageAsync - not found", storagePath);
            }
            
            return response.Value;
        }
        catch (Exception ex)
        {
            LogError(ex, "DeleteImageAsync", storagePath);
            return false;
        }
    }

    public override async Task<bool> ExistsAsync(string storagePath)
    {
        ValidateStoragePath(storagePath);
        
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
            LogError(ex, "ExistsAsync", storagePath);
            return false;
        }
    }

    public override string GetImageUrl(string storagePath)
    {
        ValidateStoragePath(storagePath);
        
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

        // For Azure Blob Storage in production, always return direct Azure Blob URLs
        // These are publicly accessible URLs since containers have public blob access
        var cleanPath = blobPath.TrimStart('/');
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(cleanPath);
        var url = blobClient.Uri.ToString();

        Logger.LogDebug("GetImageUrl (Azure Blob): {Url}", url);
        return url;
    }

    public override async Task<List<string>> ListUserImagesAsync(string userId)
    {
        ValidateUserId(userId);
        
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var prefix = $"generated/{userId}/";
            var imageFiles = new List<string>();

            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix))
            {
                var extension = Path.GetExtension(blobItem.Name);
                if (IsImageExtension(extension))
                {
                    imageFiles.Add(blobItem.Name);
                }
            }

            return imageFiles;
        }
        catch (Exception ex)
        {
            LogError(ex, "ListUserImagesAsync", $"userId: {userId}");
            return new List<string>();
        }
    }

    public override async Task<StorageFileInfo?> GetFileInfoAsync(string storagePath)
    {
        ValidateStoragePath(storagePath);
        
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
            LogError(ex, "GetFileInfoAsync", storagePath);
            return null;
        }
    }

    public override Task<string> GenerateSasUrlAsync(string storagePath, TimeSpan expiry, BlobSasPermissions permissions = BlobSasPermissions.Read)
    {
        ValidateStoragePath(storagePath);
        
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
                Logger.LogError("Cannot generate SAS URL for blob: {StoragePath}. Storage account key required.", storagePath);
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
            
            Logger.LogDebug("Generated SAS URL for blob: {StoragePath}, expires: {ExpiresOn}", 
                storagePath, sasBuilder.ExpiresOn);
            
            return Task.FromResult(sasUri.ToString());
        }
        catch (Exception ex)
        {
            LogError(ex, "GenerateSasUrlAsync", storagePath);
            throw;
        }
    }

    public override async Task<string> SaveZipAsync(Stream zipStream, string storagePath)
    {
        ValidateStoragePath(storagePath);
        
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
                    ContentType = GetContentType(".zip")
                }
            };

            await blobClient.DeleteIfExistsAsync();
            await blobClient.UploadAsync(zipStream, blobUploadOptions);

            LogOperation(LogLevel.Information, "SaveZipAsync", storagePath);
            return storagePath;
        }
        catch (Exception ex)
        {
            LogError(ex, "SaveZipAsync", storagePath);
            throw;
        }
    }

    public override async Task<bool> DeleteDirectoryAsync(string directoryPath)
    {
        ValidateStoragePath(directoryPath);
        
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

            LogOperation(LogLevel.Information, "DeleteDirectoryAsync", directoryPath, new { DeletedCount = deletedCount });
            return deletedCount > 0;
        }
        catch (Exception ex)
        {
            LogError(ex, "DeleteDirectoryAsync", directoryPath);
            return false;
        }
    }

    public override async Task<List<string>> ListFilesAsync(string prefix)
    {
        ValidateStoragePath(prefix);
        
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

            Logger.LogDebug("Listed {Count} files with prefix: {Prefix}", files.Count, prefix);
            return files;
        }
        catch (Exception ex)
        {
            LogError(ex, "ListFilesAsync", prefix);
            return new List<string>();
        }
    }
}
