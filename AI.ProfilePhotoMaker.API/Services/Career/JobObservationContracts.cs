namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>What the page asked for, after the service resolved the occupation and area (ADR 0015).</summary>
public sealed record JobObservationQuery(
    string? OccupationCode,
    string? OccupationTitle,
    string? AreaCode,
    string? AreaTitle,
    string AreaResolution,
    string? AreaInput,
    bool EligibleOnly,
    string Remote,
    string? Q);

public static class JobRemoteFilters
{
    public const string All = "all";
    public const string Eligible = "eligible";
    public const string Ineligible = "ineligible";
    public const string Unknown = "unknown";

    public static bool IsValid(string? value) => value is All or Eligible or Ineligible or Unknown;
}

public static class JobSourceReasons
{
    public const string NotConfigured = "source_not_configured";
    public const string Unavailable = "source_unavailable";
    public const string OccupationRequired = "occupation_required";
}

public sealed record JobPayDto(decimal? Min, decimal? Max, string? Unit, string? Basis, string Status);

public sealed record JobLocationDto(string? City, string? State, string? AreaCode, string Match);

public sealed record JobObservationDto(
    string ObservationId,
    string Title,
    string Organization,
    IReadOnlyList<JobLocationDto> Locations,
    bool MultiLocation,
    JobPayDto Pay,
    DateOnly? PostedOn,
    DateOnly? ClosesOn,
    bool Expired,
    string RemoteEligibility,
    string? RemoteNote,
    string? Series,
    string? Grade,
    string? SourceUrl,
    string SourceId);

/// <summary>
/// Reconciles: Fetched = Shown + DuplicateIds + DuplicateReposts + Expired + OtherLocationExcluded + RemoteUnknownExcluded
/// + RemoteIneligibleExcluded + KeywordExcluded + RemoteFilterExcluded + CappedByLimit. Matched = Shown + CappedByLimit.
/// </summary>
public sealed record JobCountsDto(
    int Fetched, int Matched, int Shown, int DuplicateIds, int DuplicateReposts, int Expired, int RemoteUnknownExcluded,
    int RemoteIneligibleExcluded, int OtherLocationExcluded, int KeywordExcluded, int RemoteFilterExcluded, int CappedByLimit)
{
    public static JobCountsDto Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}

public sealed record JobCoverageDto(
    bool Available,
    string? Reason,
    string SourceId,
    string SourceName,
    string Coverage,
    string Attribution,
    string SourceUrl,
    DateTimeOffset RetrievedAt,
    DateOnly? PostedFrom,
    DateOnly? PostedTo,
    JobCountsDto Counts);

public sealed record JobOccupationDto(string Code, string? Title);

public sealed record JobAreaDto(string? Input, string Resolution, string? Code, string? Title);

public sealed record JobPreferencesDto(string? AreaCode, bool StalePreference, string? Note);

public sealed record JobObservationResult(
    JobOccupationDto? Occupation,
    JobAreaDto Area,
    JobCoverageDto Coverage,
    JobPreferencesDto Preferences,
    IReadOnlyList<JobObservationDto> Observations,
    bool Truncated,
    string Note);

public sealed record JobSourceDto(string SourceId, string Name, bool Configured, string Coverage, string Attribution, string SourceUrl);
