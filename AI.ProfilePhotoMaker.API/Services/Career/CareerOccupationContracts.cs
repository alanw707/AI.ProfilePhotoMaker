using AI.ProfilePhotoMaker.API.Models.Career;

namespace AI.ProfilePhotoMaker.API.Services.Career;

// Request and response shapes for docs/career/api-occupation-matches.md.

public sealed class ConfirmOccupationRequest
{
    public string? OccupationCode { get; set; }
}

public sealed record CareerMatchClarificationDto(string Question, string Answer);

public sealed record CareerOccupationMatchDto(
    Guid Id,
    Guid RunId,
    string Status,
    int PinnedProfileVersion,
    int? PinnedGoalVersion,
    bool ProfileChanged,
    OccupationSource Reference,
    string MatcherVersion,
    IReadOnlyList<OccupationCandidate> Candidates,
    CareerMatchClarificationDto? Clarification,
    string? Guidance,
    string? ConfirmedCode,
    int? ConfirmedIntoGoalVersion,
    DateTime CreatedAt,
    DateTime? DecidedAt);

/// <summary>The snapshot's source plus how many occupations it holds.</summary>
public sealed record CareerOccupationReferenceDto(
    string Name,
    string Release,
    string ReleaseDate,
    string Taxonomy,
    string License,
    string LicenseUrl,
    string Url,
    string Attribution,
    int OccupationCount);

public static class CareerOccupationErrorCodes
{
    public const string MatchNotFound = "CareerOccupationMatchNotFound";
    public const string GoalRequired = "CareerGoalRequired";
    public const string MatchStale = "CareerMatchStale";
    public const string MatchNotConfirmable = "CareerMatchNotConfirmable";

    /// <summary>A market brief needs a goal with a confirmed occupation (ADR 0011).</summary>
    public const string OccupationRequired = "CareerOccupationRequired";
}

/// <summary>What a match stores in <c>ResultJson</c>; the status lives on the match row.</summary>
public sealed record StoredOccupationResult(IReadOnlyList<OccupationCandidate> Candidates, string? Guidance);

public static class OccupationMatchJson
{
    public static readonly System.Text.Json.JsonSerializerOptions Options = new(System.Text.Json.JsonSerializerDefaults.Web);
}
