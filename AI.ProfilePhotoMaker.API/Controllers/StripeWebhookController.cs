using System.IO;
using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Infrastructure.Logging;
using AI.ProfilePhotoMaker.API.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;

namespace AI.ProfilePhotoMaker.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/webhooks/stripe")]
public class StripeWebhookController : ControllerBase
{
    private readonly StripeOptions _stripeOptions;
    private readonly IStripeWebhookService _webhookService;
    private readonly ILogger<StripeWebhookController> _logger;
    private static string S(string? value) => LoggingSanitizer.Sanitize(value);

    public StripeWebhookController(IOptions<StripeOptions> stripeOptions, IStripeWebhookService webhookService, ILogger<StripeWebhookController> logger)
    {
        _stripeOptions = stripeOptions.Value;
        _webhookService = webhookService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_stripeOptions.WebhookSecret))
        {
            _logger.LogWarning("Stripe webhook received but webhook secret is not configured");
            return StatusCode(503, new { success = false, error = new { code = "StripeNotConfigured", message = "Stripe webhook secret not configured." } });
        }

        string payload;
        using (var reader = new StreamReader(Request.Body))
        {
            payload = await reader.ReadToEndAsync();
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            return BadRequest(new { success = false, error = new { code = "EmptyPayload", message = "Webhook payload is empty." } });
        }

        if (!Request.Headers.TryGetValue("Stripe-Signature", out var stripeSignature))
        {
            return BadRequest(new { success = false, error = new { code = "MissingSignature", message = "Stripe signature header is missing." } });
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, stripeSignature, _stripeOptions.WebhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Invalid Stripe webhook signature");
            return BadRequest(new { success = false, error = new { code = "InvalidSignature", message = "Invalid Stripe webhook signature." } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Stripe webhook event");
            return BadRequest(new { success = false, error = new { code = "WebhookParseError", message = "Unable to parse Stripe webhook event." } });
        }

        await _webhookService.HandleEventAsync(stripeEvent, cancellationToken);

        return Ok(new { received = true });
    }
}
