using System.Text.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services.Career;

public interface ICareerRoadmapTrackingService
{
    Task<CareerOutcome<RoadmapProgressDto>> GetProgressAsync(string ownerId, Guid roadmapId, CancellationToken ct = default);
    Task<CareerOutcome<RoadmapTaskProgressDto>> UpdateProgressAsync(string ownerId, Guid roadmapId, string taskId, UpdateTaskProgressRequest request, VersionPrecondition precondition, CancellationToken ct = default);
    Task<CareerOutcome<RoadmapTaskProgressDto>> AddHumanTaskAsync(string ownerId, Guid roadmapId, AddHumanTaskRequest request, CancellationToken ct = default);
    Task<CareerOutcome<ReplanDto>> ReplanAsync(string ownerId, Guid roadmapId, CancellationToken ct = default);
    Task<CareerOutcome<ReplanDto>> GetReplanAsync(string ownerId, Guid replanId, CancellationToken ct = default);
    Task<CareerOutcome<CareerRoadmapDto>> ApplyReplanAsync(string ownerId, Guid replanId, ApplyReplanRequest request, CancellationToken ct = default);
    Task<CareerOutcome<ReplanDto>> RejectReplanAsync(string ownerId, Guid replanId, CancellationToken ct = default);
}

/// <summary>
/// Progress on an accepted roadmap, human-authored tasks and reviewed replans (ADR 0017). Nothing here touches
/// the goal, a salary estimate or a pay analysis.
/// </summary>
public sealed class CareerRoadmapTrackingService : ICareerRoadmapTrackingService
{
    public const int MaxOutputNote = 2000;
    public const int MaxTitle = 200;
    public const int MaxDependencies = 20;
    public const int MaxHumanTasks = 50;
    private static readonly int[] MilestoneDays = { 0, 30, 60, 90 };

    private readonly ApplicationDbContext _db;
    private readonly ICareerRoadmapService _roadmaps;
    private readonly TimeProvider _clock;
    private readonly IMarketReference? _market;

    public CareerRoadmapTrackingService(ApplicationDbContext db, ICareerRoadmapService roadmaps, TimeProvider clock, IMarketReference? market = null)
    {
        _db = db;
        _roadmaps = roadmaps;
        _clock = clock;
        _market = market;
    }

    public static string TaskEtag(int version) => $"\"task-v{version}\"";

    private sealed record Row(string Id, string Title, string Origin, int Day, double PlannedEffort, List<string> DependsOn);

    private static CareerOutcome<T> RoadmapMissing<T>() => CareerOutcome<T>.NotFound(RoadmapErrorCodes.NotFound, "That roadmap was not found.");

    private static CareerOutcome<T> ReplanMissing<T>() => CareerOutcome<T>.NotFound(RoadmapTrackingErrorCodes.ReplanNotFound, "That replan was not found.");

    private static CareerOutcome<T> NotAccepted<T>() =>
        CareerOutcome<T>.AlreadyExists(RoadmapTrackingErrorCodes.NotAccepted, "Accept this roadmap before tracking progress on it.");

    private static CareerOutcome<T> Cycle<T>() => CareerOutcome<T>.AlreadyExists(RoadmapErrorCodes.Cycle, "These task dependencies form a cycle.");

    private static CareerOutcome<T> Field<T>(string field, string message) =>
        CareerOutcome<T>.Invalid(new Dictionary<string, string> { [field] = message });

    // ---- Reads -----------------------------------------------------------------

    public async Task<CareerOutcome<RoadmapProgressDto>> GetProgressAsync(string ownerId, Guid roadmapId, CancellationToken ct = default)
    {
        var row = await FindRoadmapAsync(ownerId, roadmapId, ct);
        if (row == null)
        {
            return RoadmapMissing<RoadmapProgressDto>();
        }
        if (row.Status != CareerRoadmapStatuses.Accepted)
        {
            return NotAccepted<RoadmapProgressDto>();
        }
        return CareerOutcome<RoadmapProgressDto>.Ok(new RoadmapProgressDto(row.Id, row.Version, await ViewAsync(row, ct)));
    }

