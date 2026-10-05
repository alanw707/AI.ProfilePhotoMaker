using AI.ProfilePhotoMaker.API.Services.Career;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace AI.ProfilePhotoMaker.API.Tests.Services.Career;

/// <summary>
/// The matcher runs against the real O*NET snapshot with synthetic, fictional profiles
/// (ADR 0010): three occupation families, nonstandard titles, contradictory duties and
/// profiles with nothing to match.
/// </summary>
public class OccupationMatcherTests
{
    private static readonly OccupationReferenceData Reference = new EmbeddedOccupationReference().Data!;
    private readonly ITestOutputHelper _output;

    public OccupationMatcherTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private OccupationMatchResult Match(OccupationMatchInput input)
    {
        var result = OccupationMatcher.Match(Reference, input);
        foreach (var c in result.Candidates)
        {
            _output.WriteLine($"{c.Code} {c.Title} {c.Strength} duty={c.Evidence.Count(e => e.Kind == "duty")} skill={c.Evidence.Count(e => e.Kind == "skill")}");
        }
        return result;
    }

    private static readonly string[] SoftwareDuties =
    {
        "Designed and developed backend software systems and REST APIs for an invoicing platform",
        "Modified existing software to correct errors and improve performance",
        "Wrote documentation and developed software testing and validation procedures",
        "Analyzed user needs and software requirements to determine feasibility of design"
    };

    private static readonly string[] NurseDuties =
    {
        "Assessed patients' health problems and needs and developed and implemented nursing care plans",
        "Administered medications to patients and monitored patients for reactions or side effects",
        "Recorded patients' medical information and vital signs",
        "Consulted and coordinated with healthcare team members to assess, plan, implement and evaluate patient care plans"
    };

    private static readonly string[] FrontDeskDuties =
    {
        "Scheduled patient appointments and answered telephone calls from patients",
        "Greeted patients and updated patient records and charts",
        "Transcribed medical correspondence and processed billing and insurance forms",
        "Maintained medical records and filing systems for the clinic"
    };

    private static OccupationMatchInput Input(string title, IEnumerable<string> duties, params string[] skills) =>
        new(title, null, null, skills, duties.ToList());

    private static int Rank(OccupationMatchResult result, string code) =>
        result.Candidates.Select((c, i) => (c.Code, Rank: i + 1)).FirstOrDefault(x => x.Code == code).Rank;

    [Fact]
    public void SoftwareDutiesRankSoftwareDevelopersInTheTopTwo()
    {
        var result = Match(Input("Software Engineer", SoftwareDuties, "Programming", "Systems Analysis"));

        result.Status.Should().Be("candidates");
        Rank(result, "15-1252.00").Should().BeInRange(1, 2);
        result.Ambiguous.Should().BeFalse();
        result.MatcherVersion.Should().Be("duty-overlap-2");
    }

    [Fact]
    public void NurseDutiesRankRegisteredNursesInTheTopTwo()
    {
        var result = Match(Input("Staff Nurse", NurseDuties, "Active Listening", "Service Orientation"));

        result.Status.Should().Be("candidates");
        Rank(result, "29-1141.00").Should().BeInRange(1, 2);
    }

    [Fact]
    public void FrontDeskSchedulingDutiesRankAMedicalAdminOccupationInTheTopThree()
    {
        var result = Match(Input("Front Desk Coordinator", FrontDeskDuties, "Microsoft Office"));

        result.Status.Should().Be("candidates");
        result.Candidates.Take(3).Select(c => c.Code).Should().Contain(
            code => code == "43-6013.00" || code == "43-6014.00" || code == "43-4171.00" || code == "29-2072.00");
    }

    [Theory]
    [InlineData("Code Ninja")]
    [InlineData("Chief Happiness Officer")]
    public void NonstandardTitlesAreStillMatchedByDuties(string title)
    {
        var result = Match(Input(title, SoftwareDuties, "Programming", "Systems Analysis"));

        Rank(result, "15-1252.00").Should().BeInRange(1, 2);
        result.Candidates.Single(c => c.Code == "15-1252.00").TitleMatched.Should().BeFalse();
    }

    [Fact]
    public void ATitleAloneNeverProducesACandidate()
    {
        var result = Match(Input("Software Developer", Array.Empty<string>()));

        result.Status.Should().Be("unsupported");
        result.Candidates.Should().BeEmpty();
        result.Guidance.Should().Contain("responsibilities").And.Contain("highlights");
    }

    [Fact]
    public void ATitleMatchOnlyAddsABonusToOccupationsWithDutyEvidence()
    {
        var result = Match(Input("Software Developers", SoftwareDuties, "Programming"));

        result.Candidates.Single(c => c.Code == "15-1252.00").TitleMatched.Should().BeTrue();
        result.Candidates.Where(c => c.TitleMatched).Should().OnlyContain(c => c.Evidence.Any(e => e.Kind == "duty"));
    }

