using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AI.ProfilePhotoMaker.API.Controllers;

public class StyleGenerationResult
{
    public string Style { get; set; } = string.Empty;
    public bool Success { get; set; }
    public object? Result { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Replicate API utility endpoints (model availability and health checks).
/// Training, generation, and enhancement endpoints have been moved to dedicated controllers.
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ReplicateController : ControllerBase
{
    private readonly IReplicateApiClient _replicateApiClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReplicateController> _logger;

    public ReplicateController(
        IReplicateApiClient replicateApiClient,
        IConfiguration configuration,
        ILogger<ReplicateController> logger)
    {
        _replicateApiClient = replicateApiClient;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Checks if a model is available on Replicate
    /// </summary>
    [HttpGet("model/availability/{modelId}")]
    public async Task<IActionResult> CheckModelAvailability(string modelId)
    {
        if (string.IsNullOrEmpty(modelId))
            return BadRequest(new { success = false, error = new { code = "InvalidModel", message = "Model ID is required." } });

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });

        try
        {
            // URL decode the model ID since it's passed as a path parameter
            var decodedModelId = Uri.UnescapeDataString(modelId);
            var isAvailable = await _replicateApiClient.CheckModelAvailabilityAsync(decodedModelId);

            return Ok(new
            {
                success = true,
                data = new { available = isAvailable },
                error = (object?)null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking model availability for model {ModelId}", modelId);
            return StatusCode(500, new
            {
                success = false,
                error = new
                {
                    code = "AvailabilityCheckFailed",
                    message = "Failed to check model availability. Please try again later."
                }
            });
        }
    }

    /// <summary>
    /// Health check endpoint for Replicate API connectivity and configuration
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> HealthCheck()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });

        try
        {
            var healthData = new
            {
                apiConnected = false,
                tokenValid = false,
                canCreateModels = false,
                accountStatus = "Unknown",
                configurationValid = false,
                externalUrlAccessible = false,
                error = (string?)null
            };

            // Check basic configuration
            var apiToken = Environment.GetEnvironmentVariable("REPLICATE_API_TOKEN") ?? _configuration["Replicate:ApiToken"];
            var fluxModelId = _configuration["Replicate:FluxTrainingModelId"];
            var externalApiBaseUrl = _configuration["ExternalApiBaseUrl"];

            if (string.IsNullOrEmpty(apiToken))
            {
                return Ok(new
                {
                    success = true,
                    data = healthData with { error = "REPLICATE_API_TOKEN not configured" }
                });
            }

            if (string.IsNullOrEmpty(fluxModelId) || !fluxModelId.Contains(':'))
            {
                return Ok(new
                {
                    success = true,
                    data = healthData with { error = "Replicate:FluxTrainingModelId not properly configured" }
                });
            }

            // Test basic API connectivity
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Token", apiToken);
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                var response = await httpClient.GetAsync("https://api.replicate.com/v1/account");

                if (response.IsSuccessStatusCode)
                {
                    healthData = healthData with
                    {
                        apiConnected = true,
                        tokenValid = true,
                        accountStatus = "Active"
                    };

                    // Try to check if we can create models (this is a simplified check)
                    var modelsResponse = await httpClient.GetAsync("https://api.replicate.com/v1/models");
                    if (modelsResponse.IsSuccessStatusCode)
                    {
                        healthData = healthData with { canCreateModels = true };
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    healthData = healthData with
                    {
                        apiConnected = true,
                        tokenValid = false,
                        error = "Invalid or expired API token"
                    };
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
                {
                    healthData = healthData with
                    {
                        apiConnected = true,
                        tokenValid = true,
                        accountStatus = "Payment Required",
                        error = "Replicate account requires payment"
                    };
                }
            }
            catch (HttpRequestException ex)
            {
                healthData = healthData with { error = $"Network error: {ex.Message}" };
            }
            catch (TaskCanceledException)
            {
                healthData = healthData with { error = "Request timeout connecting to Replicate API" };
            }

            // Check configuration validity
            healthData = healthData with
            {
                configurationValid = !string.IsNullOrEmpty(apiToken) &&
                                   !string.IsNullOrEmpty(fluxModelId) &&
                                   fluxModelId.Contains(':')
            };

            // Check external URL accessibility (basic check)
            if (!string.IsNullOrEmpty(externalApiBaseUrl))
            {
                try
                {
                    using var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(5);
                    var testResponse = await httpClient.GetAsync($"{externalApiBaseUrl.TrimEnd('/')}/api/image/health");
                    healthData = healthData with { externalUrlAccessible = testResponse.IsSuccessStatusCode };
                }
                catch
                {
                    healthData = healthData with { externalUrlAccessible = false };
                }
            }

            return Ok(new { success = true, data = healthData, error = (object?)null });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Replicate health check for user {UserId}", LoggingSanitizer.SanitizeId(userId));
            return StatusCode(500, new
            {
                success = false,
                error = new
                {
                    code = "HealthCheckFailed",
                    message = "Failed to perform health check"
                }
            });
        }
    }
}
