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
        return JsonSerializer.Deserialize<List<PayObservation>>(json.RootElement.GetProperty("covered").GetRawText(), Options)!
            .Where(x => x.Role == "software").Take(count).Select(x => x with { Role = "15-1252.00" }).ToList();
    }
    private static PayAnalysisInput Input(IReadOnlyList<PayObservation>? observations = null, int? requested = 150000) =>
        new(3, 2, "15-1252.00", "Software Developers", "Denver, CO", requested,
            "19740", "Denver-Aurora-Centennial, CO", "metro", "2025-05",
            EmbeddedMarketReference.ExpectedSha256, "2025-2035", PayEvidenceRules.RuleVersion,
            observations == null ? null : "fixture", observations ?? Array.Empty<PayObservation>(), AsOf);

    [Fact]
    public void QualifiedFixtureProducesIntervalAndCitedBenchmarkAndScenario()
    {
        var result = PayAnalysisBuilder.Build(Input(Fixture(12)), Reference);
        Assert.Equal("complete", result.Sections.Personalized.Status);
        Assert.Equal(110300, result.Sections.Personalized.Interval!.Low);
        Assert.Equal(195200, result.Sections.Personalized.Interval.High);
        Assert.Equal(12, result.Sections.Personalized.Cohort.Included);
        Assert.Equal(6, result.Sections.Personalized.Cohort.Employers);
        Assert.Equal(135980d, result.Sections.Benchmark.Figures.Single(f => f.Key == "medianAnnual" && f.AreaCode == "99").Value);
        Assert.Equal(137610d, result.Sections.Benchmark.Figures.Single(f => f.Key == "medianAnnual" && f.AreaCode == "19740").Value);
        Assert.Equal(14020d, result.Sections.Scenario.GapAnnual);
        Assert.Equal(10.3d, result.Sections.Scenario.GapPercent);
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
        var sparse = PayAnalysisBuilder.Build(Input(Fixture(7)), Reference).Sections.Personalized;
        Assert.Equal("insufficient_evidence", sparse.Status);
        Assert.Equal(7, sparse.Cohort.Included);
        Assert.Null(sparse.Interval);
        var rejected = Fixture(7).Concat(new[] { Fixture(1)[0] with { Currency = "EUR", Eligible = null } }).ToList();
        var excluded = PayAnalysisBuilder.Build(Input(rejected), Reference).Sections.Personalized.Cohort;
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
        Assert.NotEqual(PayAnalysisBuilder.Hash(input), PayAnalysisBuilder.Hash(input with { ProfileVersion = 4 }));
        Assert.NotEqual(PayAnalysisBuilder.Hash(input), PayAnalysisBuilder.Hash(input with { GoalVersion = 4 }));
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
}
