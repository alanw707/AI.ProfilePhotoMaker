using Microsoft.AspNetCore.Mvc;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Net.Http;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>
/// Controller for handling style preview images
/// This is a compatibility layer for the frontend that expects these endpoints
/// It redirects to Azure Blob Storage URLs or serves placeholder images
/// </summary>
[Route("api/[controller]")]
[Route("api/style-preview")]
[ApiController]
public class StylePreviewController : ControllerBase
{
    private readonly IStorageService _storageService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StylePreviewController> _logger;
    private readonly ApplicationDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebHostEnvironment _environment;
    private static readonly ConcurrentDictionary<string, bool> CheckedPreviewUrls = new(StringComparer.OrdinalIgnoreCase);
    private static string S(string? value) => LoggingSanitizer.Sanitize(value);

    public StylePreviewController(
        IStorageService storageService,
        IConfiguration configuration,
        ApplicationDbContext context,
        IHttpClientFactory httpClientFactory,
        IWebHostEnvironment environment,
        ILogger<StylePreviewController> logger)
    {
        _storageService = storageService;
        _configuration = configuration;
        _context = context;
        _httpClientFactory = httpClientFactory;
        _environment = environment;
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
            var knownStyles = new[]
            {
                "corporate", "executive", "consultant", "linkedin", "medical",
                "academic", "entrepreneur", "startup", "tech-professional", "influencer",
                "digital-nomad", "creative", "casual", "artistic", "edgy-urban",
                "glamour", "fitness", "retro-wave", "night-out", "digital-native"
            };

            var styleNames = await _context.Styles
                .AsNoTracking()
                .Where(style => style.IsActive)
                .OrderBy(style => style.Name)
                .Select(style => style.Name)
                .ToListAsync();

            if (styleNames.Count == 0)
            {
                styleNames = knownStyles.ToList();
            }

            var previews = new List<object>();

            foreach (var style in styleNames)
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

                if (url.Contains("devstoreaccount1", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Style preview URL points to dev storage: {Url}", S(url));
                }

                if (_environment.IsDevelopment() && url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    _ = CheckPreviewUrlAsync(url, style);
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

    private async Task CheckPreviewUrlAsync(string url, string style)
    {
        if (!CheckedPreviewUrls.TryAdd(url, true))
        {
            return;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await client.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Style preview check failed ({StatusCode}) for {Style}: {Url}",
                    (int)response.StatusCode, S(style), S(url));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Style preview check error for {Style}: {Url}", S(style), S(url));
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
            var isDevStorage = azureStorageConnection.Contains("UseDevelopmentStorage=true", StringComparison.OrdinalIgnoreCase)
                               || azureStorageConnection.Contains("AccountName=devstoreaccount1", StringComparison.OrdinalIgnoreCase);

            if (!isDevStorage)
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
        }

        // Direct Azure Blob Storage URL
        return $"https://{storageAccountName}.blob.core.windows.net/style-previews/{fileName}";
    }
}
