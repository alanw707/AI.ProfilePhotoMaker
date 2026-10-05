using System.Globalization;

namespace AI.ProfilePhotoMaker.API.Services.Career;

// Shapes for docs/career/api-roadmap-tracking.md (ADR 0017).

public static class RoadmapTrackingErrorCodes
{
    public const string NotAccepted = "CareerRoadmapNotAccepted";
    public const string TaskNotFound = "CareerRoadmapTaskNotFound";
    public const string MaterialNotFound = "CareerMaterialNotFound";
    public const string ReplanNotFound = "CareerReplanNotFound";
    public const string ReplanStale = "CareerReplanStale";
    public const string ReplanClosed = "CareerReplanClosed";
}

public static class RoadmapTaskOrigins
{
    public const string Generated = "generated";
    public const string Human = "human";
}

public sealed record RoadmapTaskProgressDto(
    string TaskId,
    string Title,
    string Origin,
    int MilestoneDay,
    IReadOnlyList<string> DependsOn,
    string Status,
    string EffectiveStatus,
    IReadOnlyList<string> BlockedBy,
    double? EffortHours,
    string? OutputNote,
    Guid? LinkedMaterialId,
    bool LinkedMaterialMissing,
    string Help,
    string Etag);

public sealed record RoadmapProgressDto(Guid RoadmapId, int Version, IReadOnlyList<RoadmapTaskProgressDto> Tasks);

/// <summary>Absent fields are left alone. An empty outputNote or linkedMaterialId clears the value.</summary>
public sealed class UpdateTaskProgressRequest
{
    public string? Status { get; set; }
    public double? EffortHours { get; set; }
    public string? OutputNote { get; set; }
    public string? LinkedMaterialId { get; set; }
}

public sealed class AddHumanTaskRequest
{
    public string? Title { get; set; }
    public double? EffortHours { get; set; }
    public int? MilestoneDay { get; set; }
    public List<string>? DependsOn { get; set; }
}

public sealed class ApplyReplanRequest
{
    public List<string>? AcceptedChangeIds { get; set; }
}

public sealed record ReplanField(string Field, string? Before, string? After);

public static class ReplanKinds
{
    public const string Added = "added";
    public const string Removed = "removed";
    public const string Changed = "changed";
}

public sealed record ReplanChange(string Id, string Kind, string? TaskId, string Title, IReadOnlyList<ReplanField> Fields, string Rationale);

public sealed record ReplanPreserved(string TaskId, string Title, string Reason);

public sealed record ReplanDto(Guid Id, int BaseVersion, string Status, IReadOnlyList<ReplanChange> Changes, IReadOnlyList<ReplanPreserved> Preserved);

/// <summary>What the stored proposal keeps so applying needs no second evidence read.</summary>
public sealed record StoredReplanProposal(
    RoadmapOption? Option,
    IReadOnlyList<PlanTask> Tasks,
    int ProfileVersion,
    int GoalVersion,
    string OccupationCode,
    string OccupationTitle,
    Guid? MarketBriefId,
    Guid? PayAnalysisId,
    double WeeklyEffortHours,
    string? LowTimeNote);

/// <summary>Deterministic contextual help for each template task. Free actions only, like the templates.</summary>
public static class RoadmapHelp
{
    public const string Human = "You added this task. Keep it to one small step you can finish in a single sitting, and note what you produced when you are done.";
    public const string Fallback = "Do this in one sitting if you can. Note what you produced, and mark it done when you have.";

    private static readonly (string Prefix, string Text)[] Entries =
    {
        ("Read the duty list for", "Open the published duty list and tick the duties you already do in your work today. Skip the ones that do not apply."),
        ("Write one recent example", "For each duty you ticked, write two or three sentences about one time you did it: what the situation was, what you did, and what happened."),
        ("Find a free guide or open documentation", "Search for a free beginner guide or the official documentation. Read one section and write one thing you learned."),
        ("Look for one example in your current work", "Think back over the last few months for one piece of work that shows this duty. A short note is enough."),
        ("Ask one person with a job like", "Send a short message to someone who does this job. Ask what a typical week involves and which tasks take most of their time."),
        ("Add your new examples", "Open your career profile and add the examples you wrote as highlights, then confirm the profile."),
        ("Add a city and state", "Open your goal and fill in the city and state where you would work. Local figures only appear once it is set."),
        ("Open a market brief", "Open a market brief for your target occupation to see published wage and projection figures."),
        ("Open a pay analysis", "Open a pay analysis to see how your pay target compares with published figures. It does not change your goal."),
        ("Review this roadmap", "Look back at what you finished. Decide whether to keep going, change a task, or replan from your current evidence.")
    };

