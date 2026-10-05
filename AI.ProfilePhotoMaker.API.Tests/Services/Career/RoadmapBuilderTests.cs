using AI.ProfilePhotoMaker.API.Services.Career;
using FluentAssertions;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services.Career;

/// <summary>The roadmap generator is pure and evidence-gated (ADR 0016): options, effort fitting, DAG, wording.</summary>
public class RoadmapBuilderTests
{
    private static readonly string[] Forbidden = { "raise", "salary increase", "course fee", "certified", "certificate", "tuition", "enroll" };

    private static RoadmapCandidate Candidate(
        string code, double? median = 100000, double? change = 5, int duties = 2,
        string[]? skillGaps = null, string[]? unmatched = null) =>
        new(code, $"Title {code}", median, median == null ? null : "oews", median == null ? null : "2025-05",
            change, change == null ? null : "projections", change == null ? null : "2025-35", duties,
            unmatched ?? new[] { "Test software for errors" }, skillGaps ?? new[] { "Unit testing", "Version control" });

    private static RoadmapInput Input(
        RoadmapCandidate? target = null, RoadmapCandidate[]? alternatives = null, int? hours = 6,
        bool location = true, bool brief = true, bool pay = true) =>
        new(target ?? Candidate("15-1252.00"), true, alternatives ?? Array.Empty<RoadmapCandidate>(), hours, location, brief, pay, "30.0");

    private static IEnumerable<RoadmapTask> AllTasks(RoadmapOption o) => o.ThisWeek.Concat(o.Milestones.SelectMany(m => m.Tasks));

    private static IEnumerable<string> AllText(RoadmapContent c) =>
        c.Options.SelectMany(o => AllTasks(o).Select(t => t.Title)
            .Concat(o.Rationale.Select(r => r.Text)).Concat(o.Assumptions).Concat(o.MissingEvidence).Append(o.TimelineNote))
            .Concat(c.LowTimeNote == null ? Array.Empty<string>() : new[] { c.LowTimeNote });

    // ---- Option gating ------------------------------------------------------------

    [Fact]
    public void ClosestFitIsAlwaysProducedAndUnsupportedOptionsAreOmittedWithAReason()
    {
        var content = RoadmapBuilder.Build(Input());

        content.Options.Select(o => o.Key).Should().Equal("closest_fit");
        content.Omitted.Should().BeEquivalentTo(new[]
        {
            new RoadmapOmittedOption("higher_ambition", "no_supported_alternative"),
            new RoadmapOmittedOption("steadier_transition", "no_supported_alternative")
        });
        content.Options[0].TimelineNote.Should().Be("A scenario, not a promise.");
    }

    [Fact]
    public void ClosestFitStillBuildsWhenNoFigureIsPublished()
    {
        var content = RoadmapBuilder.Build(Input(Candidate("15-1252.00", median: null, change: null)));

        content.Options.Should().ContainSingle();
        content.Options[0].MissingEvidence.Should().Contain(m => m.Contains("median wage"));
        content.Options[0].MissingEvidence.Should().Contain(m => m.Contains("projected employment"));
    }

    [Theory]
    [InlineData(110000, 2, true)]   // exactly +10 % with duty evidence
    [InlineData(109999, 2, false)]  // just under the threshold
    [InlineData(150000, 0, false)]  // paid more but no duty evidence
    public void HigherAmbitionNeedsTenPercentMoreAndDutyEvidence(double median, int duties, bool expected)
    {
        var content = RoadmapBuilder.Build(Input(Candidate("15-1252.00", 100000),
            new[] { Candidate("11-3021.00", median, change: 1, duties: duties) }));

        content.Options.Any(o => o.Key == "higher_ambition").Should().Be(expected);
        content.Omitted.Any(o => o.Key == "higher_ambition" && o.Reason == "no_supported_alternative").Should().Be(!expected);
    }

