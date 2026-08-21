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
                var url = GetPreviewUrl(storagePath);
                return Ok(new
                {
                    success = true,
                    styleName = styleName,
                    url = url,
                    fileName = fileName
                });
            }

            // Keep preview URLs behind the API so private blob containers work.
            return Ok(new
            {
                success = true,
                styleName = styleName,
                url = GetPreviewUrl(storagePath),
                fileName = fileName
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
                    url = GetPreviewUrl(storagePath);
                    size = fileInfo.Size;
                }
                else
                {
                    url = GetPreviewUrl(storagePath);
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

    private string GetPreviewUrl(string storagePath)
    {
        var apiBaseUrl = _configuration["ExternalApiBaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            apiBaseUrl = $"{Request.Scheme}://{Request.Host}";
        }

        return $"{apiBaseUrl}/profile-images/{storagePath.TrimStart('/')}";
    }
}
