namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>Only career facts and pinned reference metadata enter the deterministic calculation.</summary>
public sealed record PayAnalysisInput(int ProfileVersion, int GoalVersion, string OccupationCode, string OccupationTitle,
    string? LocationText, int? RequestedAnnual, string? AreaCode, string? AreaTitle, string AreaResolution,
    string? OewsRelease, string OewsSnapshotSha256, string? ProjectionsRelease, string RuleVersion,
    string? ObservationSourceId, IReadOnlyList<PayObservation> Observations, DateTime AsOf);

public sealed record PayCohortDto(int Included, int Excluded, int Employers, decimal LargestEmployerShare,
    bool Concentrated, bool Sensitive, IReadOnlyDictionary<string, int> ExclusionReasons);
public sealed record PayBenchmarkSection(string Key, string Title, string Status, string? Reason, string Label, string Note,
    IReadOnlyList<MarketFigureDto> Figures);
public sealed record PayPersonalizedSection(string Key, string Title, string Status, string? Reason, PayInterval? Interval,
    PayCohortDto Cohort, string Note);
public sealed record PayScenarioSection(string Key, string Title, string Status, int? RequestedAnnual,
    double? BenchmarkMedianAnnual, double? GapAnnual, double? GapPercent, string Note);
public sealed record PayAnalysisSections(PayBenchmarkSection Benchmark, PayPersonalizedSection Personalized, PayScenarioSection Scenario);
public sealed record PayQualificationDto(bool PersonalizedAllowed, IReadOnlyList<string> BlockedReasons, IReadOnlyList<PayGateRow> Gates);
public sealed record PayAnalysisContent(string Status, MarketLocationDto Location, MarketCodeDto? Published,
    PayAnalysisSections Sections, PayQualificationDto Qualification, IReadOnlyList<MarketSourceDto> Sources, string InputHash);
public sealed record PayPinnedDto(int ProfileVersion, int GoalVersion, string? OewsRelease, string OewsSnapshotSha256,
    string? ProjectionsRelease, string RuleVersion, string? ObservationSourceId);
public sealed record PayOccupationDto(string Code, string Title, string? PublishedCode, string? Mapping);
public sealed record CareerPayAnalysisDto(Guid Id, Guid RunId, string Status, PayOccupationDto Occupation,
    MarketLocationDto Location, PayPinnedDto Pinned, string InputHash, bool Stale, IReadOnlyList<string> StaleReasons,
    IReadOnlyList<object> Sections, IReadOnlyList<string> BlockedReasons, PayQualificationDto Qualification,
    IReadOnlyList<MarketSourceDto> Sources, DateTime CreatedAt);
public sealed record CareerPayAnalysisSummaryDto(Guid Id, string OccupationCode, string OccupationTitle,
    string? AreaTitle, string Status, bool PersonalizedAvailable, bool Stale, DateTime CreatedAt);
public sealed record CareerPayAnalysisListDto(IReadOnlyList<CareerPayAnalysisSummaryDto> Analyses);
public sealed record PayRecomputeDto(bool Matches, string InputHash, string StoredInputHash,
    IReadOnlyList<string> Differences, IReadOnlyList<object> Sections);
