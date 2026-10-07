namespace AI.ProfilePhotoMaker.API.Models.Career;

public static class CareerMatchStatuses
{
    public const string Proposed = "proposed";
    public const string Confirmed = "confirmed";
    public const string Dismissed = "dismissed";
    public const string Unsupported = "unsupported";
}

/// <summary>
/// The result of one occupation_match run (ADR 0010): owner-scoped, pinned to the profile and
/// goal versions the run read and to the O*NET release it matched against. It stays
/// <c>proposed</c> until the user confirms one candidate into the goal or dismisses it.
/// </summary>
public class CareerOccupationMatch
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public Guid RunId { get; set; }

    public int PinnedProfileVersion { get; set; }
    public int? PinnedGoalVersion { get; set; }

    /// <summary>O*NET release of the snapshot used, so a later snapshot never rewrites history.</summary>
    public string ReferenceRelease { get; set; } = string.Empty;
    public string MatcherVersion { get; set; } = string.Empty;

    /// <summary>proposed | confirmed | dismissed | unsupported (see <see cref="CareerMatchStatuses"/>).</summary>
    public string Status { get; set; } = CareerMatchStatuses.Proposed;

    /// <summary>The candidates with their evidence, as the matcher produced (and a clarification reordered) them.</summary>
    public string ResultJson { get; set; } = "{}";

    /// <summary>The question the run asked and the answer, when it asked.</summary>
    public string? ClarificationJson { get; set; }

    public string? ConfirmedCode { get; set; }
    public int? ConfirmedIntoGoalVersion { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
}
