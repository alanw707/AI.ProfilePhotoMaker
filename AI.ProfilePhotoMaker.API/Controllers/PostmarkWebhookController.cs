using System.Text.Json;
using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AI.ProfilePhotoMaker.API.Controllers;

/// <summary>
/// Receives Postmark delivery event webhooks (bounce, spam complaint).
/// Configure the Postmark webhook URL to:
///   POST https://api.aiprofilephotomaker.com/api/webhooks/postmark
/// And set the Authorization header to: Bearer {Email:PostmarkWebhookSecret}
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/webhooks/postmark")]
public class PostmarkWebhookController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<PostmarkWebhookController> _logger;
    private static string S(string? v) => LoggingSanitizer.Sanitize(v);
    private static string Sid(string? v) => LoggingSanitizer.SanitizeId(v);

    public PostmarkWebhookController(
        ApplicationDbContext db,
        IOptions<EmailOptions> emailOptions,
        ILogger<PostmarkWebhookController> logger)
    {
        _db = db;
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        // Verify shared secret if configured
        if (!string.IsNullOrWhiteSpace(_emailOptions.PostmarkWebhookSecret))
        {
            var authHeader = Request.Headers.Authorization.ToString();
            var expected = $"Bearer {_emailOptions.PostmarkWebhookSecret}";
            if (!string.Equals(authHeader, expected, StringComparison.Ordinal))
            {
                _logger.LogWarning("Postmark webhook received with invalid Authorization header");
                return Unauthorized();
            }
        }

        string body;
        using (var reader = new System.IO.StreamReader(Request.Body))
        {
            body = await reader.ReadToEndAsync(cancellationToken);
        }

        JsonElement root;
        try
        {
            root = JsonSerializer.Deserialize<JsonElement>(body);
        }
        catch
        {
            _logger.LogWarning("Postmark webhook: failed to parse JSON body");
            return BadRequest();
        }

        var recordType = root.TryGetProperty("RecordType", out var rt) ? rt.GetString() : null;
        var email = root.TryGetProperty("Email", out var em) ? em.GetString() : null;
        var messageId = root.TryGetProperty("MessageID", out var mid) ? mid.GetString() : null;

        if (string.IsNullOrWhiteSpace(recordType) || string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("Postmark webhook: missing RecordType or Email");
            return Ok(); // Return 200 so Postmark doesn't retry
        }

        _logger.LogInformation(
            "Postmark webhook received: {RecordType} for {Email} (MessageId={MessageId})",
            recordType,
            S(email),
            S(messageId));

        switch (recordType)
        {
            case "Bounce":
                var bounceType = root.TryGetProperty("Type", out var bt) ? bt.GetString() : null;
                await HandleBounceAsync(email, messageId, bounceType, cancellationToken);
                break;

            case "SpamComplaint":
                await HandleSpamComplaintAsync(email, cancellationToken);
                break;

            case "SubscriptionChange":
                var suppressed = root.TryGetProperty("SuppressSending", out var ss) && ss.GetBoolean();
                if (suppressed)
                    await HandleUnsubscribeAsync(email, "SubscriptionChange", cancellationToken);
                break;

            default:
                _logger.LogDebug("Postmark webhook: unhandled RecordType {RecordType}", recordType);
                break;
        }

        return Ok(new { received = true });
    }

    private async Task HandleBounceAsync(string email, string? messageId, string? bounceType, CancellationToken ct)
    {
        // Hard bounces indicate the address is permanently invalid — auto-unsubscribe
        var isHard = bounceType is "HardBounce" or "BadEmailAddress" or "ManuallyDeactivated";
        var updatedLogs = new List<MarketingEmailLog>();

        if (!string.IsNullOrWhiteSpace(messageId))
        {
            var matchedLog = await _db.MarketingEmailLogs
                .SingleOrDefaultAsync(l => l.PostmarkMessageId == messageId, ct);

            if (matchedLog != null)
            {
                if (matchedLog.Status == MarketingEmailStatus.Sent)
                {
                    matchedLog.Status = MarketingEmailStatus.Bounced;
                    updatedLogs.Add(matchedLog);
                }
            }
            else
            {
                _logger.LogWarning(
                    "Postmark bounce for {Email} referenced unknown MessageId {MessageId}; skipping log status update",
                    S(email),
                    S(messageId));
            }
        }
        else
        {
            var legacyLogs = await _db.MarketingEmailLogs
                .Where(l => l.Email == email && l.Status == MarketingEmailStatus.Sent && l.PostmarkMessageId == null)
                .ToListAsync(ct);

            foreach (var log in legacyLogs)
            {
                log.Status = MarketingEmailStatus.Bounced;
                updatedLogs.Add(log);
            }
        }

        if (isHard)
        {
            await HandleUnsubscribeAsync(email, $"HardBounce:{bounceType}", ct);
        }
        else if (updatedLogs.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        _logger.LogInformation(
            "Postmark bounce ({BounceType}) for {Email}: {LogCount} log(s) updated, unsubscribed={Unsubscribed}",
            bounceType, S(email), updatedLogs.Count, isHard);
    }

    private Task HandleSpamComplaintAsync(string email, CancellationToken ct)
        => HandleUnsubscribeAsync(email, "SpamComplaint", ct);

    private async Task HandleUnsubscribeAsync(string email, string reason, CancellationToken ct)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user == null)
        {
            _logger.LogInformation("Postmark {Reason}: no user found for {Email}", reason, S(email));
            return;
        }

        if (!user.MarketingOptOut)
        {
            user.MarketingOptOut = true;
            user.MarketingOptOutAt = DateTime.UtcNow;
        }

        // Also mark any pending logs as unsubscribed
        var pendingLogs = await _db.MarketingEmailLogs
            .Where(l => l.UserId == user.Id && l.Status == MarketingEmailStatus.Pending)
            .ToListAsync(ct);

        foreach (var log in pendingLogs)
            log.Status = MarketingEmailStatus.Unsubscribed;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Postmark {Reason}: user {UserId} opted out of marketing",
            reason, Sid(user.Id));
    }
}
