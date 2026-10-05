using System.Text.Json;
using AI.ProfilePhotoMaker.API.Models.Career;

namespace AI.ProfilePhotoMaker.API.Services.Career;

// Shapes for docs/career/api-targeted-resume.md (ADR 0018).

public static class ResumeOrigins
{
    public const string Generated = "generated";
    public const string Human = "human";
}

public static class ResumeAuthors
{
    public const string Agent = "agent";
    public const string User = "user";
}

public static class ResumeSectionKeys
{
    public const string Headline = "headline";
    public const string Summary = "summary";
    public const string ExperienceHighlights = "experience_highlights";
    public const string Skills = "skills";

    public const string Short = "short";
    public const string Long = "long";

    public static readonly IReadOnlyList<string> All = new[] { Headline, Summary, ExperienceHighlights, Skills };
    public static readonly IReadOnlyList<string> SummaryKeys = new[] { Short, Long };

    /// <summary>The section keys a material of this kind has, in order.</summary>
    public static IReadOnlyList<string> For(string? kind) => kind == CareerMaterialKinds.Summary ? SummaryKeys : All;
}

public static class ResumeErrorCodes
{
    public const string UnsupportedClaim = "CareerResumeUnsupportedClaim";
    public const string MaterialNotFound = "CareerMaterialNotFound";
    public const string MaterialProposalNotFound = "CareerMaterialProposalNotFound";
    public const string MaterialProposalClosed = "CareerMaterialProposalClosed";
}

public static class ResumeLimits
{
    public const int MaxLines = 60;
    public const int MaxLineLength = 600;
    public const int MaxFactIdsPerLine = 10;
    public const int MaxFactIdLength = 32;
    public const int MaxLineIdLength = 64;
    public const int MaxVersionsListed = 200;
    public const int VersionPageSize = 50;
    public const int ShortSummaryMax = 300;
    public const int LongSummaryMax = 1200;

    /// <summary>Total text a section may hold: summaries cap `short` and `long`; resume sections cap each line instead.</summary>
    public static int? SectionTotalMax(string kind, string key) =>
        kind != CareerMaterialKinds.Summary ? null : key == ResumeSectionKeys.Short ? ShortSummaryMax : LongSummaryMax;
}

public sealed record ResumeLine(string Id, string Text, IReadOnlyList<string> FactIds, string Origin);

public sealed record ResumeSection(string Key, IReadOnlyList<ResumeLine> Lines);

public sealed record ResumeQuestion(string Id, string FactId, string Text);

public sealed record ResumeContact(bool Name = true, bool Email = true, bool Phone = false, bool Location = false, bool Links = false);

public sealed record ResumeDraft(IReadOnlyList<ResumeSection> Sections, IReadOnlyList<ResumeQuestion> Questions);

public sealed record ResumeFactDto(string Id, string Text);

public sealed record ResumePinnedDto(int ProfileVersion, int GoalVersion, string OccupationCode);

public sealed record CareerMaterialSummaryDto(Guid Id, string Title, bool Stale, int CurrentVersion, DateTime UpdatedAt, string Kind = "resume");

public sealed record CareerMaterialListDto(IReadOnlyList<CareerMaterialSummaryDto> Materials);

public sealed record CareerMaterialDto(
    Guid Id,
    string Title,
    string Etag,
    int CurrentVersion,
    ResumePinnedDto Pinned,
    bool Stale,
    IReadOnlyList<string> StaleReasons,
    ResumeContact Contact,
    IReadOnlyList<ResumeSection> Sections,
    IReadOnlyList<ResumeQuestion> Questions,
    IReadOnlyList<ResumeFactDto> Facts,
    string Kind = "resume");

public sealed record CareerMaterialVersionSummaryDto(int Number, string Author, DateTime CreatedAt);

public sealed record CareerMaterialVersionListDto(IReadOnlyList<CareerMaterialVersionSummaryDto> Versions, int Total);

public sealed record CareerMaterialVersionDto(
    int Number,
    string Author,
    DateTime CreatedAt,
    int? RestoredFromVersion,
    ResumePinnedDto Pinned,
    ResumeContact Contact,
    IReadOnlyList<ResumeSection> Sections,
    IReadOnlyList<ResumeQuestion> Questions,
    IReadOnlyList<ResumeFactDto> Facts);

public sealed record ResumeChange(string Id, string Kind, string Section, string? Before, string? After, IReadOnlyList<string> FactIds);

public sealed record CareerMaterialProposalDto(Guid Id, string Status, int BaseVersion, IReadOnlyList<ResumeChange> Changes);

public sealed class SaveMaterialRequest
{
    public List<SaveSectionRequest?>? Sections { get; set; }
    public SaveContactRequest? Contact { get; set; }
}

public sealed class SaveSectionRequest
{
    public string? Key { get; set; }
    public List<SaveLineRequest?>? Lines { get; set; }
}

public sealed class SaveLineRequest
{
    public string? Id { get; set; }
    public string? Text { get; set; }
    public List<string?>? FactIds { get; set; }
    public string? Origin { get; set; }
}

public sealed class SaveContactRequest
{
    public bool? Name { get; set; }
    public bool? Email { get; set; }
    public bool? Phone { get; set; }
    public bool? Location { get; set; }
    public bool? Links { get; set; }
}

public sealed class ApplyMaterialProposalRequest
{
    public List<string>? AcceptedChangeIds { get; set; }
}

public static class ResumeJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

/// <summary>
/// Fact ids of a confirmed profile version (ADR 0018): title, industry, years, location, summary,
/// skill:&lt;i&gt; and highlight:&lt;i&gt;. Resolution returns the stored text, never anything else.
/// </summary>
public static class ResumeFacts
{
    public static string? Resolve(CareerProfileVersion profile, string? factId)
    {
        if (string.IsNullOrEmpty(factId))
        {
            return null;
        }
        string? Pick(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
        switch (factId)
        {
            case "title": return Pick(profile.CurrentTitle);
            case "industry": return Pick(profile.Industry);
            case "years": return profile.YearsExperience is { } y ? $"{y} years of experience" : null;
            case "location": return Pick(profile.Location);
            case "summary": return Pick(profile.Summary);
        }
        var colon = factId.IndexOf(':');
        if (colon < 0 || !int.TryParse(factId.AsSpan(colon + 1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var index)
            || factId[(colon + 1)..] != index.ToString(System.Globalization.CultureInfo.InvariantCulture))
        {
            return null;
        }
        var list = factId[..colon] switch { "skill" => profile.Skills, "highlight" => profile.Highlights, _ => null };
        return list != null && index < list.Count ? Pick(list[index]) : null;
    }

    /// <summary>Generated lines whose fact ids do not all resolve, or that cite nothing. Empty means every claim is grounded.</summary>
    public static IReadOnlyList<string> UnsupportedLineIds(CareerProfileVersion profile, IEnumerable<ResumeSection> sections) =>
        sections.SelectMany(s => s.Lines)
            .Where(l => l.Origin == ResumeOrigins.Generated && (l.FactIds.Count == 0 || l.FactIds.Any(f => Resolve(profile, f) == null)))
            .Select(l => l.Id).ToList();
}
