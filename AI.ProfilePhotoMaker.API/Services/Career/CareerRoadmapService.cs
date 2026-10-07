using System.Text.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.Career;

public interface ICareerRoadmapService
{
    Task<CareerOutcome<CareerRoadmapListDto>> ListAsync(string ownerId, CancellationToken ct = default);
    Task<CareerOutcome<CareerRoadmapDto>> GetAsync(string ownerId, Guid id, CancellationToken ct = default);
    Task<CareerOutcome<CareerRoadmapDto>> AcceptAsync(string ownerId, Guid id, AcceptRoadmapRequest request, VersionPrecondition precondition, CancellationToken ct = default);
    Task<CareerOutcome<CareerRoadmapDto>> DismissAsync(string ownerId, Guid id, CancellationToken ct = default);
    Task<CareerOutcome<CareerRoadmapDto>> UpdateTaskAsync(string ownerId, Guid id, string taskId, UpdateRoadmapTaskRequest request, CancellationToken ct = default);
}

/// <summary>
/// Reads, accepts, dismisses and edits roadmaps (ADR 0016). Accepting records the user's choice and never
/// touches the goal; editing a task's effort writes a new roadmap version.
/// </summary>
public sealed class CareerRoadmapService : ICareerRoadmapService
{
    public const int MaxListed = 20;
    private const int MaxCommitAttempts = 3;

    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public CareerRoadmapService(ApplicationDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    private sealed record Current(int? ProfileVersion, int? GoalVersion, string? OccupationCode);

    private static CareerOutcome<T> Missing<T>() => CareerOutcome<T>.NotFound(RoadmapErrorCodes.NotFound, "That roadmap was not found.");

    private static CareerOutcome<T> NotProposed<T>(string message) => CareerOutcome<T>.AlreadyExists(RoadmapErrorCodes.NotProposed, message);

    public async Task<CareerOutcome<CareerRoadmapListDto>> ListAsync(string ownerId, CancellationToken ct = default)
    {
        var rows = await _db.CareerRoadmaps.AsNoTracking().Where(r => r.OwnerId == ownerId)
            .OrderByDescending(r => r.Version).ThenByDescending(r => r.CreatedAt).Take(MaxListed).ToListAsync(ct);
        var current = await CurrentAsync(ownerId, ct);
        var items = new List<CareerRoadmapSummaryDto>();
        foreach (var r in rows)
        {
            var reasons = await StaleReasonsAsync(r, current, ct);
            items.Add(new CareerRoadmapSummaryDto(r.Id, r.Version, r.Status, reasons.Count > 0, ReadOptions(r).Count, Utc(r.CreatedAt)));
        }
        return CareerOutcome<CareerRoadmapListDto>.Ok(new CareerRoadmapListDto(items));
    }

    public async Task<CareerOutcome<CareerRoadmapDto>> GetAsync(string ownerId, Guid id, CancellationToken ct = default)
    {
        var row = await _db.CareerRoadmaps.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id && r.OwnerId == ownerId, ct);
        return row == null ? Missing<CareerRoadmapDto>() : CareerOutcome<CareerRoadmapDto>.Ok(await ToDtoAsync(row, ct));
    }

