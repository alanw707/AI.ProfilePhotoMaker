namespace AI.ProfilePhotoMaker.API.Services.Career;

// Request and response shapes for docs/career/api-market-comparison.md (ADR 0014).

public sealed record MarketMetricDto(
    string Key,
    string Label,
    string Unit,
    string Measure,
    bool Supported,
    string? Reason,
    IReadOnlyList<string> GeographyLevels);

public sealed record MarketMetricListDto(IReadOnlyList<MarketMetricDto> Metrics);

/// <summary>One place's value. <see cref="Value"/> is null unless available (a top-coded figure carries the published ceiling).</summary>
public sealed record MarketAreaValueDto(
    string AreaCode,
    string AreaTitle,
    string Type,
    double? Value,
    string Status,
    int? Rank,
    int? RankedOf,
    bool Selected,
    double? ShareOfNationalEmployment);

public sealed record MarketComparisonMetricDto(string Key, string Label, string Unit, string Measure, bool Supported, string? Reason);

public sealed record MarketComparisonNationalDto(string AreaCode, string AreaTitle, double? Value, string Status);

public sealed record MarketComparisonReferenceDto(
    string Release, string PublishedOn, string Coverage, string DefinitionsUrl, string Citation);

public sealed record MarketComparisonOccupationDto(string Code, string Title, string PublishedCode, string Mapping);

public sealed record MarketComparisonDto(
    MarketComparisonOccupationDto Occupation,
    MarketComparisonMetricDto Metric,
    string Level,
    MarketComparisonNationalDto National,
    MarketComparisonReferenceDto Reference,
    IReadOnlyList<MarketAreaValueDto> Areas,
    int SelectionLimit,
    bool Truncated);

public sealed class MarketPreferenceRequest
{
    public string? AreaCode { get; set; }
    public string? Level { get; set; }
    public bool? Confirmed { get; set; }
}

public static class MarketComparisonErrorCodes
{
    public const string AreaNotFound = "CareerAreaNotFound";
    public const string MetricUnsupported = "CareerMetricUnsupported";
}
