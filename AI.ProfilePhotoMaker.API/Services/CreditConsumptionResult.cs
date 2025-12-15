namespace AI.ProfilePhotoMaker.API.Services;

public class CreditConsumptionResult
{
    public static CreditConsumptionResult Failed(string action, string? reason = null, string? correlationId = null) =>
        new(action, false, 0, 0, reason, correlationId);

    public static CreditConsumptionResult Succeeded(string action, int weeklyCredits, int purchasedCredits, string? correlationId = null) =>
        new(action, true, weeklyCredits, purchasedCredits, error: null, correlationId);

    private CreditConsumptionResult(string action, bool success, int weeklyCredits, int purchasedCredits, string? error = null, string? correlationId = null)
    {
        Action = action;
        Success = success;
        WeeklyCreditsConsumed = weeklyCredits;
        PurchasedCreditsConsumed = purchasedCredits;
        Error = error;
        CorrelationId = correlationId;
    }

    public string Action { get; }
    public bool Success { get; }
    public int WeeklyCreditsConsumed { get; }
    public int PurchasedCreditsConsumed { get; }
    public string? Error { get; }
    public string? CorrelationId { get; }

    public int TotalCreditsConsumed => WeeklyCreditsConsumed + PurchasedCreditsConsumed;
}
