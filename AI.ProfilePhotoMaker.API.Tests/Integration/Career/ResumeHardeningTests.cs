using System.Diagnostics;
using System.Net;
using System.Text;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Services.Career;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration.Career;

/// <summary>Code-review hardening for #379: hostile files, failure cleanup, consent version.</summary>
public class ResumeHardeningTests
{
    private static byte[] Valid => ResumeFixtures.Pdf(ResumeFixtures.MorganPages);
    private static readonly TimeSpan Fast = TimeSpan.FromSeconds(2);

    // ---- Parser: object flood, overflow, budgets --------------------------------

    [Fact]
    public async Task ObjectFloodPdfIsRejectedQuicklyAsParserError()
    {
        using var factory = new ResumeImportFactory();
        var user = new CareerClient(factory);
        var flood = ResumeFixtures.ObjectFloodPdf(3 * 1024 * 1024);

        var watch = Stopwatch.StartNew();
        var resume = await CareerClient.ReadDataAsync(await user.UploadResumeAsync(flood), 201);
        watch.Stop();

        watch.Elapsed.Should().BeLessThan(Fast);
        resume.GetProperty("state").GetString().Should().Be("failed");
        resume.GetProperty("failureCode").GetString().Should().Be("ParserError");
        factory.Storage.Count.Should().Be(0);
    }

    [Fact]
    public async Task HugeObjectNumbersDoNotOverflow()
    {
        var parser = new DependencyFreeResumeParser();

        var result = await parser.ParseAsync(ResumeFixtures.HugeNumberPdf(), ResumeFormat.Pdf);

        result.Pages.All(string.IsNullOrWhiteSpace).Should().BeTrue();
    }

    [Fact]
    public async Task CancelledTokenStopsTheParser()
    {
        var parser = new DependencyFreeResumeParser();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => parser.ParseAsync(ResumeFixtures.Pdf(ResumeFixtures.MorganPages), ResumeFormat.Pdf, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task OneStreamReferencedManyTimesIsDecodedOnceAndRejectedForSize()
    {
        using var factory = new ResumeImportFactory();
        var user = new CareerClient(factory);
        var shared = ResumeFixtures.FlateStream(ResumeFixtures.RepeatedTextContent(20_000));
        var pdf = ResumeFixtures.PdfWithSharedStreams(pages: 20, referencesPerPage: 50, shared);

        var watch = Stopwatch.StartNew();
        var error = await CareerClient.ReadErrorAsync(await user.UploadResumeAsync(pdf), 413);
        watch.Stop();

        watch.Elapsed.Should().BeLessThan(Fast);
        error.GetProperty("code").GetString().Should().Be("CareerResumeTooLarge");
        factory.Storage.Count.Should().Be(0);
    }

    [Fact]
    public async Task TotalDecodedBytesAcrossStreamsAreBudgeted()
    {
        using var factory = new ResumeImportFactory();
        var user = new CareerClient(factory);
        // Each stream is under the old per-stream cap; together they exceed 20 MB.
        var spaces = Enumerable.Repeat((byte)' ', 10 * 1024 * 1024).ToArray();
        var streams = Enumerable.Range(0, 3).Select(_ => ResumeFixtures.FlateStream(spaces)).ToArray();
        var pdf = ResumeFixtures.PdfWithSharedStreams(pages: 1, referencesPerPage: 3, streams);

        var watch = Stopwatch.StartNew();
        var resume = await CareerClient.ReadDataAsync(await user.UploadResumeAsync(pdf), 201);
        watch.Stop();

        watch.Elapsed.Should().BeLessThan(Fast);
        resume.GetProperty("state").GetString().Should().Be("failed");
        resume.GetProperty("failureCode").GetString().Should().Be("ParserError");
    }

    // ---- Service: cleanup and failure handling ----------------------------------

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ArbitraryParserFailureIsAFailedResumeNotA500(bool failsAfterParsing)
    {
        IResumeParser parser = failsAfterParsing ? new BrokenResultParser() : new ThrowingParser();
        using var factory = new ResumeImportFactory { Parser = parser };
        var user = new CareerClient(factory);

        var resume = await CareerClient.ReadDataAsync(await user.UploadResumeAsync(Valid), 201);

        resume.GetProperty("state").GetString().Should().Be("failed");
        resume.GetProperty("failureCode").GetString().Should().Be("ParserError");
        factory.Storage.Count.Should().Be(0);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.CareerResumeDocuments.CountAsync(d => d.OwnerId == user.UserId)).Should().Be(1);
        (await db.CareerProfileProposals.CountAsync(p => p.OwnerId == user.UserId)).Should().Be(0);
    }

    [Fact]
    public async Task FailedBlobWriteLeavesNoRow()
    {
        using var factory = new ResumeImportFactory();
        factory.Storage.FailSaves = true;
        var user = new CareerClient(factory);
        _ = await user.UploadResumeAsync(ResumeFixtures.Pdf("warm up"), consentVersion: "stale"); // creates the user

        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IResumeImportService>();
        var act = () => service.UploadAsync(user.UserId, Valid, "r.pdf", true, ResumeImportService.DefaultConsentVersion);

        await act.Should().ThrowAsync<IOException>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.CareerResumeDocuments.AsNoTracking().CountAsync(d => d.OwnerId == user.UserId)).Should().Be(0);
        factory.Storage.Count.Should().Be(0);
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("unreadable")]
    public async Task RawFileIsDeletedForFailedAndUnreadableAndFileIs404Gone(string state)
    {
        using var factory = state == "failed"
            ? new ResumeImportFactory { Parser = new ThrowingParser() }
            : new ResumeImportFactory();
        var user = new CareerClient(factory);
        var bytes = state == "failed" ? Valid : ResumeFixtures.Pdf(new[] { new[] { "x" } }, imageOnly: true);

        var resume = await CareerClient.ReadDataAsync(await user.UploadResumeAsync(bytes), 201);

        resume.GetProperty("state").GetString().Should().Be(state);
        factory.Storage.Count.Should().Be(0);
        var error = await CareerClient.ReadErrorAsync(
            await user.GetAsync($"/api/career/resumes/{resume.GetProperty("id").GetString()}/file"), 404);
        error.GetProperty("code").GetString().Should().Be("CareerResumeFileGone");
    }

