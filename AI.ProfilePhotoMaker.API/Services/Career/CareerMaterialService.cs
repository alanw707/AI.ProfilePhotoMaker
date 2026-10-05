using System.Text.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.Career;

public interface ICareerMaterialService
{
    Task<CareerOutcome<CareerMaterialListDto>> ListAsync(string ownerId, string? kind, CancellationToken ct = default);
    Task<CareerOutcome<CareerMaterialDto>> GetAsync(string ownerId, Guid id, CancellationToken ct = default);
    Task<CareerOutcome<CareerMaterialDto>> SaveAsync(string ownerId, Guid id, SaveMaterialRequest request, VersionPrecondition precondition, CancellationToken ct = default);
    Task<CareerOutcome<CareerMaterialVersionListDto>> ListVersionsAsync(string ownerId, Guid id, int page, CancellationToken ct = default);
    Task<CareerOutcome<CareerMaterialVersionDto>> GetVersionAsync(string ownerId, Guid id, int number, CancellationToken ct = default);
    Task<CareerOutcome<CareerMaterialDto>> RestoreAsync(string ownerId, Guid id, int number, VersionPrecondition precondition, CancellationToken ct = default);
    Task<CareerOutcome<CareerMaterialProposalDto>> GetProposalAsync(string ownerId, Guid id, Guid proposalId, CancellationToken ct = default);
    Task<CareerOutcome<CareerMaterialDto>> ApplyProposalAsync(string ownerId, Guid id, Guid proposalId, ApplyMaterialProposalRequest request, VersionPrecondition precondition, CancellationToken ct = default);
    Task<CareerOutcome<CareerMaterialProposalDto>> RejectProposalAsync(string ownerId, Guid id, Guid proposalId, CancellationToken ct = default);
}

/// <summary>
/// Reads and edits targeted resumes (ADR 0018). Every save appends an immutable version and needs the current
/// ETag, so a human edit is never silently overwritten by a later save or a run's proposal.
/// </summary>
public sealed class CareerMaterialService : ICareerMaterialService
{
    private const int MaxListed = 50;

    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public CareerMaterialService(ApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public static string Etag(int version) => $"\"material-v{version}\"";

    /// <summary>The proposal's stored draft and the pins it was built from.</summary>
    public sealed record StoredProposal(
        IReadOnlyList<ResumeSection> Sections, IReadOnlyList<ResumeQuestion> Questions, int ProfileVersion, int GoalVersion, string OccupationCode);

    private static CareerOutcome<T> Missing<T>() => CareerOutcome<T>.NotFound(ResumeErrorCodes.MaterialNotFound, "That resume was not found.");

    private static CareerOutcome<T> Field<T>(string field, string message) =>
        CareerOutcome<T>.Invalid(new Dictionary<string, string> { [field] = message });

    private DateTime Now() => _clock.GetUtcNow().UtcDateTime;

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    // ---- Reads -----------------------------------------------------------------

    public async Task<CareerOutcome<CareerMaterialListDto>> ListAsync(string ownerId, string? kind, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(kind) && kind != CareerMaterialKinds.Resume)
        {
            return Field<CareerMaterialListDto>("kind", "Only resume materials are available.");
        }
        var rows = await _db.CareerMaterials.AsNoTracking().Where(m => m.OwnerId == ownerId && m.Kind == CareerMaterialKinds.Resume)
            .OrderByDescending(m => m.UpdatedAt).Take(MaxListed).ToListAsync(ct);
        var (profile, goal) = await CurrentAsync(ownerId, ct);
        return CareerOutcome<CareerMaterialListDto>.Ok(new CareerMaterialListDto(rows.Select(m =>
            new CareerMaterialSummaryDto(m.Id, m.Title, StaleReasons(m.PinnedProfileVersion, m.PinnedGoalVersion, profile, goal).Count > 0, m.CurrentVersion, Utc(m.UpdatedAt))).ToList()));
    }