    [Theory]
    [InlineData(115000, 5, 2, true)]    // +15 % band edge, equal change
    [InlineData(85000, 9, 2, true)]     // -15 % band edge, higher change
    [InlineData(115001, 9, 2, false)]   // above the band
    [InlineData(84999, 9, 2, false)]    // below the band
    [InlineData(100000, 4.9, 2, false)] // slower growth than the target
    [InlineData(100000, 9, 0, false)]   // no duty evidence
    public void SteadierTransitionNeedsGrowthAtLeastTheTargetsMedianWithinBandAndEvidence(double median, double change, int duties, bool expected)
    {
        var content = RoadmapBuilder.Build(Input(Candidate("15-1252.00", 100000, 5),
            new[] { Candidate("15-1251.00", median, change, duties) }));

        content.Options.Any(o => o.Key == "steadier_transition").Should().Be(expected);
    }

    [Fact]
    public void SupportedAlternativesCiteTheirFiguresAndSourceReleases()
    {
        var content = RoadmapBuilder.Build(Input(Candidate("15-1252.00", 100000, 5), new[]
        {
            Candidate("11-3021.00", 130000, 1),
            Candidate("15-1251.00", 95000, 8)
        }));

        content.Options.Select(o => o.Key).Should().Equal("closest_fit", "higher_ambition", "steadier_transition");
        content.Omitted.Should().BeEmpty();
        var higher = content.Options.Single(o => o.Key == "higher_ambition");
        higher.OccupationCode.Should().Be("11-3021.00");
        higher.Rationale.Should().Contain(r => r.SourceId == "oews" && r.Release == "2025-05" && r.Text.Contains("$130,000") && r.Text.Contains("30 %"));
        higher.Rationale.Should().Contain(r => r.SourceId == "onet" && r.Release == "30.0");
        content.Options.Single(o => o.Key == "steadier_transition").OccupationCode.Should().Be("15-1251.00");
    }

    [Fact]
    public void NoAlternativeIsComparedWhenTheTargetHasNoPublishedMedian()
    {
        var content = RoadmapBuilder.Build(Input(Candidate("15-1252.00", median: null, change: null),
            new[] { Candidate("11-3021.00", 900000, 50) }));

        content.Options.Select(o => o.Key).Should().Equal("closest_fit");
    }

    [Fact]
    public void SparseEvidenceIsListedAsMissingNotInvented()
    {
        var content = RoadmapBuilder.Build(Input(location: false, brief: false, pay: false));
        var missing = content.Options[0].MissingEvidence;

        missing.Should().Contain(m => m.Contains("No pay analysis"));
        missing.Should().Contain(m => m.Contains("No market brief"));
        missing.Should().Contain(m => m.Contains("no location"));
        AllTasks(content.Options[0]).Select(t => t.Title).Should().Contain(RoadmapTemplates.OpenPay);
    }

    // ---- Effort fitting -----------------------------------------------------------

    [Fact]
    public void WeekOneEffortFitsTheWeeklyHoursAndTheRestSpillsLater()
    {
        var content = RoadmapBuilder.Build(Input(hours: 3, brief: false, pay: false, location: false));
        var option = content.Options[0];

        option.ThisWeek.Sum(t => t.EffortHours).Should().BeLessThanOrEqualTo(3);
        option.Milestones.Select(m => m.Day).Should().Equal(30, 60, 90);
        option.Milestones[0].Tasks.Should().NotBeEmpty();
        content.LowTimeNote.Should().BeNull();
        // Spilling never drops a task.
        AllTasks(option).Select(t => t.Id).Should().OnlyHaveUniqueItems();
        AllTasks(option).Should().HaveCount(RoadmapBuilder.Build(Input(hours: 40, brief: false, pay: false, location: false)).Options[0]
            .ThisWeek.Count);
    }

