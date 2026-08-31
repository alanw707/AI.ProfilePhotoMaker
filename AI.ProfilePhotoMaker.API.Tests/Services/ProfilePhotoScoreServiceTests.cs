using System.Net;
using System.Text;
using System.Text.Json;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace AI.ProfilePhotoMaker.API.Tests.Services;

public class ProfilePhotoScoreServiceTests
{
    [Fact]
    public async Task ScoreAsync_BlocksVeryLowResolutionUpload()
    {
        await using var stream = new MemoryStream();
        using (var image = new Image<Rgba32>(128, 128, new Rgba32(120, 120, 120)))
        {
            await image.SaveAsync(stream, new PngEncoder());
        }
        stream.Position = 0;

        var service = new ProfilePhotoScoreService();
        var score = await service.ScoreAsync(stream, "tiny.png");

        Assert.Equal("blocked", score.QualityGate.Status);
        Assert.Contains(score.QualityGate.Reasons, reason => reason.Contains("resolution", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(score.QualityGate.Recommendations, recommendation => recommendation.Contains("1024", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ScoreAsync_RatesAProfessionalPortraitAtNinetyOrHigherWhenVisionRubricDoes()
    {
        var fixturePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../AI.ProfilePhotoMaker.UI/src/assets/marketing/before-after/executive-after.jpg"));
        await using var stream = File.OpenRead(fixturePath);

        var score = await CreateService(92).ScoreAsync(stream, "executive-after.jpg");

        Assert.True(score.OverallScore >= 90, $"Expected 90 or higher, got {score.OverallScore}.");
        Assert.Equal("pass", score.QualityGate.Status);
    }

    [Fact]
    public async Task ScoreAsync_RequiresMoreThanEightyFiveForQualityPass()
    {
        var fixturePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../AI.ProfilePhotoMaker.UI/src/assets/marketing/before-after/executive-after.jpg"));
        await using var stream = File.OpenRead(fixturePath);

        var score = await CreateService(85).ScoreAsync(stream, "executive-after.jpg");

        Assert.Equal(85, score.OverallScore);
        Assert.Equal("warning", score.QualityGate.Status);
    }

    [Fact]
    public async Task ScoreAsync_ReturnsQualityGateForUpload()
    {
        await using var stream = new MemoryStream();
        using (var image = new Image<Rgba32>(1024, 1024, new Rgba32(180, 180, 180)))
        {
            await image.SaveAsync(stream, new PngEncoder());
        }
        stream.Position = 0;

        var service = new ProfilePhotoScoreService();
        var score = await service.ScoreAsync(stream, "source.png");

        Assert.NotNull(score.QualityGate);
        Assert.Contains(score.QualityGate.Status, new[] { "pass", "warning", "blocked" });
    }

    private static ProfilePhotoScoreService CreateService(int rubricScore)
    {
        var rubric = JsonSerializer.Serialize(new
        {
            professionalism = rubricScore,
            approachability = rubricScore,
            confidence = rubricScore,
            attireBackgroundFit = rubricScore,
            roleFit = rubricScore
        });
        var response = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = rubric } } } });
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenAI:ApiKey"] = "<REDACTED>" })
            .Build();

        return new ProfilePhotoScoreService(
            new StubHttpClientFactory(response),
            configuration,
            NullLogger<ProfilePhotoScoreService>.Instance);
    }

    private sealed class StubHttpClientFactory(string response) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubHandler(response));
    }

    private sealed class StubHandler(string response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            });
    }
}
