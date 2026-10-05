using System.Globalization;

namespace AI.ProfilePhotoMaker.API.Services.Career;

/// <summary>One occupation the roadmap can reason about, with the published figures it may cite.</summary>
public sealed record RoadmapCandidate(
    string Code,
    string Title,
    double? MedianAnnual,
    string? WageSourceId,
    string? WageRelease,
    double? ChangePercent,
    string? ProjectionSourceId,
    string? ProjectionRelease,
    int DutyEvidenceCount,
    IReadOnlyList<string> UnmatchedDuties,
    IReadOnlyList<string> SkillGaps);

public sealed record RoadmapInput(
    RoadmapCandidate Target,
    bool TargetMatchFound,
    IReadOnlyList<RoadmapCandidate> Alternatives,
    int? WeeklyEffortHours,
    bool HasLocation,
    bool HasMarketBrief,
    bool HasPayAnalysis,
    string? OnetRelease);

public sealed record RoadmapContent(
    IReadOnlyList<RoadmapOption> Options,
    IReadOnlyList<RoadmapOmittedOption> Omitted,
    double WeeklyEffortHours,
    string? LowTimeNote);

public sealed class RoadmapGraphException : Exception
{
    public RoadmapGraphException(string kind, string message) : base(message) => Kind = kind;

    /// <summary>"cycle" or "unknown_dependency".</summary>
    public string Kind { get; }
}

/// <summary>
/// The fixed task library. Tasks are free actions the user can do alone; the generator has no
/// vocabulary for courses, prices, credentials or pay changes (ADR 0016), and a test scans this list.
/// </summary>
public static class RoadmapTemplates
{
    public const string ReadDuties = "Read the duty list for {0} and mark the duties you already do";
    public const string WriteExamples = "Write one recent example from your own work for each duty you marked";
    public const string SkillGap = "Find a free guide or open documentation on {0} and note one thing you learned";
    public const string DutyGap = "Look for one example in your current work that shows: {0}";
    public const string Conversation = "Ask one person with a job like \"{0}\" what a typical week involves";
    public const string UpdateProfile = "Add your new examples to your career profile";
    public const string AddLocation = "Add a city and state to your goal so local figures can be shown";
    public const string OpenBrief = "Open a market brief for your target occupation";
    public const string OpenPay = "Open a pay analysis to compare your pay target with published figures";
    public const string Review = "Review this roadmap and decide whether to keep going";

    public const string TimelineNote = "A scenario, not a promise.";
    public const string LowTimeNote = "With under 2 hours a week, this plan keeps to one small task per week and takes longer.";

    public static IReadOnlyList<string> All { get; } = new[]
    {
        ReadDuties, WriteExamples, SkillGap, DutyGap, Conversation, UpdateProfile, AddLocation, OpenBrief, OpenPay, Review,
        TimelineNote, LowTimeNote
    };
}

/// <summary>
/// Pure, deterministic roadmap generation (ADR 0016). Options appear only when the evidence supports
/// them; effort is fitted to the user's weekly hours, spilling into later milestones.
/// </summary>
public static class RoadmapBuilder
{
    // Percentages are compared as whole numbers so the edges (exactly +10 %, exactly ±15 %) are not lost to float rounding.
    public const int HigherAmbitionMinPercentOver = 10;
    public const int SteadierMedianBandPercent = 15;
    public const int LowTimeThreshold = 2;
    public const int DefaultWeeklyHours = 5;
    public const double MinEffort = 0.5;
    public const double MaxEffort = 40;
    public const string NoSupportedAlternative = "no_supported_alternative";

    private const int MaxGapTasks = 2;
    private const int MaxTitleLength = 140;

    // Milestone buckets: this week, then days 8-30, 31-60 and 61-90, in weeks.
    private static readonly int[] Days = { 0, 30, 60, 90 };
    private static readonly int[] BucketWeeks = { 1, 3, 4, 4 };

