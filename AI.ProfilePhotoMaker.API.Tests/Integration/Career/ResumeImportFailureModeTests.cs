using AI.ProfilePhotoMaker.API.Services.Career;
using FluentAssertions;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration.Career;

/// <summary>Scanner and parser failure handling. One host per test so fakes cannot leak.</summary>
public class ResumeImportFailureModeTests
{
    private static byte[] Valid => ResumeFixtures.Pdf(ResumeFixtures.MorganPages);

    [Fact]
    public async Task ThreatIsRejectedWith422AndRawFileIsDeleted()
    {
        using var factory = new ResumeImportFactory();
        factory.Scanner.Result = MalwareScanResult.Threat;
        var user = new CareerClient(factory);

        var error = await CareerClient.ReadErrorAsync(await user.UploadResumeAsync(Valid), 422);

        error.GetProperty("code").GetString().Should().Be("CareerResumeRejected");
        factory.Storage.Count.Should().Be(0);
        (await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/resumes"), 200)).GetArrayLength().Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScannerOutageIs503WithRetryAfterAndRawFileIsDeleted(bool throws)
    {
        using var factory = new ResumeImportFactory();
        if (throws)
        {
            factory.Scanner.Throw = new InvalidOperationException("scanner offline");
        }
        else
        {
            factory.Scanner.Result = MalwareScanResult.Unavailable;
        }
        var user = new CareerClient(factory);

        var error = await CareerClient.ReadErrorAsync(await user.UploadResumeAsync(Valid), 503);

        error.GetProperty("code").GetString().Should().Be("CareerScannerUnavailable");
        error.GetProperty("retryAfterSeconds").GetInt32().Should().BeGreaterThan(0);
        factory.Storage.Count.Should().Be(0);
    }

    [Fact]
    public async Task NoRegisteredScannerFailsClosed()
    {
        using var factory = new ResumeImportFactory { RegisterScanner = false };
        var user = new CareerClient(factory);

        var error = await CareerClient.ReadErrorAsync(await user.UploadResumeAsync(Valid), 503);

        error.GetProperty("code").GetString().Should().Be("CareerScannerUnavailable");
        factory.Storage.Count.Should().Be(0);
    }

    [Fact]
    public async Task SlowParserFailsWithExtractionTimeoutAndNoProposal()
    {
        using var factory = new ResumeImportFactory { Parser = new SlowParser(), ExtractionTimeoutSeconds = 0.2 };
        var user = new CareerClient(factory);

        var resume = await CareerClient.ReadDataAsync(await user.UploadResumeAsync(Valid), 201);

        resume.GetProperty("state").GetString().Should().Be("failed");
        resume.GetProperty("failureCode").GetString().Should().Be("ExtractionTimeout");
        resume.GetProperty("proposalId").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }
}
