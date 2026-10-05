namespace AI.ProfilePhotoMaker.API.Models.Career;

public static class CareerDeletionScopes
{
    public const string RawDocuments = "raw_documents";
    public const string CareerProfile = "career_profile";

    public static bool IsValid(string? scope) => scope is RawDocuments or CareerProfile;
}

public static class CareerDeletionStatuses
{
    public const string Pending = "pending";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

/// <summary>
/// The visible record of one deletion (ADR 0020): what was asked, whether every row and blob is gone,
/// and why not when it failed. Audit data, so it is exported but is not a covered entity and is never
/// removed by the purge it records.
/// </summary>
public class CareerDeletionRequest
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public string Scope { get; set; } = CareerDeletionScopes.CareerProfile;
    public string Status { get; set; } = CareerDeletionStatuses.Pending;
    public int Attempts { get; set; }

    /// <summary>Stable, user-safe reason for the last failure; never a stack trace or storage path.</summary>
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Marks the moment an owner's data in <see cref="Scope"/> was deleted. It blocks resurrection of data
/// created before it (an in-flight run finishing late, a restored backup) but never data created after.
/// It must survive the purge it records, so it is audit data outside <c>CoveredEntityTypes</c>.
/// </summary>
public class CareerTombstone
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public string Scope { get; set; } = CareerDeletionScopes.CareerProfile;
    public DateTime CreatedAt { get; set; }
}
