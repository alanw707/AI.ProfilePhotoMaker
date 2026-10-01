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
    public DateTime CreatedAt { get; set; }

    public CareerGoal? CareerGoal { get; set; }
}
