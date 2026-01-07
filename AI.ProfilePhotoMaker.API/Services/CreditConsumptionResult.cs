namespace AI.ProfilePhotoMaker.API.Services;

public class CreditConsumptionResult
{
    public static CreditConsumptionResult Failed(string action, string? reason = null, string? correlationId = null) =>
        new(action, false, 0, reason, correlationId);

    public static CreditConsumptionResult Succeeded(string action, int creditsConsumed, string? correlationId = null) =>
        new(action, true, creditsConsumed, error: null, correlationId);

    private CreditConsumptionResult(string action, bool success, int creditsConsumed, string? error = null, string? correlationId = null)
    {
        Action = action;
        Success = success;
        CreditsConsumed = creditsConsumed;
        Error = error;
        CorrelationId = correlationId;
    }

    public string Action { get; }
    public bool Success { get; }
    public int CreditsConsumed { get; }
    public string? Error { get; }
    public string? CorrelationId { get; }
    public int TotalCreditsConsumed => CreditsConsumed;
}