    [Fact]
    public void LowTimeAvailabilityKeepsOneTaskPerWeekAndSaysSo()
    {
        var content = RoadmapBuilder.Build(Input(hours: 1));
        var option = content.Options[0];

        content.LowTimeNote.Should().NotBeNullOrEmpty().And.Contain("under 2 hours");
        option.ThisWeek.Should().HaveCount(1);
        option.Milestones[0].Tasks.Count.Should().BeLessThanOrEqualTo(3);
        option.Milestones[1].Tasks.Count.Should().BeLessThanOrEqualTo(4);
        option.Milestones[2].Tasks.Count.Should().BeLessThanOrEqualTo(4);
        option.Milestones[0].Tasks.Should().NotBeEmpty();
        content.WeeklyEffortHours.Should().Be(1);
    }

    [Fact]
    public void UnsetHoursAssumeTheDefaultAndSayIt()
    {
        var content = RoadmapBuilder.Build(Input(hours: null));

        content.WeeklyEffortHours.Should().Be(RoadmapBuilder.DefaultWeeklyHours);
        content.Options[0].Assumptions.Should().Contain(a => a.Contains("did not set weekly hours"));
    }

    [Fact]
    public void EveryTaskComesAfterItsDependencies()
    {
        var content = RoadmapBuilder.Build(Input(hours: 2));
        var position = AllTasks(content.Options[0]).Select((t, i) => (t.Id, i)).ToDictionary(x => x.Id, x => x.i);

        foreach (var task in AllTasks(content.Options[0]))
        {
            task.DependsOn.Should().OnlyContain(d => position[d] < position[task.Id]);
        }
    }

    // ---- Dependency graph ---------------------------------------------------------

    [Fact]
    public void CyclesAndUnknownDependenciesAreRejected()
    {
        var cycle = new[]
        {
            new RoadmapTask("a", "A", 1, new[] { "b" }),
            new RoadmapTask("b", "B", 1, new[] { "a" })
        };
        var unknown = new[] { new RoadmapTask("a", "A", 1, new[] { "zzz" }) };

        FluentActions.Invoking(() => RoadmapBuilder.TopologicalOrder(cycle)).Should().Throw<RoadmapGraphException>().Which.Kind.Should().Be("cycle");
        FluentActions.Invoking(() => RoadmapBuilder.Schedule(cycle, 5)).Should().Throw<RoadmapGraphException>().Which.Kind.Should().Be("cycle");
        FluentActions.Invoking(() => RoadmapBuilder.TopologicalOrder(unknown)).Should().Throw<RoadmapGraphException>().Which.Kind.Should().Be("unknown_dependency");
    }

    [Fact]
    public void ASelfDependencyIsACycle()
    {
        FluentActions.Invoking(() => RoadmapBuilder.TopologicalOrder(new[] { new RoadmapTask("a", "A", 1, new[] { "a" }) }))
            .Should().Throw<RoadmapGraphException>().Which.Kind.Should().Be("cycle");
    }

    // ---- Wording ------------------------------------------------------------------

    [Fact]
    public void TheTemplateLibraryHasNoPayUpliftPaidCourseOrCredentialWording()
    {
        foreach (var text in RoadmapTemplates.All)
        {
            foreach (var word in Forbidden)
            {
                text.Should().NotContainEquivalentOf(word);
            }
        }
    }

    [Fact]
    public void GeneratedTextNeverPromisesPayAPaidCourseOrACredential()
    {
        var content = RoadmapBuilder.Build(Input(Candidate("15-1252.00", 100000, 5), new[]
        {
            Candidate("11-3021.00", 130000, 1), Candidate("15-1251.00", 95000, 8)
        }, hours: 1, location: false, brief: false, pay: false));

        foreach (var text in AllText(content))
        {
            foreach (var word in Forbidden)
            {
                text.Should().NotContainEquivalentOf(word);
            }
        }
    }

    [Fact]
    public void TheSameInputGivesTheSameRoadmap()
    {
        var input = Input(hours: 4);

        System.Text.Json.JsonSerializer.Serialize(RoadmapBuilder.Build(input)).Should()
            .Be(System.Text.Json.JsonSerializer.Serialize(RoadmapBuilder.Build(input)));
    }
}
