namespace AI.ProfilePhotoMaker.API.Services.Career;

public sealed record MarketAlternative(string Code, string Title);

public sealed record MarketBriefInput(
    string OccupationCode, string OccupationTitle, string? LocationText, IReadOnlyList<MarketAlternative> Alternatives);

/// <summary>Everything a brief says, before it is stored or shown.</summary>
public sealed record MarketBriefContent(
    string Status,
    MarketOccupationDto Occupation,
    MarketLocationDto Location,
    IReadOnlyList<MarketSectionDto> Sections,
    MarketNextActionDto NextAction,
    IReadOnlyList<MarketSourceDto> Sources,
    string? OewsRelease,
    string? ProjectionsRelease);

public static class MarketFigureStatuses
{
    public const string Available = "available";
    public const string NotAvailable = "not_available";
    public const string TopCoded = "top_coded";
    public const string NotPublished = "not_published";

    public static string Of(MarketValueStatus status) => status switch
    {
        MarketValueStatus.Available => Available,
        MarketValueStatus.TopCoded => TopCoded,
        MarketValueStatus.NotPublished => NotPublished,
        _ => NotAvailable
    };
}

public static class MarketSectionStatuses
{
    public const string Complete = "complete";
    public const string Unavailable = "unavailable";
    public const string Failed = "failed";

    public const string LocationUnresolved = "location_unresolved";
    public const string NotPublished = "not_published";
}

/// <summary>
/// Builds a market brief from the BLS reference without touching a database or a model (ADR 0011).
/// Figures are copied as published: a BLS status becomes a status, never a zero. Each section is
/// built on its own, so one unavailable source fails only the sections that need it.
/// </summary>
public static class MarketBriefBuilder
{
    public const string WagesKey = "wages";
    public const string EmploymentKey = "employment";
    public const string OutlookKey = "outlook";
    public const string AlternativesKey = "alternatives";

    public const string SetupRoute = "/app/career/setup";
    public const string OccupationRoute = "/app/career/occupation";

    private const string NotJobsNote = "Not open jobs, not a personal salary prediction, not total compensation.";
    private const int MaxAlternatives = 3;

    private sealed record FigureSpec(string Key, string Label, string Unit, Func<MarketWageRow, MarketValue> Pick);

    // Order is the order of the figures in the section.
    private static readonly FigureSpec[] WageSpecs =
    {
        new("medianAnnual", "Median annual wage", "usd_per_year", r => r.MedianAnnual),
        new("pct10Annual", "10th percentile annual wage", "usd_per_year", r => r.Pct10Annual),
        new("pct25Annual", "25th percentile annual wage", "usd_per_year", r => r.Pct25Annual),
        new("pct75Annual", "75th percentile annual wage", "usd_per_year", r => r.Pct75Annual),
        new("pct90Annual", "90th percentile annual wage", "usd_per_year", r => r.Pct90Annual),
        new("meanAnnual", "Mean annual wage", "usd_per_year", r => r.MeanAnnual),
        new("medianHourly", "Median hourly wage", "usd_per_hour", r => r.MedianHourly),
        new("meanPrse", "Mean wage relative standard error", "percent_rse", r => r.MeanPrse)
    };

    private static readonly FigureSpec[] EmploymentSpecs =
    {
        new("employment", "Employment", "jobs", r => r.Employment),
        new("employmentPrse", "Employment relative standard error", "percent_rse", r => r.EmploymentPrse)
    };

    // Only published for a local area; the national row has no concentration figures.
    private static readonly FigureSpec[] ConcentrationSpecs =
    {
        new("jobsPer1000", "Jobs per 1,000 jobs", "per_1000_jobs", r => r.JobsPer1000),
        new("locationQuotient", "Location quotient", "ratio", r => r.LocationQuotient)
    };

    // ---- Whole brief -------------------------------------------------------------

    public static MarketBriefContent Build(MarketBriefInput input, IMarketReference reference)
    {
        var location = ResolveLocation(input.LocationText, reference);
        var sections = new List<MarketSectionDto>
        {
            BuildWages(input.OccupationCode, location, reference),
            BuildEmployment(input.OccupationCode, location, reference),
            BuildOutlook(input.OccupationCode, reference),
            BuildAlternatives(input.Alternatives, reference)
        };

        return Assemble(input.OccupationCode, input.OccupationTitle, location, sections, reference);
    }