    public async Task<CareerOutcome<CareerRoadmapDto>> AcceptAsync(
        string ownerId, Guid id, AcceptRoadmapRequest request, VersionPrecondition precondition, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < MaxCommitAttempts; attempt++)
        {
            // Ownership first, so another owner's roadmap reveals nothing.
            var row = await _db.CareerRoadmaps.FirstOrDefaultAsync(r => r.Id == id && r.OwnerId == ownerId, ct);
            if (row == null)
            {
                return Missing<CareerRoadmapDto>();
            }

            var key = request.OptionKey?.Trim();
            var option = ReadOptions(row).FirstOrDefault(o => o.Key == key);
            if (option == null)
            {
                return CareerOutcome<CareerRoadmapDto>.Invalid(new Dictionary<string, string>
                {
                    ["optionKey"] = "Choose one of the options on this roadmap."
                });
            }
            if (row.Status != CareerRoadmapStatuses.Proposed)
            {
                return NotProposed<CareerRoadmapDto>("This roadmap was already decided.");
            }

            var goalVersion = await _db.CareerGoals.AsNoTracking()
                .Where(g => g.OwnerId == ownerId).Select(g => (int?)g.ActiveVersionNumber).FirstOrDefaultAsync(ct);
            if (goalVersion == null)
            {
                return CareerOutcome<CareerRoadmapDto>.AlreadyExists(
                    CareerOccupationErrorCodes.GoalRequired, "Set a career goal before accepting a roadmap.");
            }
            if (!precondition.IsPresent)
            {
                return CareerOutcome<CareerRoadmapDto>.PreconditionRequired();
            }
            if (precondition.ExpectedVersion != goalVersion)
            {
                return CareerOutcome<CareerRoadmapDto>.Conflict(goalVersion.Value);
            }

            row.Status = CareerRoadmapStatuses.Accepted;
            row.SelectedOption = option.Key;
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (CareerProfileService.IsLostRace(ex))
            {
                _db.ChangeTracker.Clear();
                continue;
            }

            // The goal is untouched: choosing another path points the user at the occupation page instead.
            var dto = await ToDtoAsync(row, ct);
            return CareerOutcome<CareerRoadmapDto>.Ok(dto with
            {
                GoalUnchanged = true,
                Note = option.Key == RoadmapKeys.ClosestFit
                    ? null
                    : $"Your goal is unchanged. To aim for {option.Title}, review it on the occupation page and confirm it there.",
                OccupationLink = option.Key == RoadmapKeys.ClosestFit ? null : MarketBriefBuilder.OccupationRoute
            });
        }
        return CareerOutcome<CareerRoadmapDto>.Busy("CareerRunBusy", "Too many requests at once. Try again shortly.", 1);
    }

    public async Task<CareerOutcome<CareerRoadmapDto>> DismissAsync(string ownerId, Guid id, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < MaxCommitAttempts; attempt++)
        {
            var row = await _db.CareerRoadmaps.FirstOrDefaultAsync(r => r.Id == id && r.OwnerId == ownerId, ct);
            if (row == null)
            {
                return Missing<CareerRoadmapDto>();
            }
            if (row.Status == CareerRoadmapStatuses.Accepted)
            {
                return NotProposed<CareerRoadmapDto>("An accepted roadmap cannot be dismissed.");
            }
            if (row.Status == CareerRoadmapStatuses.Proposed)
            {
                row.Status = CareerRoadmapStatuses.Dismissed;
                try
                {
                    await _db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException ex) when (CareerProfileService.IsLostRace(ex))
                {
                    _db.ChangeTracker.Clear();
                    continue;
                }
            }
            // Dismissing a dismissed roadmap is a no-op.
            return CareerOutcome<CareerRoadmapDto>.Ok(await ToDtoAsync(row, ct));
        }
        return CareerOutcome<CareerRoadmapDto>.Busy("CareerRunBusy", "Too many requests at once. Try again shortly.", 1);
    }

    public async Task<CareerOutcome<CareerRoadmapDto>> UpdateTaskAsync(
        string ownerId, Guid id, string taskId, UpdateRoadmapTaskRequest request, CancellationToken ct = default)
    {
        var row = await _db.CareerRoadmaps.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id && r.OwnerId == ownerId, ct);
        if (row == null)
        {
            return Missing<CareerRoadmapDto>();
        }
        if (request.EffortHours is not { } effort || double.IsNaN(effort) || effort < RoadmapBuilder.MinEffort || effort > RoadmapBuilder.MaxEffort)
        {
            return CareerOutcome<CareerRoadmapDto>.Invalid(new Dictionary<string, string>
            {
                ["effortHours"] = $"Effort must be between {RoadmapBuilder.MinEffort} and {RoadmapBuilder.MaxEffort} hours."
            });
        }
        if (row.Status == CareerRoadmapStatuses.Dismissed)
        {
            return NotProposed<CareerRoadmapDto>("A dismissed roadmap cannot be edited.");
        }

        var options = ReadOptions(row);
        var edited = options.FirstOrDefault(o => Flatten(o).Any(t => t.Id == taskId));
        if (edited == null)
        {
            return CareerOutcome<CareerRoadmapDto>.NotFound("CareerRoadmapTaskNotFound", "That task was not found on this roadmap.");
        }

        var rebuilt = new List<RoadmapOption>();
        try
        {
            // The graph is validated every time, so a stored cycle or dangling dependency is never carried into a new version.
            foreach (var option in options)
            {
                RoadmapBuilder.TopologicalOrder(Flatten(option));
                if (option != edited)
                {
                    rebuilt.Add(option);
                    continue;
                }
                var tasks = Flatten(option).Select(t => t.Id == taskId ? t with { EffortHours = effort } : t).ToList();
                var (week, milestones) = RoadmapBuilder.Schedule(tasks, row.WeeklyEffortHours);
                rebuilt.Add(option with { ThisWeek = week, Milestones = milestones });
            }
        }
        catch (RoadmapGraphException ex) when (ex.Kind == "cycle")
        {
            return CareerOutcome<CareerRoadmapDto>.AlreadyExists(RoadmapErrorCodes.Cycle, "These task dependencies form a cycle.");
        }
        catch (RoadmapGraphException)
        {
            return CareerOutcome<CareerRoadmapDto>.Invalid(new Dictionary<string, string>
            {
                ["dependsOn"] = "A task depends on a task that is not on this roadmap."
            });
        }

        var next = new CareerRoadmap
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            RunId = null,
            Version = (await _db.CareerRoadmaps.AsNoTracking().Where(r => r.OwnerId == ownerId).Select(r => (int?)r.Version).MaxAsync(ct) ?? 0) + 1,
            Status = row.Status,
            SelectedOption = row.SelectedOption,
            PinnedProfileVersion = row.PinnedProfileVersion,
            PinnedGoalVersion = row.PinnedGoalVersion,
            OccupationCode = row.OccupationCode,
            OccupationTitle = row.OccupationTitle,
            MarketBriefId = row.MarketBriefId,
            PayAnalysisId = row.PayAnalysisId,
            WeeklyEffortHours = row.WeeklyEffortHours,
            LowTimeNote = row.LowTimeNote,
            OptionsJson = JsonSerializer.Serialize(rebuilt, RoadmapJson.Options),
            OmittedJson = row.OmittedJson,
            CreatedAt = _clock.GetUtcNow().UtcDateTime
        };
        _db.CareerRoadmaps.Add(next);
        // Progress and the tasks the user added belong to the roadmap, so they move to the new version unchanged.
        foreach (var progress in await _db.CareerRoadmapTaskProgress.AsNoTracking().Where(p => p.RoadmapId == row.Id && p.OwnerId == ownerId).ToListAsync(ct))
        {
            _db.CareerRoadmapTaskProgress.Add(CareerRoadmapTrackingService.CopyOf(progress, next.Id, progress.DependsOnJson));
        }
        await _db.SaveChangesAsync(ct);
        return CareerOutcome<CareerRoadmapDto>.Ok(await ToDtoAsync(next, ct));
    }

    // ---- Mapping ---------------------------------------------------------------

    private static IReadOnlyList<RoadmapTask> Flatten(RoadmapOption option) =>
        option.ThisWeek.Concat(option.Milestones.SelectMany(m => m.Tasks)).ToList();

    private static List<RoadmapOption> ReadOptions(CareerRoadmap row) =>
        JsonSerializer.Deserialize<List<RoadmapOption>>(row.OptionsJson, RoadmapJson.Options) ?? new();

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private async Task<Current> CurrentAsync(string ownerId, CancellationToken ct)
    {
        var profile = await _db.CareerProfiles.AsNoTracking().Where(p => p.OwnerId == ownerId)
            .Select(p => (int?)p.ActiveVersionNumber).FirstOrDefaultAsync(ct);
        var goalVersion = await _db.CareerGoals.AsNoTracking().Where(g => g.OwnerId == ownerId)
            .Select(g => (int?)g.ActiveVersionNumber).FirstOrDefaultAsync(ct);
        var code = goalVersion == null
            ? null
            : await _db.CareerGoalVersions.AsNoTracking()
                .Where(v => v.OwnerId == ownerId && v.VersionNumber == goalVersion).Select(v => v.OccupationCode).FirstOrDefaultAsync(ct);
        return new Current(profile, goalVersion, code);
    }

    /// <summary>Why the roadmap no longer matches the user's data. Content never changes; only this reading does.</summary>
    private async Task<List<string>> StaleReasonsAsync(CareerRoadmap row, Current current, CancellationToken ct)
    {
        var reasons = new List<string>();
        if (current.ProfileVersion != row.PinnedProfileVersion) reasons.Add("profile_changed");
        if (current.GoalVersion != row.PinnedGoalVersion) reasons.Add("goal_changed");
        if (current.OccupationCode != row.OccupationCode) reasons.Add("occupation_changed");
        var latestBrief = await _db.CareerMarketBriefs.AsNoTracking()
            .Where(b => b.OwnerId == row.OwnerId && b.OccupationCode == row.OccupationCode)
            .OrderByDescending(b => b.CreatedAt).Select(b => (Guid?)b.Id).FirstOrDefaultAsync(ct);
        if (latestBrief != null && latestBrief != row.MarketBriefId) reasons.Add("market_brief_changed");
        return reasons;
    }

    private async Task<CareerRoadmapDto> ToDtoAsync(CareerRoadmap row, CancellationToken ct)
    {
        var reasons = await StaleReasonsAsync(row, await CurrentAsync(row.OwnerId, ct), ct);
        return new CareerRoadmapDto(
            row.Id, row.Version, row.Status, row.SelectedOption,
            new RoadmapPinnedDto(row.PinnedProfileVersion, row.PinnedGoalVersion, row.OccupationCode, row.MarketBriefId, row.PayAnalysisId),
            reasons.Count > 0, reasons, row.WeeklyEffortHours, ReadOptions(row),
            JsonSerializer.Deserialize<List<RoadmapOmittedOption>>(row.OmittedJson, RoadmapJson.Options) ?? new(),
            row.LowTimeNote);
    }
}
