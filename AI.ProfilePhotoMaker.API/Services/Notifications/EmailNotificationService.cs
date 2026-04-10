using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Net.Mime;
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
                        <li>Upload selfies in Headshot Studio (5+ for best results)</li>
                        <li>Generate headshots and download your favorites</li>
                        <li>Use Photo Transform for quick enhancements</li>
                      </ul>
                      {BuildPrimaryButton("Go to dashboard", cta)}";

        return SendEmailAsync(email, subject, body, "welcome", userId);
    }

    public Task SendAbandonedUploadNudgeAsync(string userId, string? email, string? firstName = null)
    {
        var subject = "Still want your professional headshots?";
        var cta = BuildCtaLink("app/dashboard");
        var safeName = WebUtility.HtmlEncode(firstName ?? string.Empty);
        var greeting = string.IsNullOrWhiteSpace(safeName) ? "Hey!" : $"Hey {safeName},";

        var body = $@"<p style=""margin:0 0 16px;""><strong>{greeting}</strong></p>
                      <p style=""margin:0 0 16px;"">You created your account but haven't uploaded photos yet — that's the only step left to get your professional headshots.</p>
                      <p style=""margin:0 0 16px;"">No studio. No photographer. No awkward posing. Just upload 5–15 selfies from your phone and our AI will do the rest.</p>
                      <p style=""margin:0 0 16px;""><strong>What makes good photos?</strong></p>
                      <ul style=""margin:0 0 16px; padding-left:20px;"">
                        <li>Front-facing selfies with your face clearly visible</li>
                        <li>A few different angles and expressions</li>
                        <li>Good lighting (natural light works great)</li>
                        <li>Phone photos are totally fine</li>
                      </ul>
                      <p style=""margin:0 0 16px;"">It takes about 2 minutes to upload and you'll have professional headshots ready in no time.</p>
                      {BuildPrimaryButton("Upload my photos now", cta)}";

        return SendEmailAsync(email, subject, body, "abandoned-upload-nudge", userId);
    }

    public Task SendRetentionDeletionWarningAsync(string userId, string? email, int imageCount, DateTime deletionDate, int daysUntilDeletion)
    {
        // Determine subject and message based on days until deletion
        string subject;
        string messageIntro;
        
        if (daysUntilDeletion == 14)
        {
            subject = "Reminder: Your photos will be deleted in 14 days";
            messageIntro = "This is a friendly reminder that your photos will be automatically deleted in 14 days per our data retention policy.";
        }
        else if (daysUntilDeletion == 7)
        {
            subject = "Reminder: Your photos will be deleted in 7 days";
            messageIntro = "This is a final reminder that your photos will be automatically deleted in 7 days per our data retention policy.";
        }
        else
        {
            // Generic message for any other number of days
            subject = $"Reminder: Your photos will be deleted in {daysUntilDeletion} days";
            messageIntro = $"This is a reminder that your photos will be automatically deleted in {daysUntilDeletion} days per our data retention policy.";
        }

        var cta = BuildCtaLink("app/gallery");
        var safeDeletionDate = WebUtility.HtmlEncode(deletionDate.ToString("MMMM d, yyyy"));
        var imageCountText = imageCount == 1 ? "1 photo" : $"{imageCount} photos";

        var body = $@"<p style=""margin:0 0 16px;"">{messageIntro}</p>
                      <p style=""margin:0 0 16px;"">
                        <strong>Images scheduled for deletion:</strong> {imageCountText}<br/>
                        <strong>Deletion date:</strong> {safeDeletionDate}
                      </p>
                      <p style=""margin:0 0 16px;"">If you'd like to keep any of these photos, please download them from your gallery before the deletion date. After deletion, they cannot be recovered.</p>
                      {BuildPrimaryButton("View my gallery", cta)}
                      <p style=""margin:24px 0 0; font-size:14px; color:#475569;"">This is an automated reminder about our 30-day data retention policy. If you have any questions, please reply to this email.</p>";

        var templateTag = $"retention-deletion-warning-{daysUntilDeletion}d";
        return SendEmailAsync(email, subject, body, templateTag, userId);
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

    public async Task<bool> SendMarketingEmailAsync(
        string userId,
        string email,
        string subject,
        string htmlBody,
        string unsubscribeUrl)
    {
        if (!_options.Enabled) return false;

        var safeUnsubscribeUrl = WebUtility.HtmlEncode(unsubscribeUrl);
        var bodyWithFooter = htmlBody + $@"
<p style=""margin:24px 0 0; font-size:13px; color:#64748b;"">
  You're receiving this because you signed up for AI Profile Photo Maker.
  <a href=""{safeUnsubscribeUrl}"" style=""color:#64748b; text-decoration:underline;"">Unsubscribe</a> from marketing emails.
</p>";

        try
        {
            await SendEmailAsync(email, subject, bodyWithFooter, "marketing", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to send marketing email for user {UserId}: {Reason}", Sid(userId), S(ex.Message));
            return false;
        }
    }

    public string RenderMarketingEmailPreview(string subject, string htmlBody)
    {
        var bodyWithFooter = htmlBody + @"
<p style=""margin:24px 0 0; font-size:13px; color:#64748b;"">
  You're receiving this because you signed up for AI Profile Photo Maker.
  <a href=""#"" style=""color:#64748b; text-decoration:underline;"">Unsubscribe</a> from marketing emails.
</p>";
        return WrapEmail(subject, bodyWithFooter);
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

        var hasPostmarkToken = HasUsablePostmarkToken(_options.PostmarkServerToken);

        _logger.LogInformation("Email config for {Template}: UseApi={UseApi}, PostmarkTokenSet={PostmarkTokenSet}, SmtpHost={SmtpHost}, UsernameSet={UsernameSet}",
            template,
            _options.UseApi,
            hasPostmarkToken,
            S(_options.SmtpHost),
            !string.IsNullOrWhiteSpace(_options.Username));

        if (string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            _logger.LogWarning("Email not configured (missing FromEmail); cannot send {Template}", template);
            return;
        }

        var useApi = _options.UseApi && hasPostmarkToken;
        var wrappedHtml = WrapEmail(subject, htmlBody);
        var textBody = EmailContentBuilder.BuildTextContent(wrappedHtml);

        if (_options.UseApi && !useApi)
        {
            _logger.LogWarning("Email API enabled but missing Postmark server token; falling back to SMTP when available.");
        }
        if (useApi)
        {
            await SendEmailViaPostmarkAsync(toEmail, subject, wrappedHtml, textBody, template, userId, replyToEmail, replyToName);
            return;
        }

        var canSmtp = !string.IsNullOrWhiteSpace(_options.SmtpHost);
        if (canSmtp)
        {
            await SendEmailViaSmtpAsync(toEmail, subject, wrappedHtml, textBody, template, userId, replyToEmail, replyToName);
            return;
        }

        _logger.LogWarning("No email delivery path available (UseApi={UseApi}, PostmarkTokenSet={PostmarkTokenSet}, SmtpHost={SmtpHost}) for {Template}",
            _options.UseApi,
            hasPostmarkToken,
            S(_options.SmtpHost),
            template);
    }

    private async Task SendEmailViaPostmarkAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string textBody,
        string template,
        string? userId,
        string? replyToEmail,
        string? replyToName)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.postmarkapp.com/email");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("X-Postmark-Server-Token", _options.PostmarkServerToken);

            var payload = new Dictionary<string, object>
            {
                ["From"] = BuildPostmarkFromAddress(),
                ["To"] = toEmail,
                ["Subject"] = _options.SandboxMode ? $"[SANDBOX] {subject}" : subject,
                ["HtmlBody"] = htmlBody,
                ["Tag"] = template
            };

            if (!string.IsNullOrWhiteSpace(textBody))
            {
                payload["TextBody"] = textBody;
            }

            if (!string.IsNullOrWhiteSpace(_options.PostmarkMessageStream))
            {
                payload["MessageStream"] = _options.PostmarkMessageStream;
            }

            if (!string.IsNullOrWhiteSpace(replyToEmail))
            {
                payload["ReplyTo"] = replyToName is null ? replyToEmail : $"{replyToName} <{replyToEmail}>";
            }

            var json = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                var messageId = TryExtractPostmarkMessageId(body);
                if (!string.IsNullOrWhiteSpace(messageId))
                {
                    _logger.LogInformation("Sent {Template} email via Postmark API to {Recipient} for user {UserId} (MessageId={MessageId})",
                        template, S(toEmail), Sid(userId), S(messageId));
                }
                else
                {
                    _logger.LogInformation("Sent {Template} email via Postmark API to {Recipient} for user {UserId}", template, S(toEmail), Sid(userId));
                }
            }
            else
            {
                _logger.LogWarning("Failed Postmark API email send {Template} for user {UserId}. Status {Status}: {Body}", template, Sid(userId), response.StatusCode, S(body));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed Postmark API email send {Template} for user {UserId}: {Reason}", template, Sid(userId), S(ex.Message));
        }
    }

    private async Task SendEmailViaSmtpAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string textBody,
        string template,
        string? userId,
        string? replyToEmail,
        string? replyToName)
    {
        var hasText = !string.IsNullOrWhiteSpace(textBody);
        var hasHtml = !string.IsNullOrWhiteSpace(htmlBody);

        using var mail = new MailMessage
        {
            From = new MailAddress(_options.FromEmail!, _options.FromName ?? "AI Profile Photo Maker"),
            Subject = _options.SandboxMode ? $"[SANDBOX] {subject}" : subject,
            Body = hasText ? textBody : htmlBody,
            IsBodyHtml = !hasText && hasHtml,
            BodyEncoding = Encoding.UTF8
        };
        mail.To.Add(new MailAddress(toEmail));
        if (!string.IsNullOrWhiteSpace(replyToEmail))
        {
            mail.ReplyToList.Add(new MailAddress(replyToEmail, replyToName ?? replyToEmail));
        }

        if (hasText && hasHtml)
        {
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(htmlBody, Encoding.UTF8, MediaTypeNames.Text.Html));
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

    private static string? TryExtractPostmarkMessageId(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("MessageID", out var messageId) &&
                messageId.ValueKind == JsonValueKind.String)
            {
                return messageId.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private string BuildPostmarkFromAddress()
    {
        if (string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(_options.FromName))
        {
            return _options.FromEmail;
        }

        return $"{_options.FromName} <{_options.FromEmail}>";
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

    private static bool HasUsablePostmarkToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        return !IsPlaceholderValue(token);
    }

    private static bool IsPlaceholderValue(string value)
    {
        return value.StartsWith("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase)
               || value.Contains("STORED_IN_USER_SECRETS", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("your_", StringComparison.OrdinalIgnoreCase);
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
