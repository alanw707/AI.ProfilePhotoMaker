using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;

namespace AI.ProfilePhotoMaker.API.Services;

public interface IPremiumPackageService
{
    Task<IEnumerable<PremiumPackageDto>> GetActivePackagesAsync();
    Task<UserPackageStatusDto> GetUserPackageStatusAsync(string userId);
    Task<UserPackagePurchase?> PurchasePackageAsync(string userId, int packageId, string? paymentTransactionId = null);
    Task<bool> ConsumeCreditsAsync(string userId, int credits, string action);
    Task<bool> HasAvailableCreditsAsync(string userId, int requiredCredits = 1);
    Task<UserPackagePurchase?> GetActiveUserPackageAsync(string userId);
    Task<bool> UpdateTrainedModelAsync(string userId, string modelId);
    Task CleanupExpiredModelsAsync();
    Task<bool> CanSelectStylesAsync(string userId, int styleCount);
    Task<bool> CanGenerateImagesAsync(string userId, int imageCount);
}