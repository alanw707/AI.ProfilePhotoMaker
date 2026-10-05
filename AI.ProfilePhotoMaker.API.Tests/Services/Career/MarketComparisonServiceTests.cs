using AI.ProfilePhotoMaker.API.Services.Career;
using FluentAssertions;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services.Career;

/// <summary>The normalized market comparison (#385, ADR 0014) against the shipped snapshot, read independently from its JSON.</summary>
public class MarketComparisonServiceTests
{
    private const string Software = "15-1252.00";
    private static readonly EmbeddedMarketReference Reference = new();
    private static readonly MarketComparisonService Service = new(null!, Reference, TimeProvider.System);

    private static MarketComparisonDto Compare(
        string metric = "median_wage", string level = "state", string? areas = null, string? q = null, string code = Software, int max = 500)
    {
        var outcome = Service.Compare(code, null, metric, level, areas, q, max);
        outcome.Kind.Should().Be(CareerOutcomeKind.Ok, outcome.ErrorCode);
        return outcome.Value!;
    }

    private static double Raw(string area, string soc, string field) => RawSnapshot.Cell(area, soc, field)!.Value.GetDouble();

    [Fact]
    public void MetricsListsFiveWithProjectionUnsupportedAndNationalOnly()
    {
        var metrics = Service.GetMetrics().Value!.Metrics;

        metrics.Select(m => m.Key).Should().Equal("median_wage", "employment", "location_quotient", "share_of_national_employment", "projected_change");
        var projected = metrics.Single(m => m.Key == "projected_change");
        (projected.Supported, projected.Reason).Should().Be((false, "national_only_source"));
        projected.GeographyLevels.Should().Equal("national");
        metrics.Where(m => m.Supported).Should().OnlyContain(m => m.Reason == null && m.GeographyLevels.Contains("state"));
        metrics.Single(m => m.Key == "median_wage").GeographyLevels.Should().Equal("national", "state", "metro");
        metrics.Single(m => m.Key == "location_quotient").GeographyLevels.Should().Equal("state", "metro");
    }

    [Fact]
    public void StateComparisonHasEveryStateAndTheNationWithRawSnapshotValues()
    {
        var result = Compare();

        result.Level.Should().Be("state");
        result.Areas.Should().HaveCount(51).And.OnlyContain(a => a.Type == "state");
        result.Areas.Single(a => a.AreaCode == "08").Value.Should().Be(138390).And.Be(Raw("08", "15-1252", "A_MEDIAN"));
        result.National.Should().Be(new MarketComparisonNationalDto("99", "U.S.", 135980, "available"));
        result.National.Value.Should().Be(Raw("99", "15-1252", "A_MEDIAN"));
        result.Occupation.Should().Be(new MarketComparisonOccupationDto(Software, "Software Developers", "15-1252", "exact"));
        result.SelectionLimit.Should().Be(3);
        result.Truncated.Should().BeFalse();
    }