    [Fact]
    public void ContradictoryDutiesAreAmbiguousAcrossMajorGroups()
    {
        var duties = NurseDuties.Concat(SoftwareDuties).ToArray();

        var result = Match(Input("Consultant", duties));

        result.Status.Should().Be("candidates");
        result.Ambiguous.Should().BeTrue();
        var top = result.Candidates.Take(2).Select(c => c.Code[..2]).ToList();
        top.Distinct().Should().HaveCount(2);
    }

    [Fact]
    public void ContradictoryDutiesAreAmbiguousEvenWhenOneFamilyTakesTheTopTwoPlaces()
    {
        // Live-review profile: two nursing occupations rank first and second, but software
        // is nearly as well supported, so the user must still be asked.
        var duties = new[]
        {
            "Monitored patient vital signs and recorded observations in medical records",
            "Administered medications and treatments as prescribed by physicians",
            "Assessed patient health problems and developed nursing care plans",
            "Designed and developed software applications and modified existing programs to meet user needs",
            "Analyzed user requirements and tested software systems to correct errors",
            "Wrote and maintained documentation for application code and database systems"
        };

        var result = Match(Input("Hybrid specialist", duties, "Python", "Patient care"));

        result.Ambiguous.Should().BeTrue();
        var choices = OccupationMatcher.ClarificationChoices(result, 3);
        choices.Select(c => c.Code[..2]).Should().OnlyHaveUniqueItems().And.Contain("29").And.Contain("15");
    }

    [Fact]
    public void ClarificationChoicesTakeTheLeadingOccupationOfEachFamilyInRankOrder()
    {
        var result = Match(Input("Consultant", NurseDuties.Concat(SoftwareDuties)));

        var choices = OccupationMatcher.ClarificationChoices(result, 3);

        choices.Should().NotBeEmpty();
        choices[0].Code.Should().Be(result.Candidates[0].Code);
        choices.Select(c => c.Code[..2]).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void ASingleFamilyResultIsNotAmbiguous()
    {
        Match(Input("Staff nurse", NurseDuties)).Ambiguous.Should().BeFalse();
    }

    [Fact]
    public void GibberishDutiesAreUnsupported()
    {
        var result = Match(Input("Wizard", new[] { "Flarbing the wibble quantum zorp", "Snorkeling gribbles with frumious bandersnatch" }, "Zorping"));

        result.Status.Should().Be("unsupported");
        result.Candidates.Should().BeEmpty();
    }

    [Fact]
    public void CandidatesCarryEvidenceGapsAndAtMostFiveEntries()
    {
        var result = Match(Input("Software Engineer", SoftwareDuties, "Programming", "Amazon DynamoDB", "Phlebotomy"));

        result.Candidates.Count.Should().BeLessThanOrEqualTo(5);
        var developer = result.Candidates.Single(c => c.Code == "15-1252.00");
        developer.Evidence.Should().Contain(e => e.Kind == "duty" && e.ProfileField == "highlights" && e.ReferenceKind == "task" && e.ReferenceId != null);
        developer.Evidence.Should().Contain(e => e.Kind == "skill" && e.ProfileText == "Amazon DynamoDB" && e.ReferenceKind == "technology");
        developer.MissingEvidence.Should().NotBeEmpty().And.HaveCountLessThanOrEqualTo(5);
        developer.KnownGaps.Should().NotBeEmpty().And.HaveCountLessThanOrEqualTo(5);
        developer.UnsupportedSkills.Should().Contain("Phlebotomy").And.NotContain("Programming");
        new[] { "strong", "moderate", "weak" }.Should().Contain(developer.Strength);
    }

    [Fact]
    public void SameInputGivesTheSameResult()
    {
        var input = Input("Software Engineer", SoftwareDuties, "Programming");

        System.Text.Json.JsonSerializer.Serialize(OccupationMatcher.Match(Reference, input))
            .Should().Be(System.Text.Json.JsonSerializer.Serialize(OccupationMatcher.Match(Reference, input)));
    }

    [Fact]
    public void LocationPayAndWorkArrangementAreNotMatcherInputs()
    {
        // Compile-time and reflection guard: the input type has no such fields, so they cannot sway a match.
        typeof(OccupationMatchInput).GetProperties().Select(p => p.Name).Should()
            .BeEquivalentTo("CurrentTitle", "Industry", "Summary", "Skills", "Highlights");
        typeof(OccupationMatcher).GetMethod(nameof(OccupationMatcher.Match))!.GetParameters().Select(p => p.ParameterType).Should()
            .Equal(typeof(OccupationReferenceData), typeof(OccupationMatchInput));
    }
}
