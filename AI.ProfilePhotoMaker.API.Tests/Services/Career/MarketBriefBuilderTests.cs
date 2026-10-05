using System.Text.Json;
using AI.ProfilePhotoMaker.API.Services.Career;
using FluentAssertions;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services.Career;

/// <summary>
/// The brief maps the real snapshot faithfully (ADR 0011): every figure equals the published cell,
/// BLS statuses stay statuses, and a missing source only fails the sections that need it.
/// </summary>
public class MarketBriefBuilderTests
{
    private static readonly IMarketReference Real = new EmbeddedMarketReference();

    private static MarketBriefContent Brief(
        string code = "15-1252.00", string? location = "Denver, CO", IMarketReference? reference = null,
        params MarketAlternative[] alternatives) =>
        MarketBriefBuilder.Build(new MarketBriefInput(code, "Title", location, alternatives), reference ?? Real);

    private static MarketSectionDto Section(MarketBriefContent brief, string key) => brief.Sections.Single(s => s.Key == key);

    private static MarketFigureDto Figure(MarketSectionDto section, string key, string? areaCode = null) =>
        section.Figures.Single(f => f.Key == key && (areaCode == null || f.AreaCode == areaCode));

    private static double Number(MarketFigureDto figure) => Convert.ToDouble(figure.Value, System.Globalization.CultureInfo.InvariantCulture);

    // ---- Every figure against the snapshot --------------------------------------

    // Figure key -> raw OEWS column.
    private static readonly Dictionary<string, string> WageColumns = new()
    {
        ["medianAnnual"] = "A_MEDIAN", ["pct10Annual"] = "A_PCT10", ["pct25Annual"] = "A_PCT25", ["pct75Annual"] = "A_PCT75",
        ["pct90Annual"] = "A_PCT90", ["meanAnnual"] = "A_MEAN", ["medianHourly"] = "H_MEDIAN", ["meanPrse"] = "MEAN_PRSE"
    };

    private static readonly Dictionary<string, string> EmploymentColumns = new()
    {
        ["employment"] = "TOT_EMP", ["employmentPrse"] = "EMP_PRSE", ["jobsPer1000"] = "JOBS_1000", ["locationQuotient"] = "LOC_QUOTIENT"
    };

    private static void AssertMatchesRaw(MarketFigureDto figure, string soc, string column)
    {
        var raw = RawSnapshot.Cell(figure.AreaCode!, soc, column)!.Value;
        figure.Status.Should().Be(MarketFigureStatuses.Available, $"{figure.Key} in {figure.AreaTitle}");
        raw.ValueKind.Should().Be(JsonValueKind.Number);
        Number(figure).Should().Be(raw.GetDouble(), $"{figure.Key} in {figure.AreaTitle}");
    }

    [Fact]
    public void EveryFigureOfASoftwareDeveloperBriefInDenverEqualsTheSnapshot()
    {
        var brief = Brief();

        brief.Status.Should().Be("complete");
        brief.Sections.Select(s => s.Key).Should().Equal("wages", "employment", "outlook", "alternatives");
        var wages = Section(brief, "wages");
        var employment = Section(brief, "employment");
        var outlook = Section(brief, "outlook");

        foreach (var figure in wages.Figures.Where(f => f.Key != "medianDifferenceAnnual"))
        {
            AssertMatchesRaw(figure, "15-1252", WageColumns[figure.Key]);
        }
        foreach (var figure in employment.Figures)
        {
            AssertMatchesRaw(figure, "15-1252", EmploymentColumns[figure.Key]);
        }

        var raw = RawSnapshot.Projection("15-1252");
        Number(Figure(outlook, "employment2025")).Should().Be(raw.GetProperty("employment2025Thousands").GetDouble());
        Number(Figure(outlook, "employment2035")).Should().Be(raw.GetProperty("employment2035Thousands").GetDouble());
        Number(Figure(outlook, "changePercent")).Should().Be(raw.GetProperty("changePercent").GetDouble());
        Number(Figure(outlook, "annualOpenings")).Should().Be(raw.GetProperty("annualOpeningsThousands").GetDouble());
        Figure(outlook, "typicalEducation").Value.Should().Be(raw.GetProperty("education").GetString());
    }

