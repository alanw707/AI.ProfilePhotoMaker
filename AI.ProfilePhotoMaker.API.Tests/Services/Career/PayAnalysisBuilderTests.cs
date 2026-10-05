using System.Text.Json;
using System.Text.Json.Serialization;
using AI.ProfilePhotoMaker.API.Services.Career;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services.Career;

public class PayAnalysisBuilderTests
{
    private static readonly IMarketReference Reference = new EmbeddedMarketReference();
    private static readonly DateTime AsOf = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
    private static IReadOnlyList<PayObservation> Fixture(int count)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Services", "Career", "PayEvidenceFixtures", "pay-evidence-fixtures.json")));
        // The adapter contract: rows come back keyed by the requested occupation code and area code.
        return JsonSerializer.Deserialize<List<PayObservation>>(json.RootElement.GetProperty("covered").GetRawText(), Options)!
            .Where(x => x.Role == "software").Take(count)
            .Select(x => x with { Role = "15-1252.00", Geography = "19740" }).ToList();
    }
    private static PayAnalysisInput Input(IReadOnlyList<PayObservation>? observations = null, int? requested = 150000) =>
        new(3, 2, "15-1252.00", "Software Developers", "Denver, CO", requested,
            "19740", "Denver-Aurora-Centennial, CO", "metro", "2025-05",
            EmbeddedMarketReference.ExpectedSha256, "2025-2035", PayEvidenceRules.RuleVersion,
            observations == null ? null : "fixture", observations ?? Array.Empty<PayObservation>(), AsOf);

    /// <summary>An all-passed gate set, as a licensed provider would produce; tests only.</summary>
    private static PayGateDecision Authorized() => PayGateDecision.FromRows(
        PayEvidenceGates.Current().Rows.Select(r => r with { Status = PayGateStatus.Passed }).ToList());

    [Fact]
    public void AFullCohortCannotPublishAdvertisedPayWhileNoProviderHasRights()
    {
        // The data is complete, but authorization is not the data's to give (review P0).
        var blocked = PayAnalysisBuilder.Build(Input(Fixture(12)), Reference).Sections.Personalized;

        Assert.Equal("unavailable", blocked.Status);
        Assert.Equal("provider_rights_unverified", blocked.Reason);
        Assert.Null(blocked.Interval);
        Assert.Equal(12, blocked.Cohort.Included);
    }

    [Fact]
    public void QualifiedFixtureProducesIntervalAndCitedBenchmarkAndScenario()
    {
        var result = PayAnalysisBuilder.Build(Input(Fixture(12)), Reference, qualification: Authorized());
        Assert.Equal("complete", result.Sections.Personalized.Status);
        Assert.Equal(110300, result.Sections.Personalized.Interval!.Low);
        Assert.Equal(195200, result.Sections.Personalized.Interval.High);
        Assert.Equal(12, result.Sections.Personalized.Cohort.Included);
        Assert.Equal(6, result.Sections.Personalized.Cohort.Employers);
        Assert.Equal(135980d, result.Sections.Benchmark.Figures.Single(f => f.Key == "medianAnnual" && f.AreaCode == "99").Value);
        Assert.Equal(137610d, result.Sections.Benchmark.Figures.Single(f => f.Key == "medianAnnual" && f.AreaCode == "19740").Value);
        Assert.Equal(12390d, result.Sections.Scenario.GapAnnual);
        Assert.Equal(9.0d, result.Sections.Scenario.GapPercent);
    }

    [Fact]
    public void NoProviderAndSparseCohortsNeverOfferAnInterval()
    {
        var blocked = PayAnalysisBuilder.Build(Input(), Reference);
        Assert.Equal("complete", blocked.Sections.Benchmark.Status);
        Assert.Equal("unavailable", blocked.Sections.Personalized.Status);
        Assert.Equal("provider_rights_unverified", blocked.Sections.Personalized.Reason);
        Assert.Null(blocked.Sections.Personalized.Interval);
        Assert.Contains("provider_rights_unverified", blocked.Qualification.BlockedReasons);
        var sparse = PayAnalysisBuilder.Build(Input(Fixture(7)), Reference, qualification: Authorized()).Sections.Personalized;
        Assert.Equal("insufficient_evidence", sparse.Status);
        Assert.Equal(7, sparse.Cohort.Included);
        Assert.Null(sparse.Interval);
        var rejected = Fixture(7).Concat(new[] { Fixture(1)[0] with { Currency = "EUR", Eligible = null } }).ToList();
        Assert.Equal("unavailable", PayAnalysisBuilder.Build(Input(Fixture(7)), Reference).Sections.Personalized.Status);
        var excluded = PayAnalysisBuilder.Build(Input(rejected), Reference, qualification: Authorized()).Sections.Personalized.Cohort;
        Assert.Equal(1, excluded.Excluded);
        Assert.Equal(1, excluded.ExclusionReasons["unknown or non-USD currency"]);
        Assert.Equal(1, excluded.ExclusionReasons["work-location eligibility unknown"]);
        Assert.Equal("unavailable", PayAnalysisBuilder.Build(Input(requested: null), Reference).Sections.Scenario.Status);
    }

    [Fact]
    public void ProviderOutageDoesNotEraseTheBenchmark()
    {
        var result = PayAnalysisBuilder.Build(Input(), Reference, sourceFailed: true);
        Assert.Equal("partial", result.Status);
        Assert.Equal("complete", result.Sections.Benchmark.Status);
        Assert.Equal("failed", result.Sections.Personalized.Status);
        Assert.Null(result.Sections.Personalized.Interval);
    }

    [Fact]
    public void HashIsCanonicalAndCoversPinnedFields()
    {
        var input = Input(Fixture(12));
        Assert.Equal(PayAnalysisBuilder.Build(input, Reference).InputHash, PayAnalysisBuilder.Build(input, Reference).InputHash);
        Assert.NotEqual(PayAnalysisBuilder.Hash(input), PayAnalysisBuilder.Hash(input with { AreaCode = "99" }));
        Assert.NotEqual(PayAnalysisBuilder.Hash(input), PayAnalysisBuilder.Hash(input with { RequestedAnnual = 155000 }));
        Assert.NotEqual(PayAnalysisBuilder.Hash(input), PayAnalysisBuilder.Hash(input with { OewsSnapshotSha256 = "other" }));
        // Pinned versions do not change a figure, so they are stored metadata, not hash inputs.
        Assert.Equal(PayAnalysisBuilder.Hash(input), PayAnalysisBuilder.Hash(input with { ProfileVersion = 4, GoalVersion = 4 }));
        Assert.NotEqual(PayAnalysisBuilder.Hash(input), PayAnalysisBuilder.Hash(input with { AsOf = AsOf.AddDays(-1) }));
        Assert.NotEqual(PayAnalysisBuilder.Hash(input), PayAnalysisBuilder.Hash(input with { LocationText = "Boulder, CO" }));
        Assert.NotEqual(PayAnalysisBuilder.Hash(input), PayAnalysisBuilder.Hash(input with { OccupationCode = "15-1299.08" }));
        Assert.NotEqual(PayAnalysisBuilder.Hash(input), PayAnalysisBuilder.Hash(input with { OccupationTitle = "Other" }));
        Assert.NotEqual(PayAnalysisBuilder.Hash(input), PayAnalysisBuilder.Hash(input with { AreaTitle = "Other" }));
        Assert.NotEqual(PayAnalysisBuilder.Hash(input), PayAnalysisBuilder.Hash(input with { AreaResolution = "state" }));
        Assert.NotEqual(PayAnalysisBuilder.Hash(input), PayAnalysisBuilder.Hash(input with { OewsRelease = "other" }));
        Assert.NotEqual(PayAnalysisBuilder.Hash(input), PayAnalysisBuilder.Hash(input with { ProjectionsRelease = "other" }));
        Assert.NotEqual(PayAnalysisBuilder.Hash(input), PayAnalysisBuilder.Hash(input with { RuleVersion = "other" }));
        Assert.NotEqual(PayAnalysisBuilder.Hash(input), PayAnalysisBuilder.Hash(input with { ObservationSourceId = "other" }));
        Assert.NotEqual(PayAnalysisBuilder.Hash(input), PayAnalysisBuilder.Hash(input with { Observations = Fixture(7) }));
    }

    [Fact]
    public void SharedPublishedCodeIsDisclosedAndInputCannotCarryDemographicsOrContacts()
    {
        var result = PayAnalysisBuilder.Build(Input() with { OccupationCode = "15-1299.08" }, Reference);
        Assert.Equal("shared", result.Published!.Match);
        Assert.Contains("one estimate for 15-1299", result.Sections.Benchmark.Note);
        foreach (var name in new[] { "Photo", "Gender", "Ethnicity", "Email", "Phone", "Contact" })
            Assert.Null(typeof(PayAnalysisInput).GetProperty(name));
    }

    [Fact]
    public void CohortFlagsComeFromTheRulesThresholds()
    {
        // 5 of 12 observations from one employer = 41.7% -> concentrated, and the flag is the
        // rule's 40% threshold, not the builder's own.
        var skewed = Fixture(12).Select((r, i) => i < 5 ? r with { Employer = "Big Co" } : r).ToList();
        var cohort = PayAnalysisBuilder.Build(Input(skewed), Reference, qualification: Authorized()).Sections.Personalized.Cohort;
        Assert.Equal(0.42m, cohort.LargestEmployerShare);
        Assert.True(cohort.Concentrated);

        var spread = Fixture(12).Select((r, i) => i < 4 ? r with { Employer = "Big Co" } : r).ToList();
        Assert.False(PayAnalysisBuilder.Build(Input(spread), Reference, qualification: Authorized()).Sections.Personalized.Cohort.Concentrated);
    }

    [Fact]
    public void TheInputHashCoversEverythingThatCanChangeAFigure()
    {
        var input = Input(Fixture(12));
        var hash = PayAnalysisBuilder.Hash(input);

        Assert.Equal(hash, PayAnalysisBuilder.Hash(input));
        // A changed observation value, the as-of date and the matched location text all matter.
        Assert.NotEqual(hash, PayAnalysisBuilder.Hash(input with { Observations = Fixture(12).Select((r, i) => i == 0 ? r with { Low = r.Low + 1000 } : r).ToList() }));
        Assert.NotEqual(hash, PayAnalysisBuilder.Hash(input with { AsOf = AsOf.AddDays(-1) }));
        Assert.NotEqual(hash, PayAnalysisBuilder.Hash(input with { LocationText = "Boulder, CO" }));
        Assert.NotEqual(hash, PayAnalysisBuilder.Hash(input with { RequestedAnnual = 160000 }));
        Assert.NotEqual(hash, PayAnalysisBuilder.Hash(input with { ObservationSourceId = "other-source" }));
        // The pay source changes the scenario section, so it is part of the canonical document too.
        Assert.NotEqual(hash, PayAnalysisBuilder.Hash(input with { RequestedPaySource = "desiredPayMax" }));
        // Pinned versions do not change a figure, so they are metadata, not hash inputs.
        Assert.Equal(hash, PayAnalysisBuilder.Hash(input with { ProfileVersion = 9, GoalVersion = 8 }));
        // The digest covers content, not just the count.
        Assert.NotEqual(PayAnalysisBuilder.ObservationDigest(Fixture(12)), PayAnalysisBuilder.ObservationDigest(Fixture(11)));
    }

    [Fact]
    public void ObservationsAreMatchedOnTheRequestedOccupationAndAreaKeys()
    {
        // The adapter contract: rows come back keyed by the requested code and area, and the
        // cohort filters compare on exactly those keys.
        var rows = Fixture(12).Select(r => r with { Role = "15-1252.00", Geography = "19740" }).ToList();
        var result = PayAnalysisBuilder.Build(Input(rows), Reference, qualification: Authorized());

        Assert.Equal(12, result.Sections.Personalized.Cohort.Included);
    }

    [Fact]
    public void ThePaySourceIsRecordedSoThePageCanSayWhereTheTargetCameFrom()
    {
        // The runner records which end of the goal's desired pay it compared against; the builder
        // reports it verbatim, and recompute reproduces it because the row stores it.
        var scenario = PayAnalysisBuilder.Build(
            Input(Fixture(12)) with { RequestedPaySource = "desiredPayMax" }, Reference).Sections.Scenario;

        Assert.Equal("desiredPayMax", scenario.RequestedPaySource);
        Assert.Null(PayAnalysisBuilder.Build(Input(Fixture(12)), Reference).Sections.Scenario.RequestedPaySource);
    }

    [Fact]
    public void TheScenarioNamesTheAreaItComparesAgainstAndWhereTheTargetCameFrom()
    {
        var scenario = PayAnalysisBuilder.Build(
            Input(Fixture(12)) with { RequestedPaySource = "desiredPayMin" }, Reference).Sections.Scenario;

        Assert.Equal("19740", scenario.BenchmarkAreaCode);
        Assert.Equal("Denver-Aurora-Centennial, CO", scenario.BenchmarkAreaTitle);
        Assert.Equal(137610d, scenario.BenchmarkMedianAnnual);
        Assert.Equal(12390d, scenario.GapAnnual);
        Assert.Equal("desiredPayMin", scenario.RequestedPaySource);
    }
}
