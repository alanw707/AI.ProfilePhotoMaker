namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>Ceilings and worker settings for agent runs (ADR 0009), bound from <c>Career:Agent</c>.</summary>
public sealed class CareerAgentOptions
{
    public const string SectionName = "Career:Agent";

    public int MaxSteps { get; set; } = 8;
    public int MaxRunSeconds { get; set; } = 120;

    /// <summary>Claims per run; a retry after a failure or an expired lease is another claim.</summary>
    public int MaxAttempts { get; set; } = 3;
    public int LeaseSeconds { get; set; } = 30;
    public int MaxCostCents { get; set; } = 5;
    public int MonthlyRunAllowance { get; set; } = 20;
    public bool WorkerEnabled { get; set; } = true;
    public int PollSeconds { get; set; } = 2;
}
