using AI.ProfilePhotoMaker.API.Services.Storage;
using Azure.Extensions.AspNetCore.DataProtection.Blobs;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.DataProtection;

namespace AI.ProfilePhotoMaker.API.Extensions;

/// <summary>
/// Extension methods for storage and data protection service configuration
/// </summary>
public static class StorageServiceExtensions
{
    /// <summary>
    /// Add storage services and data protection to the service collection
    /// </summary>
    public static IServiceCollection AddStorageServices(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        // Register Storage Services - choose between Local or Azure Blob Storage
        var azureStorageConnectionString = configuration.GetConnectionString("AzureStorage") ?? configuration["AzureStorage:ConnectionString"];
        if (!string.IsNullOrEmpty(azureStorageConnectionString))
        {
            services.AddSingleton<BlobServiceClient>(_ => new BlobServiceClient(azureStorageConnectionString));
            services.AddScoped<IStorageService, AzureBlobStorageService>();
        }
        else
        {
            services.AddScoped<IStorageService, LocalStorageService>();
        }

        var dpBuilder = services.AddDataProtection()
            .SetApplicationName("AIProfilePhotoMaker");

        if (!environment.IsDevelopment()
            && !environment.IsEnvironment("Testing")
            && !environment.IsEnvironment("LocalDev")
            && !string.IsNullOrEmpty(azureStorageConnectionString))
        {
            // Persist Data Protection keys to Azure Blob so cookies survive pod/revision restarts
            var dpContainer = new BlobContainerClient(azureStorageConnectionString, "dataprotection");
            try
            {
                // Ensure the container exists so DataProtection can persist keys on first run
                dpContainer.CreateIfNotExists();
                Console.WriteLine("[DataProtection] Ensured blob container 'dataprotection' exists");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DataProtection] Warning: failed to ensure container exists: {ex.Message}");
            }

            var dpBlob = dpContainer.GetBlobClient("keys.xml");
            dpBuilder.PersistKeysToAzureBlobStorage(dpBlob);
        }
        else
        {
            var persistToAzureBlobInDev = configuration.GetValue("DataProtection:PersistKeysToAzureBlob", false);
            if (persistToAzureBlobInDev && !string.IsNullOrEmpty(azureStorageConnectionString))
            {
                var dpBlob = new BlobContainerClient(azureStorageConnectionString, "dataprotection")
                    .GetBlobClient("keys.xml");
                dpBuilder.PersistKeysToAzureBlobStorage(dpBlob);
            }
            else
            {
                dpBuilder.PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(environment.ContentRootPath, "keys")));
            }
        }

        services.AddScoped<StoragePathResolver>();

        return services;
    }
}