    public static RoadmapContent Build(RoadmapInput input)
    {
        var hours = input.WeeklyEffortHours is > 0 ? input.WeeklyEffortHours.Value : DefaultWeeklyHours;
        var low = hours < LowTimeThreshold;
        var counter = 0;
        var options = new List<RoadmapOption> { BuildOption(RoadmapKeys.ClosestFit, input.Target, input, hours, ref counter) };
        var omitted = new List<RoadmapOmittedOption>();

        var higher = PickHigher(input);
        var steadier = PickSteadier(input, higher);
        if (higher != null)
        {
            options.Add(BuildOption(RoadmapKeys.HigherAmbition, higher, input, hours, ref counter));
        }
        else
        {
            omitted.Add(new RoadmapOmittedOption(RoadmapKeys.HigherAmbition, NoSupportedAlternative));
        }
        if (steadier != null)
        {
            options.Add(BuildOption(RoadmapKeys.SteadierTransition, steadier, input, hours, ref counter));
        }
        else
        {
            omitted.Add(new RoadmapOmittedOption(RoadmapKeys.SteadierTransition, NoSupportedAlternative));
        }

        return new RoadmapContent(options, omitted, hours, low ? RoadmapTemplates.LowTimeNote : null);
    }

    // ---- Option gates ----------------------------------------------------------

