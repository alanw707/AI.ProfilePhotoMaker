namespace AI.ProfilePhotoMaker.API.Models.Career;

/// <summary>
/// A saved market brief (ADR 0011): owner-scoped, one per <c>market_brief</c> run, pinned to the profile
/// and goal versions it read, the occupation it looked up and the BLS releases it was built from. It is
/// never rewritten; a changed goal or profile only makes reads report it as stale.
/// </summary>
public class CareerMarketBrief
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public Guid RunId { get; set; }

    public int PinnedProfileVersion { get; set; }
    public int PinnedGoalVersion { get; set; }

    /// <summary>The confirmed O*NET occupation the brief is about.</summary>
    public string OccupationCode { get; set; } = string.Empty;
    public string OccupationTitle { get; set; } = string.Empty;

    /// <summary>Reference periods of the sources used; null when that source was unavailable.</summary>
    public string? OewsRelease { get; set; }
    public string? ProjectionsRelease { get; set; }

    /// <summary>complete | partial.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Which BLS codes (exact or broad group) each source published this occupation under.</summary>
    public string PublishedJson { get; set; } = "{}";
    public string LocationJson { get; set; } = "{}";
    public string SectionsJson { get; set; } = "[]";
    public string SourcesJson { get; set; } = "[]";
    public string NextActionJson { get; set; } = "{}";

    public DateTime CreatedAt { get; set; }
}