    /// <summary>Joins already-built sections with the occupation mapping, sources and next action.</summary>
    public static MarketBriefContent Assemble(
        string occupationCode, string occupationTitle, MarketLocationDto location, IReadOnlyList<MarketSectionDto> sections,
        IMarketReference reference) =>
        new(
            Status(sections),
            new MarketOccupationDto(occupationCode, occupationTitle, Published(occupationCode, reference)),
            location,
            sections,
            NextAction(location, reference),
            Sources(reference),
            reference.Oews?.Source.ReferencePeriod,
            reference.Projections?.Source.ReferencePeriod);

    /// <summary>Partial when any section could not be looked up at all; unavailable sections are honest answers, not failures.</summary>
    public static string Status(IEnumerable<MarketSectionDto> sections) =>
        sections.Any(s => s.Status == MarketSectionStatuses.Failed) ? MarketBriefStatuses.Partial : MarketBriefStatuses.Complete;

    public static MarketLocationDto ResolveLocation(string? text, IMarketReference reference) =>
        // Without the OEWS areas nothing can be resolved; the wage sections report that failure themselves.
        MarketAreaResolver.Resolve(text, reference.Oews?.Areas ?? Array.Empty<MarketArea>());

    public static MarketPublishedDto Published(string onetCode, IMarketReference reference) => new(
        reference.Oews?.Crosswalk(onetCode) is { } o ? new MarketCodeDto(o.Code, o.Match) : null,
        reference.Projections?.Crosswalk(onetCode) is { } p ? new MarketCodeDto(p.Code, p.Match) : null);

    public static MarketNextActionDto NextAction(MarketLocationDto location, IMarketReference reference) =>
        location.Resolution == MarketResolutions.Unresolved && reference.Oews != null
            ? new MarketNextActionDto("Add a city and state to your goal", SetupRoute)
            : new MarketNextActionDto("Check your target occupation", OccupationRoute);

    public static IReadOnlyList<MarketSourceDto> Sources(IMarketReference reference) =>
        new[] { reference.Oews?.Source, reference.Projections?.Source }
            .Where(s => s != null)
            .Select(s => new MarketSourceDto(
                s!.Id, s.Name, s.Publisher, s.ReferencePeriod, s.PublishedOn, s.Url, s.DefinitionsUrl,
                s.License, s.Citation, s.Definition, s.Coverage))
            .ToList();

    // ---- Wages and employment ----------------------------------------------------

    public static MarketSectionDto BuildWages(string onetCode, MarketLocationDto location, IMarketReference reference) =>
        BuildOewsSection(
            WagesKey, "Wages", $"Benchmark wages for this occupation. {NotJobsNote}", onetCode, location, reference,
            WageSpecs, includeConcentration: false, withMedianDifference: true);

    public static MarketSectionDto BuildEmployment(string onetCode, MarketLocationDto location, IMarketReference reference) =>
        BuildOewsSection(
            EmploymentKey, "Employment", "Estimated wage and salary jobs in this occupation and how concentrated they are locally. Not open jobs.",
            onetCode, location, reference, EmploymentSpecs, includeConcentration: true, withMedianDifference: false);

    private static MarketSectionDto BuildOewsSection(
        string key, string title, string note, string onetCode, MarketLocationDto location, IMarketReference reference,
        FigureSpec[] specs, bool includeConcentration, bool withMedianDifference)
    {
        var oews = reference.Oews;
        if (oews == null)
        {
            return Failed(key, title, note);
        }
        if (oews.Crosswalk(onetCode) is not { } published)
        {
            return Section(key, title, MarketSectionStatuses.Unavailable, MarketSectionStatuses.NotPublished, note, Array.Empty<MarketFigureDto>());
        }

        var national = oews.Areas.First(a => a.Type == "national");
        var nationalRow = oews.Wage(national.Code, published.Code);
        var figures = WageFigures(oews, national, nationalRow, specs);

        if (location.Local is { } local)
        {
            var localArea = oews.Area(local.Code)!;
            var localRow = oews.Wage(localArea.Code, published.Code);
            figures.AddRange(WageFigures(oews, localArea, localRow, includeConcentration ? specs.Concat(ConcentrationSpecs) : specs));
            if (withMedianDifference && MedianDifference(oews, localArea, nationalRow, localRow) is { } difference)
            {
                figures.Add(difference);
            }
        }

        var reason = location.Resolution == MarketResolutions.Unresolved ? MarketSectionStatuses.LocationUnresolved : null;
        return Section(
            key, title, reason == null ? MarketSectionStatuses.Complete : MarketSectionStatuses.Unavailable, reason,
            note + MappingNote(published, oews.OccupationTitle(published.Code)), figures);
    }

