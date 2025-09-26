using Microsoft.AspNetCore.Mvc;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Services.Storage;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>
/// Controller for handling style preview images
/// This is a compatibility layer for the frontend that expects these endpoints
/// It redirects to Azure Blob Storage URLs or serves placeholder images
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class StylePreviewController : ControllerBase
{
    private readonly IStorageService _storageService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StylePreviewController> _logger;
    private static string S(string? value) => LoggingSanitizer.Sanitize(value);

    public StylePreviewController(
        IStorageService storageService,
        IConfiguration configuration,
        ILogger<StylePreviewController> logger)
    {
        _storageService = storageService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Get the URL for a specific style preview image
    /// </summary>
    [HttpGet("url/{styleName}")]
    public async Task<IActionResult> GetStylePreviewUrl(string styleName)
    {
        try
        {
            // Convert style name to filename format
            var fileName = $"{styleName.ToLower().Replace(" ", "-").Replace("/", "-")}.jpg";

            // Build the storage path for style previews
            var storagePath = $"style-previews/{fileName}";

            // Check if the file exists in storage
            bool exists = await _storageService.ExistsAsync(storagePath);

            if (exists)
            {
                // Get the public URL from storage service
                var url = _storageService.GetImageUrl(storagePath);
                return Ok(new
                {
                    success = true,
                    styleName = styleName,
                    url = url,
                    fileName = fileName
                });
            }

            // Fallback to direct Azure Blob Storage URL if file doesn't exist in storage
            var azureBlobUrl = GetDirectAzureBlobUrl(styleName);
            if (!string.IsNullOrEmpty(azureBlobUrl))
            {
                return Ok(new
                {
                    success = true,
                    styleName = styleName,
                    url = azureBlobUrl,
                    fileName = fileName
                });
            }

            // Return placeholder URL as last resort
            return Ok(new
            {
                success = true,
                styleName = styleName,
                url = "/api/placeholder/style-preview",
                fileName = "placeholder.jpg"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting style preview URL for {StyleName}", S(styleName));
            return StatusCode(500, new
            {
                success = false,
                error = new { code = "InternalError", message = "Failed to get style preview URL" }
            });
        }
    }

    /// <summary>
    /// List all available style preview images
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> ListStylePreviews()
    {
        try
        {
            // Define the known styles (should match what's in the database)
            var knownStyles = new[]
            {
                "corporate", "executive", "consultant", "linkedin", "legal",
                "medical", "author", "entrepreneur", "startup", "tech-professional",
                "influencer", "digital-nomad", "creative", "casual", "artistic",
                "edgy-urban", "glamour", "academic", "fitness", "spiritual"
            };

            var previews = new List<object>();

            foreach (var style in knownStyles)
            {
                var fileName = $"{style}.jpg";
                var storagePath = $"style-previews/{fileName}";

                // Check if file exists and get its info
                var fileInfo = await _storageService.GetFileInfoAsync(storagePath);

                string url;
                long size = 0;

                if (fileInfo != null)
                {
                    // File exists in storage, get its URL
                    url = _storageService.GetImageUrl(storagePath);
                    size = fileInfo.Size;
                }
                else
                {
                    // Use direct Azure Blob URL as fallback
                    url = GetDirectAzureBlobUrl(style) ?? "/api/placeholder/style-preview";
                }

                previews.Add(new
                {
                    style = style,
                    fileName = fileName,
                    path = $"/style-previews/{fileName}",
                    url = url,
                    size = size
                });
            }

            return Ok(new
            {
                success = true,
                count = previews.Count,
                previews = previews
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing style previews");
            return StatusCode(500, new
            {
                success = false,
                error = new { code = "InternalError", message = "Failed to list style previews" }
            });
        }
    }

    /// <summary>
    /// Generate direct Azure Blob Storage URL for a style
    /// </summary>
    private string? GetDirectAzureBlobUrl(string styleName)
    {
        if (string.IsNullOrEmpty(styleName))
            return null;

        // Convert style name to filename format
        var fileName = $"{styleName.ToLower().Replace(" ", "-").Replace("/", "-")}.jpg";

        // Use the correct Azure storage account from configuration
        var azureStorageConnection = _configuration.GetConnectionString("AzureStorage") ??
                                    _configuration["AzureStorage:ConnectionString"];

        // Extract storage account name from connection string if available
        string storageAccountName = "aipmstv16j74jubocuukg"; // Default known account

        if (!string.IsNullOrEmpty(azureStorageConnection))
        {
            // Parse storage account name from connection string
            var accountNameMatch = System.Text.RegularExpressions.Regex.Match(
                azureStorageConnection,
                @"AccountName=([^;]+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (accountNameMatch.Success)
            {
                storageAccountName = accountNameMatch.Groups[1].Value;
            }
        }

        // Direct Azure Blob Storage URL
        return $"https://{storageAccountName}.blob.core.windows.net/style-previews/{fileName}";
    }
}
