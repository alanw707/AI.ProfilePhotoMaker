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
    private readonly IConfiguration _configuration;

    private static string S(string? value) => LoggingSanitizer.Sanitize(value);
    private static string Sid(string? value) => LoggingSanitizer.SanitizeId(value);

    public EmailNotificationService(
        IOptions<EmailOptions> options,
        ILogger<EmailNotificationService> logger,
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = httpClient;
        _configuration = configuration;
    }

    private string? BuildApiConfirmEmailLink(string userId, string encodedToken)
    {
        // Prefer the backend base used across the app (prod: https://api.aiprofilephotomaker.com)
        var baseUrl = _configuration["ExternalApiBaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = _configuration["Authentication:OAuth:BaseUrl"]?.TrimEnd('/');
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        return $"{baseUrl}/api/auth/confirm-email?userId={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(encodedToken)}";
    }

    public Task SendTrainingCompletedAsync(string userId, string? email, string? modelName, string? modelVersion)
    {
        var subject = "Your model is ready 🎉";
        var cta = BuildCtaLink("app/dashboard");
        var safeModel = WebUtility.HtmlEncode(modelName ?? "Your custom model");
        var safeVersion = WebUtility.HtmlEncode(modelVersion ?? "latest");

        var body = $@"<p style=""margin:0 0 16px;"">Your model has finished training.</p>
                      <p style=""margin:0 0 16px;""><strong>{safeModel}</strong> (version {safeVersion}) is ready to generate images.</p>
                      <p style=""margin:0 0 16px;"">Open your dashboard to pick a style and start generating.</p>
                      {BuildPrimaryButton("Go to dashboard", cta)}";

        return SendEmailAsync(email, subject, body, "training-completed", userId);
    }

    public Task SendGenerationCompletedAsync(string userId, string? email, string? style, int imageCount, string? jobId = null)
    {
        var subject = "Your images are ready";
        // Link users to the gallery (known route) instead of the old jobs URL that 404s
        var cta = BuildCtaLink("app/gallery");
        var body = $@"<p style=""margin:0 0 16px;"">Your generation request has completed.</p>
                      <p style=""margin:0 0 16px;"">Style: <strong>{WebUtility.HtmlEncode(style ?? "Unknown")}</strong><br/>
                      Images: <strong>{imageCount}</strong></p>
                      <p style=""margin:0 0 16px;"">Sign in to view and download your results.</p>
                      {BuildPrimaryButton("Open gallery", cta)}";
        return SendEmailAsync(email, subject, body, "generation-completed", userId);
    }

    public Task SendGenerationFailedAsync(string userId, string? email, string? style, string? error, string? jobId = null)
    {
        var subject = "Image generation failed";
        var cta = BuildCtaLink(jobId);
        var body = $@"<p style=""margin:0 0 16px;"">Your generation request did not complete.</p>
                      <p style=""margin:0 0 16px;"">Style: <strong>{WebUtility.HtmlEncode(style ?? "Unknown")}</strong></p>
                      <p style=""margin:0 0 16px;"">Error: {WebUtility.HtmlEncode(error ?? "Unknown error")}</p>
                      <p style=""margin:0 0 16px;"">Please retry from the dashboard.</p>
                      {BuildPrimaryButton("Open dashboard", cta)}";
        return SendEmailAsync(email, subject, body, "generation-failed", userId);
    }

    public Task SendPurchaseReceiptAsync(string userId, string? email, CreditPurchase purchase)
    {
        var packageName = purchase.Package?.Name ?? $"Package {purchase.PackageId}";
        var subject = "Payment received - credits added";
        var body = $@"<p style=""margin:0 0 16px;"">Thank you for your purchase.</p>
                      <p style=""margin:0 0 16px;"">Package: <strong>{WebUtility.HtmlEncode(packageName)}</strong><br/>
                      Credits: <strong>{purchase.CreditsAwarded}</strong><br/>
                      Amount: <strong>${purchase.AmountPaid:F2}</strong><br/>
                      Transaction: <strong>{WebUtility.HtmlEncode(purchase.PaymentTransactionId ?? purchase.ExternalTransactionId ?? purchase.Id.ToString())}</strong></p>
                      <p style=""margin:0 0 16px;"">Your credits are now available in your account.</p>";
        return SendEmailAsync(email, subject, body, "purchase-receipt", userId);
    }

    public Task SendEmailVerificationAsync(string userId, string? email, string encodedToken)
    {
        var subject = "Confirm your email address";
        var cta = BuildApiConfirmEmailLink(userId, encodedToken);
        if (string.IsNullOrWhiteSpace(cta))
        {
            var confirmPath =
                $"auth/confirm-email?userId={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(encodedToken)}";
            cta = BuildCtaLink(confirmPath);
        }

        var body = $@"<p style=""margin:0 0 16px;"">Thanks for signing up for AI Profile Photo Maker.</p>
                      <p style=""margin:0 0 16px;"">To protect your account, please confirm your email address.</p>
                      {BuildPrimaryButton("Confirm email", cta)}
                      <p style=""margin:24px 0 0; font-size:14px; color:#475569;"">If you did not create this account, you can safely ignore this email.</p>";

        return SendEmailAsync(email, subject, body, "email-verification", userId);
    }

    public Task SendWelcomeAsync(string userId, string? email, string? firstName = null)
    {
        var subject = "Welcome to AI Profile Photo Maker";
        var cta = BuildCtaLink("app/dashboard");
        var safeName = WebUtility.HtmlEncode(firstName ?? string.Empty);
        var greeting = string.IsNullOrWhiteSpace(safeName) ? "Welcome!" : $"Welcome, {safeName}!";

        var body = $@"<p style=""margin:0 0 16px;""><strong>{greeting}</strong></p>
                      <p style=""margin:0 0 16px;"">Thanks for joining. You're all set to start creating professional photos.</p>
                      <ul style=""margin:0 0 16px; padding-left:20px;"">
                        <li>Upload selfies in Headshot Studio (10+ for best results)</li>
                        <li>Generate headshots and download your favorites</li>
                        <li>Use Photo Transform for quick enhancements</li>
                      </ul>
                      {BuildPrimaryButton("Go to dashboard", cta)}";

        return SendEmailAsync(email, subject, body, "welcome", userId);
    }

    public Task SendSupportFeedbackReceivedAsync(string userId, string? userEmail, FeedbackSubmission submission)
    {
        var supportEmail = _options.SupportToEmail;
        if (string.IsNullOrWhiteSpace(supportEmail))
        {
            _logger.LogInformation("Skipping support feedback email - missing support recipient for user {UserId}", Sid(userId));
            return Task.CompletedTask;
        }

        var subject = $"New {submission.Category} report from {(string.IsNullOrWhiteSpace(userEmail) ? "user" : userEmail)}";
        var safeCategory = WebUtility.HtmlEncode(submission.Category);
        var safeUserEmail = WebUtility.HtmlEncode(userEmail ?? string.Empty);
        var safePageUrl = WebUtility.HtmlEncode(submission.PageUrl ?? string.Empty);
        var safeUserAgent = WebUtility.HtmlEncode(submission.UserAgent ?? string.Empty);
        var safeMessage = WebUtility.HtmlEncode(submission.Message);
        var safeCreated = WebUtility.HtmlEncode(submission.CreatedAtUtc.ToString("u"));

        var body = $@"<p>New authenticated support message received.</p>
                      <p><strong>Category:</strong> {safeCategory}<br/>
                      <strong>UserId:</strong> {WebUtility.HtmlEncode(userId)}<br/>
                      <strong>User email:</strong> {safeUserEmail}<br/>
                      <strong>Created (UTC):</strong> {safeCreated}</p>
                      <p><strong>Page:</strong> {safePageUrl}<br/>
                      <strong>User agent:</strong> {safeUserAgent}</p>
                      <p><strong>Message:</strong></p>
                      <pre style=""white-space:pre-wrap;"">{safeMessage}</pre>";

        return SendEmailAsync(
            toEmail: supportEmail,
            subject: subject,
            htmlBody: body,
            template: "support-feedback",
            userId: userId,
            replyToEmail: userEmail);
    }

    private Task SendEmailAsync(string? toEmail, string subject, string htmlBody, string template, string? userId = null)
    {
        return SendEmailAsync(toEmail, subject, htmlBody, template, userId, replyToEmail: null, replyToName: null);
    }

    private async Task SendEmailAsync(
        string? toEmail,
        string subject,
        string htmlBody,
        string template,
        string? userId,
        string? replyToEmail,
        string? replyToName = null)
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
        var wrappedHtml = WrapEmail(subject, htmlBody);
        if (useApi)
        {
            await SendEmailViaApiAsync(toEmail, subject, wrappedHtml, template, userId, replyToEmail, replyToName);
            return;
        }

        var canSmtp = !string.IsNullOrWhiteSpace(_options.SmtpHost);
        if (canSmtp)
        {
            await SendEmailViaSmtpAsync(toEmail, subject, wrappedHtml, template, userId, replyToEmail, replyToName);
            return;
        }

        _logger.LogWarning("No email delivery path available (UseApi={UseApi}, ApiKeySet={ApiKeySet}, SmtpHost={SmtpHost}) for {Template}",
            _options.UseApi,
            !string.IsNullOrWhiteSpace(_options.ApiKey),
            S(_options.SmtpHost),
            template);
    }

    private async Task SendEmailViaApiAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string template,
        string? userId,
        string? replyToEmail,
        string? replyToName)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("api-key", _options.ApiKey);

            object payload = new
            {
                sender = new { name = _options.FromName ?? "AI Profile Photo Maker", email = _options.FromEmail },
                to = new[] { new { email = toEmail } },
                subject = _options.SandboxMode ? $"[SANDBOX] {subject}" : subject,
                htmlContent = htmlBody
            };

            if (!string.IsNullOrWhiteSpace(replyToEmail))
            {
                payload = new
                {
                    sender = new { name = _options.FromName ?? "AI Profile Photo Maker", email = _options.FromEmail },
                    to = new[] { new { email = toEmail } },
                    replyTo = new { email = replyToEmail, name = replyToName ?? replyToEmail },
                    subject = _options.SandboxMode ? $"[SANDBOX] {subject}" : subject,
                    htmlContent = htmlBody
                };
            }

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

    private async Task SendEmailViaSmtpAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string template,
        string? userId,
        string? replyToEmail,
        string? replyToName)
    {
        using var mail = new MailMessage
        {
            From = new MailAddress(_options.FromEmail!, _options.FromName ?? "AI Profile Photo Maker"),
            Subject = _options.SandboxMode ? $"[SANDBOX] {subject}" : subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        mail.To.Add(new MailAddress(toEmail));
        if (!string.IsNullOrWhiteSpace(replyToEmail))
        {
            mail.ReplyToList.Add(new MailAddress(replyToEmail, replyToName ?? replyToEmail));
        }

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

    private string? BuildCtaLink(string? relativePath)
    {
        var baseUrl = _options.FrontendBaseUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return baseUrl;
        }

        return $"{baseUrl}/{relativePath.TrimStart('/')}";
    }

    private static string BuildPrimaryButton(string label, string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var safeLabel = WebUtility.HtmlEncode(label);
        var safeUrl = WebUtility.HtmlEncode(url);
        return $@"<p style=""margin:20px 0 0;"">
                    <a href=""{safeUrl}""
                       style=""display:inline-block; background:#0ea5e9; color:#ffffff; text-decoration:none;
                              padding:12px 18px; border-radius:8px; font-weight:600; font-size:16px;"">
                      {safeLabel}
                    </a>
                  </p>";
    }

    private string WrapEmail(string subject, string htmlBody, string? preheader = null)
    {
        var safeSubject = WebUtility.HtmlEncode(subject);
        var safePreheader = WebUtility.HtmlEncode(preheader ?? "AI Profile Photo Maker updates");
        var brandName = WebUtility.HtmlEncode(_options.FromName ?? "AI Profile Photo Maker");
        var supportLine = string.IsNullOrWhiteSpace(_options.SupportToEmail)
            ? "Need help? Reply to this email and we will get back to you."
            : $"Need help? Reply to this email or contact {_options.SupportToEmail}.";
        var safeSupport = WebUtility.HtmlEncode(supportLine);

        return $@"<!doctype html>
<html lang=""en"">
  <head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
    <title>{safeSubject}</title>
  </head>
  <body style=""margin:0; padding:0; background:#0f172a;"">
    <div style=""display:none; max-height:0; overflow:hidden; opacity:0; color:transparent;"">
      {safePreheader}
    </div>
    <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""background:#0f172a; padding:24px 0;"">
      <tr>
        <td align=""center"">
          <table role=""presentation"" width=""600"" cellspacing=""0"" cellpadding=""0""
                 style=""width:600px; max-width:600px; background:#ffffff; border-radius:14px; overflow:hidden;"">
            <tr>
              <td style=""padding:20px 24px 8px; font-family:Arial, Helvetica, sans-serif;"">
                <div style=""font-size:18px; font-weight:700; color:#0f172a;"">{brandName}</div>
                <div style=""font-size:13px; color:#64748b; margin-top:4px;"">
                  Professional headshots in minutes for LinkedIn, resumes, and teams.
                </div>
              </td>
            </tr>
            <tr>
              <td style=""padding:0 24px 8px; font-family:Arial, Helvetica, sans-serif;"">
                <h1 style=""margin:0; font-size:24px; line-height:1.3; color:#0f172a;"">{safeSubject}</h1>
              </td>
            </tr>
            <tr>
              <td style=""padding:12px 24px 8px; font-family:Arial, Helvetica, sans-serif; font-size:16px; line-height:1.6; color:#0f172a;"">
                {htmlBody}
              </td>
            </tr>
            <tr>
              <td style=""padding:16px 24px 24px; font-family:Arial, Helvetica, sans-serif; font-size:13px; line-height:1.5; color:#64748b;"">
                {safeSupport}
              </td>
            </tr>
          </table>
        </td>
      </tr>
    </table>
  </body>
</html>";
    }
}