    private static List<MarketFigureDto> WageFigures(OewsData oews, MarketArea area, MarketWageRow? row, IEnumerable<FigureSpec> specs) =>
        specs.Select(spec => Figure(
            spec.Key, spec.Label, row == null ? MarketValue.NotPublished : spec.Pick(row), spec.Unit, area.Code, area.Title, oews.Source.Id))
        .ToList();

    /// <summary>Local minus national median, in dollars; only when BLS publishes both medians.</summary>
    private static MarketFigureDto? MedianDifference(OewsData oews, MarketArea local, MarketWageRow? national, MarketWageRow? localRow)
    {
        if (national?.MedianAnnual is not { Status: MarketValueStatus.Available, Number: { } nationalMedian }
            || localRow?.MedianAnnual is not { Status: MarketValueStatus.Available, Number: { } localMedian })
        {
            return null;
        }
        return Figure(
            "medianDifferenceAnnual", "Local median minus U.S. median annual wage",
            new MarketValue(localMedian - nationalMedian, MarketValueStatus.Available), "usd_per_year", local.Code, local.Title, oews.Source.Id);
    }

    // ---- Outlook -----------------------------------------------------------------

    public static MarketSectionDto BuildOutlook(string onetCode, IMarketReference reference)
    {
        const string key = OutlookKey;
        const string title = "Job outlook";
        const string note = "Projected national employment change and average annual openings from growth and replacement needs. Not current vacancies.";

        var projections = reference.Projections;
        if (projections == null)
        {
            return Failed(key, title, note);
        }
        if (projections.Crosswalk(onetCode) is not { } published)
        {
            return Section(key, title, MarketSectionStatuses.Unavailable, MarketSectionStatuses.NotPublished, note, Array.Empty<MarketFigureDto>());
        }

        var national = reference.Oews?.Areas.FirstOrDefault(a => a.Type == "national");
        var row = projections.Row(published.Code);
        MarketFigureDto F(string figureKey, string label, MarketValue value, string unit) =>
            Figure(figureKey, label, value, unit, national?.Code ?? "99", national?.Title ?? "U.S.", projections.Source.Id);

        var figures = new List<MarketFigureDto>
        {
            F("employment2025", "Employment, 2025", row?.Employment2025Thousands ?? MarketValue.NotPublished, "jobs_thousands"),
            F("employment2035", "Projected employment, 2035", row?.Employment2035Thousands ?? MarketValue.NotPublished, "jobs_thousands"),
            F("changePercent", "Projected change, 2025–35", row?.ChangePercent ?? MarketValue.NotPublished, "percent"),
            F("annualOpenings", "Average annual openings, 2025–35", row?.AnnualOpeningsThousands ?? MarketValue.NotPublished, "jobs_thousands"),
            TextFigure("typicalEducation", "Typical entry education", row?.TypicalEducation, row == null, national, projections.Source.Id)
        };
        return Section(key, title, MarketSectionStatuses.Complete, null, note + MappingNote(published, reference.Oews?.OccupationTitle(published.Code) ?? projections.OccupationTitle(published.Code)), figures);
    }

    // ---- Alternatives ------------------------------------------------------------

