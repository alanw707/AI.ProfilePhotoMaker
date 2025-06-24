using AI.ProfilePhotoMaker.API.Models;

namespace AI.ProfilePhotoMaker.API.Services;

public interface IBasicTierService
{
    Task<bool> HasAvailableCreditsAsync(string userId);
    Task<int> GetAvailableCreditsAsync(string userId);
    Task<(int weeklyCredits, int purchasedCredits)> GetCreditBreakdownAsync(string userId);
    Task<bool> ConsumeCreditsAsync(string userId, string action = "basic_generation");
    Task<bool> AddPurchasedCreditsAsync(string userId, int credits, string source = "credit_purchase");
    Task ResetWeeklyCreditsAsync(string userId);
    Task ResetAllExpiredCreditsAsync();
    Task<bool> CanUserGenerateAsync(string userId);
    Task<UserProfile?> GetUserProfileWithCreditsAsync(string userId);
    Task LogUsageAsync(string userId, string action, string? details = null, int? creditsCost = null, int? creditsRemaining = null);
}