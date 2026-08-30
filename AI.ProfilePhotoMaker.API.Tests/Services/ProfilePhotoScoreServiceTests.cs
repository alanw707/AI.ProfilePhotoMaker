using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Png;

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
    public async Task ScoreAsync_RatesReferenceProfessionalPortraitAtNinetyOrHigher()
    {
        var fixturePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../AI.ProfilePhotoMaker.UI/src/assets/marketing/before-after/executive-after.jpg"));
        await using var stream = File.OpenRead(fixturePath);

        var score = await new ProfilePhotoScoreService().ScoreAsync(stream, "executive-after.jpg");

        Assert.True(score.OverallScore >= 90, $"Expected 90 or higher, got {score.OverallScore}.");
        Assert.Equal("pass", score.QualityGate.Status);
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
}
