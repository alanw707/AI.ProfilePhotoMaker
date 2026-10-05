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

    /// <summary>Wait before a failed model call is tried again; multiplied by the attempt number.</summary>
    public int RetryBackoffSeconds { get; set; } = 5;

    /// <summary>A question nobody answers within this time fails the run and releases its unit.</summary>
    public int QuestionExpiryHours { get; set; } = 72;

    /// <summary>Seconds kept free between the end of a model call and the end of the lease.</summary>
    public const int ModelCallMarginSeconds = 5;

    /// <summary>How long one model call may take: inside the lease, so no second worker re-asks.</summary>
    public static TimeSpan ModelCallTimeoutFor(int leaseSeconds) =>
        TimeSpan.FromSeconds(Math.Max(1, leaseSeconds - ModelCallMarginSeconds));
}
