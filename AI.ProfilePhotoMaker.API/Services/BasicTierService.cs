using System.Linq.Expressions;
using System.Reflection;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace AI.ProfilePhotoMaker.API.Services;

public class BasicTierService : IBasicTierService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BasicTierService> _logger;
    private const int WeeklyCredits = 5;
    private const int DaysInWeek = 7;
    private static readonly MethodInfo? ExecuteUpdateAsyncMethod = typeof(RelationalQueryableExtensions)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .FirstOrDefault(method => method.Name == "ExecuteUpdateAsync" && method.GetParameters().Length == 3);

    public BasicTierService(ApplicationDbContext context, ILogger<BasicTierService> logger)
    {
        _context = context;
        _logger = logger;
    }

    private static Task<int>? ExecuteUpdateAsyncIfAvailable<T>(
        IQueryable<T> query,
        Expression<Func<SetPropertyCalls<T>, SetPropertyCalls<T>>> updateExpression,
        CancellationToken cancellationToken)
    {
        if (ExecuteUpdateAsyncMethod == null)
        {
            return null;
        }

        try
        {
            var method = ExecuteUpdateAsyncMethod.MakeGenericMethod(typeof(T));
            return method.Invoke(null, new object?[] { query, updateExpression, cancellationToken }) as Task<int>;
        }
        catch (TargetInvocationException)
        {
            return null;
        }
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

    public Task<CreditConsumptionResult> ConsumeCreditsAsync(string userId, string action = "basic_generation", string? correlationId = null)
    {
        var creditCost = CreditCostConfig.GetCreditCost(action);
        var canUseWeeklyCredits = CreditCostConfig.CanUseWeeklyCredits(action);
        return ConsumeCreditsInternalAsync(userId, creditCost, action, canUseWeeklyCredits, correlationId);
    }

    public Task<CreditConsumptionResult> ConsumeCreditsAsync(string userId, int customAmount, string action = "styled_generation", string? correlationId = null)
    {
        var canUseWeeklyCredits = CreditCostConfig.CanUseWeeklyCredits(action);
        return ConsumeCreditsInternalAsync(userId, customAmount, action, canUseWeeklyCredits, correlationId);
    }

    private async Task<CreditConsumptionResult> ConsumeCreditsInternalAsync(string userId, int creditCost, string action, bool canUseWeeklyCredits, string? correlationId)
    {
        if (creditCost <= 0)
        {
            _logger.LogWarning("Rejected credit consumption for user {UserId}: non-positive creditCost {CreditCost} for action {Action}", userId, creditCost, action);
            return CreditConsumptionResult.Failed(action, "invalid_credit_cost", correlationId);
        }

        var profile = await GetUserProfileWithCreditsAsync(userId);
        if (profile == null)
        {
            _logger.LogWarning("User profile not found for user {UserId}", userId);
            return CreditConsumptionResult.Failed(action, "profile_not_found", correlationId);
        }

        // Check if credits need to be reset first
        if (ShouldResetCredits(profile.LastCreditReset))
        {
            await ResetWeeklyCreditsAsync(userId);
            profile = await GetUserProfileWithCreditsAsync(userId); // Refresh after reset
        }

        if (profile == null)
        {
            _logger.LogWarning("User profile not found after reset for user {UserId}", userId);
            return CreditConsumptionResult.Failed(action, "profile_not_found_post_reset", correlationId);
        }

        var totalAvailableCredits = profile.PurchasedCredits + (canUseWeeklyCredits ? profile.Credits : 0);

        if (totalAvailableCredits < creditCost)
        {
            _logger.LogWarning("Insufficient credits for user {UserId}. Available: {Available} (Purchased: {Purchased}, Weekly: {Weekly}), Required: {Required} for {Action}",
                userId, totalAvailableCredits, profile.PurchasedCredits, canUseWeeklyCredits ? profile.Credits : 0, creditCost, action);
            return CreditConsumptionResult.Failed(action, "insufficient_credits", correlationId);
        }

        // Prioritize weekly credits first, then purchased credits as fallback
        var creditsToConsume = creditCost;
        var consumedFromPurchased = 0;
        var consumedFromWeekly = 0;
        var startingWeeklyCredits = profile.Credits;
        var startingPurchasedCredits = profile.PurchasedCredits;

        // First, use weekly credits if operation allows
        if (canUseWeeklyCredits && startingWeeklyCredits > 0)
        {
            consumedFromWeekly = Math.Min(creditsToConsume, startingWeeklyCredits);
            creditsToConsume -= consumedFromWeekly;
        }

        // Then use purchased credits if still need credits
        if (creditsToConsume > 0 && startingPurchasedCredits > 0)
        {
            consumedFromPurchased = Math.Min(creditsToConsume, startingPurchasedCredits);
            creditsToConsume -= consumedFromPurchased;
        }

        if (creditsToConsume > 0)
        {
            _logger.LogError("Credit consumption calculation error for user {UserId}", userId);
            return CreditConsumptionResult.Failed(action, "calculation_error", correlationId);
        }

        var updatedAt = DateTime.UtcNow;
        var newWeeklyCredits = startingWeeklyCredits - consumedFromWeekly;
        var newPurchasedCredits = startingPurchasedCredits - consumedFromPurchased;
        var rowsAffected = 0;

        var useExecuteUpdate = _context.Database.IsRelational();
        if (useExecuteUpdate)
        {
            var updateExpression = (Expression<Func<SetPropertyCalls<UserProfile>, SetPropertyCalls<UserProfile>>>)(updates => updates
                .SetProperty(p => p.Credits, newWeeklyCredits)
                .SetProperty(p => p.PurchasedCredits, newPurchasedCredits)
                .SetProperty(p => p.UpdatedAt, updatedAt));

            var executeUpdateTask = ExecuteUpdateAsyncIfAvailable(
                _context.UserProfiles.Where(p => p.UserId == userId),
                updateExpression,
                CancellationToken.None);

            if (executeUpdateTask == null)
            {
                _logger.LogWarning("ExecuteUpdateAsync not available; falling back to tracked update for user {UserId}", userId);
                useExecuteUpdate = false;
            }
            else
            {
                try
                {
                    rowsAffected = await executeUpdateTask;
                    if (rowsAffected == 1)
                    {
                        profile.Credits = newWeeklyCredits;
                        profile.PurchasedCredits = newPurchasedCredits;
                        profile.UpdatedAt = updatedAt;
                        _context.Entry(profile).State = EntityState.Unchanged;
                    }
                }
                catch (NotSupportedException ex)
                {
                    _logger.LogWarning(ex, "ExecuteUpdateAsync not supported; falling back to tracked update for user {UserId}", userId);
                    useExecuteUpdate = false;
                }
            }
        }

        if (!useExecuteUpdate)
        {
            profile.Credits = newWeeklyCredits;
            profile.PurchasedCredits = newPurchasedCredits;
            profile.UpdatedAt = updatedAt;
            rowsAffected = await _context.SaveChangesAsync();
        }

        if (rowsAffected != 1)
        {
            _logger.LogError("Failed to persist credit deduction for user {UserId}. Rows affected: {RowsAffected}", userId, rowsAffected);
            return CreditConsumptionResult.Failed(action, "credit_persistence_failed", correlationId);
        }

        // Log the usage with detailed breakdown
        var details = $"Consumed {creditCost} credits ({consumedFromPurchased} purchased + {consumedFromWeekly} weekly)";
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            details = $"{details}; correlationId={correlationId}";
        }
        var remainingCredits = newPurchasedCredits + newWeeklyCredits;
        await LogUsageAsync(userId, action, details, creditCost, remainingCredits);

        _logger.LogInformation("User {UserId} consumed {Credits} credits for {Action}. Remaining: {Remaining} ({Purchased} purchased + {Weekly} weekly)",
            userId, creditCost, action, remainingCredits, newPurchasedCredits, newWeeklyCredits);

        return CreditConsumptionResult.Succeeded(action, consumedFromWeekly, consumedFromPurchased, correlationId);
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

    public async Task<bool> RefundCreditsAsync(string userId, CreditConsumptionResult? consumptionResult)
    {
        if (consumptionResult == null || !consumptionResult.Success || consumptionResult.TotalCreditsConsumed <= 0)
        {
            _logger.LogDebug("No credits to refund for user {UserId}", userId);
            return true;
        }

        var correlationId = consumptionResult.CorrelationId;
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            var chargeAction = NormalizeChargeAction(consumptionResult.Action);
            var refundAction = BuildRefundAction(consumptionResult.Action);

            var chargeExists = await _context.UsageLogs
                .AnyAsync(l => l.UserId == userId &&
                               l.Action == chargeAction &&
                               l.CreditsCost.HasValue &&
                               l.CreditsCost > 0 &&
                               l.Details != null &&
                               l.Details.Contains($"correlationId={correlationId}"));

            if (!chargeExists)
            {
                _logger.LogWarning(
                    "Skipping refund for user {UserId} action {Action} with correlationId {CorrelationId}: no charge usage log found",
                    userId,
                    chargeAction,
                    correlationId);
                return true;
            }

            var refundExists = await _context.UsageLogs
                .AnyAsync(l => l.UserId == userId &&
                               l.Action == refundAction &&
                               l.Details != null &&
                               l.Details.Contains($"correlationId={correlationId}"));

            if (refundExists)
            {
                _logger.LogInformation(
                    "Skipping duplicate refund for user {UserId} action {Action} with correlationId {CorrelationId}",
                    userId,
                    refundAction,
                    correlationId);
                return true;
            }
        }

        var profile = await GetUserProfileWithCreditsAsync(userId);
        if (profile == null)
        {
            _logger.LogWarning("User profile not found for user {UserId} when refunding credits", userId);
            return false;
        }

        var weeklyBefore = profile.Credits;
        var purchasedBefore = profile.PurchasedCredits;

        var weeklyToRefund = consumptionResult.WeeklyCreditsConsumed;
        var purchasedToRefund = consumptionResult.PurchasedCreditsConsumed;

        if (weeklyToRefund > 0)
        {
            var weeklyRoom = WeeklyCredits - profile.Credits;
            if (weeklyRoom > 0)
            {
                var refundWeekly = Math.Min(weeklyToRefund, weeklyRoom);
                profile.Credits += refundWeekly;
                weeklyToRefund -= refundWeekly;
            }

            if (weeklyToRefund > 0)
            {
                // Weekly bucket already reset elsewhere; roll remainder into purchased credits so nothing is lost
                purchasedToRefund += weeklyToRefund;
                weeklyToRefund = 0;
            }
        }

        if (purchasedToRefund > 0)
        {
            profile.PurchasedCredits += purchasedToRefund;
        }

        profile.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var weeklyRefunded = profile.Credits - weeklyBefore;
        var purchasedRefunded = profile.PurchasedCredits - purchasedBefore;

        _logger.LogInformation(
            "Refunded {Total} credits to user {UserId} for {Action} (weekly +{Weekly}, purchased +{Purchased})",
            consumptionResult.TotalCreditsConsumed,
            userId,
            consumptionResult.Action,
            weeklyRefunded,
            purchasedRefunded);

        // Log refunds so UI can display credited amounts during failure states.
        // Use a stable action name: if caller already uses a *_refund action, keep it; otherwise suffix _refund.
        try
        {
            var totalRefunded = weeklyRefunded + purchasedRefunded;
            if (totalRefunded > 0)
            {
                var action = BuildRefundAction(consumptionResult.Action);
                var details = $"Refunded {totalRefunded} credits ({purchasedRefunded} purchased + {weeklyRefunded} weekly)";
                if (!string.IsNullOrWhiteSpace(consumptionResult.CorrelationId))
                {
                    details = $"{details}; correlationId={consumptionResult.CorrelationId}";
                }
                var remainingCredits = profile.PurchasedCredits + profile.Credits;
                await LogUsageAsync(userId, action, details, -totalRefunded, remainingCredits);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log credit refund usage for user {UserId}", userId);
        }

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
        var profile = await _context.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile == null)
        {
            return null;
        }

        var updated = UpgradeWeeklyCreditsIfNeeded(profile);
        if (updated)
        {
            await _context.SaveChangesAsync();
        }

        return profile;
    }

    /// <summary>
    /// Ensures stored weekly credits stay within the valid Basic-tier bounds.
    /// We intentionally avoid auto-topping balances back to the weekly allowance
    /// so that completed operations permanently consume credits until the next reset.
    /// </summary>
    private bool UpgradeWeeklyCreditsIfNeeded(UserProfile profile)
    {
        if (profile.SubscriptionTier != SubscriptionTier.Basic)
        {
            return false;
        }

        var updated = false;

        if (profile.Credits > WeeklyCredits)
        {
            profile.Credits = WeeklyCredits;
            updated = true;
        }
        else if (profile.Credits < 0)
        {
            profile.Credits = 0;
            updated = true;
        }

        if (updated)
        {
            profile.UpdatedAt = DateTime.UtcNow;
        }

        return updated;
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

    private static string NormalizeChargeAction(string action)
    {
        const string refundSuffix = "_refund";
        return action.EndsWith(refundSuffix, StringComparison.OrdinalIgnoreCase)
            ? action[..^refundSuffix.Length]
            : action;
    }

    private static string BuildRefundAction(string action)
    {
        const string refundSuffix = "_refund";
        return action.EndsWith(refundSuffix, StringComparison.OrdinalIgnoreCase)
            ? action
            : $"{action}{refundSuffix}";
    }
}
