using Microsoft.AspNetCore.Mvc;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>
/// Configuration controller for exposing safe client configuration
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<ConfigController> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// Get client-safe configuration for frontend applications
    /// </summary>
    [HttpGet("client")]
    public IActionResult GetClientConfiguration()
    {
        try
        {
            var appBaseUrl = _configuration["AppBaseUrl"] ?? "http://localhost:5032";
            var environmentName = _environment.EnvironmentName;

            var clientConfig = new
            {
                appBaseUrl = appBaseUrl,
                apiBaseUrl = $"{appBaseUrl}/api",
                frontendBaseUrl = GetFrontendBaseUrl(),
                environment = environmentName.ToLower(),
                isDevelopment = _environment.IsDevelopment(),
                isTest = string.Equals(environmentName, "Test", StringComparison.OrdinalIgnoreCase),
                isProduction = _environment.IsProduction(),
                features = new
                {
                    enableAutoUrlDetection = true,
                    enableExternalAccess = !_environment.IsProduction(),
                    enableConfigurationDebug = _environment.IsDevelopment()
                },
                oauth = new
                {
                    useExternalUrls = !appBaseUrl.Contains("localhost"),
                    redirectBaseUrl = appBaseUrl
                },
                timestamp = DateTime.UtcNow
            };

            _logger.LogInformation("Client configuration requested. Environment: {Environment}, AppBaseUrl: {AppBaseUrl}",
                environmentName, appBaseUrl);

            return Ok(new { success = true, data = clientConfig });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving client configuration");
            return StatusCode(500, new { success = false, error = "Failed to retrieve configuration" });
        }
    }

    /// <summary>
    /// Get detailed configuration status for debugging (development only)
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetConfigurationStatus()
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        try
        {
            var appBaseUrl = _configuration["AppBaseUrl"] ?? "http://localhost:5032";
            var currentRequestUrl = $"{Request.Scheme}://{Request.Host}";

            var configStatus = new
            {
                environment = _environment.EnvironmentName,
                currentRequest = new
                {
                    scheme = Request.Scheme,
                    host = Request.Host.ToString(),
                    fullUrl = currentRequestUrl,
                    isHttps = Request.IsHttps,
                    userAgent = Request.Headers.UserAgent.ToString()
                },
                configuration = new
                {
                    appBaseUrl = appBaseUrl,
                    isExternalUrl = !appBaseUrl.Contains("localhost"),
                    isNgrokUrl = appBaseUrl.Contains("ngrok"),
                    isLocaltunnelUrl = appBaseUrl.Contains("loca.lt")
                },
                headers = Request.Headers.Where(h =>
                    h.Key.StartsWith("X-Forwarded") ||
                    h.Key.StartsWith("Host") ||
                    h.Key.Equals("Origin", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(h => h.Key, h => h.Value.ToString()),
                recommendations = GetConfigurationRecommendations(appBaseUrl, currentRequestUrl)
            };

            return Ok(new { success = true, data = configStatus });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving configuration status");
            return StatusCode(500, new { success = false, error = "Failed to retrieve configuration status" });
        }
    }

    private string GetFrontendBaseUrl()
    {
        var appBaseUrl = _configuration["AppBaseUrl"] ?? "http://localhost:5032";

        // If backend is using external URL, try to determine frontend URL
        if (!appBaseUrl.Contains("localhost"))
        {
            // For tunnel services, frontend is typically on a different URL
            // This is a best guess - in practice, frontend will override this
            return appBaseUrl.Replace("5032", "4200");
        }

        // Default to localhost for local development
        return "http://localhost:4200";
    }

    private object GetConfigurationRecommendations(string appBaseUrl, string currentRequestUrl)
    {
        var recommendations = new List<string>();

        if (appBaseUrl.Contains("localhost") && !currentRequestUrl.Contains("localhost"))
        {
            recommendations.Add("Backend configured for localhost but accessed externally. Consider updating AppBaseUrl in appsettings.Development.json");
        }

        if (!appBaseUrl.Contains("localhost") && currentRequestUrl.Contains("localhost"))
        {
            recommendations.Add("Backend configured for external access but accessed via localhost. This is normal for local API testing");
        }

        if (appBaseUrl.Contains("ngrok") && !appBaseUrl.Contains("https"))
        {
            recommendations.Add("Ngrok URLs should use HTTPS for OAuth to work properly");
        }

        return new
        {
            recommendations = recommendations,
            configurationValid = recommendations.Count == 0,
            nextSteps = recommendations.Count == 0
                ? new[] { "Configuration looks good! Frontend should auto-detect this setup." }
                : recommendations.ToArray()
        };
    }
}