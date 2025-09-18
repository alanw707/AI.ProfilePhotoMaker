namespace AI.ProfilePhotoMaker.API.Configuration;

/// <summary>
/// Centralized switches for legacy fallback behavior so we can phase out
/// compatibility paths safely while gathering telemetry.
/// </summary>
public class LegacyCompatibilityOptions
{
    public const string SectionName = "LegacyCompatibility";

    /// <summary>
    /// Allow WebhookUrlResolver to fall back to AppBaseUrl when ExternalApiBaseUrl is missing.
    /// </summary>
    public bool EnableWebhookAppBaseFallback { get; set; } = true;

    /// <summary>
    /// Permit AuthService to read legacy JWT configuration keys ("JWT:") if new casing not present.
    /// </summary>
    public bool EnableAuthLegacyJwtKeyFallback { get; set; } = true;

    /// <summary>
    /// Enable the retention job to scan legacy enhanced/ paths without environment prefixes.
    /// </summary>
    public bool EnableLegacyEnhancedPathLookup { get; set; } = true;
}
