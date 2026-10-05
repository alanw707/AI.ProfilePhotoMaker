using AI.ProfilePhotoMaker.API.Services.Career;
using FluentAssertions;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services.Career;

/// <summary>The pure replan diff (#388, ADR 0017).</summary>
public class ReplanDiffTests
{
    private static CurrentPlanTask Current(string id, string title, double hours = 1, int day = 0, string[]? deps = null,
        string origin = "generated", bool done = false, bool output = false) =>
        new(id, title, origin, hours, day, deps ?? Array.Empty<string>(), done, output);

    private static PlanTask Plan(string title, double hours = 1, int day = 0, params string[] deps) => new(title, hours, day, deps);

    [Fact]
    public void IdenticalPlansProduceNoChanges()
    {
        var result = ReplanDiff.Compute(new[] { Current("t1", "A"), Current("t2", "B", deps: new[] { "A" }) },
            new[] { Plan("A"), Plan("B", 1, 0, "A") });

        result.Changes.Should().BeEmpty();
        result.Preserved.Should().BeEmpty();
    }

    [Fact]
    public void ChangedTasksListFieldLevelBeforeAndAfterWithARationale()
    {
        var result = ReplanDiff.Compute(new[] { Current("t1", "A", 1, 0), Current("t2", "B", 2, 30) },
            new[] { Plan("A", 1.5, 0), Plan("B", 2, 60, "A") });

        var a = result.Changes.Single(c => c.Title == "A");
        a.Kind.Should().Be("changed");
        a.TaskId.Should().Be("t1");
        a.Fields.Should().Equal(new ReplanField("effort", "1", "1.5"));
        var b = result.Changes.Single(c => c.Title == "B");
        b.Fields.Should().Equal(new ReplanField("milestoneDay", "30", "60"), new ReplanField("dependencies", "none", "A"));
        result.Changes.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.Rationale));
        result.Changes.Select(c => c.Id).Should().Equal("c1", "c2");
    }

    [Fact]
    public void AddedAndRemovedTasksCarryTheirValues()
    {
        var result = ReplanDiff.Compute(new[] { Current("t1", "Old", 2, 30) }, new[] { Plan("New", 1, 0) });

        var removed = result.Changes.Single(c => c.Kind == "removed");
        removed.TaskId.Should().Be("t1");
        removed.Fields.Should().Contain(new ReplanField("title", "Old", null)).And.Contain(new ReplanField("effort", "2", null));
        var added = result.Changes.Single(c => c.Kind == "added");
        added.TaskId.Should().BeNull();
        added.Fields.Should().Contain(new ReplanField("title", null, "New")).And.Contain(new ReplanField("effort", null, "1"));
    }

    [Fact]
    public void DoneOutputAndHumanTasksArePreservedAndNeverRemovedOrChanged()
    {
        var current = new[]
        {
            Current("t1", "Done one", done: true), Current("t2", "Has output", output: true),
            Current("h1", "My own", origin: "human"), Current("t3", "Plain")
        };

        var result = ReplanDiff.Compute(current, new[] { Plan("Done one", 9, 90), Plan("Brand new") });

        result.Preserved.Should().BeEquivalentTo(new[]
        {
            new ReplanPreserved("t1", "Done one", "done"), new ReplanPreserved("t2", "Has output", "has_output"), new ReplanPreserved("h1", "My own", "human")
        });
        result.Changes.Select(c => (c.Kind, c.Title)).Should().BeEquivalentTo(new[] { ("removed", "Plain"), ("added", "Brand new") });
    }

    [Fact]
    public void TheDiffIsDeterministic()
    {
        var current = new[] { Current("t1", "A", 1, 0), Current("t2", "B", 2, 30), Current("t3", "C") };
        var proposed = new[] { Plan("A", 2, 0), Plan("D") };

        ReplanDiff.Compute(current, proposed).Should().BeEquivalentTo(ReplanDiff.Compute(current, proposed), o => o.WithStrictOrdering());
    }

    [Fact]
    public void ApplyingOnlyAcceptedChangesLeavesTheRestAndKeepsIds()
    {
        var current = new[] { Current("t1", "A", 1, 0), Current("t2", "B", 2, 30, new[] { "A" }), Current("t3", "C") };
        var proposed = new[] { Plan("A", 3, 0), Plan("B", 2, 30, "A"), Plan("N", 1, 0, "A") };
        var diff = ReplanDiff.Compute(current, proposed);
        var aChange = diff.Changes.Single(c => c.Title == "A").Id;
        var added = diff.Changes.Single(c => c.Kind == "added").Id;

        var result = ReplanDiff.Apply(current, proposed, diff.Changes, new HashSet<string> { aChange, added }, 7);

        result.Select(p => p.Id).Should().Equal("t1", "t2", "t3", "t7");
        result.Single(p => p.Id == "t1").EffortHours.Should().Be(3);
        result.Single(p => p.Id == "t7").DependsOn.Should().Equal("t1");
        result.Should().Contain(p => p.Title == "C"); // the removal was not accepted
    }

    [Fact]
    public void ApplyingNothingChangesNothing()
    {
        var current = new[] { Current("t1", "A", 1, 0), Current("t2", "B", 2, 30, new[] { "A" }) };
        var diff = ReplanDiff.Compute(current, new[] { Plan("A", 4) });

        var result = ReplanDiff.Apply(current, new[] { Plan("A", 4) }, diff.Changes, new HashSet<string>(), 3);

        result.Should().BeEquivalentTo(new[]
        {
            new PlacedTask("t1", "A", 1, 0, Array.Empty<string>()), new PlacedTask("t2", "B", 2, 30, new[] { "t1" })
        }, o => o.WithStrictOrdering());
    }

    [Fact]
    public void RemovingATaskDropsDependenciesOnIt()
    {
        var current = new[] { Current("t1", "A"), Current("t2", "B", deps: new[] { "A" }) };
        var proposed = new[] { Plan("B") };
        var diff = ReplanDiff.Compute(current, proposed);

        var result = ReplanDiff.Apply(current, proposed, diff.Changes, diff.Changes.Select(c => c.Id).ToHashSet(), 3);

        result.Should().ContainSingle().Which.DependsOn.Should().BeEmpty();
    }

    [Fact]
    public void HelpTextCoversEveryTemplateAndHumanTasksAndIsFreeOfPayOrCourseWording()
    {
        foreach (var title in new[]
        {
            string.Format(RoadmapTemplates.ReadDuties, "X"), RoadmapTemplates.WriteExamples, string.Format(RoadmapTemplates.SkillGap, "X"),
            string.Format(RoadmapTemplates.DutyGap, "X"), string.Format(RoadmapTemplates.Conversation, "X"), RoadmapTemplates.UpdateProfile,
            RoadmapTemplates.AddLocation, RoadmapTemplates.OpenBrief, RoadmapTemplates.OpenPay, RoadmapTemplates.Review
        })
        {
            RoadmapHelp.For(title, "generated").Should().NotBe(RoadmapHelp.Fallback, title);
        }
        RoadmapHelp.For("Anything", "human").Should().Be(RoadmapHelp.Human);
        foreach (var text in RoadmapHelp.All)
        {
            foreach (var word in new[] { "raise", "salary increase", "course fee", "certified", "certification", "tuition", "enroll" })
            {
                text.Should().NotContainEquivalentOf(word);
            }
        }
    }
}
