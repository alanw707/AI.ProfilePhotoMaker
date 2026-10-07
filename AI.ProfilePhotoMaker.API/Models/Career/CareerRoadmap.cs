namespace AI.ProfilePhotoMaker.API.Models.Career;

public static class CareerRoadmapStatuses
{
    public const string Proposed = "proposed";
    public const string Accepted = "accepted";
    public const string Dismissed = "dismissed";
}

/// <summary>
/// A deterministic, evidence-gated career roadmap (ADR 0016). Content is immutable except for
/// <see cref="Status"/> and <see cref="SelectedOption"/>; editing a task's effort writes a new version.
/// </summary>
public class CareerRoadmap
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = "";

    /// <summary>The run that built it; null for a version written by a task-effort edit.</summary>
    public Guid? RunId { get; set; }

    /// <summary>Increments per new roadmap for the owner.</summary>
    public int Version { get; set; }

    /// <summary>proposed | accepted | dismissed. Also the concurrency token, so accept and dismiss cannot both win.</summary>
    public string Status { get; set; } = CareerRoadmapStatuses.Proposed;
    public string? SelectedOption { get; set; }

    public int PinnedProfileVersion { get; set; }
    public int PinnedGoalVersion { get; set; }
    public string OccupationCode { get; set; } = "";
    public string OccupationTitle { get; set; } = "";
    public Guid? MarketBriefId { get; set; }
    public Guid? PayAnalysisId { get; set; }
    public double WeeklyEffortHours { get; set; }
    public string? LowTimeNote { get; set; }
    public string OptionsJson { get; set; } = "[]";
    public string OmittedJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; }
}
