using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.Career;

public interface ICareerMarketComparisonService
{
    CareerOutcome<MarketMetricListDto> GetMetrics();

    Task<CareerOutcome<MarketComparisonDto>> CompareAsync(
        string ownerId, string? metric, string? level, string? areas, string? q, CancellationToken ct = default);

    Task<CareerOutcome<CareerGoalDto>> SavePreferenceAsync(
        string ownerId, MarketPreferenceRequest request, VersionPrecondition precondition, CancellationToken ct = default);
}

/// <summary>
/// One normalized comparison of an occupation across states or metros, from the pinned BLS snapshot
/// (ADR 0014). No model, no storage: values are read from the snapshot and ranked here, so the heatmap
/// and the table render the same array. Saving a preference writes a new goal version.
/// </summary>
public sealed class MarketComparisonService : ICareerMarketComparisonService
{
    public const int SelectionLimit = 3;
    public const int MaxAreas = 500;

    public const string MedianWage = "median_wage";
    public const string Employment = "employment";
    public const string LocationQuotient = "location_quotient";
    public const string ShareOfNationalEmployment = "share_of_national_employment";
    public const string ProjectedChange = "projected_change";

    private static readonly string[] AllLevels = { "national", "state", "metro" };
    private static readonly string[] LocalLevels = { "state", "metro" };

    private static readonly MarketMetricDto[] Metrics =
    {
        new(MedianWage, "Median annual wage", "usd_per_year", "BLS OEWS median annual wage", true, null, AllLevels),
        new(Employment, "Employment", "jobs", "Estimated wage and salary employment", true, null, AllLevels),
        new(LocationQuotient, "Employment concentration", "ratio",
            "Occupation's share of area employment relative to the nation", true, null, LocalLevels),
        new(ShareOfNationalEmployment, "Share of national employment", "percent",
            "This area's share of the occupation's national employment", true, null, LocalLevels),
        new(ProjectedChange, "Projected employment change", "percent", "BLS Employment Projections", false,
            "national_only_source", new[] { "national" })
    };

    private readonly ApplicationDbContext _db;
    private readonly IMarketReference _reference;
    private readonly TimeProvider _clock;

    public MarketComparisonService(ApplicationDbContext db, IMarketReference reference, TimeProvider clock)
    {
        _db = db;
        _reference = reference;
        _clock = clock;
    }

    public CareerOutcome<MarketMetricListDto> GetMetrics() => CareerOutcome<MarketMetricListDto>.Ok(new MarketMetricListDto(Metrics));

    public async Task<CareerOutcome<MarketComparisonDto>> CompareAsync(
        string ownerId, string? metric, string? level, string? areas, string? q, CancellationToken ct = default)
    {
        var invalid = Validate<MarketComparisonDto>(metric, level);
        if (invalid != null)
        {
            return invalid;
        }

        var goal = await _db.CareerGoals.AsNoTracking().FirstOrDefaultAsync(g => g.OwnerId == ownerId, ct);
        var version = goal == null
            ? null
            : await _db.CareerGoalVersions.AsNoTracking()
                .FirstOrDefaultAsync(v => v.OwnerId == ownerId && v.CareerGoalId == goal.Id && v.VersionNumber == goal.ActiveVersionNumber, ct);
        if (version?.OccupationCode == null)
        {
            return CareerOutcome<MarketComparisonDto>.AlreadyExists(
                CareerOccupationErrorCodes.OccupationRequired, "Confirm your target occupation before comparing places.");
        }
        return Compare(version.OccupationCode, version.OccupationTitle, metric, level, areas, q);
    }

