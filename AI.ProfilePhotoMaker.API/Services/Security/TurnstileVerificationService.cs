using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;

namespace AI.ProfilePhotoMaker.API.Services.Security;

public interface ITurnstileVerificationService
{
    bool IsEnabled { get; }
    Task<bool> VerifyAsync(string? token, string? remoteIp = null);
}

public class TurnstileVerificationService : ITurnstileVerificationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TurnstileVerificationService> _logger;
    private readonly HttpClient _httpClient;

    private static string S(string? value) => LoggingSanitizer.Sanitize(value);

    private sealed class TurnstileVerificationResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("error-codes")]
        public string[] ErrorCodes { get; set; } = Array.Empty<string>();
    }

    public TurnstileVerificationService(
        IConfiguration configuration,
        ILogger<TurnstileVerificationService> logger,
        HttpClient httpClient)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient;
    }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(GetSecretKey());

    private string? GetSecretKey()
    {
        var configured = _configuration["Turnstile:SecretKey"];
        var envDirect = Environment.GetEnvironmentVariable("TURNSTILE_SECRET_KEY");
        var secret = !string.IsNullOrWhiteSpace(envDirect) ? envDirect : configured;

        if (string.IsNullOrWhiteSpace(secret))
        {
            return null;
        }

        var trimmed = secret.Trim();
        if (trimmed.StartsWith("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return trimmed;
    }

    public async Task<bool> VerifyAsync(string? token, string? remoteIp = null)
    {
        var secretKey = GetSecretKey();
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var form = new Dictionary<string, string>
        {
            ["secret"] = secretKey,
            ["response"] = token.Trim()
        };

        if (!string.IsNullOrWhiteSpace(remoteIp))
        {
            form["remoteip"] = remoteIp.Trim();
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://challenges.cloudflare.com/turnstile/v0/siteverify")
            {
                Content = new FormUrlEncodedContent(form)
            };

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Turnstile verification failed: http_status={StatusCode}", response.StatusCode);
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            var parsed = JsonSerializer.Deserialize<TurnstileVerificationResponse>(json);
            if (parsed?.Success == true)
            {
                return true;
            }

            var errors = parsed?.ErrorCodes?.Length > 0 ? string.Join(",", parsed.ErrorCodes) : "unknown";
            _logger.LogWarning("Turnstile verification rejected: errors={Errors}", WebUtility.HtmlEncode(S(errors)));
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Turnstile verification failed: {ErrorMessage}", S(ex.Message));
            return false;
        }
    }
}

