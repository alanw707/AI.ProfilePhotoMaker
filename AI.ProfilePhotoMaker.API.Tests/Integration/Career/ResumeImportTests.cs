using System.Net;
using System.Text.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Services.Career;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration.Career;

/// <summary>Upload, extraction, storage and ownership behaviour (#379, docs/career/api-resume-import.md).</summary>
public class ResumeImportTests : IClassFixture<ResumeImportFactory>
{
    private readonly ResumeImportFactory _factory;

    public ResumeImportTests(ResumeImportFactory factory)
    {
        _factory = factory;
    }

    private static JsonElement[] Items(JsonElement proposal) => proposal.GetProperty("items").EnumerateArray().ToArray();

    private static JsonElement[] ForField(JsonElement proposal, string field) =>
        Items(proposal).Where(i => i.GetProperty("field").GetString() == field).ToArray();

    private static async Task<JsonElement> GetProposalAsync(CareerClient user, JsonElement resume) =>
        await CareerClient.ReadDataAsync(
            await user.GetAsync($"/api/career/profile/proposals/{resume.GetProperty("proposalId").GetString()}"), 200);

    [Fact]
    public async Task ValidPdfBecomesReadyWithAttributedItems()
    {
        var user = new CareerClient(_factory);

        var resume = await user.UploadOkAsync(ResumeFixtures.Pdf(ResumeFixtures.MorganPages), "morgan.pdf");

        resume.GetProperty("state").GetString().Should().Be("ready");
        resume.GetProperty("format").GetString().Should().Be("pdf");
        resume.GetProperty("pageCount").GetInt32().Should().Be(2);
        resume.GetProperty("fileName").GetString().Should().Be("morgan.pdf");
        resume.GetProperty("failureCode").ValueKind.Should().Be(JsonValueKind.Null);
        resume.GetProperty("expiresAt").GetDateTime().Should().BeAfter(DateTime.UtcNow.AddDays(29));

        var proposal = await GetProposalAsync(user, resume);
        proposal.GetProperty("source").GetString().Should().Be("resume");
        proposal.GetProperty("status").GetString().Should().Be("pending");
        proposal.GetProperty("resumeId").GetString().Should().Be(resume.GetProperty("id").GetString());
        proposal.GetProperty("baseProfileVersion").ValueKind.Should().Be(JsonValueKind.Null);

        var title = ForField(proposal, "currentTitle").Should().ContainSingle().Subject;
        title.GetProperty("value").GetString().Should().Be("Senior Operations Lead");
        title.GetProperty("page").GetInt32().Should().Be(1);
        title.GetProperty("excerpt").GetString().Should().Contain("Senior Operations Lead");

        ForField(proposal, "location").Single().GetProperty("value").GetString().Should().Be("Denver, CO");
        ForField(proposal, "yearsExperience").Single().GetProperty("value").GetString().Should().Be("8");

        var skills = ForField(proposal, "skills");
        skills.Select(s => s.GetProperty("value").GetString()).Should().BeEquivalentTo("SQL", "Tableau", "Process Improvement");
        skills.Should().OnlyContain(s => s.GetProperty("page").GetInt32() == 2 && s.GetProperty("section").GetString() == "Skills");

        ForField(proposal, "highlights").Should().HaveCount(3);
        ForField(proposal, "summary").Should().ContainSingle();
    }

    [Fact]
    public async Task FlateCompressedPdfIsReadToo()
    {
        var user = new CareerClient(_factory);

        var resume = await user.UploadOkAsync(ResumeFixtures.Pdf(ResumeFixtures.MorganPages, flate: true));

        resume.GetProperty("state").GetString().Should().Be("ready");
        var proposal = await GetProposalAsync(user, resume);
        ForField(proposal, "currentTitle").Single().GetProperty("value").GetString().Should().Be("Senior Operations Lead");
        ForField(proposal, "skills").Should().HaveCount(3);
    }

    [Fact]
    public async Task ValidDocxBecomesReadyWithPagesAndBullets()
    {
        var user = new CareerClient(_factory);
        var docx = ResumeFixtures.Docx(
            "Morgan Ellis", "Senior Operations Lead", "Denver, CO",
            "Experience",
            "Senior Operations Lead, Regional Health Services, 2021 - 2023",
            "• Reduced scheduling backlog by 30 percent across 12 clinics",
            ResumeFixtures.PageBreak,
            "Skills", "SQL, Tableau");

        var resume = await user.UploadOkAsync(docx, "morgan.docx");

        resume.GetProperty("format").GetString().Should().Be("docx");
        resume.GetProperty("state").GetString().Should().Be("ready");
        resume.GetProperty("pageCount").GetInt32().Should().Be(2);
        var proposal = await GetProposalAsync(user, resume);
        ForField(proposal, "highlights").Single().GetProperty("value").GetString().Should().StartWith("Reduced scheduling backlog");
        ForField(proposal, "skills").Should().HaveCount(2);
    }

