namespace AI.ProfilePhotoMaker.API.Services.Payments.Models;

public record PaymentIntentResponse(
    string PaymentIntentId,
    string ClientSecret,
    string PublishableKey,
    string PaymentTransactionId);
