using System.Security.Cryptography;
using System.Text;
using AI.ProfilePhotoMaker.API.Configuration;

namespace AI.ProfilePhotoMaker.API.Services.Marketing;

internal static class MarketingUnsubscribeTokenService
{
    private const string TokenVersion = "v1";

    public static string CreateToken(string userId, Guid campaignId, string secret)
    {
        var payload = $"{TokenVersion}|{userId}|{campaignId:N}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var signature = ComputeSignature(payloadBytes, secret);

        return $"{Base64UrlEncode(payloadBytes)}.{Base64UrlEncode(signature)}";
    }

    public static bool TryReadUserId(string token, string secret, out string userId)
    {
        userId = string.Empty;

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        var parts = token.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        byte[] payloadBytes;
        byte[] signatureBytes;

        try
        {
            payloadBytes = Base64UrlDecode(parts[0]);
            signatureBytes = Base64UrlDecode(parts[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        var expectedSignature = ComputeSignature(payloadBytes, secret);
        if (!CryptographicOperations.FixedTimeEquals(signatureBytes, expectedSignature))
        {
            return false;
        }

        var payload = Encoding.UTF8.GetString(payloadBytes);
        var payloadParts = payload.Split('|', 3, StringSplitOptions.None);
        if (payloadParts.Length != 3 || payloadParts[0] != TokenVersion)
        {
            return false;
        }

        if (!Guid.TryParseExact(payloadParts[2], "N", out _))
        {
            return false;
        }

        userId = payloadParts[1];
        return !string.IsNullOrWhiteSpace(userId);
    }

    public static string? ResolveSigningSecret(EmailOptions options)
    {
        return FirstConfigured(
            options.MarketingUnsubscribeSecret,
            options.PostmarkWebhookSecret,
            options.PostmarkServerToken);
    }

    private static string? FirstConfigured(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && !LooksLikePlaceholder(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool LooksLikePlaceholder(string value)
    {
        return value.StartsWith("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase)
               || value.Contains("STORED_IN_USER_SECRETS", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("your_", StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] ComputeSignature(byte[] payloadBytes, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return hmac.ComputeHash(payloadBytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch
        {
            2 => padded + "==",
            3 => padded + "=",
            _ => padded
        };

        return Convert.FromBase64String(padded);
    }
}
