using AI.ProfilePhotoMaker.API.Models.Career;

namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>Turns the pinned goal, the confirmed match and the BLS snapshot into the builder's input (ADR 0016).</summary>
public static class RoadmapEvidence
{
    private const int MaxAlternatives = 5;

    public static RoadmapInput Build(
        CareerGoalVersion goal, OccupationReleaseAndResult? match, IMarketReference? market,
        bool hasMarketBrief, bool hasPayAnalysis)
    {
        var candidates = match?.Candidates ?? Array.Empty<OccupationCandidate>();
        var targetCandidate = candidates.FirstOrDefault(c => c.Code == goal.OccupationCode);
        var target = ToCandidate(goal.OccupationCode!, goal.OccupationTitle!, targetCandidate, market);
        var alternatives = candidates.Where(c => c.Code != goal.OccupationCode).Take(MaxAlternatives)
            .Select(c => ToCandidate(c.Code, c.Title, c, market)).ToList();
        return new RoadmapInput(
            target, targetCandidate != null, alternatives, goal.WeeklyEffortHours,
            !string.IsNullOrWhiteSpace(goal.TargetLocation), hasMarketBrief, hasPayAnalysis, match?.Release);
    }

    private static RoadmapCandidate ToCandidate(string code, string title, OccupationCandidate? c, IMarketReference? market)
    {
        double? median = null;
        string? wageSource = null, wageRelease = null;
        var national = market?.Oews?.Areas.FirstOrDefault(a => a.Type == "national");
        if (market?.Oews is { } oews && national != null && oews.Crosswalk(code) is { } wageMapping
            && oews.Wage(national.Code, wageMapping.Code)?.MedianAnnual is { Status: MarketValueStatus.Available, Number: { } wage })
        {
            median = wage;
            wageSource = oews.Source.Id;
            wageRelease = oews.Source.ReferencePeriod;
        }

        double? change = null;
        string? projectionSource = null, projectionRelease = null;
        if (market?.Projections is { } projections && projections.Crosswalk(code) is { } projectionMapping
            && projections.Row(projectionMapping.Code)?.ChangePercent is { Status: MarketValueStatus.Available, Number: { } percent })
        {
            change = percent;
            projectionSource = projections.Source.Id;
            projectionRelease = projections.Source.ReferencePeriod;
        }

        return new RoadmapCandidate(
            code, title, median, wageSource, wageRelease, change, projectionSource, projectionRelease,
            c?.Evidence.Count(e => e.Kind == "duty") ?? 0,
            c?.MissingEvidence ?? Array.Empty<string>(),
            c?.KnownGaps ?? Array.Empty<string>());
    }
}

/// <summary>A match's candidates and the O*NET release they came from.</summary>
public sealed record OccupationReleaseAndResult(IReadOnlyList<OccupationCandidate> Candidates, string Release);