    [Fact]
    public void FigureSetsAndOrderFollowTheContract()
    {
        var brief = Brief();

        Section(brief, "wages").Figures.Select(f => $"{f.AreaCode}:{f.Key}").Should().Equal(
            new[] { "99" }.SelectMany(a => WageColumns.Keys.Select(k => $"{a}:{k}"))
                .Concat(WageColumns.Keys.Select(k => $"19740:{k}")).Append("19740:medianDifferenceAnnual"));
        Section(brief, "employment").Figures.Select(f => $"{f.AreaCode}:{f.Key}").Should().Equal(
            "99:employment", "99:employmentPrse", "19740:employment", "19740:employmentPrse", "19740:jobsPer1000", "19740:locationQuotient");
        Section(brief, "outlook").Figures.Select(f => f.Key).Should().Equal(
            "employment2025", "employment2035", "changePercent", "annualOpenings", "typicalEducation");
    }

    [Fact]
    public void KnownValuesAreCopiedAsPublished()
    {
        var brief = Brief();
        var wages = Section(brief, "wages");
        var employment = Section(brief, "employment");
        var outlook = Section(brief, "outlook");

        Number(Figure(wages, "medianAnnual", "99")).Should().Be(135980);
        Number(Figure(wages, "medianAnnual", "19740")).Should().Be(137610);
        Number(Figure(wages, "medianDifferenceAnnual")).Should().Be(1630);
        Number(Figure(employment, "employment", "99")).Should().Be(1687890);
        Number(Figure(outlook, "changePercent")).Should().Be(10.2);
        Number(Figure(outlook, "annualOpenings")).Should().Be(95.3);
        Figure(wages, "medianAnnual", "99").Should().Be(new MarketFigureDto(
            "medianAnnual", "Median annual wage", 135980d, "available", "usd_per_year", "99", "U.S.", "oews"));
        Figure(outlook, "annualOpenings").Unit.Should().Be("jobs_thousands");
        Figure(outlook, "typicalEducation").Unit.Should().Be("text");
        Figure(outlook, "annualOpenings").SourceId.Should().Be("projections");
        Section(brief, "wages").Note.Should().Be(
            "Benchmark wages for this occupation. Not open jobs, not a personal salary prediction, not total compensation.");
    }

    [Fact]
    public void EveryFigureCitesAListedSourceAndAreaAndNothingIsAStandInZero()
    {
        var brief = Brief(alternatives: new[] { new MarketAlternative("15-1211.00", "Computer Systems Analysts") });
        var sourceIds = brief.Sources.Select(s => s.Id).ToList();

        var figures = brief.Sections.SelectMany(s => s.Figures.Concat(s.Items.SelectMany(i => i.Figures))).ToList();

        figures.Should().NotBeEmpty();
        figures.Should().OnlyContain(f => sourceIds.Contains(f.SourceId) && f.AreaCode != null && f.AreaTitle != null);
        figures.Where(f => f.Status != "available" && f.Status != "top_coded").Should().OnlyContain(f => f.Value == null);
        brief.Sources.Select(s => s.Id).Should().Equal("oews", "projections");
        brief.Sources.Should().OnlyContain(s => s.License == "Public domain (U.S. government work)" && s.Citation.Length > 0
            && s.Definition.Length > 0 && s.Coverage.Length > 0 && s.PublishedOn.Length == 10);
        brief.OewsRelease.Should().Be("2025-05");
        brief.ProjectionsRelease.Should().Be("2025-2035");
        brief.Occupation.Published.Should().Be(new MarketPublishedDto(new MarketCodeDto("15-1252", "exact"), new MarketCodeDto("15-1252", "exact")));
        brief.Location.Local.Should().Be(new MarketAreaDto("19740", "Denver-Aurora-Centennial, CO", "metro"));
        brief.NextAction.Should().Be(new MarketNextActionDto("Check your target occupation", "/app/career/occupation"));
    }

    [Fact]
    public void EveryAlternativeFigureEqualsTheSnapshot()
    {
        var brief = Brief(alternatives: new[]
        {
            new MarketAlternative("15-1211.00", "Computer Systems Analysts"),
            new MarketAlternative("15-1251.00", "Computer Programmers"),
            new MarketAlternative("15-1299.08", "Computer Systems Engineers/Architects"),
            new MarketAlternative("15-1241.00", "Fourth, dropped")
        });

        var items = Section(brief, "alternatives").Items;

        items.Select(i => i.Code).Should().Equal("15-1211.00", "15-1251.00", "15-1299.08");
        Section(brief, "alternatives").Status.Should().Be("complete");
        foreach (var item in items)
        {
            var soc = item.Code[..7];
            Number(item.Figures.Single(f => f.Key == "medianAnnual")).Should()
                .Be(RawSnapshot.Cell("99", soc, "A_MEDIAN")!.Value.GetDouble());
            Number(item.Figures.Single(f => f.Key == "changePercent")).Should()
                .Be(RawSnapshot.Projection(soc).GetProperty("changePercent").GetDouble());
        }
    }

