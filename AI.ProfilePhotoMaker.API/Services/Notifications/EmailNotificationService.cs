using System.Net;
using System.Net.Mail;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Models;
using Microsoft.Extensions.Options;

namespace AI.ProfilePhotoMaker.API.Services.Notifications;

public class EmailNotificationService : IEmailNotificationService
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailNotificationService> _logger;
    private readonly HttpClient _httpClient;

    private static string S(string? value) => LoggingSanitizer.Sanitize(value);
    private static string Sid(string? value) => LoggingSanitizer.SanitizeId(value);

    public EmailNotificationService(
        IOptions<EmailOptions> options,
        ILogger<EmailNotificationService> logger,
        HttpClient httpClient)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = httpClient;
    }

    public Task SendTrainingCompletedAsync(string userId, string? email, string? modelName, string? modelVersion)
    {
        var subject = "Your model training is complete";
        var body = $@"<p>Your model has finished training.</p>
                      <p>Model: <strong>{WebUtility.HtmlEncode(modelName ?? "Custom model")}</strong><br/>
                      Version: <strong>{WebUtility.HtmlEncode(modelVersion ?? "latest")}</strong></p>
                      <p>You can start generating images now.</p>";
        return SendEmailAsync(email, subject, body, "training-completed", userId);
    }

    public Task SendGenerationCompletedAsync(string userId, string? email, string? style, int imageCount, string? jobId = null)
    {
        var subject = "Your images are ready";
        var cta = BuildCtaLink(jobId);
        var body = $@"<p>Your generation request has completed.</p>
                      <p>Style: <strong>{WebUtility.HtmlEncode(style ?? "Unknown")}</strong><br/>
                      Images: <strong>{imageCount}</strong></p>
                      <p>Sign in to view and download your results.{(string.IsNullOrWhiteSpace(cta) ? string.Empty : $"<br/><a href=\"{cta}\">Open generation</a>")}</p>";
        return SendEmailAsync(email, subject, body, "generation-completed", userId);
    }

    public Task SendGenerationFailedAsync(string userId, string? email, string? style, string? error, string? jobId = null)
    {
        var subject = "Image generation failed";
        var cta = BuildCtaLink(jobId);
        var body = $@"<p>Your generation request did not complete.</p>
                      <p>Style: <strong>{WebUtility.HtmlEncode(style ?? "Unknown")}</strong></p>
                      <p>Error: {WebUtility.HtmlEncode(error ?? "Unknown error")}</p>
                      <p>Please retry from the dashboard.{(string.IsNullOrWhiteSpace(cta) ? string.Empty : $"<br/><a href=\"{cta}\">Open generation</a>")}</p>";
        return SendEmailAsync(email, subject, body, "generation-failed", userId);
    }

    public Task SendPurchaseReceiptAsync(string userId, string? email, CreditPurchase purchase)
    {
        var packageName = purchase.Package?.Name ?? $"Package {purchase.PackageId}";
        var subject = "Payment received - credits added";
        var body = $@"<p>Thank you for your purchase.</p>
                      <p>Package: <strong>{WebUtility.HtmlEncode(packageName)}</strong><br/>
                      Credits: <strong>{purchase.CreditsAwarded}</strong><br/>
                      Amount: <strong>${purchase.AmountPaid:F2}</strong><br/>
                      Transaction: <strong>{WebUtility.HtmlEncode(purchase.PaymentTransactionId ?? purchase.ExternalTransactionId ?? purchase.Id.ToString())}</strong></p>
                      <p>Your credits are now available in your account.</p>";
        return SendEmailAsync(email, subject, body, "purchase-receipt", userId);
    }

    private async Task SendEmailAsync(string? toEmail, string subject, string htmlBody, string template, string? userId = null)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("Email disabled; skipping {Template} for user {UserId}", template, Sid(userId));
            return;
        }

        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogInformation("Skipping email {Template} - missing recipient for user {UserId}", template, Sid(userId));
            return;
        }

        _logger.LogInformation("Email config for {Template}: UseApi={UseApi}, ApiKeySet={ApiKeySet}, SmtpHost={SmtpHost}, UsernameSet={UsernameSet}",
            template,
            _options.UseApi,
            !string.IsNullOrWhiteSpace(_options.ApiKey),
            S(_options.SmtpHost),
            !string.IsNullOrWhiteSpace(_options.Username));

        if (string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            _logger.LogWarning("Email not configured (missing FromEmail); cannot send {Template}", template);
            return;
        }

        var useApi = _options.UseApi && !string.IsNullOrWhiteSpace(_options.ApiKey);
        if (useApi)
        {
            await SendEmailViaApiAsync(toEmail, subject, htmlBody, template, userId);
            return;
        }

        var canSmtp = !string.IsNullOrWhiteSpace(_options.SmtpHost);
        if (canSmtp)
        {
            await SendEmailViaSmtpAsync(toEmail, subject, htmlBody, template, userId);
            return;
        }

        _logger.LogWarning("No email delivery path available (UseApi={UseApi}, ApiKeySet={ApiKeySet}, SmtpHost={SmtpHost}) for {Template}",
            _options.UseApi,
            !string.IsNullOrWhiteSpace(_options.ApiKey),
            S(_options.SmtpHost),
            template);
    }

    private async Task SendEmailViaApiAsync(string toEmail, string subject, string htmlBody, string template, string? userId)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("api-key", _options.ApiKey);

            var payload = new
            {
                sender = new { name = _options.FromName ?? "AI Profile Photo Maker", email = _options.FromEmail },
                to = new[] { new { email = toEmail } },
                subject = _options.SandboxMode ? $"[SANDBOX] {subject}" : subject,
                htmlContent = htmlBody
            };

            var json = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Sent {Template} email via API to {Recipient} for user {UserId}", template, S(toEmail), Sid(userId));
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed API email send {Template} for user {UserId}. Status {Status}: {Body}", template, Sid(userId), response.StatusCode, S(body));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed API email send {Template} for user {UserId}: {Reason}", template, Sid(userId), S(ex.Message));
        }
    }

    private async Task SendEmailViaSmtpAsync(string toEmail, string subject, string htmlBody, string template, string? userId)
    {
        using var mail = new MailMessage
        {
            From = new MailAddress(_options.FromEmail!, _options.FromName ?? "AI Profile Photo Maker"),
            Subject = _options.SandboxMode ? $"[SANDBOX] {subject}" : subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        mail.To.Add(new MailAddress(toEmail));

        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        try
        {
            await client.SendMailAsync(mail);
            _logger.LogInformation("Sent {Template} email via SMTP to {Recipient} for user {UserId}", template, S(toEmail), Sid(userId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed SMTP email send {Template} for user {UserId}: {Reason}", template, Sid(userId), S(ex.Message));
        }
    }

    private string? BuildCtaLink(string? jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return null;
        }

        var baseUrl = _options.FrontendBaseUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        return $"{baseUrl}/dashboard/jobs/{WebUtility.UrlEncode(jobId)}";
    }
}
