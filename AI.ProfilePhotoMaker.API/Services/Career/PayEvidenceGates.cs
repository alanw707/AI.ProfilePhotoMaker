namespace AI.ProfilePhotoMaker.API.Services.Career;

public enum PayGateStatus { Passed, Failed, Unverified }

public sealed record PayGateRow(string GateId, string Requirement, PayGateStatus Status, string Evidence);

public sealed record PayGateDecision(bool PersonalizedAllowed, IReadOnlyList<string> BlockedReasons, IReadOnlyList<PayGateRow> Rows);

/// <summary>Current provider qualification, not an authorization to display personalized pay (ADR 0012).</summary>
public static class PayEvidenceGates
{
    public const string RightsBlock = "provider_rights_unverified";

    public static PayGateDecision Current() => new(false, new[] { RightsBlock }, new PayGateRow[]
    {
        new("G1", "Authorized ongoing aggregation, display, retention and attribution", PayGateStatus.Unverified,
            "Adzuna is preferred; written commercial rights and retention terms have not been obtained."),
        new("G2", "Broad U.S. occupation and geography coverage", PayGateStatus.Unverified,
            "Adzuna U.S. search is documented, but no key or direct coverage sample exists; NYC and USAJOBS are narrow."),
        new("G3", "Explicit employer-disclosed pay, basis and hours", PayGateStatus.Unverified,
            "Adzuna pay provenance is unverified; Greenhouse structured ranges lack normalized basis."),
        new("G4", "Confirmed level, employment type and work-location eligibility", PayGateStatus.Unverified,
            "Level, arrangement and remote restrictions are not validated across providers."),
        new("G5", "Canonical requisition deduplication and fresh timestamps", PayGateStatus.Unverified,
            "Cross-post reconciliation and refresh/expiry metadata need a permitted sample."),
        new("G6", "At least 10 observations from five independent employers", PayGateStatus.Failed,
            "NYC is one employer; the sampled Greenhouse cohorts did not meet five employers."),
        new("G7", "Documented rate limits, price and forecast cost", PayGateStatus.Unverified,
            "Adzuna default limits are published; no account, quote or forecast cost is approved."),
        new("G8", "Real covered and sparse cohorts with measured fallback and calibration", PayGateStatus.Failed,
            "No licensed covered cohort or holdout offer calibration exists; synthetic fixtures validate code only.")
    });
}
