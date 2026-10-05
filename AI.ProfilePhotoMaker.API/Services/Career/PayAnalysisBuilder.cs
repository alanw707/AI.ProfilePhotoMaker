using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>Pure analysis over pinned inputs. No network, model, user profile or observation storage.</summary>
public static class PayAnalysisBuilder
{
    public static IReadOnlyList<object> AsList(PayAnalysisSections sections) =>
        new object[] { sections.Benchmark, sections.Personalized, sections.Scenario };

    public static PayAnalysisContent Build(PayAnalysisInput input, IMarketReference reference, bool sourceFailed = false)
    {
        var oews = reference.Oews;
        var location = new MarketLocationDto(input.LocationText ?? "", input.AreaResolution,
            input.AreaCode == null ? null : new MarketAreaDto(input.AreaCode, input.AreaTitle ?? "", input.AreaResolution));
        var mapping = oews?.Crosswalk(input.OccupationCode);
        var figures = new List<MarketFigureDto>();
        if (oews != null && mapping != null)
        {
            foreach (var area in new[] { oews.Areas.First(a => a.Type == "national"),
                         input.AreaCode == null ? null : oews.Area(input.AreaCode) }.Where(a => a != null))
            {
                var row = oews.Wage(area!.Code, mapping.Code);
                foreach (var (key, label, value) in new[] {
                    ("medianAnnual", "Median annual wage", row?.MedianAnnual ?? MarketValue.NotPublished),
                    ("pct25Annual", "25th percentile annual wage", row?.Pct25Annual ?? MarketValue.NotPublished),
                    ("pct75Annual", "75th percentile annual wage", row?.Pct75Annual ?? MarketValue.NotPublished) })
                    figures.Add(new MarketFigureDto(key, label,
                        value.Status is MarketValueStatus.Available or MarketValueStatus.TopCoded ? value.Number : null,
                        MarketFigureStatuses.Of(value.Status), "usd_per_year", area.Code, area.Title, "oews"));
            }
        }
        var benchmarkReason = oews == null ? "source_unavailable" : mapping == null ? "not_published"
            : input.AreaResolution == MarketResolutions.Unresolved ? "location_unresolved" : null;
        var note = "Published BLS wages for this occupation, not advertised pay and not a prediction.";
        if (mapping?.Match == "shared")
            note += $" BLS publishes one estimate for {mapping.Code} {oews!.OccupationTitle(mapping.Code)}, which covers this occupation together with other detailed occupations.";
        if (mapping?.Match == "broad")
            note += $" BLS publishes this occupation only as the broader group {mapping.Code} {oews!.OccupationTitle(mapping.Code)}.";
        var benchmark = new PayBenchmarkSection("benchmark", "Occupational benchmark",
            benchmarkReason == null ? "complete" : oews == null ? "failed" : "unavailable", benchmarkReason,
            "Occupational wage benchmark", note, figures);
        var decision = PayEvidenceGates.Current();
        var evidence = PayEvidenceRules.Evaluate(input.Observations,
            new PayEvidenceQuery(input.OccupationCode, input.LocationText ?? ""), input.AsOf, qualification: decision);
        var empty = input.Observations.Count == 0;
        var reason = empty ? PayEvidenceGates.RightsBlock : evidence.Included < PayEvidenceRules.MinObservations
            ? "insufficient_observations" : evidence.Employers < PayEvidenceRules.MinEmployers ? "insufficient_employers" : null;
        var personalized = new PayPersonalizedSection("personalized", "Advertised pay from a qualified cohort",
            sourceFailed ? "failed" : empty ? "unavailable" : reason == null ? "complete" : "insufficient_evidence",
            sourceFailed ? "CareerPaySourceUnavailable" : reason,
            sourceFailed || empty ? null : evidence.Interval,
            new PayCohortDto(evidence.Included, evidence.Excluded, evidence.Employers, evidence.EmployerConcentration,
                evidence.EmployerConcentration > .5m, evidence.EmployerConcentration > .5m, evidence.ExclusionReasons),
            sourceFailed ? "The advertised-pay source is temporarily unavailable; the BLS benchmark remains independent."
                : empty ? "No qualified source: no provider has granted written rights for ongoing commercial use. See research/comparable-pay/QUALIFICATION.md."
                : evidence.Note);
        var median = figures.FirstOrDefault(f => f.Key == "medianAnnual" && f.AreaCode == "99" && f.Status == "available")?.Value as double?;
        var gap = input.RequestedAnnual.HasValue && median.HasValue ? input.RequestedAnnual.Value - median.Value : (double?)null;
        var scenario = new PayScenarioSection("scenario", "Your requested pay", gap.HasValue ? "complete" : "unavailable",
            input.RequestedAnnual, median, gap,
            gap.HasValue && median != 0 ? Math.Round(gap.Value / median!.Value * 100, 1, MidpointRounding.AwayFromZero) : null,
            "Your target is a preference, not evidence about what employers pay.");
        var sections = new PayAnalysisSections(benchmark, personalized, scenario);
        return new PayAnalysisContent(benchmark.Status == "failed" || sourceFailed ? "partial" : "complete", location,
            mapping == null ? null : new MarketCodeDto(mapping.Code, mapping.Match), sections,
            new PayQualificationDto(decision.PersonalizedAllowed, decision.BlockedReasons, decision.Rows),
            MarketBriefBuilder.Sources(reference), Hash(input));
    }

    public static string Hash(PayAnalysisInput input) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalInputJson(input)))).ToLowerInvariant();

    public static string CanonicalInputJson(PayAnalysisInput input)
    {
        // Sorted keys and JSON's invariant number formatting make the input document platform-independent.
        var fields = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["areaCode"] = input.AreaCode, ["areaResolution"] = input.AreaResolution, ["areaTitle"] = input.AreaTitle,
            ["goalVersion"] = input.GoalVersion, ["observationCount"] = input.Observations.Count,
            ["observationSourceId"] = input.ObservationSourceId, ["occupationCode"] = input.OccupationCode,
            ["occupationTitle"] = input.OccupationTitle, ["oewsRelease"] = input.OewsRelease,
            ["oewsSnapshotSha256"] = input.OewsSnapshotSha256, ["profileVersion"] = input.ProfileVersion,
            ["projectionsRelease"] = input.ProjectionsRelease, ["requestedPay"] = input.RequestedAnnual,
            ["ruleVersion"] = input.RuleVersion
        };
        return JsonSerializer.Serialize(fields);
    }
}
