using AI.ProfilePhotoMaker.API.Data;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.Career;

public interface IJobObservationService
{
    Task<CareerOutcome<JobObservationResult>> GetObservationsAsync(
        string ownerId, string? area, bool eligibleOnly, string? remote, string? q, string? occupation, CancellationToken ct = default);

    CareerOutcome<JobSourceDto> GetSource();
}

/// <summary>
/// Live job observations (ADR 0015). Read-only by construction: it reads the owner's goal, asks the source for
/// one page, normalizes in memory and returns. There is no save method, no entity and no cache; listing text is
/// data and is never parsed for instructions.
/// </summary>
public sealed class JobObservationService : IJobObservationService
{
    public const string Note =
        "Postings are observations, not employment totals or an outlook: a list of open federal jobs is not the labour market.";

    private readonly ApplicationDbContext _db;
    private readonly IMarketReference _reference;
    private readonly IJobObservationSource _source;
    private readonly TimeProvider _clock;
    private readonly ILogger<JobObservationService> _logger;
    private readonly ICareerOperatorControls? _controls;

    public JobObservationService(
        ApplicationDbContext db, IMarketReference reference, IJobObservationSource source, TimeProvider clock,
        ILogger<JobObservationService> logger, ICareerOperatorControls? controls = null)
    {
        _controls = controls;
        _db = db;
        _reference = reference;
        _source = source;
        _clock = clock;
        _logger = logger;
    }

    public CareerOutcome<JobSourceDto> GetSource()
    {
        var i = _source.Info;
        return CareerOutcome<JobSourceDto>.Ok(new JobSourceDto(i.SourceId, i.Name, _source.IsConfigured, i.Coverage, i.Attribution, i.SourceUrl));
    }

    public async Task<CareerOutcome<JobObservationResult>> GetObservationsAsync(
        string ownerId, string? area, bool eligibleOnly, string? remote, string? q, string? occupation, CancellationToken ct = default)
    {
        remote = string.IsNullOrWhiteSpace(remote) ? JobRemoteFilters.All : remote.Trim().ToLowerInvariant();
        if (!JobRemoteFilters.IsValid(remote))
        {
            return CareerOutcome<JobObservationResult>.Invalid(
                new Dictionary<string, string> { ["remote"] = "Choose all, eligible, ineligible or unknown." });
        }

        var goal = await _db.CareerGoals.AsNoTracking().FirstOrDefaultAsync(g => g.OwnerId == ownerId, ct);
        var version = goal == null
            ? null
            : await _db.CareerGoalVersions.AsNoTracking()
                .FirstOrDefaultAsync(v => v.OwnerId == ownerId && v.CareerGoalId == goal.Id && v.VersionNumber == goal.ActiveVersionNumber, ct);

        var areas = _reference.Oews?.Areas ?? Array.Empty<MarketArea>();
        var requested = string.IsNullOrWhiteSpace(area) ? null : area.Trim();
        var areaInput = requested ?? version?.PreferredAreaCode ?? version?.TargetLocation;
        var resolved = ResolveArea(areaInput, areas);

        var code = version?.OccupationCode ?? (string.IsNullOrWhiteSpace(occupation) ? null : occupation.Trim());
        var title = version?.OccupationCode != null
            ? version.OccupationTitle
            : code == null ? null : _reference.Oews?.Crosswalk(code) is { } cw ? _reference.Oews.OccupationTitle(cw.Code) : null;

        var preferredCode = version?.PreferredAreaCode;
        var stale = preferredCode != null && requested != null && resolved.Local?.Code != preferredCode;
        var staleNote = stale
            ? $"Your saved place is {version!.PreferredAreaTitle ?? preferredCode}; this list is for {resolved.Local?.Title ?? (areaInput ?? "no place")}."
            : null;

        var query = new JobObservationQuery(
            code, title, resolved.Local?.Code, resolved.Local?.Title, resolved.Resolution, resolved.Input,
            eligibleOnly, remote, q?.Trim());
        var now = _clock.GetUtcNow();
        var info = _source.Info;

        string? reason = null;
        var normalized = JobObservationNormalizer.Empty;
        if (!_source.IsConfigured)
        {
            reason = JobSourceReasons.NotConfigured;
        }
        else if (code == null)
        {
            reason = JobSourceReasons.OccupationRequired;
        }
        else if (_controls != null && (await _controls.GetAsync(ct)).SourcesDisabled)
        {
            return CareerUsage.Paused<JobObservationResult>();
        }
        else
        {
            var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            var outcome = Models.Career.CareerUsageOutcomes.Ok;
            try
            {
                var page = await _source.FetchAsync(query, ct);
                var userArea = resolved.Local == null ? null : areas.FirstOrDefault(a => a.Code == resolved.Local.Code);
                normalized = JobObservationNormalizer.Normalize(page.Items, page.ProviderTotal, query, info, now, areas, userArea);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // Any mapping or provider failure is the honest unavailable state, never a 500.
                _logger.LogWarning(ex, "Job source {Source} is unavailable", info.SourceId);
                reason = JobSourceReasons.Unavailable;
                normalized = JobObservationNormalizer.Empty;
                outcome = Models.Career.CareerUsageOutcomes.Failed;
            }
            finally
            {
                // One ledger row per external source call; counts and timing only.
                _db.CareerUsageEvents.Add(CareerUsage.Event(
                    ownerId, null, Models.Career.CareerUsageActions.JobSource, outcome, now.UtcDateTime,
                    (int)System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds));
                try { await _db.SaveChangesAsync(CancellationToken.None); } catch (DbUpdateException) { _db.ChangeTracker.Clear(); }
            }
        }

        var coverage = new JobCoverageDto(
            reason == null, reason, info.SourceId, info.Name, info.Coverage, info.Attribution, info.SourceUrl, now,
            normalized.PostedFrom, normalized.PostedTo, normalized.Counts);
        var areaDto = new JobAreaDto(resolved.Input, resolved.Resolution, resolved.Local?.Code, resolved.Local?.Title);
        return CareerOutcome<JobObservationResult>.Ok(new JobObservationResult(
            code == null ? null : new JobOccupationDto(code, title), areaDto, coverage,
            new JobPreferencesDto(preferredCode, stale, staleNote), normalized.Observations, normalized.Truncated, Note));
    }

