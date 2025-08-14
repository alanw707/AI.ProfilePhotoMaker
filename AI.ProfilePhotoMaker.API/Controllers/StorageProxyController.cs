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
    /// Proxy all requests to Azure Storage Emulator
    /// </summary>
    [HttpGet("devstoreaccount1/{**path}")]
    public async Task<IActionResult> ProxyStorageRequest(string path)
    {
        try
        {
            // Construct the full path for Azure Storage Emulator
            var azuriteUrl = $"http://127.0.0.1:10000/devstoreaccount1/{path}";
            
            _logger.LogDebug("Proxying storage request: {Path} -> {AzuriteUrl}", path, azuriteUrl);
            
            using var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync(azuriteUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Storage proxy request failed: {StatusCode} for {Path}", response.StatusCode, path);
                return StatusCode((int)response.StatusCode);
            }

            var content = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            
            _logger.LogDebug("Storage proxy request successful: {Path}, ContentType: {ContentType}, Size: {Size}", 
                path, contentType, content.Length);
            
            return File(content, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying storage request for path: {Path}", path);
            return StatusCode(500, "Storage proxy error");
        }
    }
}