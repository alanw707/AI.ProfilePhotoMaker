using System.Text.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AI.ProfilePhotoMaker.API.Services.Career;

public interface ICareerMarketService
{
    Task<CareerOutcome<CareerMarketBriefListDto>> ListAsync(string ownerId, CancellationToken ct = default);
    Task<CareerOutcome<CareerMarketBriefDto>> GetAsync(string ownerId, Guid id, CancellationToken ct = default);
    CareerOutcome<CareerMarketReferenceDto> GetReference();
}

/// <summary>
/// Reads saved market briefs (ADR 0011). A brief is never rewritten: staleness and aged data are worked
/// out at read time from the owner's current goal and profile and from the sources' publication dates.
/// </summary>
public sealed class CareerMarketService : ICareerMarketService
{
    public const int MaxListed = 20;

    public const string GoalChanged = "goal_changed";
    public const string ProfileChanged = "profile_changed";
    public const string OccupationChanged = "occupation_changed";

    private readonly ApplicationDbContext _db;
    private readonly IMarketReference _reference;
    private readonly TimeProvider _clock;
    private readonly CareerMarketOptions _options;

    public CareerMarketService(ApplicationDbContext db, IMarketReference reference, TimeProvider clock, IOptions<CareerMarketOptions> options)
    {
        _db = db;
        _reference = reference;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<CareerOutcome<CareerMarketBriefListDto>> ListAsync(string ownerId, CancellationToken ct = default)
    {
        var briefs = await _db.CareerMarketBriefs.AsNoTracking()
            .Where(b => b.OwnerId == ownerId)
            .OrderByDescending(b => b.CreatedAt)
            .Take(MaxListed)
            .ToListAsync(ct);
        var current = await CurrentAsync(ownerId, ct);

        var summaries = briefs.Select(b =>
        {
            var location = Read<MarketLocationDto>(b.LocationJson);
            return new CareerMarketBriefSummaryDto(
                b.Id, b.OccupationCode, b.OccupationTitle, AreaTitle(location), b.Status, StaleReasons(b, current).Count > 0, Utc(b.CreatedAt));
        }).ToList();
        return CareerOutcome<CareerMarketBriefListDto>.Ok(new CareerMarketBriefListDto(summaries));
    }

    public async Task<CareerOutcome<CareerMarketBriefDto>> GetAsync(string ownerId, Guid id, CancellationToken ct = default)
    {
        // Ownership first, so another owner's id reveals nothing.
        var brief = await _db.CareerMarketBriefs.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id && b.OwnerId == ownerId, ct);
        if (brief == null)
        {
            return CareerOutcome<CareerMarketBriefDto>.NotFound(CareerMarketErrorCodes.BriefNotFound, "That market brief was not found.");
        }

        var reasons = StaleReasons(brief, await CurrentAsync(ownerId, ct));
        var sources = Read<List<MarketSourceDto>>(brief.SourcesJson);
        var location = Read<MarketLocationDto>(brief.LocationJson);
        return CareerOutcome<CareerMarketBriefDto>.Ok(new CareerMarketBriefDto(
            brief.Id,
            brief.RunId,
            brief.Status,
            new MarketOccupationDto(brief.OccupationCode, brief.OccupationTitle, Read<MarketPublishedDto>(brief.PublishedJson)),
            location,
            new MarketPinnedDto(brief.PinnedProfileVersion, brief.PinnedGoalVersion, brief.OewsRelease, brief.ProjectionsRelease),
            reasons.Count > 0,
            reasons,
            DataStale(sources),
            Read<List<MarketSectionDto>>(brief.SectionsJson),
            Read<MarketNextActionDto>(brief.NextActionJson),
            sources,
            Utc(brief.CreatedAt)));
    }

    public CareerOutcome<CareerMarketReferenceDto> GetReference()
    {
        var sources = MarketBriefBuilder.Sources(_reference);
        if (sources.Count == 0)
        {
            return CareerOutcome<CareerMarketReferenceDto>.ReferenceUnavailable();
        }
        return CareerOutcome<CareerMarketReferenceDto>.Ok(new CareerMarketReferenceDto(
            sources, _reference.Oews?.Areas.Count ?? 0, _reference.Projections?.OccupationCount ?? 0));
    }

    // ---- Staleness ---------------------------------------------------------------

    /// <summary>The owner's current active goal and profile versions and the goal's occupation.</summary>
    private sealed record Current(int? ProfileVersion, int? GoalVersion, string? OccupationCode);

    private async Task<Current> CurrentAsync(string ownerId, CancellationToken ct)
    {
        var profile = await _db.CareerProfiles.AsNoTracking()
            .Where(p => p.OwnerId == ownerId).Select(p => (int?)p.ActiveVersionNumber).FirstOrDefaultAsync(ct);
        var goal = await _db.CareerGoals.AsNoTracking()
            .Where(g => g.OwnerId == ownerId).Select(g => (int?)g.ActiveVersionNumber).FirstOrDefaultAsync(ct);
        var code = goal == null
            ? null
            : await _db.CareerGoalVersions.AsNoTracking()
                .Where(v => v.OwnerId == ownerId && v.VersionNumber == goal)
                .Select(v => v.OccupationCode).FirstOrDefaultAsync(ct);
        return new Current(profile, goal, code);
    }

    private static List<string> StaleReasons(CareerMarketBrief brief, Current current)
    {
        var reasons = new List<string>();
        if (current.GoalVersion != brief.PinnedGoalVersion)
        {
            reasons.Add(GoalChanged);
        }
        if (current.ProfileVersion != brief.PinnedProfileVersion)
        {
            reasons.Add(ProfileChanged);
        }
        if (current.OccupationCode != brief.OccupationCode)
        {
            reasons.Add(OccupationChanged);
        }
        return reasons;
    }

    /// <summary>True when any source the brief cites is more than the configured number of months past its publication date.</summary>
    private bool DataStale(IEnumerable<MarketSourceDto> sources)
    {
        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        return sources.Any(s =>
            DateOnly.TryParseExact(s.PublishedOn, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var published)
            && today > published.AddMonths(_options.DataStaleMonths));
    }

    // ---- Mapping -----------------------------------------------------------------

    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, MarketBriefJson.Options)!;

    /// <summary>The national-only brief is about the whole U.S.; an unresolved location has no area.</summary>
    private static string? AreaTitle(MarketLocationDto location) => location.Resolution switch
    {
        MarketResolutions.NationalOnly => "U.S.",
        _ => location.Local?.Title
    };

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
