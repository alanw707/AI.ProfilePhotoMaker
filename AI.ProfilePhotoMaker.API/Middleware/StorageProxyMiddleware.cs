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

            // Build a proxied request that mirrors the original method and headers
            using var proxiedRequest = new HttpRequestMessage(new HttpMethod(context.Request.Method), azuriteUrl);

            // Copy request headers (skip Host which is set by HttpClient)
            foreach (var header in context.Request.Headers)
            {
                if (string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase)) continue;
                proxiedRequest.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string>)header.Value);
            }
            // Add header used to bypass ngrok browser warning when applicable
            proxiedRequest.Headers.TryAddWithoutValidation("ngrok-skip-browser-warning", "true");

            // Forward body only for methods that typically have one
            if (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) || HttpMethods.IsPatch(context.Request.Method))
            {
                proxiedRequest.Content = new StreamContent(context.Request.Body);
                if (!string.IsNullOrEmpty(context.Request.ContentType))
                {
                    proxiedRequest.Content.Headers.TryAddWithoutValidation("Content-Type", context.Request.ContentType);
                }
            }

            using var response = await httpClient.SendAsync(proxiedRequest, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);

            // Propagate status code and headers
            context.Response.StatusCode = (int)response.StatusCode;
            foreach (var header in response.Headers)
            {
                context.Response.Headers[header.Key] = new Microsoft.Extensions.Primitives.StringValues(header.Value.ToArray());
            }
            foreach (var header in response.Content.Headers)
            {
                context.Response.Headers[header.Key] = new Microsoft.Extensions.Primitives.StringValues(header.Value.ToArray());
            }
            // Remove transfer-encoding header to avoid chunking issues
            context.Response.Headers.Remove("transfer-encoding");

            // For HEAD, do not write a response body
            if (HttpMethods.IsHead(context.Request.Method))
            {
                _logger.LogDebug("Storage proxy HEAD request successful: {Path}, Status: {StatusCode}", path, context.Response.StatusCode);
                return;
            }

            // Stream body to the client for non-HEAD methods
            var content = await response.Content.ReadAsByteArrayAsync();
            await context.Response.Body.WriteAsync(content);

            _logger.LogDebug("Storage proxy request successful: {Path}, Status: {StatusCode}, Size: {Size}",
                path, context.Response.StatusCode, content.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying storage request for path: {Path}", path);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Storage proxy error");
        }
    }
}