    private static MarketLocationDto ResolveArea(string? input, IReadOnlyList<MarketArea> areas)
    {
        var text = (input ?? string.Empty).Trim();
        // A BLS area code (the saved preference, or a heatmap pick) is accepted as is.
        var byCode = text.Length > 0 && text.All(char.IsAsciiDigit) ? areas.FirstOrDefault(a => a.Code == text && a.Type != "national") : null;
        return byCode != null
            ? new MarketLocationDto(text, byCode.Type, new MarketAreaDto(byCode.Code, byCode.Title, byCode.Type))
            : MarketAreaResolver.Resolve(text, areas);
    }
}

public sealed record NormalizedJobs(
    IReadOnlyList<JobObservationDto> Observations, JobCountsDto Counts, bool Truncated, DateOnly? PostedFrom, DateOnly? PostedTo);

/// <summary>Pure normalization and filtering of raw postings (ADR 0015). No I/O, no clock, no storage.</summary>
public static class JobObservationNormalizer
{
    public const int MaxObservations = 25;
    public const int MaxTitleLength = 200;
    public const int MaxNoteLength = 300;
    public const int MaxShortLength = 100;
    public const int MaxLocations = 10;

    public static NormalizedJobs Empty { get; } = new(Array.Empty<JobObservationDto>(), JobCountsDto.Empty, false, null, null);

    public static NormalizedJobs Normalize(
        IReadOnlyList<RawJobObservation> raw, int? providerTotal, JobObservationQuery query, JobSourceInfo info, DateTimeOffset now,
        IReadOnlyList<MarketArea> areas, MarketArea? userArea)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        // Newest first, then id: the first of a duplicate group is the same whatever order the provider used.
        var ordered = raw.Where(r => !string.IsNullOrWhiteSpace(r.ProviderId))
            .OrderByDescending(r => r.PostedOn ?? DateOnly.MinValue).ThenBy(r => r.ProviderId, StringComparer.Ordinal).ToList();

        int dupIds = 0, dupReposts = 0, expired = 0, otherLocation = 0, unknownExcluded = 0, ineligibleExcluded = 0, keyword = 0, remoteFilter = 0;
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var seenReposts = new HashSet<string>(StringComparer.Ordinal);
        var kept = new List<JobObservationDto>();
        var needle = query.Q?.Trim() ?? string.Empty;
        var applyArea = userArea != null;

        foreach (var r in ordered)
        {
            if (!seenIds.Add(r.ProviderId))
            {
                dupIds++;
                continue;
            }
            if (!seenReposts.Add(RepostKey(r)))
            {
                dupReposts++;
                continue;
            }
            if (r.ClosesOn is { } closes && closes < today)
            {
                expired++;
                continue;
            }

            var locations = r.Locations.Take(MaxLocations).Select(l => Locate(l, areas, userArea)).ToList();
            if (applyArea && r.Remote != JobRemoteStatus.Eligible && !locations.Any(l => l.Match == "user_area"))
            {
                otherLocation++;
                continue;
            }

            var dto = Map(r, locations, info, today);
            if (needle.Length > 0
                && !dto.Title.Contains(needle, StringComparison.OrdinalIgnoreCase)
                && !dto.Organization.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                keyword++;
                continue;
            }
            if (query.Remote != JobRemoteFilters.All && dto.RemoteEligibility != query.Remote)
            {
                remoteFilter++;
                continue;
            }
            if (query.EligibleOnly && r.Remote != JobRemoteStatus.Eligible)
            {
                if (r.Remote == JobRemoteStatus.Unknown)
                {
                    unknownExcluded++;
                }
                else
                {
                    ineligibleExcluded++;
                }
                continue;
            }
            kept.Add(dto);
        }

