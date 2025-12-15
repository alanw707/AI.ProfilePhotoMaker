using Stripe;

namespace AI.ProfilePhotoMaker.API.Services.Payments;

internal static class StripeClientFactory
{
    internal static string GetSafeSecretKey(string? secretKey)
    {
        var trimmed = (secretKey ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(trimmed))
        {
            return trimmed;
        }

        // Avoid DI/runtime failures when Stripe is not configured in local/dev.
        // Stripe operations are still guarded by StripeOptions.HasApiKeys().
        return "sk_test_missing";
    }

    internal static StripeClient Create(string? secretKey)
    {
        return new StripeClient(GetSafeSecretKey(secretKey));
    }
}

