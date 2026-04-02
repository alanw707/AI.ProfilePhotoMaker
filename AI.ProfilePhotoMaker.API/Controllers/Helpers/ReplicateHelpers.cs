using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AI.ProfilePhotoMaker.API.Controllers.Helpers;

/// <summary>
/// Shared helper methods extracted from ReplicateController for reuse
/// by the split controllers (TrainingController, GenerationController, etc.).
/// </summary>
public static class ReplicateHelpers
{
    /// <summary>
    /// Converts a URL that may reference localhost, Azurite, or a stale ngrok tunnel
    /// into the externally-reachable equivalent so that Replicate callbacks can reach it.
    /// </summary>
    public static string ConvertToExternalApiUrl(string originalUrl, IConfiguration configuration, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(originalUrl))
        {
            return originalUrl;
        }

        if (Uri.TryCreate(originalUrl, UriKind.Absolute, out var absoluteUri))
        {
            var proxyBase = configuration["ExternalApiBaseUrl"]
                            ?? configuration["Webhooks:NgrokTunnelUrl"]
                            ?? configuration["AppBaseUrl"];

            if (string.IsNullOrWhiteSpace(proxyBase) || !Uri.TryCreate(proxyBase, UriKind.Absolute, out var proxyBaseUri))
            {
                return originalUrl;
            }

            var host = absoluteUri.Host.ToLowerInvariant();
            var isLocalHost = host is "localhost" or "127.0.0.1";
            var isAzuriteHost = host.Contains("azurite", StringComparison.OrdinalIgnoreCase);
            var isNgrokHost = host.EndsWith("ngrok-free.app", StringComparison.OrdinalIgnoreCase);

            var shouldRewrite =
                isLocalHost
                || isAzuriteHost
                || (isNgrokHost && !string.Equals(proxyBaseUri.Host, absoluteUri.Host, StringComparison.OrdinalIgnoreCase));

            if (!shouldRewrite)
            {
                return originalUrl;
            }

            return $"{proxyBaseUri.GetLeftPart(UriPartial.Authority).TrimEnd('/')}{absoluteUri.PathAndQuery}";
        }

        if (originalUrl.StartsWith("/"))
        {
            var externalBaseUrl = configuration["ExternalApiBaseUrl"];
            if (!string.IsNullOrEmpty(externalBaseUrl))
            {
                return $"{externalBaseUrl.TrimEnd('/')}{originalUrl}";
            }

            var appBaseUrl = configuration["AppBaseUrl"];
            if (!string.IsNullOrEmpty(appBaseUrl) && appBaseUrl.StartsWith("https://"))
            {
                return $"{appBaseUrl.TrimEnd('/')}{originalUrl}";
            }

            logger.LogWarning("No ExternalApiBaseUrl configured and AppBaseUrl is not HTTPS - external APIs may not be able to access: {Url}", LoggingSanitizer.Sanitize(originalUrl));
        }

