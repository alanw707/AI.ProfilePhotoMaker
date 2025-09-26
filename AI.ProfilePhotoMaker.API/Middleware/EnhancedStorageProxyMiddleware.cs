using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Services.Storage;

namespace AI.ProfilePhotoMaker.API.Middleware;

/// <summary>
/// Enhanced middleware to proxy storage requests for both Azurite (development) and Azure Blob Storage (production)
/// Handles both /devstoreaccount1/* (Azurite) and /profile-images/* (Azure Blob) requests
/// </summary>
public class EnhancedStorageProxyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<EnhancedStorageProxyMiddleware> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebHostEnvironment _environment;

    private static string S(string? value) => LoggingSanitizer.Sanitize(value);

    public EnhancedStorageProxyMiddleware(
        RequestDelegate next,
        ILogger<EnhancedStorageProxyMiddleware> logger,
        IHttpClientFactory httpClientFactory,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLower();

        _logger.LogDebug("Enhanced storage proxy processing path: {Path}", S(path));

        // Get storage service from request scope
        var storageService = context.RequestServices.GetRequiredService<IStorageService>();

        // Handle Azurite requests (development)
        if (path?.StartsWith("/devstoreaccount1/") == true)
        {
            _logger.LogInformation("Proxying Azurite request: {Path}", S(path));
            await ProxyAzuriteRequest(context, path);
            return;
        }

        // Handle Azure Blob Storage requests (all environments)
        if (path?.StartsWith("/profile-images/") == true)
        {
            _logger.LogInformation("Proxying Azure Blob Storage request: {Path}", S(path));
            await ProxyAzureBlobRequest(context, path, storageService);
            return;
        }

        // Continue to next middleware
        await _next(context);
    }

    /// <summary>
    /// Proxy requests to Azurite (development storage emulator)
    /// </summary>
    private async Task ProxyAzuriteRequest(HttpContext context, string path)
    {
        try
        {
            // Remove the leading slash and construct Azurite URL
            var azuriteUrl = $"http://127.0.0.1:10000{path}";

            _logger.LogDebug("Proxying to Azurite: {Path} -> {AzuriteUrl}", S(path), S(azuriteUrl));

            using var httpClient = _httpClientFactory.CreateClient();

            using var proxiedRequest = new HttpRequestMessage(new HttpMethod(context.Request.Method), azuriteUrl);
            foreach (var header in context.Request.Headers)
            {
                if (string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase)) continue;
                proxiedRequest.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string>)header.Value);
            }
            proxiedRequest.Headers.TryAddWithoutValidation("ngrok-skip-browser-warning", "true");

            // Forward body when appropriate
            if (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) || HttpMethods.IsPatch(context.Request.Method))
            {
                proxiedRequest.Content = new StreamContent(context.Request.Body);
                if (!string.IsNullOrEmpty(context.Request.ContentType))
                    proxiedRequest.Content.Headers.TryAddWithoutValidation("Content-Type", context.Request.ContentType);
            }

            using var response = await httpClient.SendAsync(proxiedRequest, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Azurite request failed: {StatusCode} for {Path}", response.StatusCode, S(path));
                context.Response.StatusCode = (int)response.StatusCode;
                return;
            }

            // Copy headers to response
            foreach (var header in response.Headers)
                context.Response.Headers[header.Key] = new Microsoft.Extensions.Primitives.StringValues(header.Value.ToArray());
            foreach (var header in response.Content.Headers)
                context.Response.Headers[header.Key] = new Microsoft.Extensions.Primitives.StringValues(header.Value.ToArray());
            context.Response.Headers.Remove("transfer-encoding");

            // For HEAD requests, do not write a body
            if (HttpMethods.IsHead(context.Request.Method))
            {
                context.Response.StatusCode = (int)response.StatusCode;
                _logger.LogDebug("Azurite proxy HEAD successful: {Path}, Status: {Status}", S(path), context.Response.StatusCode);
                return;
            }

            var content = await response.Content.ReadAsByteArrayAsync();
            context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            context.Response.StatusCode = (int)response.StatusCode;

            // Add cache headers for performance
            context.Response.Headers["Cache-Control"] = "public, max-age=3600";

            await context.Response.Body.WriteAsync(content);

            _logger.LogDebug("Azurite proxy successful: {Path}, ContentType: {ContentType}, Size: {Size}",
                S(path), S(context.Response.ContentType), content.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying Azurite request for path: {Path}", S(path));
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Azurite proxy error");
        }
    }

    /// <summary>
    /// Proxy requests to Azure Blob Storage via IStorageService
    /// </summary>
    private async Task ProxyAzureBlobRequest(HttpContext context, string path, IStorageService storageService)
    {
        try
        {
            // Extract storage path from URL
            // URL: /profile-images/prod/uploads/userId/fileName.png
            // Storage Path: prod/uploads/userId/fileName.png
            var storagePath = path.Substring("/profile-images/".Length);

            _logger.LogDebug("Serving image from storage: {Path} -> {StoragePath}", S(path), S(storagePath));

            // For HEAD requests, just verify existence and return headers without a body
            if (HttpMethods.IsHead(context.Request.Method))
            {
                var exists = await storageService.ExistsAsync(storagePath);
                context.Response.StatusCode = exists ? 200 : 404;
                if (exists)
                {
                    // Minimal headers for caching
                    context.Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
                    context.Response.Headers["ETag"] = $"\"{storagePath.GetHashCode():X}\"";
                }
                _logger.LogDebug("HEAD check for {StoragePath}: {Status}", S(storagePath), context.Response.StatusCode);
                return;
            }

            // Fetch image from storage service (Azure Blob or Azurite)
            var imageStream = await storageService.GetImageAsync(storagePath);

            if (imageStream == null)
            {
                _logger.LogWarning("Image not found in storage: {StoragePath}", S(storagePath));
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("Image not found");
                return;
            }

            // Set appropriate content type based on file extension
            var contentType = GetContentType(path);
            context.Response.ContentType = contentType;

            // Add cache headers for performance (1 year for images)
            context.Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
            context.Response.Headers["ETag"] = $"\"{storagePath.GetHashCode():X}\"";

            // Stream the image to the response
            await imageStream.CopyToAsync(context.Response.Body);
            await imageStream.DisposeAsync();

            _logger.LogDebug("Successfully served image: {Path}, ContentType: {ContentType}", S(path), S(contentType));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving image from storage: {Path}", S(path));
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Storage proxy error");
        }
    }

    /// <summary>
    /// Determine content type based on file extension
    /// </summary>
    private string GetContentType(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".bmp" => "image/bmp",
            ".ico" => "image/x-icon",
            _ => "application/octet-stream"
        };
    }
}
