namespace AI.ProfilePhotoMaker.API.Models.Career;

public static class CareerMaterialKinds
{
    public const string Resume = "resume";
}

public static class CareerMaterialProposalStatuses
{
    public const string Open = "open";
    public const string Applied = "applied";
    public const string Rejected = "rejected";
}

/// <summary>
/// A document the user builds from confirmed facts (ADR 0018). Content lives in immutable
/// <see cref="CareerMaterialVersion"/> rows; this row points at the current one.
/// </summary>
public class CareerMaterial
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = "";

    /// <summary>The run that created it.</summary>
    public Guid? RunId { get; set; }
    public string Kind { get; set; } = CareerMaterialKinds.Resume;
    public string Title { get; set; } = "";
    public string Status { get; set; } = "draft";

    /// <summary>Current version number. Also the concurrency token and the number in the ETag.</summary>
    public int CurrentVersion { get; set; }

    /// <summary>Mirrors the current version's pins, so stale reads need no version lookup.</summary>
    public int PinnedProfileVersion { get; set; }
    public int PinnedGoalVersion { get; set; }
    public string OccupationCode { get; set; } = "";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>An immutable snapshot of a material's lines. Never updated after insert.</summary>
public class CareerMaterialVersion
{
    public Guid Id { get; set; }
    public Guid MaterialId { get; set; }
    public string OwnerId { get; set; } = "";
    public int Number { get; set; }

    /// <summary>JSON array of sections, each with lines { id, text, factIds, origin }.</summary>
    public string SectionsJson { get; set; } = "[]";

    /// <summary>JSON array of open questions { id, factId, text }.</summary>
    public string QuestionsJson { get; set; } = "[]";

    /// <summary>JSON of which contact details the user chose to include.</summary>
    public string ContactJson { get; set; } = "{}";

    /// <summary>The profile version fact ids resolve against.</summary>
    public int PinnedProfileVersion { get; set; }
    public int PinnedGoalVersion { get; set; }
    public string OccupationCode { get; set; } = "";

    /// <summary>agent | user.</summary>
    public string Author { get; set; } = "agent";
    public int? RestoredFromVersion { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>A reviewed diff of a fresh draft against the current version (ADR 0018).</summary>
public class CareerMaterialProposal
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = "";
    public Guid MaterialId { get; set; }
    public Guid RunId { get; set; }
    public int BaseVersion { get; set; }

    /// <summary>open | applied | rejected. Also the concurrency token, so apply and reject cannot both win.</summary>
    public string Status { get; set; } = CareerMaterialProposalStatuses.Open;
    public string ChangesJson { get; set; } = "[]";

    /// <summary>The fresh draft (sections, questions) and its pins, used when changes are applied.</summary>
    public string ProposedJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}
