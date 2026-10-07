namespace AI.ProfilePhotoMaker.API.Models.Career;

/// <summary>
/// A user's confirmed professional facts (ADR 0006). One per owner. The facts
/// themselves live in immutable <see cref="CareerProfileVersion"/> rows; this row
/// only points at the active one.
/// </summary>
public class CareerProfile
{
    public Guid Id { get; set; }

    /// <summary>Server-derived from the authenticated user; never from the request.</summary>
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>
    /// Version number of the active <see cref="CareerProfileVersion"/>. Also the
    /// concurrency token, so two writers from the same base cannot both win.
    /// </summary>
    public int ActiveVersionNumber { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<CareerProfileVersion> Versions { get; set; } = new();
}

/// <summary>
/// An accepted, immutable snapshot of a profile's facts. Never updated after insert;
/// restoring an old version appends a copy.
/// </summary>
public class CareerProfileVersion
{
    public Guid Id { get; set; }
    public Guid CareerProfileId { get; set; }

    /// <summary>Duplicated from the profile so every private row is owner-filterable.</summary>
    public string OwnerId { get; set; } = string.Empty;

    public int VersionNumber { get; set; }

    public string CurrentTitle { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public int? YearsExperience { get; set; }
    public string? Location { get; set; }
    public string? Summary { get; set; }
    public List<string> Skills { get; set; } = new();
    public List<string> Highlights { get; set; } = new();
    public string? WorkArrangement { get; set; }

    /// <summary>Where these facts came from: "manual", or "resume"/"pasted" (#379).</summary>
    public string Source { get; set; } = CareerFactSource.Manual;

    /// <summary>The accepted proposal these facts came from; null for manual saves and restores.</summary>
    public Guid? SourceProposalId { get; set; }

    /// <summary>When the user explicitly confirmed these facts.</summary>
    public DateTime ConfirmedAt { get; set; }

    /// <summary>Set when this version was created by restoring an older one.</summary>
    public int? RestoredFromVersion { get; set; }

    public DateTime CreatedAt { get; set; }

    public CareerProfile? CareerProfile { get; set; }
}

public static class CareerFactSource
{
    public const string Manual = "manual";
    public const string Resume = "resume";
    public const string Pasted = "pasted";
}

public static class CareerWorkArrangement
{
    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.Ordinal) { "onsite", "hybrid", "remote", "flexible" };
}
