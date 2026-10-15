using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.Career;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.Career;

public sealed record CareerFunnelStepDto(string Step, int Users);
public sealed record CareerFunnelReturnsDto(int ActiveOnTwoOrMoreDays, int ActiveSevenOrMoreDaysAfterStart, int ActiveDaysP50, int ActiveDaysMax);
public sealed record CareerFunnelReportDto(
    DateTime From, DateTime To, DateTime MeasuredAt, int Started, IReadOnlyList<CareerFunnelStepDto> Steps, CareerFunnelReturnsDto Returns);

public interface ICareerFunnelReportService
{
    Task<CareerFunnelReportDto> BuildAsync(DateTime from, DateTime to, CancellationToken ct = default);
}

/// <summary>
/// Operator funnel over saved career state. The cohort is everyone whose career profile was created in
/// [from, to); each step counts how many of them have reached it by now. Derived from the server's own
/// records, so it covers every user regardless of cookie consent, and it returns counts only.
/// </summary>
/// <remarks>
/// Limits, by design: a step counts what still exists, so deleted career data drops out (deletion wins), and a
/// roadmap later dismissed no longer counts as accepted. "Active day" means a day with a saved change or a
/// draft/export; a visit that only reads is not recorded anywhere and so is not counted.
/// </remarks>
public sealed class CareerFunnelReportService : ICareerFunnelReportService
{
    /// <summary>Journey order (ADR 0021 nextAction order), then the optional photo handoff and purchase.</summary>
    public static readonly IReadOnlyList<string> StepOrder =
    [
        "profile_saved", "goal_saved", "occupation_confirmed", "market_brief", "pay_analysis", "roadmap_built",
        "roadmap_accepted", "resume_drafted", "material_exported", "photo_selected", "photo_package_bought"
    ];

    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public CareerFunnelReportService(ApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<CareerFunnelReportDto> BuildAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var cohortRows = await _db.CareerProfiles.AsNoTracking()
            .Where(p => p.CreatedAt >= from && p.CreatedAt < to)
            .Select(p => new { p.OwnerId, p.CreatedAt })
            .ToListAsync(ct);
        var started = cohortRows.GroupBy(p => p.OwnerId).ToDictionary(g => g.Key, g => g.Min(p => p.CreatedAt));
        var cohort = _db.CareerProfiles.Where(p => p.CreatedAt >= from && p.CreatedAt < to).Select(p => p.OwnerId);

        Task<int> Count(IQueryable<string> owners) => owners.Where(o => cohort.Contains(o)).Distinct().CountAsync(ct);

        var counts = new Dictionary<string, int>
        {
            ["profile_saved"] = started.Count,
            ["goal_saved"] = await Count(_db.CareerGoals.Select(g => g.OwnerId)),
            ["occupation_confirmed"] = await Count(_db.CareerGoalVersions
                .Where(v => v.OccupationCode != null && v.OccupationCode != "").Select(v => v.OwnerId)),
            ["market_brief"] = await Count(_db.CareerMarketBriefs.Select(b => b.OwnerId)),
            ["pay_analysis"] = await Count(_db.CareerPayAnalyses.Select(a => a.OwnerId)),
            ["roadmap_built"] = await Count(_db.CareerRoadmaps.Select(r => r.OwnerId)),
            ["roadmap_accepted"] = await Count(_db.CareerRoadmaps
                .Where(r => r.Status == CareerRoadmapStatuses.Accepted).Select(r => r.OwnerId)),
            ["resume_drafted"] = await Count(_db.CareerMaterials
                .Where(m => m.Kind == CareerMaterialKinds.Resume).Select(m => m.OwnerId)),
            // Export files expire and are deleted; the usage ledger keeps one row per export.
            ["material_exported"] = await Count(_db.CareerUsageEvents
                .Where(e => e.Action == CareerUsageActions.Export && e.Outcome == CareerUsageOutcomes.Ok).Select(e => e.OwnerId)),
            ["photo_selected"] = await Count(_db.CareerPhotoSelections.Select(s => s.OwnerId)),
        };

        var purchases = await _db.UserPackageEntitlements.AsNoTracking()
            .Where(e => e.SourcePaymentTransactionId != null
                && e.Status != PackageEntitlementStatus.Refunded && e.Status != PackageEntitlementStatus.Revoked
                && cohort.Contains(e.UserId))
            .Select(e => new { e.UserId, e.CreatedAt })
            .ToListAsync(ct);
        counts["photo_package_bought"] = purchases
            .Where(p => started.TryGetValue(p.UserId, out var start) && p.CreatedAt >= start)
            .Select(p => p.UserId).Distinct().Count();

        return new CareerFunnelReportDto(
            from, to, _clock.GetUtcNow().UtcDateTime, started.Count,
            StepOrder.Select(s => new CareerFunnelStepDto(s, counts[s])).ToList(),
            await ReturnsAsync(cohort, started, ct));
    }

    /// <summary>Member-init (not a constructor) so EF can still filter on it after the projection.</summary>
    private sealed class OwnerAt
    {
        public string Owner { get; init; } = "";
        public DateTime At { get; init; }
    }

    private async Task<CareerFunnelReturnsDto> ReturnsAsync(
        IQueryable<string> cohort, IReadOnlyDictionary<string, DateTime> started, CancellationToken ct)
    {
        // Every table that records a saved change or a draft/export, projected to (owner, when) only.
        IQueryable<OwnerAt>[] sources =
        [
            _db.CareerProfileVersions.Select(x => new OwnerAt { Owner = x.OwnerId, At = x.CreatedAt }),
            _db.CareerGoalVersions.Select(x => new OwnerAt { Owner = x.OwnerId, At = x.CreatedAt }),
            _db.CareerAgentRuns.Select(x => new OwnerAt { Owner = x.OwnerId, At = x.CreatedAt }),
            _db.CareerUsageEvents.Select(x => new OwnerAt { Owner = x.OwnerId, At = x.CreatedAt }),
            _db.CareerMarketBriefs.Select(x => new OwnerAt { Owner = x.OwnerId, At = x.CreatedAt }),
            _db.CareerPayAnalyses.Select(x => new OwnerAt { Owner = x.OwnerId, At = x.CreatedAt }),
            _db.CareerRoadmaps.Select(x => new OwnerAt { Owner = x.OwnerId, At = x.CreatedAt }),
            _db.CareerRoadmapTaskProgress.Select(x => new OwnerAt { Owner = x.OwnerId, At = x.UpdatedAt }),
            _db.CareerMaterialVersions.Select(x => new OwnerAt { Owner = x.OwnerId, At = x.CreatedAt }),
            _db.CareerPhotoSelections.Select(x => new OwnerAt { Owner = x.OwnerId, At = x.UpdatedAt }),
        ];
        var activity = new List<OwnerAt>();
        foreach (var source in sources)
        {
            activity.AddRange(await source.AsNoTracking().Where(x => cohort.Contains(x.Owner)).ToListAsync(ct));
        }

        var byOwner = activity.ToLookup(a => a.Owner, a => a.At);
        var perOwner = started.Select(kv => (Start: kv.Value, Times: byOwner[kv.Key].Append(kv.Value).ToList())).ToList();
        var days = perOwner.Select(o => o.Times.Select(t => t.Date).Distinct().Count()).Order().ToList();

        return new CareerFunnelReturnsDto(
            days.Count(d => d >= 2),
            perOwner.Count(o => o.Times.Any(t => t >= o.Start.AddDays(7))),
            CareerUsageReportService.Percentile(days, 50),
            days.LastOrDefault());
    }
}
