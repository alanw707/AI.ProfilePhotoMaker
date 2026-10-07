using System.Text.Json;
using System.Text.Json.Serialization;

namespace AI.ProfilePhotoMaker.API.Services.Career;

// Request and response shapes for docs/career/api-market-brief.md (ADR 0011).

public sealed record MarketAreaDto(string Code, string Title, string Type);

public sealed record MarketLocationDto(string Input, string Resolution, MarketAreaDto? Local);

public sealed record MarketCodeDto(string Code, string Match);

public sealed record MarketPublishedDto(MarketCodeDto? Oews, MarketCodeDto? Projections);

public sealed record MarketOccupationDto(string Code, string Title, MarketPublishedDto Published);

/// <summary>A cited figure. <see cref="Value"/> is null unless available (a top-coded figure carries the top code).</summary>
public sealed record MarketFigureDto(
    string Key, string Label, object? Value, string Status, string Unit, string? AreaCode, string? AreaTitle, string SourceId);

public sealed record MarketItemDto(string Code, string Title, IReadOnlyList<MarketFigureDto> Figures, string? Note);

public sealed record MarketSectionDto(
    string Key,
    string Title,
    string Status,
    string? Reason,
    string Note,
    IReadOnlyList<MarketFigureDto> Figures,
    IReadOnlyList<MarketItemDto> Items);

public sealed record MarketSourceDto(
    string Id, string Name, string Publisher, string ReferencePeriod, string PublishedOn, string Url, string DefinitionsUrl,
    string License, string Citation, string Definition, string Coverage);

public sealed record MarketNextActionDto(string Label, string Route);

public sealed record MarketPinnedDto(int ProfileVersion, int GoalVersion, string? OewsRelease, string? ProjectionsRelease);

public sealed record CareerMarketBriefDto(
    Guid Id,
    Guid RunId,
    string Status,
    MarketOccupationDto Occupation,
    MarketLocationDto Location,
    MarketPinnedDto Pinned,
    bool Stale,
    IReadOnlyList<string> StaleReasons,
    bool DataStale,
    IReadOnlyList<MarketSectionDto> Sections,
    MarketNextActionDto NextAction,
    IReadOnlyList<MarketSourceDto> Sources,
    DateTime CreatedAt);

public sealed record CareerMarketBriefSummaryDto(
    Guid Id, string OccupationCode, string OccupationTitle, string? AreaTitle, string Status, bool Stale, DateTime CreatedAt);

public sealed record CareerMarketBriefListDto(IReadOnlyList<CareerMarketBriefSummaryDto> Briefs);

public sealed record CareerMarketReferenceDto(IReadOnlyList<MarketSourceDto> Sources, int AreaCount, int OccupationCount);

public sealed class CareerMarketOptions
{
    public const string SectionName = "Career:Market";

    /// <summary>A source this many months past its publication date is flagged <c>dataStale</c>.</summary>
    public int DataStaleMonths { get; set; } = 18;
}

public static class CareerMarketErrorCodes
{
    public const string BriefNotFound = "CareerMarketBriefNotFound";
}

public static class MarketBriefStatuses
{
    public const string Complete = "complete";
    public const string Partial = "partial";
}

public static class MarketBriefJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };
}
