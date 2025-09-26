using Stripe;

namespace AI.ProfilePhotoMaker.API.Services.Payments;

public interface IStripeWebhookService
{
    Task HandleEventAsync(Event stripeEvent, CancellationToken cancellationToken = default);
}
