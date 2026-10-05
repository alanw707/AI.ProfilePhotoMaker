namespace AI.ProfilePhotoMaker.API.Models.Career;

/// <summary>Where an uploaded resume is in the import pipeline (ADR 0007). Stored as text.</summary>
public enum ResumeState
{
    Quarantined,
    Extracting,
    Ready,
    Unreadable,
    Failed
}

public enum ProposalStatus
{
    Pending,
    Accepted,
    Dismissed
}

public static class ResumeFailureCodes
{
    public const string ExtractionTimeout = "ExtractionTimeout";
    public const string ParserError = "ParserError";
}

public static class ProposalSources
{
    public const string Resume = "resume";
    public const string Pasted = "pasted";
}

public static class ProposalFlags
{
    public const string Conflict = "conflict";
    public const string Ambiguous = "ambiguous";
}

/// <summary>
/// An uploaded resume's metadata. The raw bytes live in private storage under the
/// opaque <see cref="StorageKey"/> and are never exposed by URL.
/// </summary>
public class ResumeDocument
{
    public Guid Id { get; set; }

    /// <summary>Server-derived from the authenticated user; never from the request.</summary>
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>Opaque, e.g. <c>career-private/resumes/{guid}</c>; carries no user data.</summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>Sanitised display name, at most 200 characters.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>"pdf" or "docx", decided from the file's bytes.</summary>
    public string Format { get; set; } = string.Empty;

    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public ResumeState State { get; set; }
    public string? FailureCode { get; set; }

    /// <summary>The proposal extracted from this file; plain id so there is no FK cycle.</summary>
    public Guid? ProposalId { get; set; }

    public DateTime ConsentedAt { get; set; }
    public DateTime UploadedAt { get; set; }

    /// <summary>When the raw file is purged.</summary>
    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Reviewable, unconfirmed changes extracted from a resume or pasted text. It is
/// pinned to the profile version that existed when it was created.
/// </summary>
public class CareerProfileProposal
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>"resume" or "pasted".</summary>
    public string Source { get; set; } = ProposalSources.Resume;

    public Guid? ResumeDocumentId { get; set; }

    /// <summary>Profile version at creation, or null when no profile existed.</summary>
    public int? BaseProfileVersion { get; set; }

    public ProposalStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public int? AcceptedIntoVersion { get; set; }

    public List<CareerProfileProposalItem> Items { get; set; } = new();
}

public class CareerProfileProposalItem
{
    public Guid Id { get; set; }
    public Guid ProposalId { get; set; }

    /// <summary>Duplicated from the proposal so every private row is owner-filterable.</summary>
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>Position in the proposal, so items read back in extraction order.</summary>
    public int Ordinal { get; set; }

    /// <summary>currentTitle | industry | yearsExperience | location | summary | skills | highlights.</summary>
    public string Field { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
    public int? Page { get; set; }
    public string? Section { get; set; }

    /// <summary>The source line, at most 300 characters.</summary>
    public string Excerpt { get; set; } = string.Empty;

    public List<string> Flags { get; set; } = new();

    public CareerProfileProposal? Proposal { get; set; }
}