    public async Task<CareerOutcome<CareerMaterialDto>> GetAsync(string ownerId, Guid id, CancellationToken ct = default)
    {
        var material = await _db.CareerMaterials.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id && m.OwnerId == ownerId, ct);
        return material == null ? Missing<CareerMaterialDto>() : CareerOutcome<CareerMaterialDto>.Ok(await ToDtoAsync(material, ct));
    }

    public async Task<CareerOutcome<CareerMaterialVersionListDto>> ListVersionsAsync(string ownerId, Guid id, int page, CancellationToken ct = default)
    {
        if (page < 1)
        {
            return Field<CareerMaterialVersionListDto>("page", "Page must be 1 or more.");
        }
        if (!await _db.CareerMaterials.AsNoTracking().AnyAsync(m => m.Id == id && m.OwnerId == ownerId, ct))
        {
            return Missing<CareerMaterialVersionListDto>();
        }
        var listed = _db.CareerMaterialVersions.AsNoTracking().Where(v => v.MaterialId == id && v.OwnerId == ownerId)
            .OrderByDescending(v => v.Number).Take(ResumeLimits.MaxVersionsListed);
        var total = await listed.CountAsync(ct);
        var rows = await listed.Skip((page - 1) * ResumeLimits.VersionPageSize).Take(ResumeLimits.VersionPageSize).ToListAsync(ct);
        return CareerOutcome<CareerMaterialVersionListDto>.Ok(new CareerMaterialVersionListDto(
            rows.Select(v => new CareerMaterialVersionSummaryDto(v.Number, v.Author, Utc(v.CreatedAt))).ToList(), total));
    }

    public async Task<CareerOutcome<CareerMaterialVersionDto>> GetVersionAsync(string ownerId, Guid id, int number, CancellationToken ct = default)
    {
        if (!await _db.CareerMaterials.AsNoTracking().AnyAsync(m => m.Id == id && m.OwnerId == ownerId, ct))
        {
            return Missing<CareerMaterialVersionDto>();
        }
        var version = await _db.CareerMaterialVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.MaterialId == id && v.OwnerId == ownerId && v.Number == number, ct);
        if (version == null)
        {
            return CareerOutcome<CareerMaterialVersionDto>.NotFound(CareerErrorCodes.VersionNotFound, "That version was not found.");
        }
        var sections = Sections(version);
        var questions = Questions(version);
        return CareerOutcome<CareerMaterialVersionDto>.Ok(new CareerMaterialVersionDto(
            version.Number, version.Author, Utc(version.CreatedAt), version.RestoredFromVersion,
            new ResumePinnedDto(version.PinnedProfileVersion, version.PinnedGoalVersion, version.OccupationCode),
            Contact(version), sections, questions, await FactsAsync(ownerId, version.PinnedProfileVersion, sections, questions, ct)));
    }

    public async Task<CareerOutcome<CareerMaterialProposalDto>> GetProposalAsync(string ownerId, Guid id, Guid proposalId, CancellationToken ct = default)
    {
        var proposal = await _db.CareerMaterialProposals.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == proposalId && p.MaterialId == id && p.OwnerId == ownerId, ct);
        return proposal == null ? ProposalMissing<CareerMaterialProposalDto>() : CareerOutcome<CareerMaterialProposalDto>.Ok(ToDto(proposal));
    }

    private static CareerOutcome<T> ProposalMissing<T>() =>
        CareerOutcome<T>.NotFound(ResumeErrorCodes.MaterialProposalNotFound, "That proposal was not found.");

    // ---- Writes ----------------------------------------------------------------

    public async Task<CareerOutcome<CareerMaterialDto>> SaveAsync(
        string ownerId, Guid id, SaveMaterialRequest request, VersionPrecondition precondition, CancellationToken ct = default)
    {
        var material = await _db.CareerMaterials.FirstOrDefaultAsync(m => m.Id == id && m.OwnerId == ownerId, ct);
        if (material == null)
        {
            return Missing<CareerMaterialDto>();
        }
        var gate = Gate<CareerMaterialDto>(material, precondition);
        if (gate != null)
        {
            return gate;
        }

        var (sections, errors) = ParseSections(request.Sections);
        if (errors != null)
        {
            return CareerOutcome<CareerMaterialDto>.Invalid(errors);
        }
        var current = await LatestAsync(material, ct);
        var contact = MergeContact(Contact(current), request.Contact);

        var profile = await ProfileAsync(ownerId, material.PinnedProfileVersion, ct);
        var unsupported = profile == null ? sections.SelectMany(s => s.Lines).Where(l => l.Origin == ResumeOrigins.Generated).Select(l => l.Id).ToList()
            : ResumeFacts.UnsupportedLineIds(profile, sections);
        if (unsupported.Count > 0)
        {
            return CareerOutcome<CareerMaterialDto>.AlreadyExists(ResumeErrorCodes.UnsupportedClaim,
                "Some generated lines are not backed by a fact in your confirmed profile.") with { Detail = string.Join(",", unsupported) };
        }

        var questions = CarryQuestions(Questions(current), sections);
        Append(material, sections, questions, contact, ResumeAuthors.User, null,
            material.PinnedProfileVersion, material.PinnedGoalVersion, material.OccupationCode);
        return await CommitAsync(material, ct);
    }

    public async Task<CareerOutcome<CareerMaterialDto>> RestoreAsync(
        string ownerId, Guid id, int number, VersionPrecondition precondition, CancellationToken ct = default)
    {
        var material = await _db.CareerMaterials.FirstOrDefaultAsync(m => m.Id == id && m.OwnerId == ownerId, ct);
        if (material == null)
        {
            return Missing<CareerMaterialDto>();
        }
        var old = await _db.CareerMaterialVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.MaterialId == id && v.OwnerId == ownerId && v.Number == number, ct);
        if (old == null)
        {
            return CareerOutcome<CareerMaterialDto>.NotFound(CareerErrorCodes.VersionNotFound, "That version was not found.");
        }
        var gate = Gate<CareerMaterialDto>(material, precondition);
        if (gate != null)
        {
            return gate;
        }
        Append(material, Sections(old), Questions(old), Contact(old), ResumeAuthors.User, old.Number,
            old.PinnedProfileVersion, old.PinnedGoalVersion, old.OccupationCode);
        return await CommitAsync(material, ct);
    }

    public async Task<CareerOutcome<CareerMaterialDto>> ApplyProposalAsync(
        string ownerId, Guid id, Guid proposalId, ApplyMaterialProposalRequest request, VersionPrecondition precondition, CancellationToken ct = default)
    {
        var material = await _db.CareerMaterials.FirstOrDefaultAsync(m => m.Id == id && m.OwnerId == ownerId, ct);
        var proposal = material == null ? null : await _db.CareerMaterialProposals
            .FirstOrDefaultAsync(p => p.Id == proposalId && p.MaterialId == id && p.OwnerId == ownerId, ct);
        if (material == null)
        {
            return Missing<CareerMaterialDto>();
        }
        if (proposal == null)
        {
            return ProposalMissing<CareerMaterialDto>();
        }
        if (proposal.Status != CareerMaterialProposalStatuses.Open)
        {
            return Closed<CareerMaterialDto>();
        }
        // The If-Match is the version the user reviewed. A human save since then (or since the run) moves it.
        var gate = Gate<CareerMaterialDto>(material, precondition);
        if (gate != null)
        {
            return gate;
        }
        if (proposal.BaseVersion != material.CurrentVersion)
        {
            return CareerOutcome<CareerMaterialDto>.Conflict(material.CurrentVersion);
        }

        var changes = JsonSerializer.Deserialize<List<ResumeChange>>(proposal.ChangesJson, ResumeJson.Options) ?? new();
        var accepted = (request.AcceptedChangeIds ?? new List<string>()).Select(i => i?.Trim() ?? "").ToHashSet(StringComparer.Ordinal);
        if (accepted.Any(a => changes.All(c => c.Id != a)))
        {
            return Field<CareerMaterialDto>("acceptedChangeIds", "One of those changes is not part of this proposal.");
        }
        var stored = JsonSerializer.Deserialize<StoredProposal>(proposal.ProposedJson, ResumeJson.Options)!;
        var current = await LatestAsync(material, ct);

        var oldProfile = await ProfileAsync(ownerId, material.PinnedProfileVersion, ct);
        var newProfile = await ProfileAsync(ownerId, stored.ProfileVersion, ct);
        if (newProfile == null)
        {
            return Field<CareerMaterialDto>("acceptedChangeIds", "The profile this proposal was built from is no longer available.");
        }

        var (sections, questions) = ApplyChanges(Sections(current), Questions(current), stored, changes.Where(c => accepted.Contains(c.Id)).ToList(), oldProfile, newProfile);
        proposal.Status = CareerMaterialProposalStatuses.Applied;
        Append(material, sections, questions, Contact(current), ResumeAuthors.Agent, null,
            stored.ProfileVersion, stored.GoalVersion, stored.OccupationCode);
        return await CommitAsync(material, ct);
    }

    public async Task<CareerOutcome<CareerMaterialProposalDto>> RejectProposalAsync(string ownerId, Guid id, Guid proposalId, CancellationToken ct = default)
    {
        var proposal = await _db.CareerMaterialProposals.FirstOrDefaultAsync(p => p.Id == proposalId && p.MaterialId == id && p.OwnerId == ownerId, ct);
        if (proposal == null)
        {
            return ProposalMissing<CareerMaterialProposalDto>();
        }
        if (proposal.Status == CareerMaterialProposalStatuses.Applied)
        {
            return Closed<CareerMaterialProposalDto>();
        }
        if (proposal.Status == CareerMaterialProposalStatuses.Open)
        {
            proposal.Status = CareerMaterialProposalStatuses.Rejected;
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (CareerProfileService.IsLostRace(ex))
            {
                _db.ChangeTracker.Clear();
                return Closed<CareerMaterialProposalDto>();
            }
        }
        return CareerOutcome<CareerMaterialProposalDto>.Ok(ToDto(proposal));
    }

    private static CareerOutcome<T> Closed<T>() =>
        CareerOutcome<T>.AlreadyExists(ResumeErrorCodes.MaterialProposalClosed, "This proposal was already decided.");

    // ---- Apply -----------------------------------------------------------------

    /// <summary>
    /// Merges accepted changes into the current lines. Human lines are never changed or removed. When the draft
    /// was built from a newer profile, kept generated lines whose cited facts changed lose their claim and become
    /// the user's own text, so no line ever cites a fact that now says something else.
    /// </summary>
    internal static (List<ResumeSection> Sections, List<ResumeQuestion> Questions) ApplyChanges(
        IReadOnlyList<ResumeSection> current, IReadOnlyList<ResumeQuestion> currentQuestions, StoredProposal stored,
        IReadOnlyList<ResumeChange> accepted, CareerProfileVersion? oldProfile, CareerProfileVersion newProfile)
    {
        bool Shifted(ResumeLine line) =>
            line.Origin == ResumeOrigins.Generated
            && (oldProfile == null || line.FactIds.Any(f => ResumeFacts.Resolve(oldProfile, f) != ResumeFacts.Resolve(newProfile, f)));

        var acceptedIds = accepted.Select(c => c.Id[(c.Id.IndexOf(':') + 1)..]).ToHashSet(StringComparer.Ordinal);
        var total = current.Sum(s => s.Lines.Count);
        var result = new List<ResumeSection>();
        foreach (var key in ResumeSectionKeys.All)
        {
            var lines = (current.FirstOrDefault(s => s.Key == key)?.Lines ?? Array.Empty<ResumeLine>()).ToList();
            var proposedLines = stored.Sections.FirstOrDefault(s => s.Key == key)?.Lines ?? Array.Empty<ResumeLine>();
            foreach (var change in accepted.Where(c => c.Section == key))
            {
                var lineId = change.Id[(change.Id.IndexOf(':') + 1)..];
                var at = lines.FindIndex(l => l.Id == lineId);
                switch (change.Kind)
                {
                    case "removed":
                        if (at >= 0 && lines[at].Origin == ResumeOrigins.Generated)
                        {
                            lines.RemoveAt(at);
                            total--;
                        }
                        break;
                    case "changed":
                        if (at >= 0 && lines[at].Origin == ResumeOrigins.Generated && proposedLines.FirstOrDefault(l => l.Id == lineId) is { } next)
                        {
                            lines[at] = next;
                        }
                        break;
                    case "added":
                        if (at < 0 && total < ResumeLimits.MaxLines && proposedLines.FirstOrDefault(l => l.Id == lineId) is { } added)
                        {
                            lines.Add(added);
                            total++;
                        }
                        break;
                }
            }
            // Lines this proposal did not just rewrite are checked against the newer facts.
            lines = lines.Select(l => !acceptedIds.Contains(l.Id) && Shifted(l) ? l with { Origin = ResumeOrigins.Human, FactIds = Array.Empty<string>() } : l).ToList();
            result.Add(new ResumeSection(key, lines));
        }

        var kept = currentQuestions.Where(q => oldProfile != null && ResumeFacts.Resolve(oldProfile, q.FactId) == ResumeFacts.Resolve(newProfile, q.FactId)).ToList();
        foreach (var q in stored.Questions)
        {
            if (kept.All(k => k.Id != q.Id) && result.SelectMany(s => s.Lines).Any(l => l.Origin == ResumeOrigins.Generated && l.FactIds.Contains(q.FactId)))
            {
                kept.Add(q);
            }
        }
        return (result, CarryQuestions(kept, result));
    }

    // ---- Validation ------------------------------------------------------------

    private static (List<ResumeSection> Sections, Dictionary<string, string>? Errors) ParseSections(List<SaveSectionRequest?>? input)
    {
        var errors = new Dictionary<string, string>();
        if (input == null)
        {
            errors["sections"] = "Send the resume sections.";
            return (new(), errors);
        }
        var byKey = new Dictionary<string, List<ResumeLine>>(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var section in input)
        {
            var key = section?.Key?.Trim();
            if (key == null || !ResumeSectionKeys.All.Contains(key) || byKey.ContainsKey(key))
            {
                errors["sections"] = $"Each section needs one unique key: {string.Join(", ", ResumeSectionKeys.All)}.";
                return (new(), errors);
            }
            var lines = new List<ResumeLine>();
            foreach (var line in section!.Lines ?? new())
            {
                var text = line?.Text?.Trim();
                if (text == null || text.Length == 0 || text.Length > ResumeLimits.MaxLineLength)
                {
                    errors["lines"] = $"Each line needs 1-{ResumeLimits.MaxLineLength} characters.";
                    return (new(), errors);
                }
                var origin = line!.Origin?.Trim() ?? ResumeOrigins.Human;
                if (origin is not (ResumeOrigins.Generated or ResumeOrigins.Human))
                {
                    errors["origin"] = "Origin must be generated or human.";
                    return (new(), errors);
                }
                var id = string.IsNullOrWhiteSpace(line.Id) ? $"u-{Guid.NewGuid():N}"[..14] : line.Id.Trim();
                if (id.Length > ResumeLimits.MaxLineIdLength || !ids.Add(id))
                {
                    errors["lines"] = "Line ids must be unique and at most 64 characters.";
                    return (new(), errors);
                }
                var factIds = (line.FactIds ?? new()).Select(f => f?.Trim() ?? "").Where(f => f.Length > 0).Distinct(StringComparer.Ordinal).ToList();
                if (factIds.Count > ResumeLimits.MaxFactIdsPerLine || factIds.Any(f => f.Length > ResumeLimits.MaxFactIdLength))
                {
                    errors["factIds"] = $"A line may cite at most {ResumeLimits.MaxFactIdsPerLine} facts.";
                    return (new(), errors);
                }
                lines.Add(new ResumeLine(id, text, factIds, origin));
            }
            byKey[key] = lines;
        }
        if (byKey.Values.Sum(l => l.Count) > ResumeLimits.MaxLines)
        {
            errors["lines"] = $"A resume can have at most {ResumeLimits.MaxLines} lines.";
            return (new(), errors);
        }
        return (ResumeSectionKeys.All.Select(k => new ResumeSection(k, byKey.GetValueOrDefault(k) ?? new())).ToList(), null);
    }

    /// <summary>A question stays until the user answers it by writing the line themselves (a human line citing it, or digits).</summary>
    private static List<ResumeQuestion> CarryQuestions(IEnumerable<ResumeQuestion> questions, IReadOnlyList<ResumeSection> sections)
    {
        var lines = sections.SelectMany(s => s.Lines).ToList();
        return questions.Where(q => !lines.Any(l => l.FactIds.Contains(q.FactId) && (l.Origin == ResumeOrigins.Human || l.Text.Any(char.IsDigit)))
            && lines.Any(l => l.FactIds.Contains(q.FactId))).ToList();
    }

    private static ResumeContact MergeContact(ResumeContact previous, SaveContactRequest? input) =>
        input == null ? previous : new ResumeContact(
            input.Name ?? previous.Name, input.Email ?? previous.Email, input.Phone ?? previous.Phone,
            input.Location ?? previous.Location, input.Links ?? previous.Links);

    // ---- Commit ----------------------------------------------------------------

    private static CareerOutcome<T>? Gate<T>(CareerMaterial material, VersionPrecondition precondition)
    {
        if (!precondition.IsPresent)
        {
            return CareerOutcome<T>.PreconditionRequired();
        }
        return precondition.ExpectedVersion != material.CurrentVersion ? CareerOutcome<T>.Conflict(material.CurrentVersion) : null;
    }

    /// <summary>Stages the next version and moves the material to it; the caller saves.</summary>
    private void Append(
        CareerMaterial material, IReadOnlyList<ResumeSection> sections, IReadOnlyList<ResumeQuestion> questions, ResumeContact contact,
        string author, int? restoredFrom, int profileVersion, int goalVersion, string occupationCode)
    {
        var now = Now();
        material.CurrentVersion++;
        material.PinnedProfileVersion = profileVersion;
        material.PinnedGoalVersion = goalVersion;
        material.OccupationCode = occupationCode;
        material.UpdatedAt = now;
        _db.CareerMaterialVersions.Add(NewVersion(material, sections, questions, contact, author, restoredFrom, now));
    }

    internal static CareerMaterialVersion NewVersion(
        CareerMaterial material, IReadOnlyList<ResumeSection> sections, IReadOnlyList<ResumeQuestion> questions, ResumeContact contact,
        string author, int? restoredFrom, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        MaterialId = material.Id,
        OwnerId = material.OwnerId,
        Number = material.CurrentVersion,
        SectionsJson = JsonSerializer.Serialize(sections, ResumeJson.Options),
        QuestionsJson = JsonSerializer.Serialize(questions, ResumeJson.Options),
        ContactJson = JsonSerializer.Serialize(contact, ResumeJson.Options),
        PinnedProfileVersion = material.PinnedProfileVersion,
        PinnedGoalVersion = material.PinnedGoalVersion,
        OccupationCode = material.OccupationCode,
        Author = author,
        RestoredFromVersion = restoredFrom,
        CreatedAt = now
    };

    private async Task<CareerOutcome<CareerMaterialDto>> CommitAsync(CareerMaterial material, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is DbUpdateException or DbUpdateConcurrencyException)
        {
            // Another writer moved the material (concurrency token) or took the version number (unique index).
            _db.ChangeTracker.Clear();
            var now = await _db.CareerMaterials.AsNoTracking().Where(m => m.Id == material.Id).Select(m => (int?)m.CurrentVersion).FirstOrDefaultAsync(ct);
            return CareerOutcome<CareerMaterialDto>.Conflict(now ?? 0);
        }
        return CareerOutcome<CareerMaterialDto>.Ok(await ToDtoAsync(material, ct));
    }

    // ---- Mapping ---------------------------------------------------------------

    private static List<ResumeSection> Sections(CareerMaterialVersion v) =>
        JsonSerializer.Deserialize<List<ResumeSection>>(v.SectionsJson, ResumeJson.Options) ?? new();

    private static List<ResumeQuestion> Questions(CareerMaterialVersion v) =>
        JsonSerializer.Deserialize<List<ResumeQuestion>>(v.QuestionsJson, ResumeJson.Options) ?? new();

    private static ResumeContact Contact(CareerMaterialVersion v) =>
        JsonSerializer.Deserialize<ResumeContact>(v.ContactJson, ResumeJson.Options) ?? new ResumeContact();

    private async Task<CareerMaterialVersion> LatestAsync(CareerMaterial material, CancellationToken ct) =>
        await _db.CareerMaterialVersions.AsNoTracking()
            .FirstAsync(v => v.MaterialId == material.Id && v.OwnerId == material.OwnerId && v.Number == material.CurrentVersion, ct);

    private async Task<CareerProfileVersion?> ProfileAsync(string ownerId, int version, CancellationToken ct) =>
        await _db.CareerProfileVersions.AsNoTracking().FirstOrDefaultAsync(v => v.OwnerId == ownerId && v.VersionNumber == version, ct);

    private async Task<(int? Profile, int? Goal)> CurrentAsync(string ownerId, CancellationToken ct) => (
        await _db.CareerProfiles.AsNoTracking().Where(p => p.OwnerId == ownerId).Select(p => (int?)p.ActiveVersionNumber).FirstOrDefaultAsync(ct),
        await _db.CareerGoals.AsNoTracking().Where(g => g.OwnerId == ownerId).Select(g => (int?)g.ActiveVersionNumber).FirstOrDefaultAsync(ct));

    private static List<string> StaleReasons(int pinnedProfile, int pinnedGoal, int? profile, int? goal)
    {
        var reasons = new List<string>();
        if (profile != pinnedProfile) reasons.Add("profile_changed");
        if (goal != pinnedGoal) reasons.Add("goal_changed");
        return reasons;
    }

    private async Task<List<ResumeFactDto>> FactsAsync(
        string ownerId, int profileVersion, IEnumerable<ResumeSection> sections, IEnumerable<ResumeQuestion> questions, CancellationToken ct)
    {
        var profile = await ProfileAsync(ownerId, profileVersion, ct);
        if (profile == null)
        {
            return new();
        }
        var ids = sections.SelectMany(s => s.Lines).SelectMany(l => l.FactIds).Concat(questions.Select(q => q.FactId)).Distinct(StringComparer.Ordinal);
        return ids.Select(i => (Id: i, Text: ResumeFacts.Resolve(profile, i))).Where(f => f.Text != null).Select(f => new ResumeFactDto(f.Id, f.Text!)).ToList();
    }

    private async Task<CareerMaterialDto> ToDtoAsync(CareerMaterial material, CancellationToken ct)
    {
        var version = await LatestAsync(material, ct);
        var sections = Sections(version);
        var questions = Questions(version);
        var (profile, goal) = await CurrentAsync(material.OwnerId, ct);
        var reasons = StaleReasons(material.PinnedProfileVersion, material.PinnedGoalVersion, profile, goal);
        return new CareerMaterialDto(
            material.Id, material.Title, Etag(material.CurrentVersion), material.CurrentVersion,
            new ResumePinnedDto(material.PinnedProfileVersion, material.PinnedGoalVersion, material.OccupationCode),
            reasons.Count > 0, reasons, Contact(version), sections, questions,
            await FactsAsync(material.OwnerId, material.PinnedProfileVersion, sections, questions, ct));
    }

    private static CareerMaterialProposalDto ToDto(CareerMaterialProposal proposal) => new(
        proposal.Id, proposal.Status, proposal.BaseVersion,
        JsonSerializer.Deserialize<List<ResumeChange>>(proposal.ChangesJson, ResumeJson.Options) ?? new());
}
