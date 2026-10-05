namespace AI.ProfilePhotoMaker.API.Models.Career;

/// <summary>Immutable pinned analysis; only read-time staleness changes.</summary>
public class CareerPayAnalysis
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = "";
    public Guid RunId { get; set; }
    public int PinnedProfileVersion { get; set; }
    public int PinnedGoalVersion { get; set; }
    public string OccupationCode { get; set; } = "";
    public string OccupationTitle { get; set; } = "";
    public string? AreaCode { get; set; }
    public string? AreaTitle { get; set; }
    public string AreaResolution { get; set; } = "";
    public string? LocationInput { get; set; }
    public int? RequestedAnnual { get; set; }

    /// <summary>Which end of the goal's desired pay was compared against: desiredPayMin or desiredPayMax.</summary>
    public string? RequestedPaySource { get; set; }
    public string? OewsRelease { get; set; }
    public string OewsSnapshotSha256 { get; set; } = "";
    public string? ProjectionsRelease { get; set; }
    public string RuleVersion { get; set; } = "";
    public string? ObservationSourceId { get; set; }
    public int ObservationCount { get; set; }
    public string InputHash { get; set; } = "";
    public string InputJson { get; set; } = "{}";
    public string Status { get; set; } = "";
    public string SectionsJson { get; set; } = "[]";
    public string QualificationJson { get; set; } = "{}";
    public string SourcesJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; }
}