    /// <summary>Another candidate whose national median is at least 10 % above the target's and that has duty evidence.</summary>
    private static RoadmapCandidate? PickHigher(RoadmapInput input)
    {
        var target = input.Target.MedianAnnual;
        if (target is not > 0)
        {
            return null;
        }
        return input.Alternatives
            .Where(c => c.Code != input.Target.Code && c.DutyEvidenceCount >= 1 && c.MedianAnnual is { } m && m * 100 >= target.Value * (100 + HigherAmbitionMinPercentOver))
            .OrderByDescending(c => c.MedianAnnual).ThenBy(c => c.Code, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>Another candidate with projected change at least the target's, a median within ±15 %, and duty evidence.</summary>
    private static RoadmapCandidate? PickSteadier(RoadmapInput input, RoadmapCandidate? already)
    {
        var target = input.Target;
        if (target.MedianAnnual is not > 0 || target.ChangePercent == null)
        {
            return null;
        }
        var eligible = input.Alternatives
            .Where(c => c.Code != target.Code && c.DutyEvidenceCount >= 1 && c.ChangePercent is { } change && change >= target.ChangePercent
                && c.MedianAnnual is { } m && Math.Abs(m - target.MedianAnnual.Value) * 100 <= target.MedianAnnual.Value * SteadierMedianBandPercent)
            .OrderByDescending(c => c.ChangePercent).ThenBy(c => c.Code, StringComparer.Ordinal)
            .ToList();
        // A different occupation from the higher-ambition option when one exists; otherwise the same one is still the best-supported steadier path.
        return eligible.FirstOrDefault(c => c.Code != already?.Code) ?? eligible.FirstOrDefault();
    }

    // ---- Options ---------------------------------------------------------------

    private static RoadmapOption BuildOption(string key, RoadmapCandidate c, RoadmapInput input, double hours, ref int counter)
    {
        var rationale = new List<RoadmapRationale>();
        var missing = new List<string>();
        var target = input.Target;

        if (c.MedianAnnual is { } median && c.WageSourceId != null && c.WageRelease != null)
        {
            if (key == RoadmapKeys.ClosestFit)
            {
                rationale.Add(new($"The national median wage for {c.Title} is {Usd(median)} a year. A published median for the occupation, not a pay offer.",
                    c.WageSourceId, c.WageRelease));
            }
            else if (target.MedianAnnual is { } t)
            {
                var percent = (median / t - 1) * 100;
                var direction = percent >= 0 ? "above" : "below";
                rationale.Add(new($"The national median for {c.Title} ({Usd(median)}) is {Math.Abs(percent).ToString("0", CultureInfo.InvariantCulture)} % {direction} the median for {target.Title} ({Usd(t)}). A published median for the occupation, not a pay offer.",
                    c.WageSourceId, c.WageRelease));
            }
        }
        else
        {
            missing.Add($"No published national median wage for {c.Title}.");
        }

        if (c.ChangePercent is { } change && c.ProjectionSourceId != null && c.ProjectionRelease != null)
        {
            rationale.Add(new($"Projected employment change for {c.Title} is {change.ToString("0.0", CultureInfo.InvariantCulture)} %. A projection for the whole occupation.",
                c.ProjectionSourceId, c.ProjectionRelease));
        }
        else
        {
            missing.Add($"No published projected employment change for {c.Title}.");
        }

        if (c.DutyEvidenceCount > 0 && input.OnetRelease != null)
        {
            rationale.Add(new($"{c.DutyEvidenceCount} of your profile duties match published tasks for {c.Title}.", "onet", input.OnetRelease));
        }
        else if (key == RoadmapKeys.ClosestFit)
        {
            missing.Add(input.TargetMatchFound
                ? $"None of your profile duties match published tasks for {c.Title}."
                : "The duty evidence from your occupation match is unavailable.");
        }
        if (c.UnmatchedDuties.Count > 0)
        {
            missing.Add($"{c.UnmatchedDuties.Count} published tasks for {c.Title} have no matching example in your profile yet.");
        }
        if (!input.HasLocation)
        {
            missing.Add("Your goal has no location, so only national figures are used.");
        }
        if (!input.HasMarketBrief)
        {
            missing.Add("No market brief for this occupation yet.");
        }
        if (!input.HasPayAnalysis)
        {
            missing.Add("No pay analysis yet, so your pay target has not been compared with published figures.");
        }

        var assumptions = new List<string>
        {
            input.WeeklyEffortHours is > 0
                ? $"You have about {hours.ToString("0.#", CultureInfo.InvariantCulture)} hours a week for this plan."
                : $"You did not set weekly hours, so {DefaultWeeklyHours} hours a week is assumed.",
            "Wage and projection figures are national and describe the occupation, not you."
        };

        var drafts = DraftTasks(c, input, key == RoadmapKeys.ClosestFit);
        var ids = new Dictionary<string, string>();
        var ordered = new List<RoadmapTask>();
        foreach (var d in drafts)
        {
            ids[d.Key] = $"t{++counter}";
        }
        foreach (var d in drafts)
        {
            ordered.Add(new RoadmapTask(ids[d.Key], d.Title, d.Hours, d.DependsOn.Select(k => ids[k]).ToList()));
        }

        var (week, milestones) = Schedule(ordered, hours);
        return new RoadmapOption(key, c.Code, c.Title, rationale, assumptions, missing, RoadmapTemplates.TimelineNote, week, milestones);
    }

    private sealed record TaskDraft(string Key, string Title, double Hours, string[] DependsOn);

    /// <summary>Tasks picked from the fixed library by the gaps this option has.</summary>
    private static List<TaskDraft> DraftTasks(RoadmapCandidate c, RoadmapInput input, bool primary)
    {
        var drafts = new List<TaskDraft>();
        if (primary && !input.HasLocation)
        {
            drafts.Add(new TaskDraft("location", RoadmapTemplates.AddLocation, 0.5, Array.Empty<string>()));
        }
        if (primary && !input.HasMarketBrief)
        {
            drafts.Add(new TaskDraft("brief", RoadmapTemplates.OpenBrief, 0.5, Array.Empty<string>()));
        }
        if (primary && !input.HasPayAnalysis)
        {
            drafts.Add(new TaskDraft("pay", RoadmapTemplates.OpenPay, 0.5, Array.Empty<string>()));
        }

        var title = Clip(c.Title);
        drafts.Add(new TaskDraft("read", string.Format(CultureInfo.InvariantCulture, RoadmapTemplates.ReadDuties, title), 1, Array.Empty<string>()));
        drafts.Add(new TaskDraft("examples", RoadmapTemplates.WriteExamples, 2, new[] { "read" }));

        var learning = new List<string>();
        var index = 0;
        foreach (var gap in c.SkillGaps.Where(g => !string.IsNullOrWhiteSpace(g)).Take(MaxGapTasks))
        {
            var key = $"skill{++index}";
            drafts.Add(new(key, string.Format(CultureInfo.InvariantCulture, RoadmapTemplates.SkillGap, Clip(gap)), 2, new[] { "examples" }));
            learning.Add(key);
        }
        foreach (var duty in c.UnmatchedDuties.Where(d => !string.IsNullOrWhiteSpace(d)).Take(1))
        {
            drafts.Add(new TaskDraft("duty1", string.Format(CultureInfo.InvariantCulture, RoadmapTemplates.DutyGap, Clip(duty)), 1.5, new[] { "examples" }));
            learning.Add("duty1");
        }
        drafts.Add(new TaskDraft("talk", string.Format(CultureInfo.InvariantCulture, RoadmapTemplates.Conversation, title), 1.5, new[] { "examples" }));
        drafts.Add(new TaskDraft("profile", RoadmapTemplates.UpdateProfile, 1, learning.Prepend("examples").ToArray()));
        drafts.Add(new TaskDraft("review", RoadmapTemplates.Review, 0.5, new[] { "profile" }));
        return drafts;
    }

    // ---- Scheduling and validation ---------------------------------------------

    /// <summary>
    /// Fits tasks, in dependency order, into this week and the 30/60/90-day milestones. A bucket holds
    /// the hours it has; the rest spills later. Under 2 hours a week each week holds at most one task.
    /// The final milestone takes whatever is left, so nothing is dropped.
    /// </summary>
    public static (IReadOnlyList<RoadmapTask> ThisWeek, IReadOnlyList<RoadmapMilestone> Milestones) Schedule(
        IReadOnlyList<RoadmapTask> tasks, double weeklyHours)
    {
        var ordered = TopologicalOrder(tasks);
        var low = weeklyHours < LowTimeThreshold;
        var buckets = new List<List<RoadmapTask>> { new(), new(), new(), new() };
        var bucket = 0;
        var used = 0.0;
        foreach (var task in ordered)
        {
            while (bucket < buckets.Count - 1 && buckets[bucket].Count > 0
                && (low ? buckets[bucket].Count >= BucketWeeks[bucket] : used + task.EffortHours > weeklyHours * BucketWeeks[bucket]))
            {
                bucket++;
                used = 0;
            }
            buckets[bucket].Add(task);
            used += task.EffortHours;
        }
        return (buckets[0], Enumerable.Range(1, 3).Select(i => new RoadmapMilestone(Days[i], buckets[i])).ToList());
    }

    /// <summary>Stable topological order. Throws <see cref="RoadmapGraphException"/> for unknown ids, duplicates and cycles.</summary>
    public static IReadOnlyList<RoadmapTask> TopologicalOrder(IReadOnlyList<RoadmapTask> tasks)
    {
        var byId = new Dictionary<string, RoadmapTask>(StringComparer.Ordinal);
        foreach (var task in tasks)
        {
            if (!byId.TryAdd(task.Id, task))
            {
                throw new RoadmapGraphException("unknown_dependency", $"Task id {task.Id} is used twice.");
            }
        }
        foreach (var task in tasks)
        {
            var unknown = task.DependsOn.FirstOrDefault(d => !byId.ContainsKey(d));
            if (unknown != null)
            {
                throw new RoadmapGraphException("unknown_dependency", $"Task {task.Id} depends on unknown task {unknown}.");
            }
        }

        var done = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<RoadmapTask>();
        while (result.Count < tasks.Count)
        {
            var next = tasks.FirstOrDefault(t => !done.Contains(t.Id) && t.DependsOn.All(done.Contains));
            if (next == null)
            {
                throw new RoadmapGraphException("cycle", "The task dependencies contain a cycle.");
            }
            done.Add(next.Id);
            result.Add(next);
        }
        return result;
    }

    // ---- Text helpers ----------------------------------------------------------

    private static string Usd(double value) => "$" + Math.Round(value).ToString("N0", CultureInfo.InvariantCulture);

    private static string Clip(string text)
    {
        var trimmed = text.Trim().TrimEnd('.');
        return trimmed.Length <= MaxTitleLength ? trimmed : trimmed[..(MaxTitleLength - 1)].TrimEnd() + "…";
    }
}