        return originalUrl;
    }

    /// <summary>
    /// Normalizes an image URL so that Replicate can fetch it. Handles Azurite
    /// proxy rewriting and generates SAS tokens for Azure Blob Storage URLs.
    /// </summary>
    public static async Task<string> NormalizeImageUrlForReplicateAsync(string originalUrl, IConfiguration configuration, IStorageService storageService, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(originalUrl)) return originalUrl;
        if (!Uri.TryCreate(originalUrl, UriKind.Absolute, out var uri)) return originalUrl;

        var host = uri.Host.ToLowerInvariant();
        var isAzurite = host.Contains("127.0.0.1") || host.Contains("localhost") || host.Contains("devstoreaccount1");
        var isAzureBlob = host.Contains(".blob.core.windows.net");

        if (isAzurite)
        {
            var proxyBase = configuration["ExternalApiBaseUrl"] ?? configuration["AppBaseUrl"];
            if (!string.IsNullOrEmpty(proxyBase))
            {
                var proxied = $"{proxyBase.TrimEnd('/')}{uri.PathAndQuery}";
                logger.LogDebug("Using proxied Azurite URL for Replicate: {Url}", LoggingSanitizer.Sanitize(proxied));
                return proxied;
            }
        }

        if (isAzureBlob && string.IsNullOrEmpty(uri.Query))
        {
            var segments = uri.AbsolutePath.Trim('/').Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2)
            {
                var container = segments[0];
                var blobPath = segments[1];
                try
                {
                    var sasUrl = await storageService.GenerateSasUrlAsync($"{container}/{blobPath}", TimeSpan.FromMinutes(10));
                    if (!string.IsNullOrEmpty(sasUrl))
                    {
                        logger.LogDebug("Generated SAS URL for Replicate enhancement (container={Container}, path={Path})", LoggingSanitizer.Sanitize(container), LoggingSanitizer.Sanitize(blobPath));
                        return sasUrl;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to generate SAS URL for Replicate enhancement (container={Container}, path={Path})", LoggingSanitizer.Sanitize(container), LoggingSanitizer.Sanitize(blobPath));
                }
            }
        }

        return originalUrl;
    }

    /// <summary>
    /// Validates that the user has a trained model in the Ready state and that
    /// the model record contains both a ReplicateModelId and TrainedModelVersion.
    /// </summary>
    public static async Task<(bool Success, ModelCreationRequest? Model, string? ErrorCode, string? ErrorMessage)>
        ValidateTrainedModelAsync(string userId, ApplicationDbContext dbContext, ILogger logger)
    {
        var trainedModel = await dbContext.ModelCreationRequests
            .Where(m => m.UserId == userId && m.Status == ModelCreationStatus.Ready)
            .OrderByDescending(m => m.CompletedAt)
            .FirstOrDefaultAsync();

        if (trainedModel == null)
        {
            return (false, null, "NoTrainedModel", "No trained model found. Please train a model before generating styled images.");
        }

        if (string.IsNullOrEmpty(trainedModel.ReplicateModelId))
        {
            logger.LogError("User {UserId} has trained model without ReplicateModelId", LoggingSanitizer.SanitizeId(userId));
            return (false, null, "IncompleteModel", "Your trained model data is incomplete. Please contact support or retrain your model.");
        }

        if (string.IsNullOrEmpty(trainedModel.TrainedModelVersion))
        {
            logger.LogError("User {UserId} has trained model without TrainedModelVersion", LoggingSanitizer.SanitizeId(userId));
            return (false, null, "IncompleteModel", "Your trained model data is incomplete. Please contact support or retrain your model.");
        }

        return (true, trainedModel, null, null);
    }

    /// <summary>
    /// Formats the model version string into the owner/model:version format
    /// expected by the Replicate API.
    /// </summary>
    public static string FormatModelVersion(string replicateModelId, string trainedModelVersion, IConfiguration configuration)
    {
        if (!string.IsNullOrEmpty(trainedModelVersion) && trainedModelVersion.Contains(":"))
        {
            var beforeColon = trainedModelVersion.Split(':')[0];
            if (beforeColon.Contains('/'))
            {
                return trainedModelVersion;
            }
        }

        if (string.IsNullOrEmpty(replicateModelId))
        {
            return trainedModelVersion;
        }

        string owner = configuration["Replicate:Owner"] ?? Environment.GetEnvironmentVariable("REPLICATE_OWNER") ?? "alanw707";
        string modelName = replicateModelId;
        if (replicateModelId.Contains('/'))
        {
            var parts = replicateModelId.Split('/', 2);
            var incomingOwner = parts[0];
            modelName = parts[1];
            if (!string.IsNullOrWhiteSpace(incomingOwner) && !string.Equals(incomingOwner, "mock", StringComparison.OrdinalIgnoreCase))
            {
                owner = incomingOwner;
            }
        }
        var ownerAndModel = $"{owner}/{modelName}";

        return $"{ownerAndModel}:{trainedModelVersion}";
    }
}
