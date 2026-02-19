using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;

namespace AI.ProfilePhotoMaker.API.Tests.Unit;

public class ReplicateApiClientNegativePromptTests
{
    [Fact]
    public async Task GenerateImagesAsync_IncludesNegativePromptFromStyleTemplate()
    {
        var httpHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        HttpRequestMessage? capturedRequest = null;

        var responseJson = JsonSerializer.Serialize(new
        {
            id = "pred-1",
            version = "test-version",
            status = "starting",
            created_at = DateTime.UtcNow
        });

        httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri != null &&
                    req.RequestUri.AbsolutePath.EndsWith("/predictions")),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        var configurationMock = new Mock<IConfiguration>();
        configurationMock.Setup(x => x["Replicate:ApiToken"]).Returns("test-token");

        var webhookResolver = new Mock<IWebhookUrlResolver>();
        webhookResolver
            .Setup(x => x.GetWebhookUrlAsync(It.IsAny<string>()))
            .ReturnsAsync("https://example.com/webhook");

        var loggerMock = new Mock<ILogger<ReplicateApiClient>>();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationDbContext(options);

        db.Styles.Add(new Style
        {
            Id = 999,
            Name = "casual",
            Description = "Casual style",
            PromptTemplate = "A photo of {subject}, casual portrait",
            NegativePromptTemplate = "formal business attire, suit, tie",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var client = new ReplicateApiClient(
            new HttpClient(httpHandlerMock.Object),
            configurationMock.Object,
            loggerMock.Object,
            db,
            webhookResolver.Object);

        await client.GenerateImagesAsync(
            trainedModelVersion: "owner/model:version",
            userId: "u1",
            style: "casual",
            userInfo: new UserInfo { Gender = "male", Ethnicity = "asian" },
            numOutputs: 2);

        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedRequest!.Content);

        var body = await capturedRequest.Content!.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        var input = document.RootElement.GetProperty("input");
        Assert.True(input.TryGetProperty("negative_prompt", out var negativePrompt));
        Assert.Equal("formal business attire, suit, tie, moles, dark spots, skin blemishes, birthmarks, skin spots", negativePrompt.GetString());
    }

    [Theory]
    [InlineData("professional headshot")]
    [InlineData("corporate portrait")]
    [InlineData("studio lighting")]
    public void ApplyRealismModifier_NeverProducesSubtleSkinPores(string basePrompt)
    {
        // AC1: Call ApplyRealismModifier many times with deterministic seeds and verify:
        //   1. "subtle skin pores" never appears (removed from pool)
        //   2. "soft natural finish" appears at least once (replacement is in pool)
        bool foundSoftNaturalFinish = false;

        for (int i = 0; i < 100; i++)
        {
            var result = ReplicateApiClient.ApplyRealismModifier(basePrompt, new Random(i));
            Assert.DoesNotContain("subtle skin pores", result, StringComparison.OrdinalIgnoreCase);
            if (result.Contains("soft natural finish", StringComparison.OrdinalIgnoreCase))
                foundSoftNaturalFinish = true;
        }

        Assert.True(foundSoftNaturalFinish, "Expected 'soft natural finish' to appear in at least one of 100 seeded iterations — verify it is present in s_realismPromptModifiers");
    }

    [Fact]
    public async Task GenerateImagesAsync_AppendsBlemishNegatives_ToNonEmptyTemplate()
    {
        // AC2: Verify blemish suffix is appended to non-empty negative prompt template
        var httpHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        HttpRequestMessage? capturedRequest = null;

        var responseJson = JsonSerializer.Serialize(new
        {
            id = "pred-ac2",
            version = "test-version",
            status = "starting",
            created_at = DateTime.UtcNow
        });

        httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri != null &&
                    req.RequestUri.AbsolutePath.EndsWith("/predictions")),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        var configurationMock = new Mock<IConfiguration>();
        configurationMock.Setup(x => x["Replicate:ApiToken"]).Returns("test-token");

        var webhookResolver = new Mock<IWebhookUrlResolver>();
        webhookResolver
            .Setup(x => x.GetWebhookUrlAsync(It.IsAny<string>()))
            .ReturnsAsync("https://example.com/webhook");

        var loggerMock = new Mock<ILogger<ReplicateApiClient>>();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationDbContext(options);

        // Style with non-empty negative prompt template
        db.Styles.Add(new Style
        {
            Id = 888,
            Name = "executive",
            Description = "Executive style",
            PromptTemplate = "A photo of {subject}, executive portrait",
            NegativePromptTemplate = "casual attire, messy background",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var client = new ReplicateApiClient(
            new HttpClient(httpHandlerMock.Object),
            configurationMock.Object,
            loggerMock.Object,
            db,
            webhookResolver.Object);

        await client.GenerateImagesAsync(
            trainedModelVersion: "owner/model:version",
            userId: "u-ac2",
            style: "executive",
            userInfo: new UserInfo { Gender = "female", Ethnicity = "caucasian" },
            numOutputs: 2);

        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedRequest!.Content);

        var body = await capturedRequest.Content!.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        var input = document.RootElement.GetProperty("input");
        Assert.True(input.TryGetProperty("negative_prompt", out var negativePrompt));
        var negativePromptValue = negativePrompt.GetString();
        
        Assert.NotNull(negativePromptValue);
        Assert.Contains("moles", negativePromptValue, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dark spots", negativePromptValue, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("skin blemishes", negativePromptValue, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("birthmarks", negativePromptValue, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("skin spots", negativePromptValue, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateImagesAsync_DoesNotAppendBlemishNegatives_ToEmptyTemplate()
    {
        // AC3: Edge case - verify empty template stays empty (no trailing comma issues)
        var httpHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        HttpRequestMessage? capturedRequest = null;

        var responseJson = JsonSerializer.Serialize(new
        {
            id = "pred-ac3",
            version = "test-version",
            status = "starting",
            created_at = DateTime.UtcNow
        });

        httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri != null &&
                    req.RequestUri.AbsolutePath.EndsWith("/predictions")),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        var configurationMock = new Mock<IConfiguration>();
        configurationMock.Setup(x => x["Replicate:ApiToken"]).Returns("test-token");

        var webhookResolver = new Mock<IWebhookUrlResolver>();
        webhookResolver
            .Setup(x => x.GetWebhookUrlAsync(It.IsAny<string>()))
            .ReturnsAsync("https://example.com/webhook");

        var loggerMock = new Mock<ILogger<ReplicateApiClient>>();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationDbContext(options);

        // Style with empty/whitespace negative prompt template
        db.Styles.Add(new Style
        {
            Id = 999,
            Name = "minimal",
            Description = "Minimal style",
            PromptTemplate = "A photo of {subject}, minimal portrait",
            NegativePromptTemplate = "   ", // Whitespace only - should be treated as empty
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var client = new ReplicateApiClient(
            new HttpClient(httpHandlerMock.Object),
            configurationMock.Object,
            loggerMock.Object,
            db,
            webhookResolver.Object);

        await client.GenerateImagesAsync(
            trainedModelVersion: "owner/model:version",
            userId: "u-ac3",
            style: "minimal",
            userInfo: new UserInfo { Gender = "male", Ethnicity = "asian" },
            numOutputs: 2);

        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedRequest!.Content);

        var body = await capturedRequest.Content!.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        var input = document.RootElement.GetProperty("input");
        Assert.True(input.TryGetProperty("negative_prompt", out var negativePrompt));
        var negativePromptValue = negativePrompt.GetString();
        
        // Empty template should result in empty string (no suffix appended)
        Assert.Equal(string.Empty, negativePromptValue);
    }
}

