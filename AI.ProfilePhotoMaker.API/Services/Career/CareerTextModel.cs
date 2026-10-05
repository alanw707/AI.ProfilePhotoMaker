namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>
/// What the model sees: the task, the facts pinned by the run and the tools it may
/// ask for. Profile and goal text is data for the model, never instructions.
/// </summary>
public sealed record CareerModelRequest(
    string Task,
    CareerProfileFactsDto Profile,
    CareerGoalFactsDto? Goal,
    IReadOnlyList<string> AllowedTools,
    string? Answer);

/// <summary>Either a final text or a request to call a tool, plus what the call cost.</summary>
public sealed record CareerModelResult(string? FinalText, string? ToolCall, int UsageTokens, int CostCents)
{
    public static CareerModelResult Text(string text, int usageTokens = 0, int costCents = 0) =>
        new(text, null, usageTokens, costCents);

    public static CareerModelResult Tool(string name, int usageTokens = 0, int costCents = 0) =>
        new(null, name, usageTokens, costCents);
}

/// <summary>
/// The only way agent runs reach a text model (ADR 0009). Production registers no
/// implementation until an owner picks a provider, so runs fail closed with 503.
/// </summary>
public interface ICareerTextModel
{
    Task<CareerModelResult> CompleteAsync(CareerModelRequest request, CancellationToken ct = default);
}

/// <summary>Deterministic, offline stand-in for Development, LocalDev and Testing.</summary>
public sealed class FakeCareerTextModel : ICareerTextModel
{
    public Task<CareerModelResult> CompleteAsync(CareerModelRequest request, CancellationToken ct = default)
    {
        var profile = request.Profile;
        var parts = new List<string>();

        var experience = profile.YearsExperience is { } years ? $" with {years} years of experience" : string.Empty;
        parts.Add($"{profile.CurrentTitle}{experience}.");
        if (profile.Skills.Count > 0)
        {
            parts.Add($"Skills include {string.Join(", ", profile.Skills.Take(5))}.");
        }
        if (profile.Highlights.Count > 0)
        {
            parts.Add($"Highlights: {string.Join("; ", profile.Highlights.Take(3))}.");
        }
        if (request.Goal != null)
        {
            parts.Add($"Aiming for a {request.Goal.TargetRole} role.");
        }
        else if (!string.IsNullOrWhiteSpace(request.Answer))
        {
            parts.Add($"Written for {request.Answer.Trim()}.");
        }

        var text = string.Join(" ", parts);
        return Task.FromResult(CareerModelResult.Text(text.Length > 1500 ? text[..1500] : text, usageTokens: 120, costCents: 1));
    }
}
