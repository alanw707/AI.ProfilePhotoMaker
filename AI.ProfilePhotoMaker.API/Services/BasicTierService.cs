using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services;

public class BasicTierService : IBasicTierService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BasicTierService> _logger;
    private const int WeeklyCredits = 3;
    private const int DaysInWeek = 7;

    public BasicTierService(ApplicationDbContext context, ILogger<BasicTierService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> HasAvailableCreditsAsync(string userId)
    {
        var profile = await GetUserProfileWithCreditsAsync(userId);
        if (profile == null) return false;

        // Check if credits need to be reset (weekly reset)
        if (ShouldResetCredits(profile.LastCreditReset))
        {
            await ResetWeeklyCreditsAsync(userId);
            profile = await GetUserProfileWithCreditsAsync(userId); // Refresh after reset
        }

        return (profile?.Credits ?? 0) > 0 || (profile?.PurchasedCredits ?? 0) > 0;
    }

    public async Task<int> GetAvailableCreditsAsync(string userId)
    {
        var profile = await GetUserProfileWithCreditsAsync(userId);
        if (profile == null) return 0;

        // Check if credits need to be reset (weekly reset)
        if (ShouldResetCredits(profile.LastCreditReset))
        {
            await ResetWeeklyCreditsAsync(userId);
            profile = await GetUserProfileWithCreditsAsync(userId); // Refresh after reset
        }

        return (profile?.Credits ?? 0) + (profile?.PurchasedCredits ?? 0);
    }

    public async Task<(int weeklyCredits, int purchasedCredits)> GetCreditBreakdownAsync(string userId)
    {
        var profile = await GetUserProfileWithCreditsAsync(userId);
        if (profile == null) return (0, 0);

        // Check if credits need to be reset (weekly reset)
        if (ShouldResetCredits(profile.LastCreditReset))
        {
            await ResetWeeklyCreditsAsync(userId);
            profile = await GetUserProfileWithCreditsAsync(userId); // Refresh after reset
        }

        return (profile?.Credits ?? 0, profile?.PurchasedCredits ?? 0);
    }

    public async Task<bool> ConsumeCreditsAsync(string userId, string action = "basic_generation")
    {
        var profile = await GetUserProfileWithCreditsAsync(userId);
        if (profile == null)
        {
            _logger.LogWarning("User profile not found for user {UserId}", userId);
            return false;
        }

        // Get the credit cost for this action
        var creditCost = CreditCostConfig.GetCreditCost(action);
        var canUseWeeklyCredits = CreditCostConfig.CanUseWeeklyCredits(action);

        // Check if credits need to be reset first
        if (ShouldResetCredits(profile.LastCreditReset))
        {
            await ResetWeeklyCreditsAsync(userId);
            profile = await GetUserProfileWithCreditsAsync(userId); // Refresh after reset
        }

        if (profile == null)
        {
            _logger.LogWarning("User profile not found after reset for user {UserId}", userId);
            return false;
        }

        var totalAvailableCredits = profile.PurchasedCredits + (canUseWeeklyCredits ? profile.Credits : 0);

        if (totalAvailableCredits < creditCost)
        {
            _logger.LogWarning("Insufficient credits for user {UserId}. Available: {Available} (Purchased: {Purchased}, Weekly: {Weekly}), Required: {Required} for {Action}", 
                userId, totalAvailableCredits, profile.PurchasedCredits, canUseWeeklyCredits ? profile.Credits : 0, creditCost, action);
            return false;
        }

        // Prioritize purchased credits first, then weekly credits (for basic operations only)
        var creditsToConsume = creditCost;
        var consumedFromPurchased = 0;
        var consumedFromWeekly = 0;

        // First, use purchased credits if available
        if (profile.PurchasedCredits > 0)
        {
            consumedFromPurchased = Math.Min(creditsToConsume, profile.PurchasedCredits);
            profile.PurchasedCredits -= consumedFromPurchased;
            creditsToConsume -= consumedFromPurchased;
        }

        // Then use weekly credits if operation allows and still need credits
        if (creditsToConsume > 0 && canUseWeeklyCredits && profile.Credits > 0)
        {
            consumedFromWeekly = Math.Min(creditsToConsume, profile.Credits);
            profile.Credits -= consumedFromWeekly;
            creditsToConsume -= consumedFromWeekly;
        }

        if (creditsToConsume > 0)
        {
            _logger.LogError("Credit consumption calculation error for user {UserId}", userId);
            return false;
        }

        profile.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Log the usage with detailed breakdown
        var details = $"Consumed {creditCost} credits ({consumedFromPurchased} purchased + {consumedFromWeekly} weekly)";
        var remainingCredits = profile.PurchasedCredits + profile.Credits;
        await LogUsageAsync(userId, action, details, creditCost, remainingCredits);

        _logger.LogInformation("User {UserId} consumed {Credits} credits for {Action}. Remaining: {Remaining} ({Purchased} purchased + {Weekly} weekly)", 
            userId, creditCost, action, remainingCredits, profile.PurchasedCredits, profile.Credits);

        return true;
    }

    public async Task<bool> AddPurchasedCreditsAsync(string userId, int credits, string source = "credit_purchase")
    {
        var profile = await GetUserProfileWithCreditsAsync(userId);
        if (profile == null)
        {
            _logger.LogWarning("User profile not found for user {UserId} when adding purchased credits", userId);
            return false;
        }

        profile.PurchasedCredits += credits;
        profile.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Log the credit addition
        await LogUsageAsync(userId, source, $"Added {credits} purchased credits", -credits, profile.PurchasedCredits + profile.Credits);

        _logger.LogInformation("Added {Credits} purchased credits to user {UserId}. New total: {Total} ({Purchased} purchased + {Weekly} weekly)", 
            credits, userId, profile.PurchasedCredits + profile.Credits, profile.PurchasedCredits, profile.Credits);

        return true;
    }

    public async Task ResetWeeklyCreditsAsync(string userId)
    {
        var profile = await GetUserProfileWithCreditsAsync(userId);
        if (profile == null)
        {
            _logger.LogWarning("User profile not found for user {UserId} during credit reset", userId);
            return;
        }

        profile.Credits = WeeklyCredits;
        profile.LastCreditReset = DateTime.UtcNow;
        profile.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Log the reset
        await LogUsageAsync(userId, "credit_reset", $"Weekly credits reset to {WeeklyCredits}", 0, WeeklyCredits);

        _logger.LogInformation("Reset weekly credits for user {UserId} to {Credits}", userId, WeeklyCredits);
    }

    public async Task ResetAllExpiredCreditsAsync()
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-DaysInWeek);
        
        var expiredProfiles = await _context.UserProfiles
            .Where(p => p.LastCreditReset < cutoffDate && p.SubscriptionTier == SubscriptionTier.Basic)
            .ToListAsync();

        foreach (var profile in expiredProfiles)
        {
            profile.Credits = WeeklyCredits;
            profile.LastCreditReset = DateTime.UtcNow;
            profile.UpdatedAt = DateTime.UtcNow;

            // Log the reset
            await LogUsageAsync(profile.UserId, "credit_reset", $"Weekly credits reset to {WeeklyCredits} (batch job)", 0, WeeklyCredits);
        }

        if (expiredProfiles.Any())
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Reset credits for {Count} users in batch job", expiredProfiles.Count);
        }
    }

    public async Task<bool> CanUserGenerateAsync(string userId)
    {
        var profile = await GetUserProfileWithCreditsAsync(userId);
        if (profile == null) return false;

        // Premium users can always generate
        if (profile.SubscriptionTier != SubscriptionTier.Basic)
            return true;

        // Basic users need available credits
        return await HasAvailableCreditsAsync(userId);
    }

    public async Task<UserProfile?> GetUserProfileWithCreditsAsync(string userId)
    {
        return await _context.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);
    }

    public async Task LogUsageAsync(string userId, string action, string? details = null, int? creditsCost = null, int? creditsRemaining = null)
    {
        try
        {
            var usageLog = new UsageLog
            {
                UserId = userId,
                Action = action,
                Details = details,
                CreditsCost = creditsCost,
                CreditsRemaining = creditsRemaining,
                CreatedAt = DateTime.UtcNow
            };

            _context.UsageLogs.Add(usageLog);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log usage for user {UserId}, action {Action}", userId, action);
        }
    }

    private static bool ShouldResetCredits(DateTime lastReset)
    {
        var daysSinceReset = (DateTime.UtcNow - lastReset).TotalDays;
        return daysSinceReset >= DaysInWeek;
    }
}