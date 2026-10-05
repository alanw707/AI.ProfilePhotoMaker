namespace AI.ProfilePhotoMaker.API.Services.Career;

public enum PayBasis { Annual, Hourly, Unknown }

public sealed record PayObservation(
    string? Id, string? CanonicalId, string? Employer, string? Role, string? Geography,
    string? Level, string? EmploymentType, bool? Eligible, bool EmployerDisclosed,
    string? Currency, PayBasis Basis, int? AnnualHours, decimal Low, decimal High, DateTime UpdatedAt);

public sealed record PayEvidenceQuery(string Role, string Geography, string? Level = null, string? EmploymentType = null);

public sealed record PayInterval(decimal Low, decimal High, string Unit, string Definition);

/// <summary>A separately labelled, caller-supplied figure from the pinned BLS snapshot; never advertised pay.</summary>
public sealed record PayBenchmarkFallback(
    decimal Low, decimal High, string Source, string Measure, string Period, string Label = "Occupational wage benchmark");

public sealed record PayEvidenceResult(
    string RuleVersion, string Role, string Geography, string Level, int Included, int Employers,
    decimal EmployerConcentration, int Excluded, IReadOnlyDictionary<string, int> ExclusionReasons,
    PayInterval? Interval, string Decision, string Note, PayBenchmarkFallback? BenchmarkFallback,
    bool PersonalizedAllowed, IReadOnlyList<string> BlockedReasons);

public sealed record NormalizedPayObservation(PayObservation Record, decimal Low, decimal High, IReadOnlyList<string> Reasons);