    /// <summary>The comparison for an occupation code; the controller path resolves the code from the goal.</summary>
    public CareerOutcome<MarketComparisonDto> Compare(
        string? occupationCode, string? occupationTitle, string? metric, string? level, string? areas, string? q, int maxAreas = MaxAreas)
    {
        var invalid = Validate<MarketComparisonDto>(metric, level);
        if (invalid != null)
        {
            return invalid;
        }
        if (string.IsNullOrWhiteSpace(occupationCode))
        {
            return CareerOutcome<MarketComparisonDto>.AlreadyExists(
                CareerOccupationErrorCodes.OccupationRequired, "Confirm your target occupation before comparing places.");
        }

        var oews = _reference.Oews;
        var crosswalk = oews?.Crosswalk(occupationCode);
        if (oews == null || crosswalk == null)
        {
            return CareerOutcome<MarketComparisonDto>.ReferenceUnavailable();
        }

        var national = oews.Areas.First(a => a.Type == "national");
        var nationalRow = oews.Wage(national.Code, crosswalk.Code);

        var places = oews.Areas.Where(a => a.Type == level).ToList();
        var values = places.Select(a =>
        {
            var row = oews.Wage(a.Code, crosswalk.Code);
            var (value, status) = Read(metric!, row, nationalRow);
            return (Area: a, Value: value, Status: status, Sort: ReadUnrounded(metric!, row, nationalRow), Share: Read(ShareOfNationalEmployment, row, nationalRow) is { Status: MarketValueStatus.Available } s ? s.Number : null);
        }).ToList();

        // Rank only what BLS published, best (highest) first, on the unrounded figure (rounding is for display only); ties fall to the area code so the order is stable.
        var ranked = values.Where(v => v.Status == MarketValueStatus.Available)
            .OrderByDescending(v => v.Sort).ThenBy(v => v.Area.Code, StringComparer.Ordinal).ToList();
        var ranks = ranked.Select((v, i) => (v.Area.Code, Rank: i + 1)).ToDictionary(r => r.Code, r => r.Rank, StringComparer.Ordinal);

        var selected = (areas ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal).Take(SelectionLimit).ToHashSet(StringComparer.Ordinal);
        var needle = q?.Trim() ?? string.Empty;

        var rows = values
            .Where(v => needle.Length == 0 || v.Area.Title.Contains(needle, StringComparison.OrdinalIgnoreCase))
            .OrderBy(v => ranks.TryGetValue(v.Area.Code, out var r) ? r : int.MaxValue)
            .ThenBy(v => v.Area.Title, StringComparer.OrdinalIgnoreCase).ThenBy(v => v.Area.Code, StringComparer.Ordinal)
            .ToList();
        var truncated = rows.Count > maxAreas;

        var dtos = rows.Take(maxAreas).Select(v => new MarketAreaValueDto(
            v.Area.Code, v.Area.Title, v.Area.Type, v.Value, MarketFigureStatuses.Of(v.Status),
            ranks.TryGetValue(v.Area.Code, out var rank) ? rank : null,
            ranks.ContainsKey(v.Area.Code) ? ranks.Count : null,
            selected.Contains(v.Area.Code), v.Share)).ToList();

        var def = Metrics.Single(m => m.Key == metric);
        var (natValue, natStatus) = Read(metric!, nationalRow, nationalRow, national: true);
        return CareerOutcome<MarketComparisonDto>.Ok(new MarketComparisonDto(
            new MarketComparisonOccupationDto(
                occupationCode, occupationTitle ?? oews.OccupationTitle(crosswalk.Code) ?? crosswalk.Code, crosswalk.Code, crosswalk.Match),
            new MarketComparisonMetricDto(def.Key, def.Label, def.Unit, def.Measure, def.Supported, def.Reason),
            level!,
            new MarketComparisonNationalDto(national.Code, national.Title, natValue, MarketFigureStatuses.Of(natStatus)),
            new MarketComparisonReferenceDto(
                oews.Source.ReferencePeriod, oews.Source.PublishedOn, oews.Source.Coverage, oews.Source.DefinitionsUrl, oews.Source.Citation),
            dtos, SelectionLimit, truncated));
    }

    private static CareerOutcome<T>? Validate<T>(string? metric, string? level)
    {
        var errors = new Dictionary<string, string>();
        var def = Metrics.FirstOrDefault(m => m.Key == metric);
        if (def == null)
        {
            errors["metric"] = "Choose one of the listed metrics.";
        }
        if (level is not ("state" or "metro"))
        {
            errors["level"] = "Choose state or metro.";
        }
        if (errors.Count > 0)
        {
            return CareerOutcome<T>.Invalid(errors);
        }
        if (!def!.Supported)
        {
            return CareerOutcome<T>.AlreadyExists(MarketComparisonErrorCodes.MetricUnsupported, def.Reason!);
        }
        if (!def.GeographyLevels.Contains(level!))
        {
            errors["level"] = "That metric is not published at this level.";
            return CareerOutcome<T>.Invalid(errors);
        }
        return null;
    }

    /// <summary>The figure used to order areas: the same as <see cref="Read"/> but the share is not rounded.</summary>
    private static double? ReadUnrounded(string metric, MarketWageRow? row, MarketWageRow? nationalRow)
    {
        if (metric == ShareOfNationalEmployment && row?.Employment is { Status: MarketValueStatus.Available, Number: { } emp }
            && nationalRow?.Employment is { Status: MarketValueStatus.Available, Number: > 0 } n)
        {
            return emp / n.Number!.Value * 100;
        }
        return Read(metric, row, nationalRow).Number;
    }

