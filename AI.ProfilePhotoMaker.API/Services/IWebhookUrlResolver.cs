namespace AI.ProfilePhotoMaker.API.Services;

/// <summary>
/// Service for resolving webhook URLs in different environments
/// </summary>
public interface IWebhookUrlResolver
{
    /// <summary>
    /// Resolves the appropriate webhook base URL for the current environment
    /// </summary>
    /// <returns>HTTPS URL suitable for webhook callbacks, or null if webhooks should be disabled</returns>
    Task<string?> GetWebhookBaseUrlAsync();

    /// <summary>
    /// Gets the full webhook URL for a specific endpoint path
    /// </summary>
    /// <param name="endpointPath">The webhook endpoint path (e.g., "/api/webhooks/replicate/prediction-complete")</param>
    /// <returns>Complete webhook URL, or null if webhooks should be disabled</returns>
    Task<string?> GetWebhookUrlAsync(string endpointPath);

    /// <summary>
    /// Validates that the resolved webhook URL is accessible and returns HTTPS
    /// </summary>
    /// <returns>True if webhook URL is valid and accessible</returns>
    Task<bool> ValidateWebhookUrlAsync();
}