    // ---- Progress writes ---------------------------------------------------------

    public async Task<CareerOutcome<RoadmapTaskProgressDto>> UpdateProgressAsync(
        string ownerId, Guid roadmapId, string taskId, UpdateTaskProgressRequest request, VersionPrecondition precondition, CancellationToken ct = default)
    {
        var roadmap = await FindRoadmapAsync(ownerId, roadmapId, ct);
        if (roadmap == null)
        {
            return RoadmapMissing<RoadmapTaskProgressDto>();
        }
        if (roadmap.Status != CareerRoadmapStatuses.Accepted)
        {
            return NotAccepted<RoadmapTaskProgressDto>();
        }

        var progressRows = await _db.CareerRoadmapTaskProgress.Where(p => p.RoadmapId == roadmapId && p.OwnerId == ownerId).ToListAsync(ct);
        var tasks = TasksOf(roadmap, progressRows);
        if (tasks.All(t => t.Id != taskId))
        {
            return CareerOutcome<RoadmapTaskProgressDto>.NotFound(RoadmapTrackingErrorCodes.TaskNotFound, "That task was not found on this roadmap.");
        }

        var existing = progressRows.FirstOrDefault(p => p.TaskId == taskId);
        var currentVersion = existing?.RowVersion ?? 0;
        if (!precondition.IsPresent)
        {
            return CareerOutcome<RoadmapTaskProgressDto>.PreconditionRequired();
        }
        if (precondition.ExpectedVersion != currentVersion)
        {
            return CareerOutcome<RoadmapTaskProgressDto>.Conflict(currentVersion);
        }

        var errors = new Dictionary<string, string>();
        if (request.Status != null && !RoadmapTaskStatuses.IsKnown(request.Status))
        {
            errors["status"] = "Choose not_started, in_progress, done or blocked.";
        }
        if (request.EffortHours is { } effort && (double.IsNaN(effort) || effort < RoadmapBuilder.MinEffort || effort > RoadmapBuilder.MaxEffort))
        {
            errors["effortHours"] = $"Effort must be between {RoadmapBuilder.MinEffort} and {RoadmapBuilder.MaxEffort} hours.";
        }
        if (request.OutputNote is { Length: > MaxOutputNote })
        {
            errors["outputNote"] = $"Keep the note under {MaxOutputNote} characters.";
        }
        Guid? material = null;
        var clearMaterial = false;
        if (request.LinkedMaterialId != null)
        {
            if (request.LinkedMaterialId.Length == 0)
            {
                clearMaterial = true;
            }
            else if (Guid.TryParse(request.LinkedMaterialId, out var parsed))
            {
                material = parsed;
            }
            else
            {
                errors["linkedMaterialId"] = "That is not a valid material id.";
            }
        }
        if (errors.Count > 0)
        {
            return CareerOutcome<RoadmapTaskProgressDto>.Invalid(errors);
        }
        // Ownership is checked on write, so another user's material id reveals nothing.
        if (material != null && !await _db.CareerResumeDocuments.AsNoTracking().AnyAsync(d => d.Id == material && d.OwnerId == ownerId, ct))
        {
            return CareerOutcome<RoadmapTaskProgressDto>.NotFound(RoadmapTrackingErrorCodes.MaterialNotFound, "That material was not found.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        if (existing == null)
        {
            existing = new CareerRoadmapTaskProgress
            {
                Id = Guid.NewGuid(), OwnerId = ownerId, RoadmapId = roadmapId, TaskId = taskId,
                Origin = RoadmapTaskOrigins.Generated, Status = RoadmapTaskStatuses.NotStarted
            };
            _db.CareerRoadmapTaskProgress.Add(existing);
        }
        if (request.Status != null) existing.Status = request.Status;
        if (request.EffortHours != null) existing.EffortHours = request.EffortHours;
        if (request.OutputNote != null) existing.OutputNote = request.OutputNote.Trim().Length == 0 ? null : request.OutputNote.Trim();
        if (clearMaterial) existing.LinkedMaterialId = null;
        if (material != null) existing.LinkedMaterialId = material;
        existing.RowVersion = currentVersion + 1;
        existing.UpdatedAt = now;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (CareerProfileService.IsLostRace(ex))
        {
            _db.ChangeTracker.Clear();
            var latest = await _db.CareerRoadmapTaskProgress.AsNoTracking()
                .Where(p => p.RoadmapId == roadmapId && p.TaskId == taskId).Select(p => (int?)p.RowVersion).FirstOrDefaultAsync(ct);
            return CareerOutcome<RoadmapTaskProgressDto>.Conflict(latest ?? 0);
        }

        var view = await ViewAsync(roadmap, ct);
        return CareerOutcome<RoadmapTaskProgressDto>.Ok(view.First(t => t.TaskId == taskId));
    }

    public async Task<CareerOutcome<RoadmapTaskProgressDto>> AddHumanTaskAsync(
        string ownerId, Guid roadmapId, AddHumanTaskRequest request, CancellationToken ct = default)
    {
        var roadmap = await FindRoadmapAsync(ownerId, roadmapId, ct);
        if (roadmap == null)
        {
            return RoadmapMissing<RoadmapTaskProgressDto>();
        }
        if (roadmap.Status != CareerRoadmapStatuses.Accepted)
        {
            return NotAccepted<RoadmapTaskProgressDto>();
        }

        var errors = new Dictionary<string, string>();
        var title = request.Title?.Trim();
        if (string.IsNullOrEmpty(title) || title.Length > MaxTitle)
        {
            errors["title"] = $"Give the task a title of up to {MaxTitle} characters.";
        }
        if (request.EffortHours is not { } hours || double.IsNaN(hours) || hours < RoadmapBuilder.MinEffort || hours > RoadmapBuilder.MaxEffort)
        {
            errors["effortHours"] = $"Effort must be between {RoadmapBuilder.MinEffort} and {RoadmapBuilder.MaxEffort} hours.";
        }
        if (request.MilestoneDay is not { } day || !MilestoneDays.Contains(day))
        {
            errors["milestoneDay"] = "Choose 0, 30, 60 or 90.";
        }
        var depends = (request.DependsOn ?? new List<string>()).Select(d => d?.Trim() ?? "").Distinct(StringComparer.Ordinal).ToList();
        if (depends.Count > MaxDependencies)
        {
            errors["dependsOn"] = $"A task can depend on at most {MaxDependencies} others.";
        }
        if (errors.Count > 0)
        {
            return CareerOutcome<RoadmapTaskProgressDto>.Invalid(errors);
        }

        var progressRows = await _db.CareerRoadmapTaskProgress.Where(p => p.RoadmapId == roadmapId && p.OwnerId == ownerId).ToListAsync(ct);
        var existing = TasksOf(roadmap, progressRows);
        if (depends.Any(d => existing.All(t => t.Id != d)))
        {
            return Field<RoadmapTaskProgressDto>("dependsOn", "A task depends on a task that is not on this roadmap.");
        }
        var humans = progressRows.Where(p => p.Origin == RoadmapTaskOrigins.Human).ToList();
        if (humans.Count >= MaxHumanTasks)
        {
            return Field<RoadmapTaskProgressDto>("title", $"A roadmap can hold up to {MaxHumanTasks} tasks you added.");
        }

        var nextNumber = humans.Select(h => int.TryParse(h.TaskId.AsSpan(1), out var n) ? n : 0).DefaultIfEmpty(0).Max() + 1;
        var newId = $"h{nextNumber}";
        try
        {
            // The whole graph is validated, so a stored cycle is never carried forward.
            RoadmapBuilder.TopologicalOrder(existing.Select(t => new RoadmapTask(t.Id, t.Title, t.PlannedEffort, t.DependsOn))
                .Append(new RoadmapTask(newId, title!, request.EffortHours!.Value, depends)).ToList());
        }
        catch (RoadmapGraphException ex) when (ex.Kind == "cycle")
        {
            return Cycle<RoadmapTaskProgressDto>();
        }
        catch (RoadmapGraphException)
        {
            return Field<RoadmapTaskProgressDto>("dependsOn", "A task depends on a task that is not on this roadmap.");
        }

        _db.CareerRoadmapTaskProgress.Add(new CareerRoadmapTaskProgress
        {
            Id = Guid.NewGuid(), OwnerId = ownerId, RoadmapId = roadmapId, TaskId = newId,
            Origin = RoadmapTaskOrigins.Human, Title = title, MilestoneDay = request.MilestoneDay,
            PlannedEffortHours = request.EffortHours, DependsOnJson = JsonSerializer.Serialize(depends, RoadmapJson.Options),
            Status = RoadmapTaskStatuses.NotStarted, RowVersion = 1, UpdatedAt = _clock.GetUtcNow().UtcDateTime
        });
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (CareerProfileService.IsLostRace(ex))
        {
            _db.ChangeTracker.Clear();
            return CareerOutcome<RoadmapTaskProgressDto>.Busy("CareerRunBusy", "Too many requests at once. Try again shortly.", 1);
        }
        return CareerOutcome<RoadmapTaskProgressDto>.Created((await ViewAsync(roadmap, ct)).First(t => t.TaskId == newId));
    }

    // ---- Replan --------------------------------------------------------------------

    public async Task<CareerOutcome<ReplanDto>> ReplanAsync(string ownerId, Guid roadmapId, CancellationToken ct = default)
    {
        var roadmap = await FindRoadmapAsync(ownerId, roadmapId, ct);
        if (roadmap == null)
        {
            return RoadmapMissing<ReplanDto>();
        }
        if (roadmap.Status != CareerRoadmapStatuses.Accepted)
        {
            return NotAccepted<ReplanDto>();
        }

        var goalVersionNumber = await _db.CareerGoals.AsNoTracking().Where(g => g.OwnerId == ownerId)
            .Select(g => (int?)g.ActiveVersionNumber).FirstOrDefaultAsync(ct);
        var goal = goalVersionNumber == null ? null
            : await _db.CareerGoalVersions.AsNoTracking().FirstOrDefaultAsync(v => v.OwnerId == ownerId && v.VersionNumber == goalVersionNumber, ct);
        if (goal?.OccupationCode == null || goal.OccupationTitle == null)
        {
            return CareerOutcome<ReplanDto>.AlreadyExists(CareerOccupationErrorCodes.OccupationRequired, "Confirm an occupation for your goal before replanning.");
        }
        var profileVersion = await _db.CareerProfiles.AsNoTracking().Where(p => p.OwnerId == ownerId)
            .Select(p => (int?)p.ActiveVersionNumber).FirstOrDefaultAsync(ct) ?? 0;
        var briefId = await _db.CareerMarketBriefs.AsNoTracking().Where(b => b.OwnerId == ownerId && b.OccupationCode == goal.OccupationCode)
            .OrderByDescending(b => b.CreatedAt).Select(b => (Guid?)b.Id).FirstOrDefaultAsync(ct);
        var payId = await _db.CareerPayAnalyses.AsNoTracking().Where(p => p.OwnerId == ownerId && p.OccupationCode == goal.OccupationCode)
            .OrderByDescending(p => p.CreatedAt).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);

        OccupationReleaseAndResult? match = null;
        if (goal.OccupationMatchId != null)
        {
            var matchRow = await _db.CareerOccupationMatches.AsNoTracking().FirstOrDefaultAsync(m => m.Id == goal.OccupationMatchId && m.OwnerId == ownerId, ct);
            var stored = matchRow == null ? null : JsonSerializer.Deserialize<StoredOccupationResult>(matchRow.ResultJson, OccupationMatchJson.Options);
            match = matchRow == null || stored == null ? null : new OccupationReleaseAndResult(stored.Candidates, matchRow.ReferenceRelease);
        }
        var content = RoadmapBuilder.Build(RoadmapEvidence.Build(goal, match, _market, briefId != null, payId != null));
        var option = content.Options.FirstOrDefault(o => o.Key == roadmap.SelectedOption);

        var proposed = new List<PlanTask>();
        if (option != null)
        {
            var titles = Flatten(option).ToDictionary(t => t.Task.Id, t => t.Task.Title, StringComparer.Ordinal);
            proposed = Flatten(option).Select(t => new PlanTask(t.Task.Title, t.Task.EffortHours, t.Day, t.Task.DependsOn.Select(d => titles[d]).ToList())).ToList();
        }

        var progressRows = await _db.CareerRoadmapTaskProgress.AsNoTracking().Where(p => p.RoadmapId == roadmapId && p.OwnerId == ownerId).ToListAsync(ct);
        var current = CurrentTasks(roadmap, progressRows);
        var diff = ReplanDiff.Compute(current, proposed);

        var replan = new CareerRoadmapReplan
        {
            Id = Guid.NewGuid(), OwnerId = ownerId, RoadmapId = roadmapId, BaseVersion = roadmap.Version,
            Status = RoadmapReplanStatuses.Open,
            ChangesJson = JsonSerializer.Serialize(diff.Changes, RoadmapJson.Options),
            PreservedJson = JsonSerializer.Serialize(diff.Preserved, RoadmapJson.Options),
            ProposedJson = JsonSerializer.Serialize(new StoredReplanProposal(
                option, proposed, profileVersion, goalVersionNumber!.Value, goal.OccupationCode, goal.OccupationTitle,
                briefId, payId, content.WeeklyEffortHours, content.LowTimeNote), RoadmapJson.Options),
            CreatedAt = _clock.GetUtcNow().UtcDateTime
        };
        _db.CareerRoadmapReplans.Add(replan);
        await _db.SaveChangesAsync(ct);
        return CareerOutcome<ReplanDto>.Created(ToDto(replan));
    }

    public async Task<CareerOutcome<ReplanDto>> GetReplanAsync(string ownerId, Guid replanId, CancellationToken ct = default)
    {
        var replan = await _db.CareerRoadmapReplans.AsNoTracking().FirstOrDefaultAsync(r => r.Id == replanId && r.OwnerId == ownerId, ct);
        return replan == null ? ReplanMissing<ReplanDto>() : CareerOutcome<ReplanDto>.Ok(ToDto(replan));
    }

    public async Task<CareerOutcome<ReplanDto>> RejectReplanAsync(string ownerId, Guid replanId, CancellationToken ct = default)
    {
        var replan = await _db.CareerRoadmapReplans.FirstOrDefaultAsync(r => r.Id == replanId && r.OwnerId == ownerId, ct);
        if (replan == null)
        {
            return ReplanMissing<ReplanDto>();
        }
        if (replan.Status != RoadmapReplanStatuses.Open)
        {
            return Closed<ReplanDto>();
        }
        replan.Status = RoadmapReplanStatuses.Rejected;
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (CareerProfileService.IsLostRace(ex))
        {
            _db.ChangeTracker.Clear();
            return Closed<ReplanDto>();
        }
        return CareerOutcome<ReplanDto>.Ok(ToDto(replan));
    }

    public async Task<CareerOutcome<CareerRoadmapDto>> ApplyReplanAsync(string ownerId, Guid replanId, ApplyReplanRequest request, CancellationToken ct = default)
    {
        var replan = await _db.CareerRoadmapReplans.FirstOrDefaultAsync(r => r.Id == replanId && r.OwnerId == ownerId, ct);
        if (replan == null)
        {
            return ReplanMissing<CareerRoadmapDto>();
        }
        if (replan.Status != RoadmapReplanStatuses.Open)
        {
            return Closed<CareerRoadmapDto>();
        }

        var roadmap = await _db.CareerRoadmaps.AsNoTracking().FirstOrDefaultAsync(r => r.Id == replan.RoadmapId && r.OwnerId == ownerId, ct);
        var latest = await _db.CareerRoadmaps.AsNoTracking()
            .Where(r => r.OwnerId == ownerId && r.Status == CareerRoadmapStatuses.Accepted).Select(r => (int?)r.Version).MaxAsync(ct);
        if (roadmap == null || latest > replan.BaseVersion)
        {
            return CareerOutcome<CareerRoadmapDto>.AlreadyExists(RoadmapTrackingErrorCodes.ReplanStale, "The roadmap changed after this replan was made. Replan again.");
        }

        var changes = JsonSerializer.Deserialize<List<ReplanChange>>(replan.ChangesJson, RoadmapJson.Options) ?? new();
        var accepted = (request.AcceptedChangeIds ?? new List<string>()).Select(i => i?.Trim() ?? "").ToHashSet(StringComparer.Ordinal);
        if (accepted.Any(id => changes.All(c => c.Id != id)))
        {
            return Field<CareerRoadmapDto>("acceptedChangeIds", "One of those changes is not part of this replan.");
        }
        var proposal = JsonSerializer.Deserialize<StoredReplanProposal>(replan.ProposedJson, RoadmapJson.Options)!;

        var progressRows = await _db.CareerRoadmapTaskProgress.AsNoTracking().Where(p => p.RoadmapId == roadmap.Id && p.OwnerId == ownerId).ToListAsync(ct);
        var generated = CurrentTasks(roadmap, progressRows).Where(t => t.Origin == RoadmapTaskOrigins.Generated).ToList();
        var options = ReadOptions(roadmap);
        var firstNew = options.SelectMany(o => Flatten(o).Select(t => t.Task.Id)).Select(id => int.TryParse(id.AsSpan(1), out var n) ? n : 0)
            .DefaultIfEmpty(0).Max() + 1;
        var placed = ReplanDiff.Apply(generated, proposal.Tasks, changes, accepted, firstNew);

        var keptGenerated = placed.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var humans = progressRows.Where(p => p.Origin == RoadmapTaskOrigins.Human).ToList();
        var allIds = keptGenerated.Concat(humans.Select(h => h.TaskId)).ToHashSet(StringComparer.Ordinal);
        List<string> HumanDeps(CareerRoadmapTaskProgress h) =>
            (JsonSerializer.Deserialize<List<string>>(h.DependsOnJson ?? "[]", RoadmapJson.Options) ?? new()).Where(allIds.Contains).ToList();

        IReadOnlyList<RoadmapTask> ordered;
        try
        {
            ordered = RoadmapBuilder.TopologicalOrder(placed.Select(p => new RoadmapTask(p.Id, p.Title, p.EffortHours, p.DependsOn))
                .Concat(humans.Select(h => new RoadmapTask(h.TaskId, h.Title ?? "", h.PlannedEffortHours ?? 0, HumanDeps(h)))).ToList());
        }
        catch (RoadmapGraphException ex) when (ex.Kind == "cycle")
        {
            return Cycle<CareerRoadmapDto>();
        }
        catch (RoadmapGraphException)
        {
            return Field<CareerRoadmapDto>("acceptedChangeIds", "Those changes leave a task depending on a task that is not on this roadmap.");
        }

        var dayById = placed.ToDictionary(p => p.Id, p => p.MilestoneDay, StringComparer.Ordinal);
        var tasksById = placed.ToDictionary(p => p.Id, p => new RoadmapTask(p.Id, p.Title, p.EffortHours, p.DependsOn), StringComparer.Ordinal);
        List<RoadmapTask> OnDay(int day) => ordered.Where(t => dayById.TryGetValue(t.Id, out var d) && d == day).Select(t => tasksById[t.Id]).ToList();

        var index = options.FindIndex(o => o.Key == roadmap.SelectedOption);
        if (index >= 0)
        {
            var basis = proposal.Option ?? options[index];
            options[index] = basis with
            {
                ThisWeek = OnDay(0),
                Milestones = new[] { 30, 60, 90 }.Select(d => new RoadmapMilestone(d, OnDay(d))).ToList()
            };
        }

        var next = new CareerRoadmap
        {
            Id = Guid.NewGuid(), OwnerId = ownerId, RunId = null,
            Version = (await _db.CareerRoadmaps.AsNoTracking().Where(r => r.OwnerId == ownerId).Select(r => (int?)r.Version).MaxAsync(ct) ?? 0) + 1,
            Status = CareerRoadmapStatuses.Accepted, SelectedOption = roadmap.SelectedOption,
            PinnedProfileVersion = proposal.ProfileVersion, PinnedGoalVersion = proposal.GoalVersion,
            OccupationCode = proposal.OccupationCode, OccupationTitle = proposal.OccupationTitle,
            MarketBriefId = proposal.MarketBriefId, PayAnalysisId = proposal.PayAnalysisId,
            WeeklyEffortHours = proposal.WeeklyEffortHours, LowTimeNote = proposal.LowTimeNote,
            OptionsJson = JsonSerializer.Serialize(options, RoadmapJson.Options), OmittedJson = roadmap.OmittedJson,
            CreatedAt = _clock.GetUtcNow().UtcDateTime
        };
        _db.CareerRoadmaps.Add(next);

        // Progress carries over by task id; rows of tasks the user accepted the removal of are dropped.
        foreach (var row in progressRows)
        {
            if (row.Origin == RoadmapTaskOrigins.Human)
            {
                _db.CareerRoadmapTaskProgress.Add(CopyOf(row, next.Id, JsonSerializer.Serialize(HumanDeps(row), RoadmapJson.Options)));
            }
            else if (keptGenerated.Contains(row.TaskId))
            {
                _db.CareerRoadmapTaskProgress.Add(CopyOf(row, next.Id, row.DependsOnJson));
            }
        }
        replan.Status = RoadmapReplanStatuses.Applied;
        replan.AppliedRoadmapId = next.Id;
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (CareerProfileService.IsLostRace(ex))
        {
            _db.ChangeTracker.Clear();
            return Closed<CareerRoadmapDto>();
        }
        return await _roadmaps.GetAsync(ownerId, next.Id, ct);
    }

    // ---- Helpers ---------------------------------------------------------------------

    private static CareerOutcome<T> Closed<T>() =>
        CareerOutcome<T>.AlreadyExists(RoadmapTrackingErrorCodes.ReplanClosed, "This replan was already applied or rejected.");

    internal static CareerRoadmapTaskProgress CopyOf(CareerRoadmapTaskProgress row, Guid roadmapId, string? dependsOnJson) => new()
    {
        Id = Guid.NewGuid(), OwnerId = row.OwnerId, RoadmapId = roadmapId, TaskId = row.TaskId, Origin = row.Origin, Title = row.Title,
        MilestoneDay = row.MilestoneDay, PlannedEffortHours = row.PlannedEffortHours, DependsOnJson = dependsOnJson, Status = row.Status,
        EffortHours = row.EffortHours, OutputNote = row.OutputNote, LinkedMaterialId = row.LinkedMaterialId,
        RowVersion = row.RowVersion, UpdatedAt = row.UpdatedAt
    };

    private Task<CareerRoadmap?> FindRoadmapAsync(string ownerId, Guid id, CancellationToken ct) =>
        _db.CareerRoadmaps.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id && r.OwnerId == ownerId, ct);