    [Fact]
    public void NoAlternativesIsAnHonestUnavailableSection()
    {
        var alternatives = Section(Brief(), "alternatives");

        alternatives.Status.Should().Be("unavailable");
        alternatives.Reason.Should().Be("not_published");
        alternatives.Items.Should().BeEmpty();
    }

    // ---- Statuses ---------------------------------------------------------------

    [Fact]
    public void ATopCodedLocalWageCarriesTheTopCodeAndNoDifference()
    {
        // Pediatric specialist wages in Denver are "#": at or above $239,200 a year / $115 an hour.
        var brief = Brief("29-1214.00");
        var wages = Section(brief, "wages");

        var median = Figure(wages, "medianAnnual", "19740");
        median.Status.Should().Be("top_coded");
        Number(median).Should().Be(239200);
        median.Unit.Should().Be("usd_per_year");
        Figure(wages, "pct90Annual", "19740").Status.Should().Be("top_coded");
        Figure(wages, "medianHourly", "19740").Value.Should().Be(115d);
        Figure(wages, "meanPrse", "19740").Status.Should().Be("available");
        wages.Figures.Should().NotContain(f => f.Key == "medianDifferenceAnnual");
        Figure(wages, "medianAnnual", "99").Status.Should().Be("available");
    }

    [Fact]
    public void ASuppressedLocalCellIsNotAvailableWithNoValue()
    {
        // Funeral home managers in Denver: the median wage is "*".
        RawSnapshot.Cell("19740", "11-9171", "A_MEDIAN")!.Value.GetString().Should().Be("*");

        var wages = Section(Brief("11-9171.00"), "wages");

        var median = Figure(wages, "medianAnnual", "19740");
        median.Status.Should().Be("not_available");
        median.Value.Should().BeNull();
        wages.Figures.Should().NotContain(f => f.Key == "medianDifferenceAnnual");
        Figure(wages, "medianAnnual", "99").Status.Should().Be("available");
        wages.Status.Should().Be("complete");
    }

    [Fact]
    public void AMissingLocalRowIsNotPublishedNeverZero()
    {
        var metro = RawSnapshot.Root.GetProperty("areas").EnumerateArray()
            .First(a => a.GetProperty("type").GetString() == "metro" && !RawSnapshot.HasWageRow(a.GetProperty("code").GetString()!, "15-1252"));
        var title = metro.GetProperty("title").GetString()!;

        var wages = Section(Brief(location: title), "wages");

        wages.Figures.Where(f => f.AreaCode == metro.GetProperty("code").GetString()).Should()
            .OnlyContain(f => f.Status == "not_published" && f.Value == null);
        wages.Figures.Should().NotContain(f => f.Key == "medianDifferenceAnnual");
    }

    [Fact]
    public void ABroadOnlyOccupationStatesTheBroadMapping()
    {
        var brief = Brief("13-1021.00");

        brief.Occupation.Published.Oews.Should().Be(new MarketCodeDto("13-1020", "broad"));
        brief.Occupation.Published.Projections.Should().Be(new MarketCodeDto("13-1020", "broad"));
        Section(brief, "wages").Note.Should().Contain("broader group 13-1020");
        Number(Figure(Section(brief, "wages"), "medianAnnual", "99")).Should()
            .Be(RawSnapshot.Cell("99", "13-1020", "A_MEDIAN")!.Value.GetDouble());
        Section(brief, "wages").Status.Should().Be("complete");
    }

    [Fact]
    public void AnOccupationBlsDoesNotPublishIsUnavailableNotPublished()
    {
        var brief = Brief("21-1011.00");

        brief.Status.Should().Be("complete");
        foreach (var key in new[] { "wages", "employment", "outlook" })
        {
            var section = Section(brief, key);
            section.Status.Should().Be("unavailable");
            section.Reason.Should().Be("not_published");
            section.Figures.Should().BeEmpty();
        }
        brief.Occupation.Published.Should().Be(new MarketPublishedDto(null, null));
    }

    // ---- Location ---------------------------------------------------------------

