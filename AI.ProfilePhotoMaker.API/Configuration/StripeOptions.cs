namespace AI.ProfilePhotoMaker.API.Configuration;

public class StripeOptions
{
    public const string SectionName = "Stripe";

    public string PublishableKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public bool AllowLiveKeysInDevelopment { get; set; } = false;

    public bool HasApiKeys()
    {
        return !string.IsNullOrWhiteSpace(SecretKey)
               && !string.IsNullOrWhiteSpace(PublishableKey);
    }

    public bool HasWebhookSecret()
    {
        return !string.IsNullOrWhiteSpace(WebhookSecret);
    }

    public bool UsesLiveMode()
    {
        return SecretKey.StartsWith("sk_live_", StringComparison.OrdinalIgnoreCase)
               || PublishableKey.StartsWith("pk_live_", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsConfigured()
    {
        return HasApiKeys() && HasWebhookSecret();
    }
}
