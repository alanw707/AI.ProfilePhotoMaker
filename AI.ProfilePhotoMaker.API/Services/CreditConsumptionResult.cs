namespace AI.ProfilePhotoMaker.API.Services;

public class CreditConsumptionResult
{
    public static CreditConsumptionResult Failed(string action, string? reason = null) => new(action, false, 0, 0, reason);

    public static CreditConsumptionResult Succeeded(string action, int weeklyCredits, int purchasedCredits) =>
        new(action, true, weeklyCredits, purchasedCredits);

    private CreditConsumptionResult(string action, bool success, int weeklyCredits, int purchasedCredits, string? error = null)
    {
        Action = action;
        Success = success;
        WeeklyCreditsConsumed = weeklyCredits;
        PurchasedCreditsConsumed = purchasedCredits;
        Error = error;
    }

    public string Action { get; }
    public bool Success { get; }
    public int WeeklyCreditsConsumed { get; }
    public int PurchasedCreditsConsumed { get; }
    public string? Error { get; }

    public int TotalCreditsConsumed => WeeklyCreditsConsumed + PurchasedCreditsConsumed;
}
