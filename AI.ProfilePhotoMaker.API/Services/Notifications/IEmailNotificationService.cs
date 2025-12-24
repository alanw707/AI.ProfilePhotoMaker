using AI.ProfilePhotoMaker.API.Models;

namespace AI.ProfilePhotoMaker.API.Services.Notifications;

public interface IEmailNotificationService
{
    Task SendTrainingCompletedAsync(string userId, string? email, string? modelName, string? modelVersion);
    Task SendGenerationCompletedAsync(string userId, string? email, string? style, int imageCount, string? jobId = null);
    Task SendGenerationFailedAsync(string userId, string? email, string? style, string? error, string? jobId = null);
    Task SendPurchaseReceiptAsync(string userId, string? email, CreditPurchase purchase);
    Task SendSupportFeedbackReceivedAsync(string userId, string? userEmail, FeedbackSubmission submission);
    Task SendEmailVerificationAsync(string userId, string? email, string encodedToken);
    Task SendWelcomeAsync(string userId, string? email, string? firstName = null);
    Task SendRetentionDeletionWarningAsync(string userId, string? email, int imageCount, DateTime deletionDate, int daysUntilDeletion);
}