    [Fact]
    public async Task OverlappingDatesAndVagueClaimsAreFlagged()
    {
        var user = new CareerClient(_factory);
        var docx = ResumeFixtures.Docx(
            "Experience",
            "Operations Analyst, Northwind Clinics, 2018 - 2022",
            "Scheduling Coordinator, Harbor Dental, 2020 - 2023",
            "• Involved in quarterly planning");

        var proposal = await GetProposalAsync(user, await user.UploadOkAsync(docx, "r.docx"));

        ForField(proposal, "yearsExperience").Single().GetProperty("flags").EnumerateArray()
            .Select(f => f.GetString()).Should().Contain("conflict");
        ForField(proposal, "highlights").Single().GetProperty("flags").EnumerateArray()
            .Select(f => f.GetString()).Should().Contain("ambiguous");
    }

    [Fact]
    public async Task ImageOnlyPdfIsUnreadableAndPasteFallbackWorks()
    {
        var user = new CareerClient(_factory);

        var resume = await user.UploadOkAsync(ResumeFixtures.Pdf(new[] { new[] { "ignored" } }, imageOnly: true));

        resume.GetProperty("state").GetString().Should().Be("unreadable");
        resume.GetProperty("proposalId").ValueKind.Should().Be(JsonValueKind.Null);

        var paste = await CareerClient.ReadDataAsync(
            await user.PostPasteAsync("Senior Operations Lead\nDenver, CO\nSkills\nSQL, Tableau"), 201);
        paste.GetProperty("source").GetString().Should().Be("pasted");
        paste.GetProperty("resumeId").ValueKind.Should().Be(JsonValueKind.Null);
        ForField(paste, "currentTitle").Single().GetProperty("page").ValueKind.Should().Be(JsonValueKind.Null);
        ForField(paste, "skills").Should().HaveCount(2);
    }

