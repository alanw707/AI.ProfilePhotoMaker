namespace AI.ProfilePhotoMaker.API.Models.Career;

/// <summary>
/// A user's career goal (ADR 0006). One per owner in #378; the goal's content lives
/// in immutable <see cref="CareerGoalVersion"/> rows.
/// </summary>
public class CareerGoal
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>Active version number; also the concurrency token.</summary>
    public int ActiveVersionNumber { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<CareerGoalVersion> Versions { get; set; } = new();
}

public class CareerGoalVersion
{
    public Guid Id { get; set; }
    public Guid CareerGoalId { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public int VersionNumber { get; set; }

    public string TargetRole { get; set; } = string.Empty;
    public string? TargetLocation { get; set; }
    public string? WorkArrangement { get; set; }

    /// <summary>Desired pay in whole USD per year.</summary>
    public int? DesiredPayMin { get; set; }
    public int? DesiredPayMax { get; set; }
    public int? WeeklyEffortHours { get; set; }

    /// <summary>
    /// The profile version the user had when confirming this goal. The goal is stale
    /// once the profile moves past it (or a profile appears when there was none).
    /// </summary>
    public int? BasedOnProfileVersion { get; set; }

    public string Source { get; set; } = CareerFactSource.Manual;
    public DateTime ConfirmedAt { get; set; }

    /// <summary>Set when this version was created by restoring an older one.</summary>
    public int? RestoredFromVersion { get; set; }

    /// <summary>
    /// The confirmed O*NET occupation (ADR 0010), carried forward by later edits and restores.
    /// All four are set together, or all null.
    /// </summary>
    public string? OccupationCode { get; set; }
    public string? OccupationTitle { get; set; }
    public string? OccupationReferenceRelease { get; set; }
    public Guid? OccupationMatchId { get; set; }

    /// <summary>
    /// The place the user explicitly saved from the market comparison (ADR 0014), carried forward by
    /// later edits and restores. All three are set together, or all null.
    /// </summary>
    public string? PreferredAreaCode { get; set; }
    public string? PreferredAreaTitle { get; set; }
    public string? PreferredAreaLevel { get; set; }

    public DateTime CreatedAt { get; set; }

    public CareerGoal? CareerGoal { get; set; }
}