    [Fact]
    public void AnUnresolvedLocationKeepsNationalFiguresAndAsksForACityAndState()
    {
        var brief = Brief(location: "Atlantis, ZZ");

        brief.Location.Resolution.Should().Be("unresolved");
        brief.Location.Local.Should().BeNull();
        brief.NextAction.Should().Be(new MarketNextActionDto("Add a city and state to your goal", "/app/career/setup"));
        foreach (var key in new[] { "wages", "employment" })
        {
            var section = Section(brief, key);
            section.Status.Should().Be("unavailable");
            section.Reason.Should().Be("location_unresolved");
            section.Figures.Should().OnlyContain(f => f.AreaCode == "99");
            section.Figures.Should().NotBeEmpty();
        }
        Section(brief, "outlook").Status.Should().Be("complete");
        brief.Status.Should().Be("complete");
    }

    [Theory]
    [InlineData("Remote")]
    [InlineData(null)]
    public void RemoteOrBlankIsNationalOnlyAndComplete(string? location)
    {
        var brief = Brief(location: location);

        brief.Location.Resolution.Should().Be("national_only");
        Section(brief, "wages").Status.Should().Be("complete");
        Section(brief, "wages").Figures.Should().OnlyContain(f => f.AreaCode == "99");
        brief.NextAction.Route.Should().Be("/app/career/occupation");
    }

    [Fact]
    public void AStateLocationUsesStateFigures()
    {
        var brief = Brief(location: "Colorado");

        brief.Location.Resolution.Should().Be("state");
        var median = Figure(Section(brief, "wages"), "medianAnnual", "08");
        Number(median).Should().Be(RawSnapshot.Cell("08", "15-1252", "A_MEDIAN")!.Value.GetDouble());
        median.AreaTitle.Should().Be("Colorado");
    }

    // ---- Partial failure --------------------------------------------------------

    [Fact]
    public void AnUnavailableProjectionsSourceFailsOnlyTheOutlookAndAlternatives()
    {
        var brief = Brief(reference: FakeMarketReference.WithoutProjections(), alternatives: new[] { new MarketAlternative("15-1211.00", "x") });

        brief.Status.Should().Be("partial");
        Section(brief, "wages").Status.Should().Be("complete");
        Section(brief, "employment").Status.Should().Be("complete");
        Number(Figure(Section(brief, "wages"), "medianAnnual", "99")).Should().Be(135980);
        var outlook = Section(brief, "outlook");
        outlook.Status.Should().Be("failed");
        outlook.Reason.Should().Be("CareerReferenceUnavailable");
        outlook.Figures.Should().BeEmpty();
        Section(brief, "alternatives").Status.Should().Be("failed");
        brief.Sources.Select(s => s.Id).Should().Equal("oews");
        brief.ProjectionsRelease.Should().BeNull();
        brief.Occupation.Published.Projections.Should().BeNull();
    }

    [Fact]
    public void AnUnavailableOewsSourceFailsWagesAndEmploymentButKeepsTheOutlook()
    {
        var brief = Brief(reference: FakeMarketReference.WithoutOews());

        brief.Status.Should().Be("partial");
        Section(brief, "wages").Status.Should().Be("failed");
        Section(brief, "employment").Status.Should().Be("failed");
        Section(brief, "outlook").Status.Should().Be("complete");
        Number(Figure(Section(brief, "outlook"), "changePercent")).Should().Be(10.2);
        brief.NextAction.Route.Should().Be("/app/career/occupation");
    }

    // ---- Pay basis (review R1) -------------------------------------------------

    [Fact]
    public void AnAnnualOnlyOccupationHasNoHourlyWageRatherThanTooFewResponses()
    {
        // Elementary teachers: BLS publishes annual wages only.
        var row = new EmbeddedMarketReference().Oews!.Wage("99", "25-2021")!;

        row.MedianAnnual.Status.Should().Be(MarketValueStatus.Available);
        row.MedianHourly.Status.Should().Be(MarketValueStatus.NotPublished);
    }

    [Fact]
    public void AnHourlyOnlyOccupationHasNoAnnualWagesRatherThanTooFewResponses()
    {
        // Actors: BLS publishes hourly wages only.
        var row = new EmbeddedMarketReference().Oews!.Wage("99", "27-2011")!;

        row.MedianHourly.Number.Should().Be(29.05);
        new[] { row.MeanAnnual, row.Pct10Annual, row.Pct25Annual, row.MedianAnnual, row.Pct75Annual, row.Pct90Annual }
            .Select(v => v.Status).Should().OnlyContain(s => s == MarketValueStatus.NotPublished);
    }
}
