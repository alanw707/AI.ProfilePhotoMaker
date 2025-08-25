using Microsoft.AspNetCore.Mvc;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>
/// Proxy controller to forward storage requests to Azure Storage Emulator
/// This allows external APIs like Replicate to access storage through the ngrok tunnel
/// </summary>
[ApiController]
public class StorageProxyController : ControllerBase
{
    private readonly ILogger<StorageProxyController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public StorageProxyController(ILogger<StorageProxyController> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Proxy GET requests to Azure Storage Emulator
    /// </summary>
    [HttpGet("devstoreaccount1/{**path}")]
    public async Task<IActionResult> ProxyStorageRequest(string path)
    {
        return await ProxyRequest(path, HttpMethod.Get);
    }

    /// <summary>
    /// Proxy HEAD requests to Azure Storage Emulator (for blob existence checks)
    /// </summary>
    [HttpHead("devstoreaccount1/{**path}")]
    public async Task<IActionResult> ProxyStorageHeadRequest(string path)
    {
        return await ProxyRequest(path, HttpMethod.Head);
    }

    private async Task<IActionResult> ProxyRequest(string path, HttpMethod method)
    {
        try
        {
            // Construct the full path for Azure Storage Emulator
            var azuriteUrl = $"http://127.0.0.1:10000/devstoreaccount1/{path}";

            _logger.LogDebug("Proxying storage {Method} request: {Path} -> {AzuriteUrl}", method.Method, path, azuriteUrl);

            using var httpClient = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(method, azuriteUrl);
            
            // Copy query parameters from original request
            if (Request.QueryString.HasValue)
            {
                var uriBuilder = new UriBuilder(azuriteUrl);
                uriBuilder.Query = Request.QueryString.Value.TrimStart('?');
                request.RequestUri = uriBuilder.Uri;
            }

            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Storage proxy {Method} request failed: {StatusCode} for {Path}", 
                    method.Method, response.StatusCode, path);
                return StatusCode((int)response.StatusCode);
            }

            // For HEAD requests, return headers only
            if (method == HttpMethod.Head)
            {
                var result = new EmptyResult();
                foreach (var header in response.Headers)
                {
                    Response.Headers[header.Key] = header.Value.ToArray();
                }
                foreach (var header in response.Content.Headers)
                {
                    Response.Headers[header.Key] = header.Value.ToArray();
                }
                Response.StatusCode = (int)response.StatusCode;
                return result;
            }

            // For GET requests, return content
            var content = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";

            _logger.LogDebug("Storage proxy {Method} request successful: {Path}, ContentType: {ContentType}, Size: {Size}",
                method.Method, path, contentType, content.Length);

            return File(content, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying storage {Method} request for path: {Path}", method.Method, path);
            return StatusCode(500, "Storage proxy error");
        }
    }
}