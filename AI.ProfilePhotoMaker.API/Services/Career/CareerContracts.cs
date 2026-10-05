namespace AI.ProfilePhotoMaker.API.Services.Career;

// Request and response shapes for docs/career/api-profile-goal.md.

public sealed class CareerProfileRequest
{
    public string? CurrentTitle { get; set; }
    public string? Industry { get; set; }
    public int? YearsExperience { get; set; }
    public string? Location { get; set; }
    public string? Summary { get; set; }
    public List<string?>? Skills { get; set; }
    public List<string?>? Highlights { get; set; }
    public string? WorkArrangement { get; set; }
    public bool Confirmed { get; set; }
}

public sealed class CareerGoalRequest
{
    public string? TargetRole { get; set; }
    public string? TargetLocation { get; set; }
    public string? WorkArrangement { get; set; }
    public int? DesiredPayMin { get; set; }
    public int? DesiredPayMax { get; set; }
    public int? WeeklyEffortHours { get; set; }
    public bool Confirmed { get; set; }
}

public sealed record CareerProvenanceDto(string Source, DateTime ConfirmedAt, int? RestoredFromVersion = null, Guid? SourceProposalId = null);

public sealed record CareerProfileFactsDto(
    string CurrentTitle,
    string? Industry,
    int? YearsExperience,
    string? Location,
    string? Summary,
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> Highlights,
    string? WorkArrangement);

