using System.Net;
using System.Net.Mail;
using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Models;
using Microsoft.Extensions.Options;

namespace AI.ProfilePhotoMaker.API.Services.Notifications;

public class EmailNotificationService : IEmailNotificationService
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailNotificationService> _logger;

    private static string S(string? value) => LoggingSanitizer.Sanitize(value);
    private static string Sid(string? value) => LoggingSanitizer.SanitizeId(value);

    public EmailNotificationService(IOptions<EmailOptions> options, ILogger<EmailNotificationService> logger)
    {
        _options = options.Value;
        _logger = logger;
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

    public Task SendGenerationCompletedAsync(string userId, string? email, string? style, int imageCount)
    {
        var subject = "Your images are ready";
        var body = $@"<p>Your generation request has completed.</p>
                      <p>Style: <strong>{WebUtility.HtmlEncode(style ?? "Unknown")}</strong><br/>
                      Images: <strong>{imageCount}</strong></p>
                      <p>Sign in to view and download your results.</p>";
        return SendEmailAsync(email, subject, body, "generation-completed", userId);
    }

    public Task SendGenerationFailedAsync(string userId, string? email, string? style, string? error)
    {
        var subject = "Image generation failed";
        var body = $@"<p>Your generation request did not complete.</p>
                      <p>Style: <strong>{WebUtility.HtmlEncode(style ?? "Unknown")}</strong></p>
                      <p>Error: {WebUtility.HtmlEncode(error ?? "Unknown error")}</p>
                      <p>Please retry from the dashboard.</p>";
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

        if (string.IsNullOrWhiteSpace(_options.FromEmail) || string.IsNullOrWhiteSpace(_options.SmtpHost))
        {
            _logger.LogWarning("Email not configured (missing FromEmail or SmtpHost); cannot send {Template}", template);
            return;
        }

        var mail = new MailMessage
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
            _logger.LogInformation("Sent {Template} email to {Recipient} for user {UserId}", template, S(toEmail), Sid(userId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send {Template} email to {Recipient} for user {UserId}", template, S(toEmail), Sid(userId));
        }
    }
}