    public static IReadOnlyList<string> All { get; } = Entries.Select(e => e.Text).Append(Human).Append(Fallback).ToList();

    public static string For(string title, string origin)
    {
        if (origin == RoadmapTaskOrigins.Human)
        {
            return Human;
        }
        foreach (var (prefix, text) in Entries)
        {
            if (title.StartsWith(prefix, StringComparison.Ordinal))
            {
                return text;
            }
        }
        return Fallback;
    }
}

// ---- Replan diff (pure) ------------------------------------------------------------

/// <summary>A task the new plan proposes, referring to its dependencies by title.</summary>
public sealed record PlanTask(string Title, double EffortHours, int MilestoneDay, IReadOnlyList<string> DependsOnTitles);

/// <summary>A task on the roadmap now, with the progress facts the diff must respect.</summary>
public sealed record CurrentPlanTask(
    string TaskId, string Title, string Origin, double EffortHours, int MilestoneDay,
    IReadOnlyList<string> DependsOnTitles, bool Done, bool HasOutput);

public sealed record PlacedTask(string Id, string Title, double EffortHours, int MilestoneDay, IReadOnlyList<string> DependsOn);

public sealed record ReplanResult(IReadOnlyList<ReplanChange> Changes, IReadOnlyList<ReplanPreserved> Preserved);

/// <summary>
/// Deterministic roadmap diff (ADR 0017). Generated tasks are matched by title. Human tasks, done tasks and tasks
/// with output or a linked material are preserved: they are never removed and never changed.
/// </summary>
public static class ReplanDiff
{
    public const string Title = "title";
    public const string Effort = "effort";
    public const string MilestoneDay = "milestoneDay";
    public const string Dependencies = "dependencies";

