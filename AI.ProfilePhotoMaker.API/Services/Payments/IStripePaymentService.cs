using AI.ProfilePhotoMaker.API.Services.Payments.Models;

namespace AI.ProfilePhotoMaker.API.Services.Payments;

public interface IStripePaymentService
{
    Task<PaymentIntentResponse> CreatePaymentIntentAsync(string userId, int packageId, string? couponCode = null, CancellationToken cancellationToken = default);
}
