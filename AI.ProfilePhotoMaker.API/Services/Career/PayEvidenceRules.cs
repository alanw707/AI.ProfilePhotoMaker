namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>Pure candidate-1.0 calculation over supplied observations; no provider or model access.</summary>
public static class PayEvidenceRules
{
    public const int LookbackDays = 90;
    public const int MinObservations = 10;
    public const int MinEmployers = 5;
    public const string RuleVersion = "candidate-1.0";

    /// <summary>A cohort where one employer supplies more than this share is flagged concentrated.</summary>
    public const decimal ConcentrationThreshold = 0.4m;

    /// <summary>Sensitivity limit: an interval end moving more than this share when a group is removed.</summary>
    public const decimal SensitivityLimit = 0.1m;

    /// <summary>Smallest reduced cohort the sensitivity check will measure.</summary>
    public const int MinSensitivityObservations = 2;

    public static decimal? Quantile(IEnumerable<decimal> values, decimal p)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        if (sorted.Length == 0)
        {
            return null;
        }
        var position = (sorted.Length - 1) * p;
        var low = (int)decimal.Floor(position);
        var high = (int)decimal.Ceiling(position);
        return sorted[low] + (sorted[high] - sorted[low]) * (position - low);
    }

    public static NormalizedPayObservation Normalize(PayObservation record, DateTime now)
    {
        var reasons = new List<string>();
        if (string.IsNullOrEmpty(record.Id) || string.IsNullOrEmpty(record.Employer)
            || string.IsNullOrEmpty(record.Role) || string.IsNullOrEmpty(record.Geography))
        {
            reasons.Add("missing identity or matching field");
        }
        if (!record.EmployerDisclosed)
        {
            reasons.Add("not employer-disclosed pay");
        }
        if (record.Currency != "USD")
        {
            reasons.Add("unknown or non-USD currency");
        }
        if (record.Basis != PayBasis.Annual && !(record.Basis == PayBasis.Hourly && record.AnnualHours > 0))
        {
            reasons.Add("unknown pay basis or annual hours");
        }
        var age = now - record.UpdatedAt;
        if (age < TimeSpan.Zero || age > TimeSpan.FromDays(LookbackDays))
        {
            reasons.Add("outside 90-day lookback");
        }
        if (record.Eligible == false)
        {
            reasons.Add("work-location ineligible");
        }
        if (record.Eligible == null)
        {
            reasons.Add("work-location eligibility unknown");
        }
        var multiplier = record.Basis == PayBasis.Hourly ? record.AnnualHours ?? 0 : 1;
        var low = record.Low * multiplier;
        var high = record.High * multiplier;
        if (low <= 0 || high < low)
        {
            reasons.Add("invalid pay range");
        }
        return new NormalizedPayObservation(record, low, high, reasons);
    }

    public static PayEvidenceResult Evaluate(
        IEnumerable<PayObservation> records, PayEvidenceQuery query, DateTime now,
        PayBenchmarkFallback? benchmarkFallback = null, PayGateDecision? qualification = null)
    {
        // No qualification means no authorization: a caller cannot opt in with a flag.
        var decision = qualification ?? PayEvidenceGates.Current();
        var included = new List<NormalizedPayObservation>();
        var excluded = 0;
        var exclusionReasons = new Dictionary<string, int>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            var normalized = Normalize(record, now);
            var reasons = new List<string>(normalized.Reasons);
            if (record.Role != query.Role)
            {
                reasons.Add("different occupation family");
            }
            if (record.Geography != query.Geography)
            {
                reasons.Add("different geography");
            }
            if (!string.IsNullOrEmpty(query.Level) && record.Level != query.Level)
            {
                reasons.Add("different or unknown level");
            }
            if (!string.IsNullOrEmpty(query.EmploymentType) && record.EmploymentType != query.EmploymentType)
            {
                reasons.Add("different or unknown employment type");
            }
            var key = string.IsNullOrEmpty(record.CanonicalId) ? record.Id ?? "" : record.CanonicalId;
            if (seen.Contains(key))
            {
                reasons.Add("duplicate requisition");
            }
            if (reasons.Count > 0)
            {
                excluded++;
                foreach (var reason in reasons)
                {
                    exclusionReasons[reason] = exclusionReasons.GetValueOrDefault(reason) + 1;
                }
                continue;
            }
            seen.Add(key);
            included.Add(normalized);
        }

        var employerCounts = included.GroupBy(n => n.Record.Employer, StringComparer.Ordinal)
            .Select(group => group.Count()).ToArray();
        var employers = employerCounts.Length;
        var concentration = included.Count == 0 ? 0m
            : Math.Round((decimal)employerCounts.Max() / included.Count * 100, 0, MidpointRounding.AwayFromZero) / 100;
        var supported = included.Count >= MinObservations && employers >= MinEmployers;
        // Sensitivity: the same rule without the largest employer, and without hourly-normalized rows.
        var largestEmployer = included.GroupBy(n => n.Record.Employer, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).FirstOrDefault()?.Key;
        var withoutLargest = IntervalFor(included.Where(n => n.Record.Employer != largestEmployer).ToList());
        var withoutHourly = IntervalFor(included.Where(n => n.Record.Basis != PayBasis.Hourly).ToList());
        var interval = supported ? new PayInterval(
            Math.Round(Quantile(included.Select(n => n.Low), .25m)!.Value, 0, MidpointRounding.AwayFromZero),
            Math.Round(Quantile(included.Select(n => n.High), .75m)!.Value, 0, MidpointRounding.AwayFromZero),
            "USD / year", "P25 of advertised lower bounds to P75 of advertised upper bounds; linear interpolation") : null;
        return new PayEvidenceResult(
            RuleVersion, query.Role, query.Geography, query.Level ?? "any", included.Count, employers,
            concentration, excluded, exclusionReasons, interval,
            supported ? "observed interval" : "occupational benchmark fallback",
            supported ? "Employer-disclosed advertised pay, not an offer prediction." : "Insufficient independent current observations or employers.",
            // Authorization comes from the verified gate rows; an absent decision blocks.
            benchmarkFallback, decision.PersonalizedAllowed, decision.BlockedReasons)
        {
            IntervalWithoutLargestEmployer = withoutLargest,
            IntervalWithoutHourly = withoutHourly,
            Sensitive = interval != null && (Moves(interval, withoutLargest) || Moves(interval, withoutHourly))
        };
    }

    /// <summary>
    /// The interval for a sub-cohort used by the sensitivity check. It deliberately does not
    /// require the publication floors: the question is how much the interval moves, not whether
    /// the reduced cohort could be published.
    /// </summary>
    private static PayInterval? IntervalFor(IReadOnlyList<NormalizedPayObservation> rows)
    {
        if (rows.Count < MinSensitivityObservations)
        {
            return null;
        }
        return new PayInterval(
            Math.Round(Quantile(rows.Select(n => n.Low), .25m)!.Value, 0, MidpointRounding.AwayFromZero),
            Math.Round(Quantile(rows.Select(n => n.High), .75m)!.Value, 0, MidpointRounding.AwayFromZero),
            "USD / year", "P25 of advertised lower bounds to P75 of advertised upper bounds; linear interpolation");
    }

    /// <summary>True when removing a group moved either end of the interval beyond the limit.</summary>
    private static bool Moves(PayInterval full, PayInterval? reduced) =>
        reduced != null && (Moved(full.Low, reduced.Low) || Moved(full.High, reduced.High));

    private static bool Moved(decimal full, decimal reduced) =>
        full != 0 && Math.Abs(reduced - full) / Math.Abs(full) > SensitivityLimit;
}
