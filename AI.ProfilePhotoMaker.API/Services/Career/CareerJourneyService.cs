using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.Career;

public sealed record JourneyProfileDto(int Version, bool Confirmed);
public sealed record JourneyGoalDto(int Version, string? OccupationCode, string? OccupationTitle, string? Location);
public sealed record JourneyNextActionDto(string Key, string Route);
public sealed record JourneyLatestResultDto(string Kind, Guid Id, int? Version, DateTime CreatedAt);
public sealed record JourneyActiveRunDto(Guid Id, string Task, string Status, DateTime StartedAt);
public sealed record JourneyStaleDto(string Kind, Guid Id, IReadOnlyList<string> Reasons);
public sealed record CareerJourneyDto(
    JourneyProfileDto? Profile, JourneyGoalDto? Goal, JourneyNextActionDto NextAction,
    JourneyLatestResultDto? LatestResult, IReadOnlyList<JourneyActiveRunDto> ActiveRuns, IReadOnlyList<JourneyStaleDto> Stale);

public interface ICareerJourneyService
{
    Task<CareerOutcome<CareerJourneyDto>> GetAsync(string ownerId, CancellationToken ct = default);
}

/// <summary>
/// The single server-derived career context (ADR 0021). Reads saved state only and never writes;
/// nextAction follows a fixed order and is never influenced by request input or stored free text.
/// </summary>
public sealed class CareerJourneyService : ICareerJourneyService
{
    private const int MaxPerKind = 200;
    private static readonly TimeSpan FailedWindow = TimeSpan.FromHours(24);

    private readonly ApplicationDbContext _db;
    private readonly IMarketReference _reference;
    private readonly TimeProvider _clock;

    public CareerJourneyService(ApplicationDbContext db, IMarketReference reference, TimeProvider clock)
    {
        _db = db;
        _reference = reference;
        _clock = clock;
    }

    public async Task<CareerOutcome<CareerJourneyDto>> GetAsync(string ownerId, CancellationToken ct = default)
    {
        var profileVersion = await _db.CareerProfiles.AsNoTracking().Where(p => p.OwnerId == ownerId)
            .Select(p => (int?)p.ActiveVersionNumber).FirstOrDefaultAsync(ct);
        var goalVersion = await _db.CareerGoals.AsNoTracking().Where(g => g.OwnerId == ownerId)
            .Select(g => (int?)g.ActiveVersionNumber).FirstOrDefaultAsync(ct);
        var goal = goalVersion == null ? null : await _db.CareerGoalVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.OwnerId == ownerId && v.VersionNumber == goalVersion, ct);

        var stale = new List<JourneyStaleDto>();
        var results = new List<JourneyLatestResultDto>();

        // Projections only (no payload blobs), newest first, bounded per kind.
        var briefs = await _db.CareerMarketBriefs.AsNoTracking().Where(b => b.OwnerId == ownerId).OrderByDescending(b => b.CreatedAt).Take(MaxPerKind)
            .Select(b => new { b.Id, b.CreatedAt, b.PinnedProfileVersion, b.PinnedGoalVersion, b.OccupationCode }).ToListAsync(ct);
        var analyses = await _db.CareerPayAnalyses.AsNoTracking().Where(a => a.OwnerId == ownerId).OrderByDescending(a => a.CreatedAt).Take(MaxPerKind)
            .Select(a => new { a.Id, a.CreatedAt, a.PinnedProfileVersion, a.PinnedGoalVersion, a.OccupationCode, a.AreaResolution, a.AreaCode }).ToListAsync(ct);
        var roadmaps = await _db.CareerRoadmaps.AsNoTracking().Where(r => r.OwnerId == ownerId).OrderByDescending(r => r.CreatedAt).Take(MaxPerKind)
            .Select(r => new { r.Id, r.CreatedAt, r.Version, r.Status, r.PinnedProfileVersion, r.PinnedGoalVersion }).ToListAsync(ct);
        var materials = await _db.CareerMaterials.AsNoTracking().Where(m => m.OwnerId == ownerId).OrderByDescending(m => m.UpdatedAt).Take(MaxPerKind)
            .Select(m => new { m.Id, m.Kind, m.CreatedAt, m.CurrentVersion, m.PinnedProfileVersion, m.PinnedGoalVersion }).ToListAsync(ct);
        var now = _clock.GetUtcNow().UtcDateTime;
        var resumeIds = materials.Where(m => m.Kind == CareerMaterialKinds.Resume).Select(m => m.Id).ToList();
        var exportRows = await _db.CareerExports.AsNoTracking()
            .Where(e => e.OwnerId == ownerId && resumeIds.Contains(e.MaterialId) && e.ExpiresAt > now)
            .Select(e => new { e.MaterialId, e.Version }).Distinct().ToListAsync(ct);
        var exported = exportRows.Select(e => (e.MaterialId, e.Version)).ToHashSet();

        var briefReasons = briefs.ToDictionary(b => b.Id, b => Reasons(b.PinnedProfileVersion, b.PinnedGoalVersion, b.OccupationCode, profileVersion, goalVersion, goal));
        var location = MarketBriefBuilder.ResolveLocation(goal?.TargetLocation, _reference);
        var payReasons = analyses.ToDictionary(a => a.Id, a =>
        {
            var r = Reasons(a.PinnedProfileVersion, a.PinnedGoalVersion, a.OccupationCode, profileVersion, goalVersion, goal);
            if (location.Resolution != a.AreaResolution || location.Local?.Code != a.AreaCode) r.Add("area_changed");
            return r;
        });
        var roadmapReasons = roadmaps.ToDictionary(r => r.Id, r => Reasons(r.PinnedProfileVersion, r.PinnedGoalVersion, null, profileVersion, goalVersion, goal));
        var materialReasons = materials.ToDictionary(m => m.Id, m =>
        {
            var r = new List<string>();
            if (profileVersion != m.PinnedProfileVersion) r.Add("profile_changed");
            if ((goalVersion ?? 0) != m.PinnedGoalVersion) r.Add("goal_changed");
            return r;
        });

