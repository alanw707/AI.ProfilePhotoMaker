namespace AI.ProfilePhotoMaker.API.Models.Career;

/// <summary>
/// One cost/latency record per model step, export or external source call (ADR 0022). It never holds
/// prompts, resume or profile text, or model output: only counts, cost, timing and an outcome code.
/// </summary>
public class CareerUsageEvent
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public Guid? RunId { get; set; }

    /// <summary>One of <see cref="CareerUsageActions"/>.</summary>
    public string Action { get; set; } = string.Empty;
    public string? Model { get; set; }

    /// <summary>The text model reports a single total, so there is no input/output split.</summary>
    public int Tokens { get; set; }
    public int CostCents { get; set; }
    public int LatencyMs { get; set; }

    /// <summary>"ok", "failed" or "timeout".</summary>
    public string Outcome { get; set; } = CareerUsageOutcomes.Ok;

    /// <summary>Age of the external source's data in seconds, when it reports one.</summary>
    public int? SourceAgeSeconds { get; set; }
    public DateTime CreatedAt { get; set; }
}

public static class CareerUsageActions
{
    public const string ModelStep = "model_step";
    public const string Export = "export";
    public const string JobSource = "job_source";
}

public static class CareerUsageOutcomes
{
    public const string Ok = "ok";
    public const string Failed = "failed";
    public const string Timeout = "timeout";
}

/// <summary>
/// The operator's switches (one row, Id = 1). Operator state, not user data: deliberately outside
/// <c>CareerPrivateDataService.CoveredEntityTypes</c>, so it has no owner and survives every purge.
/// </summary>
public class CareerOperatorState
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public bool GenerationDisabled { get; set; }
    public bool SourcesDisabled { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Concurrency token bumped by every run create (ADR 0022): serializes the global backpressure and cost
    /// checks across users, so two creates cannot both pass them against the same counts.
    /// </summary>
    public int GuardVersion { get; set; }
}
