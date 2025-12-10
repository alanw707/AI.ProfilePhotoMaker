using AI.ProfilePhotoMaker.API.Models;

namespace AI.ProfilePhotoMaker.API.Services.Notifications;

public interface IEmailNotificationService
{
    Task SendTrainingCompletedAsync(string userId, string? email, string? modelName, string? modelVersion);
    Task SendGenerationCompletedAsync(string userId, string? email, string? style, int imageCount, string? jobId = null);
    Task SendGenerationFailedAsync(string userId, string? email, string? style, string? error, string? jobId = null);
    Task SendPurchaseReceiptAsync(string userId, string? email, CreditPurchase purchase);
}