    [Fact]
    public async Task FailedDeleteDuringPurgeDoesNotStopLaterRows()
    {
        using var factory = new ResumeImportFactory();
        var user = new CareerClient(factory);
        for (var i = 0; i < 3; i++)
        {
            await user.UploadOkAsync(Valid);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var row in await db.CareerResumeDocuments.Where(d => d.OwnerId == user.UserId).ToListAsync())
            {
                row.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            }
            await db.SaveChangesAsync();
        }

        factory.Storage.FailNextDeletes = 1;
        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IResumeImportService>();
            (await service.PurgeExpiredResumesAsync(DateTime.UtcNow)).Should().Be(2);
        }

        factory.Storage.Count.Should().Be(1);
        using var check = factory.Services.CreateScope();
        (await check.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .CareerResumeDocuments.CountAsync(d => d.OwnerId == user.UserId)).Should().Be(1);
    }

    // ---- Inspector -----------------------------------------------------------------

    [Fact]
    public async Task DocxWithUnderstatedSizeIsNotA500AndIsFast()
    {
        using var factory = new ResumeImportFactory();
        var user = new CareerClient(factory);

        var watch = Stopwatch.StartNew();
        var response = await user.UploadResumeAsync(ResumeFixtures.DocxWithUnderstatedSize(), "r.docx");
        watch.Stop();

        watch.Elapsed.Should().BeLessThan(Fast);
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        (await response.Content.ReadAsStringAsync()).Should().NotContain("Exception");
    }

    [Fact]
    public void InspectorTreatsAnyArchiveFailureAsUnsupported()
    {
        // Starts like a ZIP but the directory is garbage.
        var bytes = new byte[] { 0x50, 0x4B, 0x03, 0x04 }.Concat(Enumerable.Repeat((byte)0xFF, 64)).ToArray();

        var result = ResumeFileInspector.Inspect(bytes);

        result.Rejection.Should().Be(CareerOutcomeKind.Unsupported);
    }

    [Theory]
    [InlineData(25, 201)]
    [InlineData(26, 413)]
    public async Task PageLimitIsExactlyTwentyFive(int pages, int status)
    {
        using var factory = new ResumeImportFactory();
        var user = new CareerClient(factory);

        var response = await user.UploadResumeAsync(ResumeFixtures.PdfWithPageCount(pages));

        ((int)response.StatusCode).Should().Be(status);
        if (status == 413)
        {
            (await CareerClient.ReadErrorAsync(response, 413)).GetProperty("code").GetString().Should().Be("CareerResumeTooLarge");
            factory.Storage.Count.Should().Be(0);
            using var scope = factory.Services.CreateScope();
            (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .CareerResumeDocuments.CountAsync(d => d.OwnerId == user.UserId)).Should().Be(0);
        }
    }

    // ---- File names ------------------------------------------------------------------

    [Fact]
    public void FileNamesLoseControlAndFormatCharacters()
    {
        var name = ResumeImportService.SanitiseFileName("re\u202Esume\u0007\u200B.pdf", ResumeFormat.Pdf);

        name.Should().Be("resume.pdf");
    }

    [Fact]
    public void LongFileNamesKeepTheirExtensionAndNeverSplitSurrogatePairs()
    {
        // 199 ASCII chars then an emoji (two UTF-16 units) puts the 200 cut mid-pair.
        var stem = new string('a', 199) + "\U0001F600" + new string('b', 50);

        var name = ResumeImportService.SanitiseFileName(stem + ".pdf", ResumeFormat.Pdf);

        name.Length.Should().BeLessThanOrEqualTo(200);
        name.Should().EndWith(".pdf");
        name.Should().Be(new string('a', 196) + ".pdf");
        name.Where(char.IsSurrogate).Should().BeEmpty();
    }

    [Fact]
    public void FileNameOfOnlyControlCharactersFallsBack() =>
        ResumeImportService.SanitiseFileName("\u202E\u0000", ResumeFormat.Docx).Should().Be("resume.docx");

    // ---- Consent version --------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("resume-notice-2020-01-01")]
    public async Task StaleOrMissingConsentVersionIs400AndNothingIsStored(string? version)
    {
        using var factory = new ResumeImportFactory();
        var user = new CareerClient(factory);

        var error = await CareerClient.ReadErrorAsync(await user.UploadResumeAsync(Valid, consentVersion: version), 400);

        error.GetProperty("fieldErrors").GetProperty("consent").GetString().Should().Be("The notice changed; read it again.");
        factory.Storage.Count.Should().Be(0);
    }

    [Fact]
    public async Task ConsentVersionIsRecordedAndReturned()
    {
        using var factory = new ResumeImportFactory();
        var user = new CareerClient(factory);

        var resume = await user.UploadOkAsync(Valid);

        resume.GetProperty("consentVersion").GetString().Should().Be("resume-notice-2026-10-04");
        using var scope = factory.Services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .CareerResumeDocuments.SingleAsync(d => d.OwnerId == user.UserId);
        row.ConsentVersion.Should().Be("resume-notice-2026-10-04");
    }
}
