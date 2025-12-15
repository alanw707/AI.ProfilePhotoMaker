using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace AI.ProfilePhotoMaker.API.Tests.Unit;

public class ReplicateApiClientUrlNormalizationTests
{
    [Fact]
    public async Task NormalizeImageUrlForExternalAccessAsync_RewritesAzuriteHost_ToNgrok()
    {
        var ngrokBase = "https://clear-anteater-usually.ngrok-free.app";
        var configurationMock = new Mock<IConfiguration>();
        configurationMock.Setup(x => x["Replicate:ApiToken"]).Returns("test-token");
        configurationMock.Setup(x => x["Webhooks:NgrokTunnelUrl"]).Returns(ngrokBase);

        var loggerMock = new Mock<ILogger<ReplicateApiClient>>();
        var webhookResolver = new Mock<IWebhookUrlResolver>();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationDbContext(options);

        var client = new ReplicateApiClient(
            new HttpClient(new HttpMessageHandlerStub()),
            configurationMock.Object,
            loggerMock.Object,
            db,
            webhookResolver.Object);

        var original =
            "http://azurite:10000/devstoreaccount1/profile-images/dev/training-zips/user.zip?sv=2024-11-04&sig=abc";

        var normalized = await client.NormalizeImageUrlForExternalAccessAsync(original);

        Assert.StartsWith(
            $"{ngrokBase}/devstoreaccount1/profile-images/dev/training-zips/user.zip?",
            normalized);
        Assert.Contains("sig=abc", normalized);
        Assert.Contains("ngrok-skip-browser-warning=true", normalized);
    }

    [Fact]
    public async Task NormalizeImageUrlForExternalAccessAsync_DoesNotRewriteAzureBlobUrls()
    {
        var ngrokBase = "https://clear-anteater-usually.ngrok-free.app";
        var configurationMock = new Mock<IConfiguration>();
        configurationMock.Setup(x => x["Replicate:ApiToken"]).Returns("test-token");
        configurationMock.Setup(x => x["Webhooks:NgrokTunnelUrl"]).Returns(ngrokBase);
        configurationMock.Setup(x => x["ExternalApiBaseUrl"]).Returns("https://api.example.com");

        var loggerMock = new Mock<ILogger<ReplicateApiClient>>();
        var webhookResolver = new Mock<IWebhookUrlResolver>();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationDbContext(options);

        var client = new ReplicateApiClient(
            new HttpClient(new HttpMessageHandlerStub()),
            configurationMock.Object,
            loggerMock.Object,
            db,
            webhookResolver.Object);

        var original = "https://myaccount.blob.core.windows.net/container/path/file.zip?sv=2024-11-04&sig=abc";

        var normalized = await client.NormalizeImageUrlForExternalAccessAsync(original);

        Assert.Equal(original, normalized);
    }

    private sealed class HttpMessageHandlerStub : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