    private static List<RoadmapOption> ReadOptions(CareerRoadmap row) =>
        JsonSerializer.Deserialize<List<RoadmapOption>>(row.OptionsJson, RoadmapJson.Options) ?? new();

    private static IEnumerable<(RoadmapTask Task, int Day)> Flatten(RoadmapOption option) =>
        option.ThisWeek.Select(t => (t, 0)).Concat(option.Milestones.SelectMany(m => m.Tasks.Select(t => (t, m.Day))));

    private static ReplanDto ToDto(CareerRoadmapReplan r) => new(
        r.Id, r.BaseVersion, r.Status,
        JsonSerializer.Deserialize<List<ReplanChange>>(r.ChangesJson, RoadmapJson.Options) ?? new(),
        JsonSerializer.Deserialize<List<ReplanPreserved>>(r.PreservedJson, RoadmapJson.Options) ?? new());

    /// <summary>The selected option's generated tasks, then the human-authored ones, in a stable order.</summary>
    private static List<Row> TasksOf(CareerRoadmap roadmap, IReadOnlyList<CareerRoadmapTaskProgress> progress)
    {
        var rows = new List<Row>();
        var option = ReadOptions(roadmap).FirstOrDefault(o => o.Key == roadmap.SelectedOption);
        if (option != null)
        {
            rows.AddRange(Flatten(option).Select(t => new Row(t.Task.Id, t.Task.Title, RoadmapTaskOrigins.Generated, t.Day, t.Task.EffortHours, t.Task.DependsOn.ToList())));
        }
        rows.AddRange(progress.Where(p => p.Origin == RoadmapTaskOrigins.Human)
            .OrderBy(p => int.TryParse(p.TaskId.AsSpan(1), out var n) ? n : 0)
            .Select(p => new Row(p.TaskId, p.Title ?? "", RoadmapTaskOrigins.Human, p.MilestoneDay ?? 0, p.PlannedEffortHours ?? 0,
                JsonSerializer.Deserialize<List<string>>(p.DependsOnJson ?? "[]", RoadmapJson.Options) ?? new())));
        return rows;
    }