    /// <summary>The metric's value in one area. Suppressed and missing cells carry no number; a top-coded one carries its ceiling.</summary>
    private static MarketValue Read(string metric, MarketWageRow? row, MarketWageRow? nationalRow, bool national = false)
    {
        if (row == null)
        {
            return MarketValue.NotPublished;
        }
        switch (metric)
        {
            case MedianWage:
                return row.MedianAnnual;
            case Employment:
                return row.Employment;
            case LocationQuotient:
                return national ? MarketValue.NotPublished : row.LocationQuotient;
            case ShareOfNationalEmployment:
                if (national)
                {
                    return MarketValue.NotPublished;
                }
                if (row.Employment.Status != MarketValueStatus.Available)
                {
                    return row.Employment.Status == MarketValueStatus.NotPublished ? MarketValue.NotPublished : MarketValue.NotAvailable;
                }
                return nationalRow?.Employment is { Status: MarketValueStatus.Available, Number: > 0 } n
                    ? new MarketValue(Math.Round(row.Employment.Number!.Value / n.Number!.Value * 100, 1, MidpointRounding.AwayFromZero), MarketValueStatus.Available)
                    : MarketValue.NotAvailable;
            default:
                return MarketValue.NotPublished;
        }
    }

    // ---- Preference --------------------------------------------------------------

    public async Task<CareerOutcome<CareerGoalDto>> SavePreferenceAsync(
        string ownerId, MarketPreferenceRequest request, VersionPrecondition precondition, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string>();
        var code = request.AreaCode?.Trim();
        if (string.IsNullOrEmpty(code) || code.Length > 10)
        {
            errors["areaCode"] = "Choose a place.";
        }
        if (request.Level is not ("state" or "metro"))
        {
            errors["level"] = "Choose state or metro.";
        }
        if (request.Confirmed != true)
        {
            errors["confirmed"] = "Confirm the location before it is saved.";
        }
        if (errors.Count > 0)
        {
            return CareerOutcome<CareerGoalDto>.Invalid(errors);
        }

        var goal = await _db.CareerGoals.FirstOrDefaultAsync(g => g.OwnerId == ownerId, ct);
        if (goal == null)
        {
            return CareerOutcome<CareerGoalDto>.AlreadyExists(CareerOccupationErrorCodes.GoalRequired, "Save a career goal first.");
        }

        var oews = _reference.Oews;
        if (oews == null)
        {
            return CareerOutcome<CareerGoalDto>.ReferenceUnavailable();
        }
        var area = oews.Area(code!);
        if (area == null || area.Type != request.Level)
        {
            return CareerOutcome<CareerGoalDto>.NotFound(MarketComparisonErrorCodes.AreaNotFound, "That place is not in the market data.");
        }

        if (!precondition.IsPresent)
        {
            return CareerOutcome<CareerGoalDto>.PreconditionRequired();
        }
        if (precondition.ExpectedVersion != goal.ActiveVersionNumber)
        {
            return CareerOutcome<CareerGoalDto>.Conflict(goal.ActiveVersionNumber);
        }

        var active = await _db.CareerGoalVersions.AsNoTracking().FirstAsync(
            v => v.OwnerId == ownerId && v.CareerGoalId == goal.Id && v.VersionNumber == goal.ActiveVersionNumber, ct);
        var profileVersion = await _db.CareerProfiles.AsNoTracking()
            .Where(p => p.OwnerId == ownerId).Select(p => (int?)p.ActiveVersionNumber).FirstOrDefaultAsync(ct);
        var now = _clock.GetUtcNow().UtcDateTime;
        var next = new CareerGoalVersion
        {
            Id = Guid.NewGuid(),
            CareerGoalId = goal.Id,
            OwnerId = ownerId,
            VersionNumber = goal.ActiveVersionNumber + 1,
            TargetRole = active.TargetRole,
            TargetLocation = area.Title.Length > 120 ? area.Title[..120] : area.Title,
            WorkArrangement = active.WorkArrangement,
            DesiredPayMin = active.DesiredPayMin,
            DesiredPayMax = active.DesiredPayMax,
            WeeklyEffortHours = active.WeeklyEffortHours,
            BasedOnProfileVersion = profileVersion,
            Source = CareerFactSource.Manual,
            ConfirmedAt = now,
            CreatedAt = now,
            PreferredAreaCode = area.Code,
            PreferredAreaTitle = area.Title,
            PreferredAreaLevel = area.Type
        };
        CareerProfileService.CarryOccupation(active, next);
        next.PreferredAreaCode = area.Code;
        next.PreferredAreaTitle = area.Title;
        next.PreferredAreaLevel = area.Type;

        goal.ActiveVersionNumber = next.VersionNumber;
        goal.UpdatedAt = now;
        _db.CareerGoalVersions.Add(next);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (CareerProfileService.IsLostRace(ex))
        {
            _db.ChangeTracker.Clear();
            var current = await _db.CareerGoals.AsNoTracking().FirstOrDefaultAsync(g => g.Id == goal.Id && g.OwnerId == ownerId, ct);
            return CareerOutcome<CareerGoalDto>.Conflict(current?.ActiveVersionNumber ?? 0);
        }
        return CareerOutcome<CareerGoalDto>.Ok(CareerProfileService.ToDto(goal, next, profileVersion));
    }
}