    [Fact]
    public void EveryAreaValueMatchesTheRawSnapshot()
    {
        foreach (var area in Compare("employment").Areas)
        {
            RawSnapshot.Cell(area.AreaCode, "15-1252", "TOT_EMP")!.Value.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Number);
            area.Value.Should().Be(Raw(area.AreaCode, "15-1252", "TOT_EMP"));
        }
    }

    [Fact]
    public void MetroComparisonIncludesAlaskaAndHawaiiAndNoNonmetropolitanAreas()
    {
        var result = Compare(level: "metro");

        result.Areas.Should().HaveCount(393).And.OnlyContain(a => a.Type == "metro");
        result.Areas.Select(a => a.AreaTitle).Should().Contain(new[] { "Anchorage, AK", "Urban Honolulu, HI", "Denver-Aurora-Centennial, CO" });
        result.Areas.Should().NotContain(a => a.AreaTitle.Contains("nonmetropolitan", StringComparison.OrdinalIgnoreCase));
        result.Areas.Single(a => a.AreaCode == "19740").Value.Should().Be(Raw("19740", "15-1252", "A_MEDIAN"));
    }

    [Fact]
    public void RanksAreBestFirstAndDeterministicOnTiesWithSuppressedAreasUnranked()
    {
        var result = Compare("location_quotient");
        var again = Compare("location_quotient");
        var ranked = result.Areas.Where(a => a.Rank != null).ToList();

        again.Should().BeEquivalentTo(result, o => o.WithStrictOrdering());
        ranked.Select(a => a.Rank).Should().Equal(Enumerable.Range(1, ranked.Count).Select(i => (int?)i));
        ranked.Should().OnlyContain(a => a.RankedOf == ranked.Count && a.Status == "available");
        for (var i = 1; i < ranked.Count; i++)
        {
            (ranked[i - 1].Value > ranked[i].Value
                || (ranked[i - 1].Value == ranked[i].Value && string.CompareOrdinal(ranked[i - 1].AreaCode, ranked[i].AreaCode) < 0))
                .Should().BeTrue();
        }
        // The snapshot has real ties (0.15 twice), so the tie rule is exercised, not assumed.
        ranked.GroupBy(a => a.Value).Should().Contain(g => g.Count() > 1);
        result.Areas.Where(a => a.Rank == null).Should().OnlyContain(a => a.Value == null && a.RankedOf == null);
    }

    [Fact]
    public void ASuppressedStateMedianIsNotAvailableWithoutAValueAndIsNeverRankedLowest()
    {
        var result = Compare();
        var alaska = result.Areas.Single(a => a.AreaCode == "02");

        RawSnapshot.Cell("02", "15-1252", "A_MEDIAN")!.Value.GetString().Should().Be("*");
        (alaska.Status, alaska.Value, alaska.Rank, alaska.RankedOf).Should().Be(("not_available", null, null, null));
        var ranked = result.Areas.Where(a => a.Rank != null).ToList();
        ranked.Should().HaveCount(50);
        ranked.Max(a => a.Rank).Should().Be(50);
    }

    [Fact]
    public void ASuppressedMetroMedianIsNotAvailableWithANullValue()
    {
        var denver = Compare(level: "metro", code: "11-9171.00").Areas.Single(a => a.AreaCode == "19740");

        RawSnapshot.Cell("19740", "11-9171", "A_MEDIAN")!.Value.GetString().Should().Be("*");
        (denver.Status, denver.Value, denver.Rank).Should().Be(("not_available", null, null));
    }

    [Fact]
    public void ATopCodedCellCarriesTheCeilingAndIsNotRanked()
    {
        var result = Compare(code: "29-1022.00");
        var alabama = result.Areas.Single(a => a.AreaCode == "01");

        RawSnapshot.Cell("01", "29-1022", "A_MEDIAN")!.Value.GetString().Should().Be("#");
        (alabama.Status, alabama.Value, alabama.Rank).Should().Be(("top_coded", 239200, null));
        result.Areas.Where(a => a.Rank != null).Should().NotContain(a => a.Status == "top_coded");
    }

    [Fact]
    public void AnAreaWithoutARowIsNotPublished()
    {
        var area = Compare(level: "metro", code: "11-9171.00").Areas.First(a => RawSnapshot.HasWageRow(a.AreaCode, "11-9171") == false);

        (area.Status, area.Value, area.Rank).Should().Be(("not_published", null, null));
    }

    [Fact]
    public void ShareOfNationalEmploymentIsAreaOverNationalRoundedToOneDecimal()
    {
        var colorado = Compare("share_of_national_employment").Areas.Single(a => a.AreaCode == "08");

        Raw("08", "15-1252", "TOT_EMP").Should().Be(43320);
        Raw("99", "15-1252", "TOT_EMP").Should().Be(1687890);
        colorado.Value.Should().Be(2.6).And.Be(Math.Round(43320d / 1687890d * 100, 1));
        colorado.ShareOfNationalEmployment.Should().Be(2.6);
    }

    [Fact]
    public void QueryFiltersByTitleCaseInsensitivelyButRanksStayWhole()
    {
        var all = Compare(level: "metro");
        var filtered = Compare(level: "metro", q: "dEnVeR");

        filtered.Areas.Should().NotBeEmpty().And.OnlyContain(a => a.AreaTitle.Contains("denver", StringComparison.OrdinalIgnoreCase));
        var denver = filtered.Areas.Single(a => a.AreaCode == "19740");
        denver.Rank.Should().Be(all.Areas.Single(a => a.AreaCode == "19740").Rank);
        denver.RankedOf.Should().Be(all.Areas.Count(a => a.Rank != null));
    }

    [Fact]
    public void AreasMarkUpToThreeSelectedAndIgnoreUnknownCodes()
    {
        var result = Compare(areas: "08, 99999 ,06,36,48");

        result.Areas.Where(a => a.Selected).Select(a => a.AreaCode).Should().BeEquivalentTo("08", "06");
        Compare(areas: "08,06,36,48,12").Areas.Count(a => a.Selected).Should().Be(3);
        Compare(areas: "zzz").Areas.Should().OnlyContain(a => !a.Selected);
        Compare(areas: "19740").Areas.Should().OnlyContain(a => !a.Selected); // a metro code is not a state
    }

    [Fact]
    public void TheResponseIsCappedAndSaysSo()
    {
        var capped = Compare(level: "metro", max: 10);

        capped.Areas.Should().HaveCount(10);
        capped.Truncated.Should().BeTrue();
        capped.Areas[0].Rank.Should().Be(1);
        Compare(level: "metro", max: 393).Truncated.Should().BeFalse();
        MarketComparisonService.MaxAreas.Should().Be(500);
    }

    [Fact]
    public void ThePayloadCarriesReleasePublicationDateAndCoverageForDisclosure()
    {
        var reference = Compare().Reference;
        var source = Reference.Oews!.Source;

        reference.Release.Should().Be(source.ReferencePeriod).And.Be(RawSnapshot.Root.GetProperty("sources").GetProperty("oews").GetProperty("referencePeriod").GetString());
        reference.PublishedOn.Should().Be(source.PublishedOn).And.MatchRegex(@"^\d{4}-\d{2}-\d{2}$");
        reference.Coverage.Should().Be(source.Coverage).And.NotBeNullOrWhiteSpace();
        reference.DefinitionsUrl.Should().NotBeNullOrWhiteSpace();
        reference.Citation.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("nope", "state")]
    [InlineData("median_wage", "county")]
    [InlineData("median_wage", "national")]
    [InlineData(null, null)]
    public void UnknownMetricOrLevelIsAValidationError(string? metric, string? level)
    {
        var outcome = Service.Compare(Software, null, metric, level, null, null);

        outcome.Kind.Should().Be(CareerOutcomeKind.Invalid);
        outcome.ErrorCode.Should().Be("ValidationError");
    }

    [Fact]
    public void AnUnsupportedMetricIsAConflictWithItsReason()
    {
        var outcome = Service.Compare(Software, null, "projected_change", "state", null, null);

        outcome.Kind.Should().Be(CareerOutcomeKind.AlreadyExists);
        (outcome.ErrorCode, outcome.Message).Should().Be(("CareerMetricUnsupported", "national_only_source"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NoOccupationIsAConflict(string? code)
    {
        var outcome = Service.Compare(code, null, "median_wage", "state", null, null);

        outcome.Kind.Should().Be(CareerOutcomeKind.AlreadyExists);
        outcome.ErrorCode.Should().Be("CareerOccupationRequired");
    }

    [Fact]
    public void WithoutTheOewsHalfTheComparisonIsUnavailable()
    {
        var service = new MarketComparisonService(null!, FakeMarketReference.WithoutOews(), TimeProvider.System);

        service.Compare(Software, null, "median_wage", "state", null, null).Kind.Should().Be(CareerOutcomeKind.Unavailable);
    }
}