    public static MarketSectionDto BuildAlternatives(IReadOnlyList<MarketAlternative> alternatives, IMarketReference reference)
    {
        const string key = AlternativesKey;
        const string title = "Related occupations";
        const string note = "Other occupations from your confirmed match, with their national median wage and projected change. Benchmarks, not job offers.";

        var oews = reference.Oews;
        var projections = reference.Projections;
        if (oews == null || projections == null)
        {
            // Each alternative cites both sources; with one missing the comparison would be half a table.
            return Failed(key, title, note);
        }

        var items = alternatives
            .Where(a => a.Code.Length > 0)
            .DistinctBy(a => a.Code)
            .Take(MaxAlternatives)
            .Select(a => AlternativeItem(a, oews, projections))
            .ToList();
        return items.Count == 0
            ? Section(key, title, MarketSectionStatuses.Unavailable, MarketSectionStatuses.NotPublished, note, Array.Empty<MarketFigureDto>())
            : Section(key, title, MarketSectionStatuses.Complete, null, note, Array.Empty<MarketFigureDto>(), items);
    }

    private static MarketItemDto AlternativeItem(MarketAlternative alternative, OewsData oews, ProjectionsData projections)
    {
        var national = oews.Areas.First(a => a.Type == "national");
        var wageMapping = oews.Crosswalk(alternative.Code);
        var wage = wageMapping == null ? null : oews.Wage(national.Code, wageMapping.Code)?.MedianAnnual;
        var projectionMapping = projections.Crosswalk(alternative.Code);
        var change = projectionMapping == null ? null : projections.Row(projectionMapping.Code)?.ChangePercent;
        var mappingNote = wageMapping is { Match: not "exact" }
            ? MappingNote(wageMapping, oews.OccupationTitle(wageMapping.Code))
            : projectionMapping is { Match: not "exact" }
                ? MappingNote(projectionMapping, projections.OccupationTitle(projectionMapping.Code))
                : null;

        return new MarketItemDto(alternative.Code, alternative.Title, new[]
        {
            Figure("medianAnnual", "Median annual wage", wage ?? MarketValue.NotPublished, "usd_per_year", national.Code, national.Title, oews.Source.Id),
            Figure("changePercent", "Projected change, 2025–35", change ?? MarketValue.NotPublished, "percent", national.Code, national.Title, projections.Source.Id)
        }, mappingNote);
    }

    // ---- Pieces ------------------------------------------------------------------

    private static MarketFigureDto Figure(
        string key, string label, MarketValue value, string unit, string areaCode, string areaTitle, string sourceId) =>
        new(key, label,
            // Only a published number is shown; a top-coded figure shows its ceiling, never a stand-in.
            value.Status is MarketValueStatus.Available or MarketValueStatus.TopCoded ? value.Number : null,
            MarketFigureStatuses.Of(value.Status), unit, areaCode, areaTitle, sourceId);

    private static MarketFigureDto TextFigure(
        string key, string label, string? text, bool rowMissing, MarketArea? area, string sourceId) =>
        new(key, label, string.IsNullOrWhiteSpace(text) ? null : text,
            rowMissing ? MarketFigureStatuses.NotPublished : string.IsNullOrWhiteSpace(text) ? MarketFigureStatuses.NotAvailable : MarketFigureStatuses.Available,
            "text", area?.Code ?? "99", area?.Title ?? "U.S.", sourceId);

    private static MarketSectionDto Section(
        string key, string title, string status, string? reason, string note, IReadOnlyList<MarketFigureDto> figures,
        IReadOnlyList<MarketItemDto>? items = null) =>
        new(key, title, status, reason, note, figures, items ?? Array.Empty<MarketItemDto>());

    private static MarketSectionDto Failed(string key, string title, string note) =>
        Section(key, title, MarketSectionStatuses.Failed, CareerAgentErrorCodes.ReferenceUnavailable, note, Array.Empty<MarketFigureDto>());

    /// <summary>Name the published group when its estimate is not unique to the detailed occupation.</summary>
    private static string MappingNote(MarketCrosswalkEntry entry, string? title) => entry.Match switch
    {
        "broad" => $" BLS publishes this occupation only as the broader group {entry.Code} {title}.",
        "shared" => $" BLS publishes one estimate for {entry.Code} {title}, which covers this occupation together with other detailed occupations.",
        _ => string.Empty
    };
}
