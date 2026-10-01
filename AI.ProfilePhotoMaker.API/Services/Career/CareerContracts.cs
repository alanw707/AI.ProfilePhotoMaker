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

public sealed record CareerProvenanceDto(string Source, DateTime ConfirmedAt, int? RestoredFromVersion = null);

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

public sealed record CareerGoalDto(
    Guid Id,
    int Version,
    string Etag,
    CareerGoalFactsDto Goal,
    int? BasedOnProfileVersion,
    bool IsStale,
    CareerProvenanceDto Provenance,
    DateTime CreatedAt,
    DateTime UpdatedAt);

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
    AlreadyExists
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
    int? CurrentVersion = null)
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
}
