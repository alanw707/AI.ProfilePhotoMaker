using System.Security.Cryptography;
using System.Text;
using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Controllers;
using AI.ProfilePhotoMaker.API.Services.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Stripe;

namespace AI.ProfilePhotoMaker.API.Tests.Controllers;

public class StripeWebhookControllerCompatibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CloverWebhook_RequiresValidSignature_AndDeserializesPaymentIntent(bool tampered)
    {
        const string secret = "whsec_unit_test_only";
        const string payload = """
            {"id":"evt_compatibility","object":"event","api_version":"2025-10-29.clover",
             "type":"payment_intent.succeeded","livemode":false,"created":1788630000,
             "pending_webhooks":1,"request":{"id":"req_fixture","idempotency_key":null},
             "data":{"object":{"id":"pi_compatibility","object":"payment_intent",
             "status":"succeeded","amount":1999,"amount_received":1999,"currency":"usd",
             "metadata":{"user_id":"test-user","package_id":"2"}}}}
            """;
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"))).ToLowerInvariant();
        var service = new Mock<IStripeWebhookService>();
        service.Setup(s => s.HandleEventAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var controller = new StripeWebhookController(Options.Create(new StripeOptions { WebhookSecret = secret }),
            service.Object, NullLogger<StripeWebhookController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(tampered ? payload.Replace("1999", "9999") : payload));
        controller.Request.Headers["Stripe-Signature"] = $"t={timestamp},v1={signature}";

        var response = await controller.Receive(default);

        if (tampered)
        {
            Assert.IsType<BadRequestObjectResult>(response);
            service.Verify(s => s.HandleEventAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        else
        {
            Assert.IsType<OkObjectResult>(response);
            service.Verify(s => s.HandleEventAsync(It.Is<Event>(e => e.Type == "payment_intent.succeeded" &&
                ((PaymentIntent)e.Data.Object).Metadata["package_id"] == "2" &&
                ((PaymentIntent)e.Data.Object).AmountReceived == 1999), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