    private static List<CurrentPlanTask> CurrentTasks(CareerRoadmap roadmap, IReadOnlyList<CareerRoadmapTaskProgress> progress)
    {
        var tasks = TasksOf(roadmap, progress);
        var titles = tasks.ToDictionary(t => t.Id, t => t.Title, StringComparer.Ordinal);
        return tasks.Select(t =>
        {
            var p = progress.FirstOrDefault(x => x.TaskId == t.Id);
            return new CurrentPlanTask(
                t.Id, t.Title, t.Origin, t.PlannedEffort, t.Day,
                t.DependsOn.Where(titles.ContainsKey).Select(d => titles[d]).ToList(),
                p?.Status == RoadmapTaskStatuses.Done,
                !string.IsNullOrEmpty(p?.OutputNote) || p?.LinkedMaterialId != null);
        }).ToList();
    }

    private async Task<List<RoadmapTaskProgressDto>> ViewAsync(CareerRoadmap roadmap, CancellationToken ct)
    {
        var progress = await _db.CareerRoadmapTaskProgress.AsNoTracking().Where(p => p.RoadmapId == roadmap.Id && p.OwnerId == roadmap.OwnerId).ToListAsync(ct);
        var tasks = TasksOf(roadmap, progress);
        var byId = tasks.ToDictionary(t => t.Id, StringComparer.Ordinal);
        var linked = progress.Where(p => p.LinkedMaterialId != null).Select(p => p.LinkedMaterialId!.Value).Distinct().ToList();
        var present = linked.Count == 0
            ? new HashSet<Guid>()
            : (await _db.CareerResumeDocuments.AsNoTracking().Where(d => d.OwnerId == roadmap.OwnerId && linked.Contains(d.Id)).Select(d => d.Id).ToListAsync(ct)).ToHashSet();
        string StatusOf(string id) => progress.FirstOrDefault(p => p.TaskId == id)?.Status ?? RoadmapTaskStatuses.NotStarted;

        return tasks.Select(t =>
        {
            var p = progress.FirstOrDefault(x => x.TaskId == t.Id);
            var status = p?.Status ?? RoadmapTaskStatuses.NotStarted;
            var blockedBy = status == RoadmapTaskStatuses.Done
                ? new List<string>()
                : t.DependsOn.Where(d => byId.ContainsKey(d) && StatusOf(d) != RoadmapTaskStatuses.Done).Select(d => byId[d].Title).ToList();
            var effective = status != RoadmapTaskStatuses.Done && (blockedBy.Count > 0 || status == RoadmapTaskStatuses.Blocked)
                ? RoadmapTaskStatuses.Blocked
                : status;
            return new RoadmapTaskProgressDto(
                t.Id, t.Title, t.Origin, t.Day, t.DependsOn, status, effective, blockedBy,
                p?.EffortHours ?? t.PlannedEffort, p?.OutputNote, p?.LinkedMaterialId,
                p?.LinkedMaterialId is { } m && !present.Contains(m),
                RoadmapHelp.For(t.Title, t.Origin), TaskEtag(p?.RowVersion ?? 0));
        }).ToList();
    }
}
