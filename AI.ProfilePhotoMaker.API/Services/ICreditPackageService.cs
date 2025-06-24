using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;

namespace AI.ProfilePhotoMaker.API.Services;

public interface ICreditPackageService
{
    Task<IEnumerable<CreditPackageDto>> GetActiveCreditPackagesAsync();
    Task<CreditPurchase?> PurchaseCreditPackageAsync(string userId, int packageId, string? paymentTransactionId = null);
    Task<IEnumerable<CreditPurchase>> GetUserPurchaseHistoryAsync(string userId);
}