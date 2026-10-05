using AI.ProfilePhotoMaker.API.Services.Career;
using FluentAssertions;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration.Career;

/// <summary>
/// Fixture-only quality checks for the built-in extractor and parser. Measured
/// results are recorded in docs/career/resume-import-quality.md.
/// </summary>
public class ResumeFactExtractorTests
{
    private static readonly DateTime AsOf = new(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ExtractorIsDeterministic()
    {
        var first = ResumeFactExtractor.Extract(ResumeFixtures.MorganPages.Select(p => string.Join("\n", p)).ToList(), pasted: false, AsOf);
        var second = ResumeFactExtractor.Extract(ResumeFixtures.MorganPages.Select(p => string.Join("\n", p)).ToList(), pasted: false, AsOf);

        first.Should().BeEquivalentTo(second, o => o.WithStrictOrdering());
    }

    [Fact]
    public void PresentEndDateCountsUpToTheClockNotARealYear()
    {
        var items = ResumeFactExtractor.Extract(
            new[] { "Experience\nOperations Lead, Acme Clinics, 2020 - Present" }, pasted: false, AsOf);

        items.Single(i => i.Field == "yearsExperience").Value.Should().Be("6");
    }

    [Fact]
    public void NoTextYieldsNoItems()
    {
        ResumeFactExtractor.Extract(new[] { "" }, pasted: false, AsOf).Should().BeEmpty();
    }

    [Fact]
    public async Task ParserReadsTextAndPageCountFromBothFormats()
    {
        var parser = new DependencyFreeResumeParser();

        var pdf = await parser.ParseAsync(ResumeFixtures.Pdf(ResumeFixtures.MorganPages), ResumeFormat.Pdf);
        var flate = await parser.ParseAsync(ResumeFixtures.Pdf(ResumeFixtures.MorganPages, flate: true), ResumeFormat.Pdf);
        var image = await parser.ParseAsync(ResumeFixtures.Pdf(new[] { new[] { "x" } }, imageOnly: true), ResumeFormat.Pdf);
        var docx = await parser.ParseAsync(ResumeFixtures.Docx("One", ResumeFixtures.PageBreak, "Two"), ResumeFormat.Docx);

        pdf.PageCount.Should().Be(2);
        pdf.Pages[0].Should().Contain("Senior Operations Lead");
        pdf.Pages[1].Should().Contain("SQL, Tableau");
        flate.Pages.Should().Equal(pdf.Pages);
        image.PageCount.Should().Be(1);
        image.Pages.Should().OnlyContain(p => string.IsNullOrWhiteSpace(p));
        docx.PageCount.Should().Be(2);
        docx.Pages[1].Should().Contain("Two");
    }

    [Fact]
    public void MeasuredFieldCountsMatchTheDocumentedFixtures()
    {
        var pages = ResumeFixtures.MorganPages.Select(p => string.Join("\n", p)).ToList();

        var counts = ResumeFactExtractor.Extract(pages, pasted: false, AsOf)
            .GroupBy(i => i.Field).ToDictionary(g => g.Key, g => g.Count());

        counts.Should().Equal(new Dictionary<string, int>
        {
            ["currentTitle"] = 1, ["location"] = 1, ["yearsExperience"] = 1,
            ["summary"] = 1, ["skills"] = 3, ["highlights"] = 3
        });
    }
}