        foreach (var b in briefs) { Add(stale, "market_brief", b.Id, briefReasons[b.Id]); }
        foreach (var a in analyses) { Add(stale, "pay_analysis", a.Id, payReasons[a.Id]); }
        foreach (var r in roadmaps) { Add(stale, "roadmap", r.Id, roadmapReasons[r.Id]); }
        foreach (var m in materials) { Add(stale, m.Kind, m.Id, materialReasons[m.Id]); }

        if (briefs.FirstOrDefault() is { } lb) results.Add(new("market_brief", lb.Id, null, Utc(lb.CreatedAt)));
        if (analyses.FirstOrDefault() is { } la) results.Add(new("pay_analysis", la.Id, null, Utc(la.CreatedAt)));
        if (roadmaps.FirstOrDefault() is { } lr) results.Add(new("roadmap", lr.Id, lr.Version, Utc(lr.CreatedAt)));
        if (materials.OrderByDescending(m => m.CreatedAt).FirstOrDefault() is { } lm) results.Add(new(lm.Kind, lm.Id, lm.CurrentVersion, Utc(lm.CreatedAt)));

        var openProposal = await _db.CareerProfileProposals.AsNoTracking()
            .AnyAsync(p => p.OwnerId == ownerId && p.Status == ProposalStatus.Pending, ct);

        var next = NextAction(profileVersion, openProposal, goal,
            briefs.Any(b => briefReasons[b.Id].Count == 0),
            analyses.Any(a => payReasons[a.Id].Count == 0),
            roadmaps.Any(r => r.Status != CareerRoadmapStatuses.Dismissed && roadmapReasons[r.Id].Count == 0),
            roadmaps.Any(r => r.Status == CareerRoadmapStatuses.Accepted && roadmapReasons[r.Id].Count == 0),
            materials.Any(m => m.Kind == CareerMaterialKinds.Resume && materialReasons[m.Id].Count == 0),
            materials.Any(m => m.Kind == CareerMaterialKinds.Resume && materialReasons[m.Id].Count == 0 && !exported.Contains((m.Id, m.CurrentVersion))));

        var cutoff = _clock.GetUtcNow().UtcDateTime - FailedWindow;
        var runs = await _db.CareerAgentRuns.AsNoTracking()
            .Where(r => r.OwnerId == ownerId && (r.Status == CareerRunStatus.Queued || r.Status == CareerRunStatus.Working
                || (r.Status == CareerRunStatus.Failed && r.UpdatedAt >= cutoff)))
            .OrderByDescending(r => r.CreatedAt).Take(20).ToListAsync(ct);

        return CareerOutcome<CareerJourneyDto>.Ok(new CareerJourneyDto(
            profileVersion == null ? null : new JourneyProfileDto(profileVersion.Value, true),
            goal == null ? null : new JourneyGoalDto(goal.VersionNumber, goal.OccupationCode, goal.OccupationTitle, goal.TargetLocation),
            next,
            results.OrderByDescending(r => r.CreatedAt).FirstOrDefault(),
            runs.Select(r => new JourneyActiveRunDto(r.Id, r.Task, r.Status == CareerRunStatus.Failed ? "failed" : r.Status == CareerRunStatus.Queued ? "queued" : "running",
                Utc(r.StartedAt ?? r.CreatedAt))).ToList(),
            stale));
    }

    private static JourneyNextActionDto NextAction(int? profile, bool openProposal, CareerGoalVersion? goal,
        bool brief, bool pay, bool roadmap, bool accepted, bool resume, bool resumeNotExported)
    {
        const string root = "/app/career";
        if (profile == null) return new("create_profile", $"{root}/setup");
        if (openProposal) return new("confirm_profile", $"{root}/profile");
        if (goal == null) return new("set_goal", $"{root}/setup");
        if (string.IsNullOrEmpty(goal.OccupationCode)) return new("confirm_occupation", $"{root}/occupation");
        if (!brief) return new("build_brief", $"{root}/market");
        if (!pay) return new("analyze_pay", $"{root}/pay");
        if (!roadmap) return new("build_roadmap", $"{root}/roadmap");
        if (!accepted) return new("accept_roadmap", $"{root}/roadmap");
        if (!resume) return new("draft_resume", $"{root}/resume");
        if (resumeNotExported) return new("export_material", $"{root}/materials");
        return new("none", root);
    }

    private static List<string> Reasons(int pinnedProfile, int pinnedGoal, string? pinnedOccupation, int? profile, int? goalVersion, CareerGoalVersion? goal)
    {
        var reasons = new List<string>();
        if (goalVersion != pinnedGoal) reasons.Add("goal_changed");
        if (profile != pinnedProfile) reasons.Add("profile_changed");
        if (pinnedOccupation != null && goal?.OccupationCode != pinnedOccupation) reasons.Add("occupation_changed");
        return reasons;
    }

    private static void Add(List<JourneyStaleDto> list, string kind, Guid id, List<string> reasons)
    {
        if (reasons.Count > 0) list.Add(new(kind, id, reasons));
    }

    private static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);
}
