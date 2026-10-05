using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.Career;

public interface ICareerProposalService
{
    Task<CareerOutcome<CareerProfileProposalDto>> CreateFromPasteAsync(string ownerId, PasteProposalRequest request, CancellationToken ct = default);
    Task<CareerOutcome<CareerProfileProposalDto>> GetAsync(string ownerId, Guid id, CancellationToken ct = default);
    Task<CareerOutcome<CareerProfileDto>> AcceptAsync(string ownerId, Guid id, AcceptProposalRequest request, VersionPrecondition precondition, CancellationToken ct = default);
    Task<CareerOutcome<CareerProfileProposalDto>> DismissAsync(string ownerId, Guid id, CancellationToken ct = default);
}

/// <summary>Creates proposals; shared by the upload pipeline and the paste endpoint.</summary>
internal static class ProposalStore
{
    /// <summary>Adds (without saving) a proposal pinned to the owner's current profile version.</summary>
    public static async Task<CareerProfileProposal> AddAsync(
        ApplicationDbContext db, string ownerId, string source, Guid? resumeId,
        IReadOnlyList<ExtractedItem> items, DateTime now, CancellationToken ct)
    {
        var baseVersion = await db.CareerProfiles.AsNoTracking()
            .Where(p => p.OwnerId == ownerId)
            .Select(p => (int?)p.ActiveVersionNumber)
            .FirstOrDefaultAsync(ct);

        var proposal = new CareerProfileProposal
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Source = source,
            ResumeDocumentId = resumeId,
            BaseProfileVersion = baseVersion,
            Status = ProposalStatus.Pending,
            CreatedAt = now
        };

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            proposal.Items.Add(new CareerProfileProposalItem
            {
                Id = Guid.NewGuid(),
                ProposalId = proposal.Id,
                OwnerId = ownerId,
                Ordinal = i,
                Field = item.Field,
                Value = item.Value,
                Page = item.Page,
                Section = item.Section,
                Excerpt = item.Excerpt,
                Flags = item.Flags.ToList()
            });
        }

        db.CareerProfileProposals.Add(proposal);
        return proposal;
    }
}

/// <summary>
/// Reviewable proposals (ADR 0007). Nothing here changes the profile except
/// <see cref="AcceptAsync"/>, which merges only the items the user chose.
/// </summary>
public sealed class CareerProposalService : ICareerProposalService
{
    public const int MaxPasteCharacters = ResumeFileInspector.MaxCharacters;

    private readonly ApplicationDbContext _db;
    private readonly ICareerProfileService _profiles;
    private readonly TimeProvider _clock;
    private readonly ILogger<CareerProposalService> _logger;

    public CareerProposalService(
        ApplicationDbContext db, ICareerProfileService profiles, TimeProvider clock, ILogger<CareerProposalService> logger)
    {
        _db = db;
        _profiles = profiles;
        _clock = clock;
        _logger = logger;
    }

    public async Task<CareerOutcome<CareerProfileProposalDto>> CreateFromPasteAsync(
        string ownerId, PasteProposalRequest request, CancellationToken ct = default)
    {
        var text = request.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return CareerOutcome<CareerProfileProposalDto>.Invalid(new Dictionary<string, string> { ["text"] = "Paste your resume text." });
        }
        if (text.Length > MaxPasteCharacters)
        {
            return CareerOutcome<CareerProfileProposalDto>.Invalid(
                new Dictionary<string, string> { ["text"] = $"Use {MaxPasteCharacters:N0} characters or fewer." });
        }