    [Fact]
    public async Task PasteRejectsEmptyAndOversizeText()
    {
        var user = new CareerClient(_factory);

        var emptyError = await CareerClient.ReadErrorAsync(await user.PostPasteAsync(string.Empty), 400);
        emptyError.GetProperty("fieldErrors").TryGetProperty("text", out _).Should().BeTrue();

        var tooLong = await CareerClient.ReadErrorAsync(await user.PostPasteAsync(new string('a', 100_001)), 400);
        tooLong.GetProperty("code").GetString().Should().Be("ValidationError");

        (await user.PostPasteAsync(new string('a', 100_000))).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task MaliciousInstructionsAreDataAndNeverApplied()
    {
        var user = new CareerClient(_factory);
        await user.CreateProfileAsync("Data analyst");

        var resume = await user.UploadOkAsync(ResumeFixtures.Pdf(ResumeFixtures.InjectionPages));

        resume.GetProperty("state").GetString().Should().Be("ready");
        var proposal = await GetProposalAsync(user, resume);
        proposal.GetProperty("status").GetString().Should().Be("pending");
        ForField(proposal, "currentTitle").Should().BeEmpty("an instruction sentence is not a title");

        var profile = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/profile"), 200);
        profile.GetProperty("version").GetInt32().Should().Be(1);
        profile.GetProperty("facts").GetProperty("currentTitle").GetString().Should().Be("Data analyst");
    }

    [Fact]
    public async Task UploadWithoutConsentOrFileIsValidationError()
    {
        var user = new CareerClient(_factory);

        var noConsent = await CareerClient.ReadErrorAsync(
            await user.UploadResumeAsync(ResumeFixtures.Pdf("Analyst"), consent: null), 400);
        noConsent.GetProperty("fieldErrors").TryGetProperty("consent", out _).Should().BeTrue();

        var falseConsent = await CareerClient.ReadErrorAsync(
            await user.UploadResumeAsync(ResumeFixtures.Pdf("Analyst"), consent: "false"), 400);
        falseConsent.GetProperty("code").GetString().Should().Be("ValidationError");

        var noFile = await CareerClient.ReadErrorAsync(await user.UploadResumeAsync(null), 400);
        noFile.GetProperty("fieldErrors").TryGetProperty("file", out _).Should().BeTrue();
    }

    public static TheoryData<string, int, string> Rejections => new()
    {
        { "oversize", 413, "CareerResumeTooLarge" },
        { "pages", 413, "CareerResumeTooLarge" },
        { "fakepng", 415, "CareerResumeUnsupported" },
        { "ole", 415, "CareerResumeUnsupported" },
        { "encrypted", 415, "CareerResumeUnsupported" },
        { "zipbomb", 415, "CareerResumeUnsupported" },
        { "manyentries", 415, "CareerResumeUnsupported" },
        { "garbage", 415, "CareerResumeUnsupported" },
    };

    private static byte[] Fixture(string name) => name switch
    {
        "oversize" => ResumeFixtures.Oversize(),
        "pages" => ResumeFixtures.PdfWithPageCount(30),
        "fakepng" => ResumeFixtures.FakePng(),
        "ole" => ResumeFixtures.OleFile(),
        "encrypted" => ResumeFixtures.Pdf(ResumeFixtures.MorganPages, encrypted: true),
        "zipbomb" => ResumeFixtures.ZipBombDocx(),
        "manyentries" => ResumeFixtures.DocxWithEntries(250),
        _ => new byte[] { 1, 2, 3, 4, 5 }
    };

    [Theory]
    [MemberData(nameof(Rejections))]
    public async Task UnsafeOrUnsupportedFilesAreRejectedAndNothingIsStored(string fixture, int status, string code)
    {
        var user = new CareerClient(_factory);
        var before = _factory.Storage.Count;

        // A PNG renamed .pdf must be judged by its bytes, not its name.
        var response = await user.UploadResumeAsync(Fixture(fixture), "resume.pdf");

        var error = await CareerClient.ReadErrorAsync(response, status);
        error.GetProperty("code").GetString().Should().Be(code);
        _factory.Storage.Count.Should().Be(before);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.CareerResumeDocuments.CountAsync(d => d.OwnerId == user.UserId)).Should().Be(0);

        var list = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/resumes"), 200);
        list.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task UnsupportedFilesExplainWhichProblem()
    {
        var user = new CareerClient(_factory);

        var ole = await CareerClient.ReadErrorAsync(await user.UploadResumeAsync(ResumeFixtures.OleFile(), "old.doc"), 415);
        var encrypted = await CareerClient.ReadErrorAsync(
            await user.UploadResumeAsync(ResumeFixtures.Pdf(ResumeFixtures.MorganPages, encrypted: true)), 415);

        ole.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
        encrypted.GetProperty("detail").GetString().Should().NotBe(ole.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task TooManyCharactersIsRejectedAfterExtraction()
    {
        var user = new CareerClient(_factory);
        var before = _factory.Storage.Count;
        var lines = Enumerable.Range(0, 120).Select(_ => new string('a', 1000)).ToArray();

        var error = await CareerClient.ReadErrorAsync(await user.UploadResumeAsync(ResumeFixtures.Docx(lines), "big.docx"), 413);

        error.GetProperty("code").GetString().Should().Be("CareerResumeTooLarge");
        _factory.Storage.Count.Should().Be(before);
    }

    [Fact]
    public async Task ListGetAndFileAreOwnerScopedAndFileIsAnAttachment()
    {
        var alice = new CareerClient(_factory);
        var bob = new CareerClient(_factory);
        var pdf = ResumeFixtures.Pdf(ResumeFixtures.MorganPages);
        var resume = await alice.UploadOkAsync(pdf, "morgan.pdf");
        var id = resume.GetProperty("id").GetString();

        (await CareerClient.ReadDataAsync(await alice.GetAsync("/api/career/resumes"), 200)).GetArrayLength().Should().Be(1);
        (await CareerClient.ReadDataAsync(await bob.GetAsync("/api/career/resumes"), 200)).GetArrayLength().Should().Be(0);
        (await CareerClient.ReadDataAsync(await alice.GetAsync($"/api/career/resumes/{id}"), 200))
            .GetProperty("id").GetString().Should().Be(id);

        var file = await alice.GetAsync($"/api/career/resumes/{id}/file");
        file.StatusCode.Should().Be(HttpStatusCode.OK);
        file.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        file.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        (await file.Content.ReadAsByteArrayAsync()).Should().Equal(pdf);

        await CareerClient.ReadErrorAsync(await bob.GetAsync($"/api/career/resumes/{id}"), 404);
        await CareerClient.ReadErrorAsync(await bob.GetAsync($"/api/career/resumes/{id}/file"), 404);
        await CareerClient.ReadErrorAsync(await bob.SendAsync(HttpMethod.Delete, $"/api/career/resumes/{id}"), 404);
        _factory.Storage.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task StoredKeyIsOpaqueAndPrivate()
    {
        var user = new CareerClient(_factory);
        var resume = await user.UploadOkAsync(ResumeFixtures.Pdf(ResumeFixtures.MorganPages), "morgan-ellis-resume.pdf");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await db.CareerResumeDocuments.SingleAsync(d => d.OwnerId == user.UserId);

        row.StorageKey.Should().StartWith("career-private/resumes/");
        row.StorageKey.Should().NotContainEquivalentOf("morgan");
        row.StorageKey.Should().NotContain(user.UserId);
        row.Sha256.Should().HaveLength(64);
        row.State.ToString().Should().Be("Ready");
        _factory.Storage.Contains(row.StorageKey).Should().BeTrue();
        resume.GetProperty("sizeBytes").GetInt64().Should().Be(row.SizeBytes);
    }

    [Fact]
    public async Task DeleteRemovesRawFileMetadataAndPendingProposal()
    {
        var user = new CareerClient(_factory);
        var resume = await user.UploadOkAsync(ResumeFixtures.Pdf(ResumeFixtures.MorganPages));
        var id = resume.GetProperty("id").GetString();
        var proposalId = resume.GetProperty("proposalId").GetString();
        var before = _factory.Storage.Count;

        (await user.SendAsync(HttpMethod.Delete, $"/api/career/resumes/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        _factory.Storage.Count.Should().Be(before - 1);
        await CareerClient.ReadErrorAsync(await user.GetAsync($"/api/career/resumes/{id}"), 404);
        await CareerClient.ReadErrorAsync(await user.GetAsync($"/api/career/profile/proposals/{proposalId}"), 404);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.CareerProfileProposalItems.CountAsync(i => i.OwnerId == user.UserId)).Should().Be(0);
    }

    [Fact]
    public async Task PurgeRemovesExpiredRawFilesOnly()
    {
        var user = new CareerClient(_factory);
        var expired = await user.UploadOkAsync(ResumeFixtures.Pdf(ResumeFixtures.MorganPages), "old.pdf");
        var fresh = await user.UploadOkAsync(ResumeFixtures.Pdf(ResumeFixtures.MorganPages), "new.pdf");

        string expiredKey, freshKey;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var rows = await db.CareerResumeDocuments.Where(d => d.OwnerId == user.UserId).ToListAsync();
            var oldRow = rows.Single(r => r.Id.ToString() == expired.GetProperty("id").GetString());
            expiredKey = oldRow.StorageKey;
            freshKey = rows.Single(r => r.Id.ToString() == fresh.GetProperty("id").GetString()).StorageKey;
            oldRow.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IResumeImportService>();
            (await service.PurgeExpiredResumesAsync(DateTime.UtcNow)).Should().Be(1);
        }

        _factory.Storage.Contains(expiredKey).Should().BeFalse();
        _factory.Storage.Contains(freshKey).Should().BeTrue();
        await CareerClient.ReadErrorAsync(await user.GetAsync($"/api/career/resumes/{expired.GetProperty("id").GetString()}"), 404);
        await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/resumes/{fresh.GetProperty("id").GetString()}"), 200);
    }

    [Fact]
    public async Task LogsNeverContainResumeTextOrFileNames()
    {
        var user = new CareerClient(_factory);
        var resume = await user.UploadOkAsync(ResumeFixtures.Pdf(ResumeFixtures.MorganPages), "morgan-ellis-private.pdf");
        await user.UploadResumeAsync(ResumeFixtures.Pdf(ResumeFixtures.MorganPages, encrypted: true), "morgan-ellis-encrypted.pdf");
        await user.PostPasteAsync("Morgan Ellis\nSenior Operations Lead\nSkills\nSQL");
        await user.GetAsync($"/api/career/resumes/{resume.GetProperty("id").GetString()}/file");

        var logged = string.Join("\n", _factory.Logs.Messages);

        logged.Should().NotBeEmpty();
        foreach (var secret in new[] { "Morgan Ellis", "Regional Health", "Northwind", "KPI report", "Tableau", "morgan-ellis", "Denver" })
        {
            logged.Should().NotContain(secret);
        }
    }
}