    public static ReplanResult Compute(IReadOnlyList<CurrentPlanTask> current, IReadOnlyList<PlanTask> proposed)
    {
        var proposedByTitle = proposed.GroupBy(p => p.Title, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var changes = new List<ReplanChange>();
        var preserved = new List<ReplanPreserved>();
        var counter = 0;
        string NextId() => $"c{++counter}";

        foreach (var task in current)
        {
            var reason = task.Origin == RoadmapTaskOrigins.Human ? "human" : task.Done ? "done" : task.HasOutput ? "has_output" : null;
            if (reason != null)
            {
                preserved.Add(new ReplanPreserved(task.TaskId, task.Title, reason));
                continue;
            }

            if (!proposedByTitle.TryGetValue(task.Title, out var next))
            {
                changes.Add(new ReplanChange(NextId(), ReplanKinds.Removed, task.TaskId, task.Title,
                    Fields(task.Title, task.EffortHours, task.MilestoneDay, task.DependsOnTitles, null, null, null, removed: true),
                    "Your current evidence no longer calls for this task, and nothing has been recorded on it."));
                continue;
            }

            var fields = new List<ReplanField>();
            if (!Same(task.EffortHours, next.EffortHours))
            {
                fields.Add(new ReplanField(Effort, Hours(task.EffortHours), Hours(next.EffortHours)));
            }
            if (task.MilestoneDay != next.MilestoneDay)
            {
                fields.Add(new ReplanField(MilestoneDay, Day(task.MilestoneDay), Day(next.MilestoneDay)));
            }
            if (!task.DependsOnTitles.ToHashSet(StringComparer.Ordinal).SetEquals(next.DependsOnTitles))
            {
                fields.Add(new ReplanField(Dependencies, Deps(task.DependsOnTitles), Deps(next.DependsOnTitles)));
            }
            if (fields.Count > 0)
            {
                changes.Add(new ReplanChange(NextId(), ReplanKinds.Changed, task.TaskId, task.Title, fields,
                    $"The suggested {string.Join(" and ", fields.Select(f => Label(f.Field)))} for this task changed to fit your current evidence and weekly hours."));
            }
        }

        var currentTitles = current.Where(t => t.Origin == RoadmapTaskOrigins.Generated).Select(t => t.Title).ToHashSet(StringComparer.Ordinal);
        foreach (var task in proposed.Where(p => !currentTitles.Contains(p.Title)))
        {
            changes.Add(new ReplanChange(NextId(), ReplanKinds.Added, null, task.Title,
                Fields(task.Title, null, null, null, task.EffortHours, task.MilestoneDay, task.DependsOnTitles, removed: false),
                "Your current profile, goal and evidence call for this new task."));
        }
        return new ReplanResult(changes, preserved);
    }

    /// <summary>
    /// The generated tasks after applying only the accepted changes. Retained tasks keep their ids so progress
    /// carries over; added tasks take ids from <paramref name="firstNewNumber"/>. Dependencies on tasks that are
    /// not in the result are dropped.
    /// </summary>
    public static IReadOnlyList<PlacedTask> Apply(
        IReadOnlyList<CurrentPlanTask> currentGenerated, IReadOnlyList<PlanTask> proposed,
        IReadOnlyList<ReplanChange> changes, IReadOnlySet<string> accepted, int firstNewNumber)
    {
        var proposedByTitle = proposed.GroupBy(p => p.Title, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var working = currentGenerated
            .Select(t => new Working(t.TaskId, t.Title, t.EffortHours, t.MilestoneDay, t.DependsOnTitles.ToList())).ToList();
        var number = firstNewNumber;

        foreach (var change in changes.Where(c => accepted.Contains(c.Id)))
        {
            switch (change.Kind)
            {
                case ReplanKinds.Removed:
                    working.RemoveAll(w => w.Title == change.Title);
                    break;
                case ReplanKinds.Changed when working.FirstOrDefault(w => w.Title == change.Title) is { } target
                                              && proposedByTitle.TryGetValue(change.Title, out var next):
                    foreach (var field in change.Fields)
                    {
                        switch (field.Field)
                        {
                            case Effort: target.Effort = next.EffortHours; break;
                            case MilestoneDay: target.Day = next.MilestoneDay; break;
                            case Dependencies: target.DependsOn = next.DependsOnTitles.ToList(); break;
                        }
                    }
                    break;
                case ReplanKinds.Added when proposedByTitle.TryGetValue(change.Title, out var added) && working.All(w => w.Title != added.Title):
                    working.Add(new Working($"t{number++}", added.Title, added.EffortHours, added.MilestoneDay, added.DependsOnTitles.ToList()));
                    break;
            }
        }

        var idByTitle = working.ToDictionary(w => w.Title, w => w.Id, StringComparer.Ordinal);
        return working.Select(w => new PlacedTask(
            w.Id, w.Title, w.Effort, w.Day,
            w.DependsOn.Where(idByTitle.ContainsKey).Select(t => idByTitle[t]).Distinct().ToList())).ToList();
    }

    private sealed class Working
    {
        public Working(string id, string title, double effort, int day, List<string> dependsOn)
        {
            Id = id; Title = title; Effort = effort; Day = day; DependsOn = dependsOn;
        }
        public string Id { get; }
        public string Title { get; }
        public double Effort { get; set; }
        public int Day { get; set; }
        public List<string> DependsOn { get; set; }
    }

    private static IReadOnlyList<ReplanField> Fields(
        string title, double? beforeEffort, int? beforeDay, IReadOnlyList<string>? beforeDeps,
        double? afterEffort, int? afterDay, IReadOnlyList<string>? afterDeps, bool removed) =>
        new[]
        {
            new ReplanField(Title, removed ? title : null, removed ? null : title),
            new ReplanField(Effort, beforeEffort is { } be ? Hours(be) : null, afterEffort is { } ae ? Hours(ae) : null),
            new ReplanField(MilestoneDay, beforeDay is { } bd ? Day(bd) : null, afterDay is { } ad ? Day(ad) : null),
            new ReplanField(Dependencies, beforeDeps != null ? Deps(beforeDeps) : null, afterDeps != null ? Deps(afterDeps) : null)
        };

    private static bool Same(double a, double b) => Math.Abs(a - b) < 0.0001;
    private static string Hours(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Day(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Deps(IReadOnlyList<string> titles) => titles.Count == 0 ? "none" : string.Join("; ", titles.OrderBy(t => t, StringComparer.Ordinal));
    private static string Label(string field) => field switch
    {
        Effort => "effort",
        MilestoneDay => "milestone day",
        _ => "dependencies"
    };
}
