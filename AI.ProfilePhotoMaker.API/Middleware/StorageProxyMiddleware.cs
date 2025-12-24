using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.WebUtilities;

namespace AI.ProfilePhotoMaker.API.Middleware;

/// <summary>
/// Middleware to proxy Azure Storage requests through ngrok tunnel
/// </summary>
public class StorageProxyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<StorageProxyMiddleware> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Uri _storageOrigin;

    private static string S(string? value) => LoggingSanitizer.Sanitize(value);

    public StorageProxyMiddleware(
        RequestDelegate next,
        ILogger<StorageProxyMiddleware> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _storageOrigin = ResolveStorageOrigin(configuration);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var originalPath = context.Request.Path.Value;
        var pathForCheck = originalPath?.ToLower();

        _logger.LogDebug("Storage proxy middleware processing path: {Path}", S(originalPath));

        // Check if this is a storage proxy request (case-insensitive check)
        if (pathForCheck?.StartsWith("/devstoreaccount1/") == true && originalPath != null)
        {
            _logger.LogInformation("Storage proxy middleware intercepting request: {Path}", S(originalPath));

            // Handle CORS preflight locally (Azurite doesn't speak CORS).
            if (HttpMethods.IsOptions(context.Request.Method))
            {
                await ApplyCorsHeadersAsync(context);
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }

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
            // Preserve query string (SAS tokens live here) when proxying to Azurite
            var query = GetForwardedQueryString(context.Request.QueryString);
            var relative = $"{path.TrimStart('/')}{query}";
            var azuriteUrl = new Uri(_storageOrigin, relative).ToString();

            _logger.LogDebug("Proxying storage request: {Path} -> {AzuriteUrl}", S(path), S(azuriteUrl));

            using var httpClient = _httpClientFactory.CreateClient();

            // Build a proxied request that mirrors the original method and headers
            using var proxiedRequest = new HttpRequestMessage(new HttpMethod(context.Request.Method), azuriteUrl);

            // Only forward a small safe subset of headers. Azurite can reject unknown/forwarded headers
            // that are common in reverse-proxy setups.
            CopyHeaderIfPresent(context.Request.Headers, proxiedRequest, "Range");
            CopyHeaderIfPresent(context.Request.Headers, proxiedRequest, "If-Modified-Since");
            CopyHeaderIfPresent(context.Request.Headers, proxiedRequest, "If-None-Match");
            CopyHeaderIfPresent(context.Request.Headers, proxiedRequest, "If-Match");
            CopyHeaderIfPresent(context.Request.Headers, proxiedRequest, "If-Unmodified-Since");
            CopyHeaderIfPresent(context.Request.Headers, proxiedRequest, "If-Range");

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

            // Add CORS headers for browser fetch() usage in docker-local and other cross-origin dev setups.
            await ApplyCorsHeadersAsync(context);

            // For HEAD, do not write a response body
            if (HttpMethods.IsHead(context.Request.Method))
            {
                _logger.LogDebug("Storage proxy HEAD request successful: {Path}, Status: {StatusCode}", S(path), context.Response.StatusCode);
                return;
            }

            // Stream body to the client for non-HEAD methods (avoid buffering large blobs in memory)
            await response.Content.CopyToAsync(context.Response.Body);

            _logger.LogDebug("Storage proxy request successful: {Path}, Status: {StatusCode}",
                S(path), context.Response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying storage request for path: {Path}", S(path));
            await ApplyCorsHeadersAsync(context);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Storage proxy error");
        }
    }

    private static async Task ApplyCorsHeadersAsync(HttpContext context)
    {
        if (!context.Request.Headers.ContainsKey("Origin"))
        {
            return;
        }

        var environment = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var policyName = environment.IsDevelopment() ? "AllowDevelopment" : "V1Production";

        var policyProvider = context.RequestServices.GetRequiredService<ICorsPolicyProvider>();
        var corsPolicy = await policyProvider.GetPolicyAsync(context, policyName);
        if (corsPolicy == null)
        {
            return;
        }

        var corsService = context.RequestServices.GetRequiredService<ICorsService>();
        var corsResult = corsService.EvaluatePolicy(context, corsPolicy);
        corsService.ApplyResult(corsResult, context.Response);
    }

    private static string GetForwardedQueryString(QueryString original)
    {
        if (!original.HasValue)
        {
            return string.Empty;
        }

        // ngrok's browser warning can be bypassed by adding `ngrok-skip-browser-warning=true` to the URL.
        // Azurite rejects unknown query parameters, so strip this before proxying.
        var parsed = QueryHelpers.ParseQuery(original.Value!);
        if (!parsed.ContainsKey("ngrok-skip-browser-warning"))
        {
            return original.Value!;
        }

        var items = new List<KeyValuePair<string, string?>>();
        foreach (var entry in parsed)
        {
            if (string.Equals(entry.Key, "ngrok-skip-browser-warning", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var value in entry.Value)
            {
                items.Add(new KeyValuePair<string, string?>(entry.Key, value));
            }
        }

        return QueryString.Create(items).Value ?? string.Empty;
    }

    private static Uri ResolveStorageOrigin(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AzureStorage")
                               ?? configuration["AzureStorage:ConnectionString"];

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            var blobEndpoint = TryReadConnectionStringValue(connectionString, "BlobEndpoint");
            if (!string.IsNullOrWhiteSpace(blobEndpoint) && Uri.TryCreate(blobEndpoint, UriKind.Absolute, out var blobUri))
            {
                var origin = blobUri.GetLeftPart(UriPartial.Authority);
                if (Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
                {
                    return originUri;
                }
            }
        }

        // Fallback for dev environments where Azurite runs on the same host process.
        return new Uri("http://127.0.0.1:10000");
    }

    private static string? TryReadConnectionStringValue(string connectionString, string key)
    {
        // Azure storage connection strings are ; separated key=value pairs.
        foreach (var segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = segment.IndexOf('=', StringComparison.Ordinal);
            if (idx <= 0) continue;

            var segmentKey = segment[..idx];
            if (!string.Equals(segmentKey, key, StringComparison.OrdinalIgnoreCase)) continue;

            return segment[(idx + 1)..];
        }

        return null;
    }

    private static void CopyHeaderIfPresent(IHeaderDictionary source, HttpRequestMessage destination, string headerName)
    {
        if (source.TryGetValue(headerName, out var values) && values.Count > 0)
        {
            destination.Headers.TryAddWithoutValidation(headerName, (IEnumerable<string>)values);
        }
    }
}
