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

        return (profile?.Credits ?? 0) > 0;
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

        return profile?.Credits ?? 0;
    }

    public Task<CreditConsumptionResult> ConsumeCreditsAsync(string userId, string action = "basic_generation", string? correlationId = null, CancellationToken cancellationToken = default)
    {
        var creditCost = CreditCostConfig.GetCreditCost(action);
        return ConsumeCreditsInternalAsync(userId, creditCost, action, correlationId, cancellationToken);
    }

    public Task<CreditConsumptionResult> ConsumeCreditsAsync(string userId, int customAmount, string action = "styled_generation", string? correlationId = null, CancellationToken cancellationToken = default)
    {
        return ConsumeCreditsInternalAsync(userId, customAmount, action, correlationId, cancellationToken);
    }
    private async Task<CreditConsumptionResult> ConsumeCreditsInternalAsync(string userId, int creditCost, string action, string? correlationId, CancellationToken cancellationToken)
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

        var totalAvailableCredits = profile.Credits;

        if (totalAvailableCredits < creditCost)
        {
            _logger.LogWarning("Insufficient credits for user {UserId}. Available: {Available}, Required: {Required} for {Action}",
                userId, totalAvailableCredits, creditCost, action);
            return CreditConsumptionResult.Failed(action, "insufficient_credits", correlationId);
        }

        var updatedAt = DateTime.UtcNow;
        var newCredits = totalAvailableCredits - creditCost;
        var rowsAffected = 0;
        int? persistedCredits = null;

        var useExecuteUpdate = _context.Database.IsRelational();
        if (useExecuteUpdate)
        {
            var updateExpression = (Expression<Func<SetPropertyCalls<UserProfile>, SetPropertyCalls<UserProfile>>>)(updates => updates
                .SetProperty(p => p.Credits, p => p.Credits - creditCost)
                .SetProperty(p => p.UpdatedAt, updatedAt));

            var executeUpdateTask = ExecuteUpdateAsyncIfAvailable(
                _context.UserProfiles.Where(p => p.UserId == userId && p.Credits >= creditCost),
                updateExpression,
                cancellationToken);

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
                        persistedCredits = await _context.UserProfiles
                            .Where(p => p.UserId == userId)
                            .Select(p => (int?)p.Credits)
                            .FirstOrDefaultAsync();
                        profile.Credits = persistedCredits ?? newCredits;
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
            profile.Credits = newCredits;
            profile.UpdatedAt = updatedAt;
            rowsAffected = await _context.SaveChangesAsync(cancellationToken);
            persistedCredits = profile.Credits;
        }

        if (rowsAffected != 1)
        {
            if (useExecuteUpdate)
            {
                _logger.LogWarning("Failed to deduct credits for user {UserId} due to insufficient balance or concurrent update", userId);
                return CreditConsumptionResult.Failed(action, "insufficient_credits", correlationId);
            }

            _logger.LogError("Failed to persist credit deduction for user {UserId}. Rows affected: {RowsAffected}", userId, rowsAffected);
            return CreditConsumptionResult.Failed(action, "credit_persistence_failed", correlationId);
        }

        var details = $"Consumed {creditCost} credits";
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            details = $"{details}; correlationId={correlationId}";
        }
        var remainingCredits = persistedCredits ?? newCredits;
        await LogUsageAsync(userId, action, details, creditCost, remainingCredits);

        _logger.LogInformation("User {UserId} consumed {Credits} credits for {Action}. Remaining: {Remaining}",
            userId, creditCost, action, remainingCredits);

        return CreditConsumptionResult.Succeeded(action, creditCost, correlationId);
    }

    public async Task<bool> AddPurchasedCreditsAsync(string userId, int credits, string source = "credit_purchase")
    {
        var profile = await GetUserProfileWithCreditsAsync(userId);
        if (profile == null)
        {
            _logger.LogWarning("User profile not found for user {UserId} when adding credits", userId);
            return false;
        }

        profile.Credits += credits;
        profile.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Log the credit addition
        await LogUsageAsync(userId, source, $"Added {credits} credits", -credits, profile.Credits);

        _logger.LogInformation("Added {Credits} credits to user {UserId}. New total: {Total}",
            credits, userId, profile.Credits);

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

        var creditsBefore = profile.Credits;
        var creditsToRefund = consumptionResult.CreditsConsumed;
        if (creditsToRefund > 0)
        {
            profile.Credits += creditsToRefund;
        }

        profile.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var creditsRefunded = profile.Credits - creditsBefore;

        _logger.LogInformation(
            "Refunded {Total} credits to user {UserId} for {Action}",
            creditsRefunded,
            userId,
            consumptionResult.Action);

        // Log refunds so UI can display credited amounts during failure states.
        // Use a stable action name: if caller already uses a *_refund action, keep it; otherwise suffix _refund.
        try
        {
                if (creditsRefunded > 0)
                {
                    var action = BuildRefundAction(consumptionResult.Action);
                    var details = $"Refunded {creditsRefunded} credits";
                    if (!string.IsNullOrWhiteSpace(consumptionResult.CorrelationId))
                    {
                        details = $"{details}; correlationId={consumptionResult.CorrelationId}";
                    }
                    await LogUsageAsync(userId, action, details, -creditsRefunded, profile.Credits);
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

        var creditsBefore = profile.Credits;
        if (profile.Credits < WeeklyCredits)
        {
            profile.Credits = WeeklyCredits;
        }
        profile.LastCreditReset = DateTime.UtcNow;
        profile.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        if (profile.Credits != creditsBefore)
        {
            await LogUsageAsync(userId, "credit_reset", $"Weekly credits topped up to {WeeklyCredits}", 0, profile.Credits);
            _logger.LogInformation("Topped up credits for user {UserId} to {Credits}", userId, profile.Credits);
        }
        else
        {
            _logger.LogInformation("Weekly credit reset checked for user {UserId}; balance unchanged ({Credits})", userId, profile.Credits);
        }
    }

    public async Task ResetAllExpiredCreditsAsync()
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-DaysInWeek);

        var expiredProfiles = await _context.UserProfiles
            .Where(p => p.LastCreditReset < cutoffDate && p.SubscriptionTier == SubscriptionTier.Basic)
            .ToListAsync();

        foreach (var profile in expiredProfiles)
        {
            var creditsBefore = profile.Credits;
            if (profile.Credits < WeeklyCredits)
            {
                profile.Credits = WeeklyCredits;
            }
            profile.LastCreditReset = DateTime.UtcNow;
            profile.UpdatedAt = DateTime.UtcNow;

            if (profile.Credits != creditsBefore)
            {
                await LogUsageAsync(profile.UserId, "credit_reset", $"Weekly credits topped up to {WeeklyCredits} (batch job)", 0, profile.Credits);
            }
        }

        if (expiredProfiles.Any())
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Processed credit reset for {Count} users in batch job", expiredProfiles.Count);
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

        var updated = NormalizeCreditsIfNeeded(profile);
        if (updated)
        {
            await _context.SaveChangesAsync();
        }

        return profile;
    }

    /// <summary>
    /// Ensures stored credits never drift below zero.
    /// </summary>
    private static bool NormalizeCreditsIfNeeded(UserProfile profile)
    {
        var updated = false;

        if (profile.Credits < 0)
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
