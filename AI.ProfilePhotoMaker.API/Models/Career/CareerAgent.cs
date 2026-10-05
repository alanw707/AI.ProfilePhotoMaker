namespace AI.ProfilePhotoMaker.API.Models.Career;

/// <summary>Run lifecycle (ADR 0009). Stored as a string.</summary>
public enum CareerRunStatus
{
    Queued,
    Working,
    NeedsInput,
    Completed,
    Failed,
    Cancelled
}

public static class CareerAgentTasks
{
    public const string ProfileSummary = "profile_summary";
}

public static class CareerStepKinds
{
    public const string Tool = "tool";
    public const string Model = "model";
    public const string Question = "question";
    public const string Save = "save";
}

public static class CareerStepNames
{
    public const string ReadProfile = "read_profile";
    public const string ReadGoal = "read_goal";
    public const string AskAudience = "ask_audience";
    public const string DraftSummary = "draft_summary";
    public const string SaveProposal = "save_proposal";
}

/// <summary>
/// One request to the career agent (ADR 0009). A worker leases it, and every write
/// it makes carries <see cref="FencingToken"/>, so a worker that lost its lease or
/// was cancelled cannot change the run any more.
/// </summary>
public class CareerAgentRun
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public string Task { get; set; } = CareerAgentTasks.ProfileSummary;
    public CareerRunStatus Status { get; set; }

    /// <summary>Client-supplied key; unique per owner so a retried request finds its run.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>SHA-256 of the normalized request body, to tell a replay from a key reuse.</summary>
    public string RequestHash { get; set; } = string.Empty;

    public int? PinnedProfileVersion { get; set; }
    public int? PinnedGoalVersion { get; set; }

    /// <summary>Ordinal the next step will get.</summary>
    public int CheckpointOrdinal { get; set; } = 1;
    public string? CheckpointJson { get; set; }

    public string? QuestionId { get; set; }
    public string? QuestionText { get; set; }
    public string? Answer { get; set; }

    /// <summary>Claims so far; capped by <c>MaxAttempts</c>.</summary>
    public int Attempts { get; set; }

    public string? LeaseOwner { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }

    /// <summary>Bumped on every claim and on cancel; the concurrency token that fences stale writers.</summary>
    public long FencingToken { get; set; }

    public Guid? ProposalId { get; set; }
    public string? ErrorCode { get; set; }

    /// <summary>True once a provider call was made, so the allowance is spent rather than released.</summary>
    public bool ModelCalled { get; set; }
    public int CostCents { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? Deadline { get; set; }

    public List<CareerAgentStep> Steps { get; set; } = new();
}

/// <summary>An executed step. Its output is saved before anything uses it, so a resumed run never repeats it.</summary>
public class CareerAgentStep
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }

    /// <summary>Duplicated from the run so every private row is owner-filterable.</summary>
    public string OwnerId { get; set; } = string.Empty;

    public int Ordinal { get; set; }

    /// <summary>"{runId}:{ordinal}"; unique, so a step can never be recorded twice.</summary>
    public string OperationId { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "completed";
    public string? OutputJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public CareerAgentRun? Run { get; set; }
}

/// <summary>
/// Monthly career run budget, separate from photo credits. A run reserves a unit when
/// created; the unit becomes used once a model call was made, or is released.
/// </summary>
public class CareerAllowance
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>First instant of the UTC month this row covers.</summary>
    public DateTime PeriodStart { get; set; }

    public int Reserved { get; set; }
    public int Used { get; set; }

    /// <summary>Bumped on every write; the concurrency token, so racing writers cannot overspend.</summary>
    public int Version { get; set; }
}