public sealed record CareerProfileDto(
    Guid Id,
    int Version,
    string Etag,
    CareerProfileFactsDto Facts,
    CareerProvenanceDto Provenance,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CareerProfileVersionSummaryDto(int Version, DateTime CreatedAt, string Source, string CurrentTitle, bool IsActive);

public sealed record CareerProfileVersionDto(int Version, CareerProfileFactsDto Facts, CareerProvenanceDto Provenance, DateTime CreatedAt, bool IsActive);

public sealed record CareerGoalFactsDto(
    string TargetRole,
    string? TargetLocation,
    string? WorkArrangement,
    int? DesiredPayMin,
    int? DesiredPayMax,
    int? WeeklyEffortHours);

/// <summary>The confirmed O*NET occupation on a goal (ADR 0010).</summary>
public sealed record CareerGoalOccupationDto(string Code, string Title, string ReferenceRelease, Guid? MatchId);

/// <summary>The place saved from the market comparison (ADR 0014).</summary>
public sealed record CareerGoalPreferredAreaDto(string Code, string Title, string Level);

public sealed record CareerGoalDto(
    Guid Id,
    int Version,
    string Etag,
    CareerGoalFactsDto Goal,
    int? BasedOnProfileVersion,
    bool IsStale,
    CareerProvenanceDto Provenance,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    CareerGoalOccupationDto? Occupation = null,
    CareerGoalPreferredAreaDto? PreferredArea = null);

public sealed record CareerGoalVersionSummaryDto(int Version, DateTime CreatedAt, string TargetRole, bool IsActive);

public sealed record CareerGoalVersionDto(int Version, CareerGoalFactsDto Goal, int? BasedOnProfileVersion, CareerProvenanceDto Provenance, DateTime CreatedAt, bool IsActive);

/// <summary>Outcome kinds the controller translates to the spec #376 status codes.</summary>
public enum CareerOutcomeKind
{
    Ok,
    Created,
    NotFound,
    Invalid,
    PreconditionRequired,
    VersionConflict,
    AlreadyExists,
    TooLarge,
    Unsupported,
    Rejected,
    Unavailable,
    QuotaExceeded,
    Gone
}

/// <summary>
/// Result of a career operation. Errors carry a stable code, and validation errors
/// carry field messages keyed by the camelCase request property name.
/// </summary>
public sealed record CareerOutcome<T>(
    CareerOutcomeKind Kind,
    T? Value = default,
    string? ErrorCode = null,
    string? Message = null,
    IReadOnlyDictionary<string, string>? FieldErrors = null,
    int? CurrentVersion = null,
    string? Detail = null,
    int? RetryAfterSeconds = null)
{
    public static CareerOutcome<T> Ok(T value) => new(CareerOutcomeKind.Ok, value);
    public static CareerOutcome<T> Created(T value) => new(CareerOutcomeKind.Created, value);

    public static CareerOutcome<T> NotFound(string code, string message) =>
        new(CareerOutcomeKind.NotFound, ErrorCode: code, Message: message);

    public static CareerOutcome<T> Invalid(IReadOnlyDictionary<string, string> fieldErrors) =>
        new(CareerOutcomeKind.Invalid, ErrorCode: CareerErrorCodes.Validation,
            Message: "Some fields need attention.", FieldErrors: fieldErrors);

    public static CareerOutcome<T> PreconditionRequired() =>
        new(CareerOutcomeKind.PreconditionRequired, ErrorCode: CareerErrorCodes.PreconditionRequired,
            Message: "Send the If-Match header with the version you edited.");

    public static CareerOutcome<T> Conflict(int currentVersion) =>
        new(CareerOutcomeKind.VersionConflict, ErrorCode: CareerErrorCodes.VersionConflict,
            Message: "This was changed in another tab or device. Reload the latest version.",
            CurrentVersion: currentVersion);

    public static CareerOutcome<T> TooLarge(string detail) =>
        new(CareerOutcomeKind.TooLarge, ErrorCode: CareerErrorCodes.ResumeTooLarge,
            Message: "This file is too large to import.", Detail: detail);

    public static CareerOutcome<T> Unsupported(string detail) =>
        new(CareerOutcomeKind.Unsupported, ErrorCode: CareerErrorCodes.ResumeUnsupported,
            Message: "We can only import a regular PDF or DOCX file.", Detail: detail);

    public static CareerOutcome<T> Rejected() =>
        new(CareerOutcomeKind.Rejected, ErrorCode: CareerErrorCodes.ResumeRejected,
            Message: "This file was rejected by the security check and has been deleted.");

    public static CareerOutcome<T> ScannerUnavailable(int retryAfterSeconds) =>
        new(CareerOutcomeKind.Unavailable, ErrorCode: CareerErrorCodes.ScannerUnavailable,
            Message: "Resume import is temporarily unavailable. Try again shortly.", RetryAfterSeconds: retryAfterSeconds);

    public static CareerOutcome<T> ModelUnavailable() =>
        new(CareerOutcomeKind.Unavailable, ErrorCode: CareerAgentErrorCodes.ModelUnavailable,
            Message: "The career assistant is not available yet.");

    public static CareerOutcome<T> ReferenceUnavailable() =>
        new(CareerOutcomeKind.Unavailable, ErrorCode: CareerAgentErrorCodes.ReferenceUnavailable,
            Message: "Occupation matching is temporarily unavailable.");

    public static CareerOutcome<T> Busy(string code, string message, int retryAfterSeconds) =>
        new(CareerOutcomeKind.Unavailable, ErrorCode: code, Message: message, RetryAfterSeconds: retryAfterSeconds);

    public static CareerOutcome<T> QuotaExceeded(string code, string message) =>
        new(CareerOutcomeKind.QuotaExceeded, ErrorCode: code, Message: message);

    public static CareerOutcome<T> Gone(string code, string message) =>
        new(CareerOutcomeKind.Gone, ErrorCode: code, Message: message);

    public static CareerOutcome<T> AlreadyExists(string code, string message) =>
        new(CareerOutcomeKind.AlreadyExists, ErrorCode: code, Message: message);
}

public static class CareerErrorCodes
{
    public const string Disabled = "CareerWorkspaceDisabled";
    public const string Validation = "ValidationError";
    public const string PreconditionRequired = "CareerPreconditionRequired";
    public const string VersionConflict = "CareerVersionConflict";
    public const string ProfileNotFound = "CareerProfileNotFound";
    public const string GoalNotFound = "CareerGoalNotFound";
    public const string VersionNotFound = "CareerVersionNotFound";
    public const string GoalAlreadyExists = "CareerGoalAlreadyExists";
    public const string ResumeNotFound = "CareerResumeNotFound";
    public const string ResumeFileGone = "CareerResumeFileGone";
    public const string ProposalNotFound = "CareerProposalNotFound";
    public const string ProposalAlreadyDecided = "CareerProposalAlreadyDecided";
    public const string ResumeTooLarge = "CareerResumeTooLarge";
    public const string ResumeUnsupported = "CareerResumeUnsupported";
    public const string ResumeRejected = "CareerResumeRejected";
    public const string ScannerUnavailable = "CareerScannerUnavailable";
    public const string PhotoNotFound = "CareerPhotoNotFound";
    public const string PhotoIsPreview = "CareerPhotoIsPreview";
}

// ---- Resume import and proposals (docs/career/api-resume-import.md) ----------

public sealed record ResumeDocumentDto(
    Guid Id,
    string FileName,
    string Format,
    long SizeBytes,
    int PageCount,
    string State,
    string? FailureCode,
    Guid? ProposalId,
    string ConsentVersion,
    DateTime UploadedAt,
    DateTime ExpiresAt);

public sealed record ProposalItemDto(
    Guid Id,
    string Field,
    string Value,
    string? CurrentValue,
    int? Page,
    string? Section,
    string Excerpt,
    IReadOnlyList<string> Flags);

public sealed record CareerProfileProposalDto(
    Guid Id,
    string Source,
    Guid? ResumeId,
    int? BaseProfileVersion,
    string Status,
    bool IsStale,
    IReadOnlyList<ProposalItemDto> Items,
    DateTime CreatedAt);

public sealed class PasteProposalRequest
{
    public string? Text { get; set; }
}

public sealed class AcceptProposalRequest
{
    public List<Guid>? ItemIds { get; set; }
}

/// <summary>The original bytes for the owner-only download endpoint.</summary>
public sealed record ResumeFileResult(Stream Content, string ContentType, string FileName);

// ---- Photo handoff (docs/career/api-photo-handoff.md) ------------------------

public sealed record CareerPhotoDto(int Id, string ImageUrl, DateTime CreatedAt, string Style, bool IsWatermarkedPreview);

/// <summary>Read-only copy of an active photo package; the photo workspace stays the authority.</summary>
public sealed record CareerPhotoEntitlementDto(
    string PackageCode,
    string PackageName,
    int RemainingCandidates,
    int RemainingRefinements,
    int RemainingPremiumAugmentations,
    bool PlatformExportKitAvailable,
    DateTime? ExpiresAt);

public sealed record CareerPhotoListDto(
    IReadOnlyList<CareerPhotoDto> Photos,
    int? SelectedPhotoId,
    bool SelectedPhotoAvailable,
    IReadOnlyList<CareerPhotoEntitlementDto> Entitlements);

public sealed class CareerPhotoSelectionRequest
{
    public int? ProcessedImageId { get; set; }
}

public sealed record CareerPhotoSelectionDto(int SelectedPhotoId, Guid? CareerGoalId, DateTime SelectedAt);
