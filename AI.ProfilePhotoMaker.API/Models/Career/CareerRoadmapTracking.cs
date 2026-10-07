namespace AI.ProfilePhotoMaker.API.Models.Career;

public static class RoadmapTaskStatuses
{
    public const string NotStarted = "not_started";
    public const string InProgress = "in_progress";
    public const string Done = "done";
    public const string Blocked = "blocked";

    public static bool IsKnown(string? value) => value is NotStarted or InProgress or Done or Blocked;
}

public static class RoadmapReplanStatuses
{
    public const string Open = "open";
    public const string Applied = "applied";
    public const string Rejected = "rejected";
}

/// <summary>
/// Progress on one task of an accepted roadmap (ADR 0017). Generated tasks get a row lazily on first
/// write (an absent row reads as version 0); human-authored tasks are stored here in full.
/// </summary>
public class CareerRoadmapTaskProgress
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = "";
    public Guid RoadmapId { get; set; }
    public string TaskId { get; set; } = "";

    /// <summary>generated | human.</summary>
    public string Origin { get; set; } = "generated";

    /// <summary>Human tasks only; generated task titles live in the roadmap content.</summary>
    public string? Title { get; set; }
    public int? MilestoneDay { get; set; }
    public double? PlannedEffortHours { get; set; }

    /// <summary>Human tasks only: JSON array of task ids.</summary>
    public string? DependsOnJson { get; set; }

    public string Status { get; set; } = RoadmapTaskStatuses.NotStarted;
    public double? EffortHours { get; set; }
    public string? OutputNote { get; set; }
    public Guid? LinkedMaterialId { get; set; }

    /// <summary>Optimistic concurrency token, and the number in the task's ETag.</summary>
    public int RowVersion { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>A stored replan proposal: a reviewed diff the user applies in part or rejects (ADR 0017).</summary>
public class CareerRoadmapReplan
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = "";
    public Guid RoadmapId { get; set; }
    public int BaseVersion { get; set; }

    /// <summary>open | applied | rejected. Also the concurrency token, so apply and reject cannot both win.</summary>
    public string Status { get; set; } = RoadmapReplanStatuses.Open;
    public string ChangesJson { get; set; } = "[]";
    public string PreservedJson { get; set; } = "[]";

    /// <summary>The freshly built option and its tasks, used when changes are applied.</summary>
    public string ProposedJson { get; set; } = "{}";
    public Guid? AppliedRoadmapId { get; set; }
    public DateTime CreatedAt { get; set; }
}