        var now = Now();
        var items = ResumeFactExtractor.Extract(new[] { text }, pasted: true, now);
        var proposal = await ProposalStore.AddAsync(_db, ownerId, ProposalSources.Pasted, null, items, now, ct);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Pasted proposal {ProposalId} created with {ItemCount} items", proposal.Id, items.Count);
        return CareerOutcome<CareerProfileProposalDto>.Created(await ToDtoAsync(proposal, ownerId, ct));
    }

    public async Task<CareerOutcome<CareerProfileProposalDto>> GetAsync(string ownerId, Guid id, CancellationToken ct = default)
    {
        var proposal = await FindAsync(ownerId, id, ct);
        return proposal == null
            ? NotFound<CareerProfileProposalDto>()
            : CareerOutcome<CareerProfileProposalDto>.Ok(await ToDtoAsync(proposal, ownerId, ct));
    }

    public async Task<CareerOutcome<CareerProfileDto>> AcceptAsync(
        string ownerId, Guid id, AcceptProposalRequest request, VersionPrecondition precondition, CancellationToken ct = default)
    {
        var proposal = await FindAsync(ownerId, id, ct);
        if (proposal == null)
        {
            return NotFound<CareerProfileDto>();
        }
        if (proposal.Status != ProposalStatus.Pending)
        {
            return CareerOutcome<CareerProfileDto>.AlreadyExists(
                CareerErrorCodes.ProposalAlreadyDecided, "This proposal was already accepted or dismissed.");
        }

        var chosenIds = (request.ItemIds ?? new List<Guid>()).Distinct().ToList();
        if (chosenIds.Count == 0)
        {
            return Invalid<CareerProfileDto>("itemIds", "Choose at least one item to accept.");
        }
        var chosen = proposal.Items.Where(i => chosenIds.Contains(i.Id)).OrderBy(i => i.Ordinal).ToList();
        if (chosen.Count != chosenIds.Count)
        {
            return Invalid<CareerProfileDto>("itemIds", "Some items are not part of this proposal.");
        }

        var current = await CurrentProfileAsync(ownerId, ct);
        if (current != null)
        {
            if (!precondition.IsPresent)
            {
                return CareerOutcome<CareerProfileDto>.PreconditionRequired();
            }
            // Stale if the client's ETag is old, or the profile moved since the proposal was made.
            if (precondition.ExpectedVersion != current.VersionNumber || proposal.BaseProfileVersion != current.VersionNumber)
            {
                return CareerOutcome<CareerProfileDto>.Conflict(current.VersionNumber);
            }
        }
        else if (proposal.BaseProfileVersion != null)
        {
            return CareerOutcome<CareerProfileDto>.Conflict(0);
        }

        var (facts, errors) = CareerInputValidator.Validate(Merge(current, chosen));
        if (facts == null)
        {
            return CareerOutcome<CareerProfileDto>.Invalid(errors);
        }

        var outcome = await _profiles.AppendFromProposalAsync(
            ownerId, facts, proposal.Source, proposal.Id, proposal.BaseProfileVersion,
            beforeCommit: version =>
            {
                proposal.Status = ProposalStatus.Accepted;
                proposal.DecidedAt = Now();
                proposal.AcceptedIntoVersion = version;
            },
            ct);

        if (outcome.Kind == CareerOutcomeKind.Ok)
        {
            _logger.LogInformation("Proposal {ProposalId} accepted into profile version {Version}", proposal.Id, outcome.Value!.Version);
        }
        return outcome;
    }

    public async Task<CareerOutcome<CareerProfileProposalDto>> DismissAsync(string ownerId, Guid id, CancellationToken ct = default)
    {
        var proposal = await FindAsync(ownerId, id, ct);
        if (proposal == null)
        {
            return NotFound<CareerProfileProposalDto>();
        }
        if (proposal.Status == ProposalStatus.Accepted)
        {
            return CareerOutcome<CareerProfileProposalDto>.AlreadyExists(
                CareerErrorCodes.ProposalAlreadyDecided, "This proposal was already accepted.");
        }

        if (proposal.Status == ProposalStatus.Pending)
        {
            proposal.Status = ProposalStatus.Dismissed;
            proposal.DecidedAt = Now();
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Proposal {ProposalId} dismissed", proposal.Id);
        }
        return CareerOutcome<CareerProfileProposalDto>.Ok(await ToDtoAsync(proposal, ownerId, ct));
    }

    // ---- Merge -------------------------------------------------------------

    /// <summary>
    /// Applies chosen items to the current facts: skills and highlights are appended
    /// (de-duplicated, case-insensitively); every other field is replaced.
    /// </summary>
    private static CareerProfileRequest Merge(CareerProfileVersion? current, IReadOnlyList<CareerProfileProposalItem> chosen)
    {
        var request = new CareerProfileRequest
        {
            CurrentTitle = current?.CurrentTitle,
            Industry = current?.Industry,
            YearsExperience = current?.YearsExperience,
            Location = current?.Location,
            Summary = current?.Summary,
            Skills = current?.Skills.Cast<string?>().ToList() ?? new List<string?>(),
            Highlights = current?.Highlights.Cast<string?>().ToList() ?? new List<string?>(),
            WorkArrangement = current?.WorkArrangement,
            // Choosing items and accepting is the explicit confirmation.
            Confirmed = true
        };

        foreach (var item in chosen)
        {
            switch (item.Field)
            {
                case "currentTitle": request.CurrentTitle = item.Value; break;
                case "industry": request.Industry = item.Value; break;
                case "location": request.Location = item.Value; break;
                case "summary": request.Summary = item.Value; break;
                case "yearsExperience":
                    request.YearsExperience = int.TryParse(item.Value, out var years) ? years : request.YearsExperience;
                    break;
                case "skills": AppendUnique(request.Skills!, item.Value); break;
                case "highlights": AppendUnique(request.Highlights!, item.Value); break;
            }
        }
        return request;
    }

    private static void AppendUnique(List<string?> list, string value)
    {
        if (!list.Any(existing => string.Equals(existing?.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            list.Add(value);
        }
    }

    // ---- Helpers -------------------------------------------------------------

    private DateTime Now() => _clock.GetUtcNow().UtcDateTime;

    private Task<CareerProfileProposal?> FindAsync(string ownerId, Guid id, CancellationToken ct) =>
        _db.CareerProfileProposals.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == ownerId, ct);

    /// <summary>The owner's active profile version row, or null when no profile exists.</summary>
    private async Task<CareerProfileVersion?> CurrentProfileAsync(string ownerId, CancellationToken ct)
    {
        var profile = await _db.CareerProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.OwnerId == ownerId, ct);
        return profile == null
            ? null
            : await _db.CareerProfileVersions.AsNoTracking().FirstOrDefaultAsync(
                v => v.OwnerId == ownerId && v.CareerProfileId == profile.Id && v.VersionNumber == profile.ActiveVersionNumber, ct);
    }

    private async Task<CareerProfileProposalDto> ToDtoAsync(CareerProfileProposal proposal, string ownerId, CancellationToken ct)
    {
        var current = await CurrentProfileAsync(ownerId, ct);
        var stale = current != null && proposal.BaseProfileVersion != current.VersionNumber;

        var items = proposal.Items.OrderBy(i => i.Ordinal).Select(i => new ProposalItemDto(
            i.Id, i.Field, i.Value, CurrentValue(current, i.Field), i.Page, i.Section, i.Excerpt, i.Flags)).ToList();

        return new CareerProfileProposalDto(
            proposal.Id, proposal.Source, proposal.ResumeDocumentId, proposal.BaseProfileVersion,
            proposal.Status.ToString().ToLowerInvariant(), stale, items,
            DateTime.SpecifyKind(proposal.CreatedAt, DateTimeKind.Utc));
    }

    private static string? CurrentValue(CareerProfileVersion? current, string field) => field switch
    {
        "currentTitle" => current?.CurrentTitle,
        "industry" => current?.Industry,
        "yearsExperience" => current?.YearsExperience?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        "location" => current?.Location,
        "summary" => current?.Summary,
        _ => null
    };

    private static CareerOutcome<T> NotFound<T>() =>
        CareerOutcome<T>.NotFound(CareerErrorCodes.ProposalNotFound, "That proposal was not found.");

    private static CareerOutcome<T> Invalid<T>(string field, string message) =>
        CareerOutcome<T>.Invalid(new Dictionary<string, string> { [field] = message });
}
