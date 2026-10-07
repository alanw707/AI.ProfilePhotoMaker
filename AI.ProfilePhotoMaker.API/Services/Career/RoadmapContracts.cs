using System.Text.Json;
using System.Text.Json.Serialization;

namespace AI.ProfilePhotoMaker.API.Services.Career;

// Shapes for docs/career/api-roadmap.md (ADR 0016).

public static class RoadmapKeys
{
    public const string ClosestFit = "closest_fit";
    public const string HigherAmbition = "higher_ambition";
    public const string SteadierTransition = "steadier_transition";

    public static bool IsKnown(string? key) => key is ClosestFit or HigherAmbition or SteadierTransition;
}

public static class RoadmapErrorCodes
{
    public const string NotFound = "CareerRoadmapNotFound";
    public const string NotProposed = "CareerRoadmapNotProposed";
    public const string Cycle = "CareerRoadmapCycle";
}

public static class RoadmapJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

public sealed record RoadmapTask(string Id, string Title, double EffortHours, IReadOnlyList<string> DependsOn);

public sealed record RoadmapRationale(string Text, string SourceId, string Release);

public sealed record RoadmapMilestone(int Day, IReadOnlyList<RoadmapTask> Tasks);

public sealed record RoadmapOption(
    string Key,
    string OccupationCode,
    string Title,
    IReadOnlyList<RoadmapRationale> Rationale,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> MissingEvidence,
    string TimelineNote,
    IReadOnlyList<RoadmapTask> ThisWeek,
    IReadOnlyList<RoadmapMilestone> Milestones);

public sealed record RoadmapOmittedOption(string Key, string Reason);

public sealed record RoadmapPinnedDto(int ProfileVersion, int GoalVersion, string OccupationCode, Guid? MarketBriefId, Guid? PayAnalysisId);

public sealed record CareerRoadmapDto(
    Guid Id,
    int Version,
    string Status,
    string? SelectedOption,
    RoadmapPinnedDto Pinned,
    bool Stale,
    IReadOnlyList<string> StaleReasons,
    double WeeklyEffortHours,
    IReadOnlyList<RoadmapOption> Options,
    IReadOnlyList<RoadmapOmittedOption> OmittedOptions,
    string? LowTimeNote,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? GoalUnchanged = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Note = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? OccupationLink = null);

public sealed record CareerRoadmapSummaryDto(Guid Id, int Version, string Status, bool Stale, int OptionCount, DateTime CreatedAt);

public sealed record CareerRoadmapListDto(IReadOnlyList<CareerRoadmapSummaryDto> Roadmaps);

public sealed class AcceptRoadmapRequest
{
    public string? OptionKey { get; set; }
}

public sealed class UpdateRoadmapTaskRequest
{
    public double? EffortHours { get; set; }
}
