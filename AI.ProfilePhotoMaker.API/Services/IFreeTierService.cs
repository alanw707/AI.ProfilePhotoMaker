using AI.ProfilePhotoMaker.API.Models;

namespace AI.ProfilePhotoMaker.API.Services;

public interface IFreeTierService
{
    Task<bool> HasAvailableCreditsAsync(string userId);
    Task<int> GetAvailableCreditsAsync(string userId);
    Task<bool> ConsumeCreditsAsync(string userId, int credits = 1, string action = "free_generation");
    Task ResetWeeklyCreditsAsync(string userId);
    Task ResetAllExpiredCreditsAsync();
    Task<bool> CanUserGenerateAsync(string userId);
    Task<UserProfile?> GetUserProfileWithCreditsAsync(string userId);
    Task LogUsageAsync(string userId, string action, string? details = null, int? creditsCost = null, int? creditsRemaining = null);
}