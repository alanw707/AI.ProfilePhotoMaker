using System.Net;

namespace AI.ProfilePhotoMaker.API.Services;

/// <summary>
/// Resolves webhook URLs for different environments with ngrok support for development
/// </summary>
public class WebhookUrlResolver : IWebhookUrlResolver
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<WebhookUrlResolver> _logger;
    private readonly HttpClient _httpClient;

    // Cache resolved URL to avoid repeated lookups
    private string? _cachedWebhookBaseUrl;
    private DateTime? _lastResolutionTime;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

    public WebhookUrlResolver(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<WebhookUrlResolver> logger,
        HttpClient httpClient)
    {
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<string?> GetWebhookBaseUrlAsync()
    {
        // Use cached URL if still valid
        if (_cachedWebhookBaseUrl != null &&
            _lastResolutionTime.HasValue &&
            DateTime.UtcNow - _lastResolutionTime.Value < _cacheExpiration)
        {
            return _cachedWebhookBaseUrl;
        }

        try
        {
            string? resolvedUrl = null;

            if (_environment.IsDevelopment())
            {
                resolvedUrl = await ResolveDevelopmentWebhookUrlAsync();
            }
            else
            {
                resolvedUrl = ResolveProductionWebhookUrl();
            }

            // Validate that the URL is HTTPS
            if (resolvedUrl != null && !resolvedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Webhook URL is not HTTPS: {WebhookUrl}. Webhooks will be disabled.", resolvedUrl);
                return null;
            }

            // Cache the result
            _cachedWebhookBaseUrl = resolvedUrl;
            _lastResolutionTime = DateTime.UtcNow;

            _logger.LogInformation("Resolved webhook base URL: {WebhookUrl} (Environment: {Environment})",
                resolvedUrl ?? "DISABLED", _environment.EnvironmentName);

            return resolvedUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve webhook base URL. Webhooks will be disabled.");
            return null;
        }
    }

    public async Task<string?> GetWebhookUrlAsync(string endpointPath)
    {
        var baseUrl = await GetWebhookBaseUrlAsync();
        if (baseUrl == null)
        {
            return null;
        }

        // Ensure endpointPath starts with /
        if (!endpointPath.StartsWith("/"))
        {
            endpointPath = "/" + endpointPath;
        }

        return baseUrl.TrimEnd('/') + endpointPath;
    }

    public async Task<bool> ValidateWebhookUrlAsync()
    {
        var webhookUrl = await GetWebhookUrlAsync("/health"); // Use health endpoint for validation
        if (webhookUrl == null)
        {
            return false;
        }

        try
        {
            using var response = await _httpClient.GetAsync(webhookUrl);
            var isValid = response.StatusCode != HttpStatusCode.NotFound; // Accept any response except 404

            _logger.LogInformation("Webhook URL validation: {WebhookUrl} -> {StatusCode} ({IsValid})",
                webhookUrl, response.StatusCode, isValid ? "VALID" : "INVALID");

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Webhook URL validation failed for: {WebhookUrl}", webhookUrl);
            return false;
        }
    }

    private async Task<string?> ResolveDevelopmentWebhookUrlAsync()
    {
        // 1. Check for explicit ngrok tunnel URL in configuration
        var explicitNgrokUrl = _configuration["Webhooks:NgrokTunnelUrl"];
        if (!string.IsNullOrWhiteSpace(explicitNgrokUrl))
        {
            _logger.LogDebug("Using explicit ngrok tunnel URL from configuration: {NgrokUrl}", explicitNgrokUrl);
            return explicitNgrokUrl.TrimEnd('/');
        }

        // 2. Check for webhook base URL override
        var webhookBaseUrl = _configuration["Webhooks:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(webhookBaseUrl))
        {
            _logger.LogDebug("Using webhook base URL override: {WebhookBaseUrl}", webhookBaseUrl);
            return webhookBaseUrl.TrimEnd('/');
        }

        // 3. Try to detect ngrok tunnel via ngrok API
        var ngrokUrl = await DetectNgrokTunnelAsync();
        if (ngrokUrl != null)
        {
            _logger.LogDebug("Detected ngrok tunnel via API: {NgrokUrl}", ngrokUrl);
            return ngrokUrl;
        }

        // 4. Fall back to localhost HTTPS if configured
        var appBaseUrl = _configuration["AppBaseUrl"];
        if (!string.IsNullOrWhiteSpace(appBaseUrl) && appBaseUrl.StartsWith("https://"))
        {
            _logger.LogDebug("Using HTTPS app base URL for webhooks: {AppBaseUrl}", appBaseUrl);
            return appBaseUrl.TrimEnd('/');
        }

        // 5. No suitable webhook URL found for development
        _logger.LogWarning("No suitable webhook URL found for development environment. " +
                          "Consider setting up ngrok or configuring Webhooks:NgrokTunnelUrl in appsettings.Development.json");
        return null;
    }

    private string? ResolveProductionWebhookUrl()
    {
        // In production, always use the app base URL
        var appBaseUrl = _configuration["AppBaseUrl"];
        if (string.IsNullOrWhiteSpace(appBaseUrl))
        {
            _logger.LogError("AppBaseUrl not configured for production environment");
            return null;
        }

        return appBaseUrl.TrimEnd('/');
    }

    private async Task<string?> DetectNgrokTunnelAsync()
    {
        try
        {
            // ngrok API endpoint (default local)
            var ngrokApiUrl = _configuration["Webhooks:NgrokApiUrl"] ?? "http://localhost:4040/api/tunnels";

            _logger.LogDebug("Attempting to detect ngrok tunnel via API: {NgrokApiUrl}", ngrokApiUrl);

            using var response = await _httpClient.GetAsync(ngrokApiUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("ngrok API not accessible: {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var tunnelsData = System.Text.Json.JsonSerializer.Deserialize<NgrokApiResponse>(json, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Find HTTPS tunnel
            var httpsTunnel = tunnelsData?.Tunnels?.FirstOrDefault(t =>
                t.Proto == "https" && t.PublicUrl?.StartsWith("https://") == true);

            if (httpsTunnel?.PublicUrl != null)
            {
                _logger.LogDebug("Found ngrok HTTPS tunnel: {TunnelUrl}", httpsTunnel.PublicUrl);
                return httpsTunnel.PublicUrl.TrimEnd('/');
            }

            _logger.LogDebug("No HTTPS tunnel found in ngrok API response");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to detect ngrok tunnel via API");
            return null;
        }
    }

    // Data models for ngrok API response
    private class NgrokApiResponse
    {
        public List<NgrokTunnel>? Tunnels { get; set; }
    }

    private class NgrokTunnel
    {
        public string? PublicUrl { get; set; }
        public string? Proto { get; set; }
        public NgrokConfig? Config { get; set; }
    }

    private class NgrokConfig
    {
        public string? Addr { get; set; }
    }
}