using System.Text.Json;
using System.Text.Json.Serialization;
using AI.ProfilePhotoMaker.API.Services.Career;

namespace AI.ProfilePhotoMaker.API.Tests.Services.Career;

public class PayEvidenceRulesTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Services", "Career", "PayEvidenceFixtures", "pay-evidence-fixtures.json")));

    private static List<PayObservation> Rows(JsonElement element) =>
        JsonSerializer.Deserialize<List<PayObservation>>(element.GetRawText(), JsonOptions)!;

    private static PayEvidenceQuery Query(string role, string geography) => new(role, geography, "senior", "full-time");

    private static void AssertExpected(PayEvidenceResult result, JsonElement expected)
    {
        Assert.Equal(expected.GetProperty("included").GetInt32(), result.Included);
        Assert.Equal(expected.GetProperty("employers").GetInt32(), result.Employers);
        Assert.Equal(expected.GetProperty("employerConcentration").GetDecimal(), result.EmployerConcentration);
        Assert.Equal(expected.GetProperty("excluded").GetInt32(), result.Excluded);
        Assert.Equal(expected.GetProperty("decision").GetString(), result.Decision);
        var interval = expected.GetProperty("interval");
        if (interval.ValueKind == JsonValueKind.Null)
        {
            Assert.Null(result.Interval);
        }
        else
        {
            Assert.Equal(interval.GetProperty("low").GetDecimal(), result.Interval!.Low);
            Assert.Equal(interval.GetProperty("high").GetDecimal(), result.Interval.High);
            Assert.Equal(interval.GetProperty("unit").GetString(), result.Interval.Unit);
            Assert.Equal(interval.GetProperty("definition").GetString(), result.Interval.Definition);
        }
        var reasons = expected.GetProperty("exclusionReasons");
        Assert.Equal(reasons.EnumerateObject().Count(), result.ExclusionReasons.Count);
        foreach (var reason in reasons.EnumerateObject())
        {
            Assert.Equal(reason.Value.GetInt32(), result.ExclusionReasons[reason.Name]);
        }
    }

    [Theory]
    [InlineData("software", "Denver, CO", 110300, 195200)]
    [InlineData("operations", "Denver, CO", 84300, 156200)]
    [InlineData("nursing", "Seattle, WA", 97300, 162200)]
    public void CoveredFamiliesReproduceAdvertisedIntervals(string role, string geography, int low, int high)
    {
        using var fixture = Fixture();
        var root = fixture.RootElement;
        var result = PayEvidenceRules.Evaluate(Rows(root.GetProperty("covered")), Query(role, geography),
            root.GetProperty("asOf").GetDateTime());
        Assert.Equal(root.GetProperty("ruleVersion").GetString(), result.RuleVersion);
        AssertExpected(result, root.GetProperty("expectedResults").GetProperty(role));
        Assert.Equal(low, result.Interval!.Low);
        Assert.Equal(high, result.Interval.High);
        Assert.Equal("Employer-disclosed advertised pay, not an offer prediction.", result.Note);
        Assert.False(result.PersonalizedAllowed);
        Assert.Contains("provider_rights_unverified", result.BlockedReasons);
    }

    [Fact]
    public void InterpolationUsesFractionalPositions()
    {
        Assert.Equal(17.5m, PayEvidenceRules.Quantile(new decimal[] { 10, 20, 30, 40 }, .25m));
        Assert.Equal(32.5m, PayEvidenceRules.Quantile(new decimal[] { 10, 20, 30, 40 }, .75m));
    }

    [Fact]
    public void EdgeCasesExcludeReasonsButIncludeExplicitHourlyHours()
    {
        using var fixture = Fixture();
        var root = fixture.RootElement;
        var rows = Rows(root.GetProperty("covered"));
        rows.AddRange(Rows(root.GetProperty("edgeCases")));
        var result = PayEvidenceRules.Evaluate(rows, Query("software", "Denver, CO"), root.GetProperty("asOf").GetDateTime());
        AssertExpected(result, root.GetProperty("expectedResults").GetProperty("edgeCases"));
        Assert.Equal(13, result.Included);
        foreach (var reason in new[] { "duplicate requisition", "work-location eligibility unknown", "not employer-disclosed pay",
            "outside 90-day lookback", "unknown pay basis or annual hours", "different or unknown level" })
        {
            Assert.Equal(1, result.ExclusionReasons[reason]);
        }
        var hourly = rows.Single(r => r.Id == "hourly");
        var hourlyOnly = PayEvidenceRules.Normalize(hourly, root.GetProperty("asOf").GetDateTime());
        Assert.Equal(99840m, hourlyOnly.Low);
        Assert.Equal(145600m, hourlyOnly.High);
    }

    [Fact]
    public void InsufficientObservationsOrEmployersNeverProducesAnInterval()
    {
        using var fixture = Fixture();
        var root = fixture.RootElement;
        var software = Rows(root.GetProperty("covered")).Where(r => r.Role == "software").ToList();
        var asOf = root.GetProperty("asOf").GetDateTime();
        var few = PayEvidenceRules.Evaluate(software.Take(7), Query("software", "Denver, CO"), asOf);
        AssertExpected(few, root.GetProperty("expectedResults").GetProperty("sparse"));
        Assert.Equal("Insufficient independent current observations or employers.", few.Note);
        var one = PayEvidenceRules.Evaluate(software.Select(r => r with { Employer = "one employer" }), Query("software", "Denver, CO"), asOf);
        AssertExpected(one, root.GetProperty("expectedResults").GetProperty("oneEmployer"));
        var none = PayEvidenceRules.Evaluate(Array.Empty<PayObservation>(), Query("software", "Denver, CO"), asOf);
        Assert.Null(none.Interval);
        Assert.Equal(0, none.EmployerConcentration);
    }

    [Fact]
    public void LookbackIncludesDay90ButExcludesDay91()
    {
        using var fixture = Fixture();
        var root = fixture.RootElement;
        var row = Rows(root.GetProperty("covered"))[0];
        var asOf = root.GetProperty("asOf").GetDateTime();
        Assert.Empty(PayEvidenceRules.Normalize(row with { UpdatedAt = asOf.AddDays(-90) }, asOf).Reasons);
        Assert.Contains("outside 90-day lookback", PayEvidenceRules.Normalize(row with { UpdatedAt = asOf.AddDays(-91) }, asOf).Reasons);
        Assert.Contains("outside 90-day lookback", PayEvidenceRules.Normalize(row with { UpdatedAt = asOf.AddSeconds(1) }, asOf).Reasons);
    }

    [Fact]
    public void CurrencyRemoteAndUnknownBasisCannotBecomePay()
    {
        using var fixture = Fixture();
        var root = fixture.RootElement;
        var row = Rows(root.GetProperty("covered"))[0] with
        {
            Currency = "EUR", Eligible = null, Basis = PayBasis.Hourly, AnnualHours = null
        };
        var result = PayEvidenceRules.Evaluate(new[] { row }, Query("software", "Denver, CO"), root.GetProperty("asOf").GetDateTime());
        Assert.Null(result.Interval);
        Assert.Equal(0, result.Included);
        Assert.Equal(1, result.ExclusionReasons["unknown or non-USD currency"]);
        Assert.Equal(1, result.ExclusionReasons["work-location eligibility unknown"]);
        Assert.Equal(1, result.ExclusionReasons["unknown pay basis or annual hours"]);
    }

    [Fact]
    public void CallerSuppliedBenchmarkNeverReplacesAnUnsupportedAdvertisedInterval()
    {
        using var fixture = Fixture();
        var root = fixture.RootElement;
        var rows = Rows(root.GetProperty("covered")).Take(7);
        var query = Query("software", "Denver, CO");
        var asOf = root.GetProperty("asOf").GetDateTime();
        Assert.Null(PayEvidenceRules.Evaluate(rows, query, asOf).BenchmarkFallback);

        // The caller must obtain this separately from the pinned OEWS snapshot; it is not a posting.
        var benchmark = new PayBenchmarkFallback(100000m, 150000m, "BLS OEWS", "annual P25–P75", "May 2025");
        var result = PayEvidenceRules.Evaluate(rows, query, asOf, benchmark);
        Assert.Same(benchmark, result.BenchmarkFallback);
        Assert.Equal("Occupational wage benchmark", result.BenchmarkFallback!.Label);
        Assert.Null(result.Interval);
        Assert.False(result.PersonalizedAllowed);
    }

    [Fact]
    public void ExclusionsKeepReferenceReasonOrderAndDoNotDeduplicateRejectedRows()
    {
        using var fixture = Fixture();
        var root = fixture.RootElement;
        var asOf = root.GetProperty("asOf").GetDateTime();
        var row = Rows(root.GetProperty("covered"))[0] with
        {
            Id = null, EmployerDisclosed = false, Currency = "EUR", Basis = PayBasis.Unknown,
            Eligible = null, Low = 0, UpdatedAt = asOf.AddDays(-91)
        };
        Assert.Equal(new[]
        {
            "missing identity or matching field", "not employer-disclosed pay", "unknown or non-USD currency",
            "unknown pay basis or annual hours", "outside 90-day lookback", "work-location eligibility unknown",
            "invalid pay range"
        }, PayEvidenceRules.Normalize(row, asOf).Reasons);
        var result = PayEvidenceRules.Evaluate(new[] { row, row }, Query("software", "Denver, CO"), asOf);
        Assert.Equal(2, result.Excluded);
        Assert.DoesNotContain("duplicate requisition", result.ExclusionReasons.Keys);
        Assert.Null(result.Interval);
    }

    [Fact]
    public void AQualificationIsAuthorizedOnlyWhenEveryGatePasses()
    {
        var rows = PayEvidenceGates.Current().Rows;
        Assert.False(PayGateDecision.FromRows(rows).PersonalizedAllowed);
        Assert.True(PayGateDecision.FromRows(rows.Select(r => r with { Status = PayGateStatus.Passed }).ToList()).PersonalizedAllowed);
        Assert.False(PayGateDecision.FromRows(Array.Empty<PayGateRow>()).PersonalizedAllowed);
        Assert.False(PayGateDecision.FromRows(rows.Select(r => r with { Status = PayGateStatus.Unverified }).ToList()).PersonalizedAllowed);
    }

    [Fact]
    public void AQualifiedCohortIsStillBlockedWhileTheProviderRightsAreUnverified()
    {
        using var fixture = Fixture();
        var root = fixture.RootElement;
        var asOf = root.GetProperty("asOf").GetDateTime();
        var rows = Rows(root.GetProperty("covered"));

        // A covered cohort on its own never authorizes personalized pay.
        var result = PayEvidenceRules.Evaluate(rows, Query("software", "Denver, CO"), asOf);
        Assert.NotNull(result.Interval);
        Assert.False(result.PersonalizedAllowed);
        Assert.Contains("provider_rights_unverified", result.BlockedReasons);

        // Only an all-passed gate set does, and the decision carries no flag a caller can set.
        var qualified = PayGateDecision.FromRows(
            PayEvidenceGates.Current().Rows.Select(r => r with { Status = PayGateStatus.Passed }).ToList());
        var authorized = PayEvidenceRules.Evaluate(rows, Query("software", "Denver, CO"), asOf, null, qualified);
        Assert.True(authorized.PersonalizedAllowed);
        Assert.Empty(authorized.BlockedReasons);
    }

    [Fact]
    public void CurrentProviderQualificationBlocksPersonalizedPay()
    {
        var gates = PayEvidenceGates.Current();
        Assert.False(gates.PersonalizedAllowed);
        Assert.Contains("provider_rights_unverified", gates.BlockedReasons);
        Assert.Equal(8, gates.Rows.Count);
        Assert.Contains(gates.Rows, g => g.GateId == "G1" && g.Status == PayGateStatus.Unverified);
        Assert.Contains(gates.Rows, g => g.GateId == "G8" && g.Status == PayGateStatus.Failed);
    }

    [Fact]
    public void ConcentrationIsFlaggedAboveFortyPercent()
    {
        using var fixture = Fixture();
        var root = fixture.RootElement;
        var asOf = root.GetProperty("asOf").GetDateTime();
        var rows = Rows(root.GetProperty("covered")).Where(r => r.Role == "software").ToList();

        // 5 of 12 observations from one employer = 41.7% -> concentrated.
        var concentrated = PayEvidenceRules.Evaluate(
            rows.Select((r, i) => i < 5 ? r with { Employer = "Big Co" } : r).ToList(), Query("software", "Denver, CO"), asOf);
        Assert.Equal(0.42m, concentrated.EmployerConcentration);
        Assert.True(concentrated.Concentrated);

        // 4 of 12 = 33.3% -> not concentrated.
        var spread = PayEvidenceRules.Evaluate(
            rows.Select((r, i) => i < 4 ? r with { Employer = "Big Co" } : r).ToList(), Query("software", "Denver, CO"), asOf);
        Assert.Equal(0.33m, spread.EmployerConcentration);
        Assert.False(spread.Concentrated);
    }

    [Fact]
    public void SensitivityIsFlaggedOnlyWhenRemovingAGroupMovesTheInterval()
    {
        using var fixture = Fixture();
        var root = fixture.RootElement;
        var asOf = root.GetProperty("asOf").GetDateTime();
        var rows = Rows(root.GetProperty("covered")).Where(r => r.Role == "software").ToList();

        // Half the cohort is one employer paying far above the rest, so dropping it moves an
        // interval end by more than 10% and the result must be flagged sensitive.
        var skewed = rows.Select((r, i) => i < 6
            ? r with { Employer = "High payer", Low = r.Low + 90_000, High = r.High + 90_000 }
            : r).ToList();
        var sensitive = PayEvidenceRules.Evaluate(skewed, Query("software", "Denver, CO"), asOf);
        Assert.True(sensitive.Sensitive);
        Assert.NotNull(sensitive.IntervalWithoutLargestEmployer);

        // The plain fixture spreads pay evenly, so removing any employer stays inside the limit.
        var stable = PayEvidenceRules.Evaluate(rows, Query("software", "Denver, CO"), asOf);
        Assert.False(stable.Sensitive);
        Assert.NotNull(stable.IntervalWithoutLargestEmployer);
    }
}
