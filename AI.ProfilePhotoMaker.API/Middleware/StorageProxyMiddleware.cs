namespace AI.ProfilePhotoMaker.API.Middleware;

/// <summary>
/// Middleware to proxy Azure Storage requests through ngrok tunnel
/// </summary>
public class StorageProxyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<StorageProxyMiddleware> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public StorageProxyMiddleware(RequestDelegate next, ILogger<StorageProxyMiddleware> logger, IHttpClientFactory httpClientFactory)
    {
        _next = next;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var originalPath = context.Request.Path.Value;
        var pathForCheck = originalPath?.ToLower();

        _logger.LogDebug("Storage proxy middleware processing path: {Path}", originalPath);

        // Check if this is a storage proxy request (case-insensitive check)
        if (pathForCheck?.StartsWith("/devstoreaccount1/") == true && originalPath != null)
        {
            _logger.LogInformation("Storage proxy middleware intercepting request: {Path}", originalPath);
            await ProxyStorageRequest(context, originalPath);
            return;
        }

        // Continue to next middleware
        await _next(context);
    }

    private async Task ProxyStorageRequest(HttpContext context, string path)
    {
        try
        {
            // Remove the leading slash and construct Azurite URL
            var azuriteUrl = $"http://127.0.0.1:10000{path}";

            _logger.LogDebug("Proxying storage request: {Path} -> {AzuriteUrl}", path, azuriteUrl);

            using var httpClient = _httpClientFactory.CreateClient();

            // Add ngrok header to skip browser warning page for Replicate API access
            httpClient.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");

            var response = await httpClient.GetAsync(azuriteUrl);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Storage proxy request failed: {StatusCode} for {Path}", response.StatusCode, path);
                context.Response.StatusCode = (int)response.StatusCode;
                return;
            }

            var content = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";

            context.Response.ContentType = contentType;
            context.Response.StatusCode = 200;
            await context.Response.Body.WriteAsync(content);

            _logger.LogDebug("Storage proxy request successful: {Path}, ContentType: {ContentType}, Size: {Size}",
                path, contentType, content.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying storage request for path: {Path}", path);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Storage proxy error");
        }
    }
}