        var shown = kept.Take(MaxObservations).ToList();
        var dates = shown.Where(o => o.PostedOn != null).Select(o => o.PostedOn!.Value).ToList();
        return new NormalizedJobs(
            shown,
            new JobCountsDto(
                raw.Count, kept.Count, shown.Count, dupIds, dupReposts, expired, unknownExcluded, ineligibleExcluded, otherLocation,
                keyword, remoteFilter, kept.Count - shown.Count),
            kept.Count > MaxObservations || providerTotal > raw.Count,
            dates.Count > 0 ? dates.Min() : null,
            dates.Count > 0 ? dates.Max() : null);
    }

    private static string RepostKey(RawJobObservation r) => string.Join('|',
        Norm(r.Organization), Norm(r.Title),
        string.Join(';', r.Locations.Take(MaxLocations).Select(l => $"{Norm(l.City)},{Norm(l.State)}").OrderBy(x => x, StringComparer.Ordinal)),
        r.PayMin, r.PayMax, r.PayUnit, r.ClosesOn?.ToString("O"), Norm(r.Series), Norm(r.Grade));

    private static string Norm(string? s) => (s ?? string.Empty).Trim().ToLowerInvariant();

    private static JobLocationDto Locate(RawJobLocation loc, IReadOnlyList<MarketArea> areas, MarketArea? userArea)
    {
        var city = Bound(loc.City, MaxShortLength);
        var state = Bound(loc.State, MaxShortLength);
        string? metroCode = null;
        string? stateCode = null;
        if (city != null && state != null && areas.Count > 0
            && MarketAreaResolver.Resolve($"{city}, {state}", areas).Local is { Type: "metro" } metro)
        {
            metroCode = metro.Code;
        }
        if (state != null && areas.Count > 0 && MarketAreaResolver.Resolve(state, areas).Local is { } st)
        {
            stateCode = areas.FirstOrDefault(a => a.Code == st.Code)?.State;
        }

        var matches = userArea != null
            && (userArea.Type == "metro" ? metroCode == userArea.Code : stateCode != null && stateCode.Equals(userArea.State, StringComparison.OrdinalIgnoreCase));
        return new JobLocationDto(city, state, metroCode, matches ? "user_area" : "other");
    }

    private static JobObservationDto Map(RawJobObservation r, List<JobLocationDto> locations, JobSourceInfo info, DateOnly today)
    {
        var expired = r.ClosesOn is { } c && c < today;
        var hasPay = !expired && r.PayUnit != null && (r.PayMin != null || r.PayMax != null)
            && !(r.PayMin != null && r.PayMax != null && r.PayMin > r.PayMax);
        var pay = hasPay
            ? new JobPayDto(r.PayMin, r.PayMax, r.PayUnit, r.PayBasis, "available")
            : new JobPayDto(null, null, null, null, "not_available");
        var remote = r.Remote switch
        {
            JobRemoteStatus.Eligible => JobRemoteFilters.Eligible,
            JobRemoteStatus.Ineligible => JobRemoteFilters.Ineligible,
            _ => JobRemoteFilters.Unknown
        };
        return new JobObservationDto(
            $"{info.SourceId}:{Bound(r.ProviderId, MaxShortLength)}",
            Bound(r.Title, MaxTitleLength) ?? string.Empty,
            Bound(r.Organization, MaxTitleLength) ?? string.Empty,
            locations, locations.Count > 1, pay, r.PostedOn, r.ClosesOn, expired, remote,
            Bound(r.RemoteNote, MaxNoteLength), Bound(r.Series, MaxShortLength), Bound(r.Grade, MaxShortLength),
            ValidLink(r.SourceUrl, info), info.SourceId);
    }

    /// <summary>The provider's own posting link: https and an allowlisted host, else no link at all.</summary>
    internal static string? ValidLink(string? url, JobSourceInfo info) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo)
            && info.AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase) && uri.AbsoluteUri.Length <= 500
            ? uri.AbsoluteUri
            : null;

    private static string? Bound(string? s, int max)
    {
        var t = s?.Trim();
        if (string.IsNullOrEmpty(t))
        {
            return null;
        }
        return t.Length <= max ? t : t[..max];
    }
}
