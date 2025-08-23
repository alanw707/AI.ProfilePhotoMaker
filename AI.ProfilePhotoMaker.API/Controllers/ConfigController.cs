using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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

    /// <summary>
    /// Validates critical configuration settings for the application
    /// </summary>
    [HttpGet("validate")]
    [Authorize]
    public IActionResult ValidateConfiguration()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });

        try
        {
            var validation = new
            {
                replicateApiToken = ValidateReplicateApiToken(),
                fluxTrainingModelId = ValidateFluxTrainingModelId(),
                externalApiBaseUrl = ValidateExternalApiBaseUrl(),
                webhookSecret = ValidateWebhookSecret(),
                azureStorage = ValidateAzureStorage(),
                googleAuth = ValidateGoogleAuth(),
                overallValid = false
            };

            // Calculate overall validity
            validation = validation with
            {
                overallValid = validation.replicateApiToken.GetType().GetProperty("valid")?.GetValue(validation.replicateApiToken) as bool? == true &&
                              validation.fluxTrainingModelId.GetType().GetProperty("valid")?.GetValue(validation.fluxTrainingModelId) as bool? == true &&
                              validation.externalApiBaseUrl.GetType().GetProperty("valid")?.GetValue(validation.externalApiBaseUrl) as bool? == true &&
                              validation.webhookSecret.GetType().GetProperty("valid")?.GetValue(validation.webhookSecret) as bool? == true &&
                              validation.azureStorage.GetType().GetProperty("valid")?.GetValue(validation.azureStorage) as bool? == true &&
                              validation.googleAuth.GetType().GetProperty("valid")?.GetValue(validation.googleAuth) as bool? == true
            };

            return Ok(new { success = true, data = validation, error = (object?)null });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during configuration validation for user {UserId}", userId);
            return StatusCode(500, new
            {
                success = false,
                error = new
                {
                    code = "ValidationFailed",
                    message = "Failed to validate configuration"
                }
            });
        }
    }

    private object ValidateReplicateApiToken()
    {
        var envToken = Environment.GetEnvironmentVariable("REPLICATE_API_TOKEN");
        var cfgToken = _configuration["Replicate:ApiToken"];
        var token = envToken ?? cfgToken;

        if (string.IsNullOrEmpty(token))
        {
            return new { valid = false, message = "REPLICATE_API_TOKEN not configured", source = "none" };
        }

        if (!token.StartsWith("r8_"))
        {
            return new { valid = false, message = "Invalid token format (should start with 'r8_')", source = envToken != null ? "environment" : "config" };
        }

        return new { valid = true, message = "Token configured", source = envToken != null ? "environment" : "config" };
    }

    private object ValidateFluxTrainingModelId()
    {
        var modelId = _configuration["Replicate:FluxTrainingModelId"];

        if (string.IsNullOrEmpty(modelId))
        {
            return new { valid = false, message = "Replicate:FluxTrainingModelId not configured" };
        }

        if (!modelId.Contains(':'))
        {
            return new { valid = false, message = "Invalid format (should be 'owner/model:version')" };
        }

        var parts = modelId.Split(':');
        if (parts.Length != 2 || string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1]))
        {
            return new { valid = false, message = "Invalid format (should be 'owner/model:version')" };
        }

        return new { valid = true, message = "Model ID configured correctly", value = modelId };
    }

    private object ValidateExternalApiBaseUrl()
    {
        var externalUrl = _configuration["ExternalApiBaseUrl"];
        var appBaseUrl = _configuration["AppBaseUrl"];

        if (string.IsNullOrEmpty(externalUrl))
        {
            if (!string.IsNullOrEmpty(appBaseUrl) && appBaseUrl.StartsWith("https://"))
            {
                return new { valid = true, message = "Using AppBaseUrl as fallback", fallback = true, value = appBaseUrl };
            }
            return new { valid = false, message = "ExternalApiBaseUrl not configured and AppBaseUrl not HTTPS" };
        }

        if (!externalUrl.StartsWith("https://"))
        {
            return new { valid = false, message = "ExternalApiBaseUrl must use HTTPS" };
        }

        return new { valid = true, message = "External API URL configured", value = externalUrl };
    }

    private object ValidateWebhookSecret()
    {
        var envSecret = Environment.GetEnvironmentVariable("REPLICATE_WEBHOOK_SECRET");
        var cfgSecret = _configuration["Replicate:WebhookSecret"];
        var secret = envSecret ?? cfgSecret;

        if (string.IsNullOrEmpty(secret))
        {
            return new { valid = false, message = "REPLICATE_WEBHOOK_SECRET not configured" };
        }

        if (!secret.StartsWith("whsec_"))
        {
            return new { valid = false, message = "Invalid webhook secret format (should start with 'whsec_')" };
        }

        return new { valid = true, message = "Webhook secret configured", source = envSecret != null ? "environment" : "config" };
    }

    private object ValidateAzureStorage()
    {
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING") ??
                              _configuration.GetConnectionString("AzureStorage");

        if (string.IsNullOrEmpty(connectionString))
        {
            return new { valid = false, message = "AZURE_STORAGE_CONNECTION_STRING not configured" };
        }

        if (connectionString.Equals("UseDevelopmentStorage=true", StringComparison.OrdinalIgnoreCase))
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
            {
                return new { valid = false, message = "Cannot use development storage in production" };
            }
            return new { valid = true, message = "Using development storage (Azurite)", development = true };
        }

        if (connectionString.Contains("blob.core.windows.net"))
        {
            return new { valid = true, message = "Azure Blob Storage configured" };
        }

        return new { valid = false, message = "Invalid Azure Storage connection string format" };
    }

    private object ValidateGoogleAuth()
    {
        var clientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ??
                      _configuration["Authentication:Google:ClientId"];
        var clientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") ??
                          _configuration["Authentication:Google:ClientSecret"];

        if (string.IsNullOrEmpty(clientId))
        {
            return new { valid = false, message = "GOOGLE_CLIENT_ID not configured" };
        }

        if (string.IsNullOrEmpty(clientSecret))
        {
            return new { valid = false, message = "GOOGLE_CLIENT_SECRET not configured" };
        }

        if (!clientId.Contains(".apps.googleusercontent.com"))
        {
            return new { valid = false, message = "Invalid Google Client ID format" };
        }

        if (!clientSecret.StartsWith("GOCSPX-"))
        {
            return new { valid = false, message = "Invalid Google Client Secret format" };
        }

        return new { valid = true, message = "Google authentication configured" };
    }
}