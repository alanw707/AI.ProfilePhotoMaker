using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services;

public class FreeTierService : IFreeTierService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<FreeTierService> _logger;
    private const int WeeklyFreeCredits = 3;
    private const int DaysInWeek = 7;

    public FreeTierService(ApplicationDbContext context, ILogger<FreeTierService> logger)
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
            return WeeklyFreeCredits > 0;
        }

        return profile.FreeCredits > 0;
    }

    public async Task<int> GetAvailableCreditsAsync(string userId)
    {
        var profile = await GetUserProfileWithCreditsAsync(userId);
        if (profile == null) return 0;

        // Check if credits need to be reset (weekly reset)
        if (ShouldResetCredits(profile.LastCreditReset))
        {
            await ResetWeeklyCreditsAsync(userId);
            return WeeklyFreeCredits;
        }

        return profile.FreeCredits;
    }

    public async Task<bool> ConsumeCreditsAsync(string userId, int credits = 1, string action = "free_generation")
    {
        var profile = await GetUserProfileWithCreditsAsync(userId);
        if (profile == null)
        {
            _logger.LogWarning("User profile not found for user {UserId}", userId);
            return false;
        }

        // Check if credits need to be reset first
        if (ShouldResetCredits(profile.LastCreditReset))
        {
            await ResetWeeklyCreditsAsync(userId);
            profile = await GetUserProfileWithCreditsAsync(userId); // Refresh after reset
        }

        if (profile == null || profile.FreeCredits < credits)
        {
            _logger.LogWarning("Insufficient credits for user {UserId}. Available: {Available}, Requested: {Requested}", 
                userId, profile?.FreeCredits ?? 0, credits);
            return false;
        }

        // Consume credits
        profile.FreeCredits -= credits;
        profile.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Log the usage
        await LogUsageAsync(userId, action, $"Consumed {credits} credits", credits, profile.FreeCredits);

        _logger.LogInformation("User {UserId} consumed {Credits} credits. Remaining: {Remaining}", 
            userId, credits, profile.FreeCredits);

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

        profile.FreeCredits = WeeklyFreeCredits;
        profile.LastCreditReset = DateTime.UtcNow;
        profile.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Log the reset
        await LogUsageAsync(userId, "credit_reset", $"Weekly credits reset to {WeeklyFreeCredits}", 0, WeeklyFreeCredits);

        _logger.LogInformation("Reset weekly credits for user {UserId} to {Credits}", userId, WeeklyFreeCredits);
    }

    public async Task ResetAllExpiredCreditsAsync()
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-DaysInWeek);
        
        var expiredProfiles = await _context.UserProfiles
            .Where(p => p.LastCreditReset < cutoffDate && p.SubscriptionTier == SubscriptionTier.Free)
            .ToListAsync();

        foreach (var profile in expiredProfiles)
        {
            profile.FreeCredits = WeeklyFreeCredits;
            profile.LastCreditReset = DateTime.UtcNow;
            profile.UpdatedAt = DateTime.UtcNow;

            // Log the reset
            await LogUsageAsync(profile.UserId, "credit_reset", $"Weekly credits reset to {WeeklyFreeCredits} (batch job)", 0, WeeklyFreeCredits);
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
        if (profile.SubscriptionTier != SubscriptionTier.Free)
            return true;

        // Free users need available credits
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