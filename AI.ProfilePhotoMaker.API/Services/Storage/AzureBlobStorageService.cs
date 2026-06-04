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

    internal static async Task EnsureContainerExistsAsync(BlobContainerClient containerClient, CancellationToken cancellationToken = default)
    {
        // Production storage accounts disable public blob access. Asking Azure to create a
        // public container fails with PublicAccessNotPermitted before upload can start.
        await containerClient.CreateIfNotExistsAsync(
            publicAccessType: PublicAccessType.None,
            cancellationToken: cancellationToken);
    }

    public override async Task<string> SaveImageAsync(Stream imageStream, string fileName, string userId, string folderType = "generated")
    {
        ValidateUserId(userId);
        ValidateFileName(fileName);

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await EnsureContainerExistsAsync(containerClient);

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
            await EnsureContainerExistsAsync(containerClient);

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

            // Normalize if a full URL was provided (e.g., Azurite or Azure Blob URL)
            if (Uri.TryCreate(storagePath, UriKind.Absolute, out var uri))
            {
                var path = uri.AbsolutePath.TrimStart('/');
                // Azurite format: /devstoreaccount1/{container}/{blob...}
                // Azure format: /{container}/{blob...}
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 2)
                {
                    if (string.Equals(segments[0], "devstoreaccount1", StringComparison.OrdinalIgnoreCase))
                    {
                        containerName = segments[1];
                        blobPath = string.Join('/', segments.Skip(2));
                    }
                    else
                    {
                        containerName = segments[0];
                        blobPath = string.Join('/', segments.Skip(1));
                    }
                }
                else
                {
                    // Fallback to configured container and raw path
                    containerName = _containerName;
                    blobPath = path;
                }
            }
            else
            {
                // Relative storage path
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

        // Optional proxy: when enabled, serve images via API to support private containers and caching
        var proxyEnabled = bool.TryParse(Configuration["Storage:ProxyBlobRequests"], out var flag) && flag;
        if (proxyEnabled)
        {
            var apiBaseUrl = Configuration["ExternalApiBaseUrl"]?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(apiBaseUrl))
            {
                apiBaseUrl = Configuration["AppBaseUrl"]?.TrimEnd('/') ?? "https://localhost:5001";
            }

            var proxyPath = "/profile-images/" + storagePath.TrimStart('/');
            var proxiedUrl = apiBaseUrl + proxyPath;
            Logger.LogDebug("GetImageUrl (Proxy Enabled): {Url}", proxiedUrl);
            return proxiedUrl;
        }

        // Default: return direct Azure Blob URL (public blob access)
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
        var url = blobClient.Uri.ToString();

        if (IsAzuriteEndpoint(_blobServiceClient.Uri))
        {
            var appBaseUrl = Configuration["AppBaseUrl"]?.TrimEnd('/') ?? "http://localhost:5032";
            var proxiedUrl = $"{appBaseUrl}/profile-images/{storagePath.TrimStart('/')}";
            Logger.LogDebug("GetImageUrl (Azurite Proxy): {Url}", proxiedUrl);
            return proxiedUrl;
        }

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
            var (containerName, blobPath) = ResolveContainerAndBlob(storagePath);

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobPath.TrimStart('/'));

            // For Azurite/local development, returning a plain URL avoids SAS version incompatibilities
            // (Azurite may reject newer `sv` values). Browser delivery still goes through the API proxy.
            if (IsAzuriteEndpoint(_blobServiceClient.Uri))
            {
                return Task.FromResult(blobClient.Uri.ToString());
            }

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

    /// <summary>
    /// Resolve container and blob path from a storage path that may be absolute (Azurite/Azure) or relative.
    /// Exposed for unit testing to ensure we generate SAS URLs against the correct container.
    /// </summary>
    internal (string container, string blobPath) ResolveContainerAndBlob(string storagePath)
    {
        // Default: configured container and provided path
        string containerName = _containerName;
        string blobPath = storagePath.TrimStart('/');

        // Absolute Azure/Azurite URL handling
        if (Uri.TryCreate(storagePath, UriKind.Absolute, out var uri))
        {
            var path = uri.AbsolutePath.TrimStart('/');
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2)
            {
                if (string.Equals(segments[0], "devstoreaccount1", StringComparison.OrdinalIgnoreCase))
                {
                    containerName = segments[1];
                    blobPath = string.Join('/', segments.Skip(2));
                }
                else
                {
                    containerName = segments[0];
                    blobPath = string.Join('/', segments.Skip(1));
                }
            }
            else
            {
                blobPath = path;
            }

            return (containerName, blobPath);
        }

        // Explicit container prefixes
        if (storagePath.StartsWith("style-previews/", StringComparison.OrdinalIgnoreCase))
        {
            return ("style-previews", storagePath.Substring("style-previews/".Length));
        }

        if (storagePath.StartsWith("profile-images/", StringComparison.OrdinalIgnoreCase))
        {
            return ("profile-images", storagePath.Substring("profile-images/".Length));
        }

        // Fallback
        return (containerName, blobPath);
    }

    private static bool IsAzuriteEndpoint(Uri serviceUri)
    {
        if (serviceUri.Port == 10000)
        {
            return true;
        }

        var host = serviceUri.Host ?? string.Empty;
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
               || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
               || host.Contains("azurite", StringComparison.OrdinalIgnoreCase);
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
            await EnsureContainerExistsAsync(containerClient);

            var blobClient = containerClient.GetBlobClient(blobPath.TrimStart('/'));

            // Upload with overwrite to avoid a delete+upload window where external fetchers can see a 404.
            if (zipStream.CanSeek)
            {
                zipStream.Position = 0;
            }

            await blobClient.UploadAsync(zipStream, overwrite: true);
            await blobClient.SetHttpHeadersAsync(new BlobHttpHeaders
            {
                ContentType = GetContentType(".zip")
            });

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
