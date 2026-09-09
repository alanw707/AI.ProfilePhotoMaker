using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;

namespace AI.ProfilePhotoMaker.API.Services;

public interface ICreditPackageService
{
    Task<IEnumerable<CreditPackageDto>> GetActiveCreditPackagesAsync();
    Task<CreditPurchaseResult> PurchaseCreditPackageAsync(
        string userId,
        int packageId,
        string? paymentTransactionId = null,
        int? previewProcessedImageId = null,
        string? webhookOperationKey = null,
        string? webhookOperationToken = null);
    Task<IEnumerable<CreditPurchase>> GetUserPurchaseHistoryAsync(string userId);
}

public record CreditPurchaseResult(bool Success, PaymentStatus Status, CreditPurchase? Purchase, string? ErrorCode = null, string? ErrorMessage = null);
