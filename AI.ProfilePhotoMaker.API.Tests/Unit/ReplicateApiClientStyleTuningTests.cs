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

public class ReplicateApiClientStyleTuningTests
{
    [Theory]
    [InlineData("executive", 2.8, 34)]
    [InlineData("CASUAL", 2.3, 40)]
    [InlineData("unknown", 3.0, 28)]
    public async Task GenerateImagesAsync_ResolvesStyleTuningByGroup(
        string style,
        double expectedGuidanceScale,
        int expectedInferenceSteps)
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

        var configuration = BuildConfiguration();
        var webhookResolver = new Mock<IWebhookUrlResolver>();
        webhookResolver
            .Setup(x => x.GetWebhookUrlAsync(It.IsAny<string>()))
            .ReturnsAsync("https://example.com/webhook");

        var loggerMock = new Mock<ILogger<ReplicateApiClient>>();
        await using var db = BuildDbContext();
        await SeedStylesAsync(db);

        var client = new ReplicateApiClient(
            new HttpClient(httpHandlerMock.Object),
            configuration,
            loggerMock.Object,
            db,
            webhookResolver.Object);

        await client.GenerateImagesAsync(
            trainedModelVersion: "owner/model:version",
            userId: "u1",
            style: style,
            userInfo: new UserInfo { Gender = "female", Ethnicity = "latina" },
            numOutputs: 2);

        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedRequest!.Content);

        var body = await capturedRequest.Content!.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        var input = document.RootElement.GetProperty("input");
        var guidanceScale = input.GetProperty("guidance_scale").GetDouble();
        var numInferenceSteps = input.GetProperty("num_inference_steps").GetInt32();

        Assert.Equal(expectedGuidanceScale, guidanceScale, 3);
        Assert.Equal(expectedInferenceSteps, numInferenceSteps);
    }

    [Fact]
    public async Task GenerateImagesAsync_AppendsRealismModifier()
    {
        var httpHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        HttpRequestMessage? capturedRequest = null;

        var responseJson = JsonSerializer.Serialize(new
        {
            id = "pred-2",
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

        var configuration = BuildConfiguration();
        var webhookResolver = new Mock<IWebhookUrlResolver>();
        webhookResolver
            .Setup(x => x.GetWebhookUrlAsync(It.IsAny<string>()))
            .ReturnsAsync("https://example.com/webhook");

        var loggerMock = new Mock<ILogger<ReplicateApiClient>>();
        await using var db = BuildDbContext();
        db.Styles.Add(new Style
        {
            Id = 1001,
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
            configuration,
            loggerMock.Object,
            db,
            webhookResolver.Object);

        await client.GenerateImagesAsync(
            trainedModelVersion: "owner/model:version",
            userId: "u2",
            style: "casual",
            userInfo: new UserInfo { Gender = "male", Ethnicity = "asian" },
            numOutputs: 2);

        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedRequest!.Content);

        var body = await capturedRequest.Content!.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var input = document.RootElement.GetProperty("input");
        var prompt = input.GetProperty("prompt").GetString() ?? string.Empty;

        var allowedModifiers = new[]
        {
            "natural skin texture",
            "soft natural finish",
            "soft natural sheen",
            "realistic skin detail",
            "unretouched look",
            "candid lighting"
        };

        Assert.Contains(allowedModifiers, modifier => prompt.Contains(modifier, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("slight skin redness", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fine facial hair", prompt, StringComparison.OrdinalIgnoreCase);
    }

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Replicate:ApiToken"] = new string('R', 40),
                ["Replicate:StyleTuning:DefaultGuidanceScale"] = "3.0",
                ["Replicate:StyleTuning:DefaultNumInferenceSteps"] = "28",
                ["Replicate:StyleTuning:ProGuidanceScale"] = "2.8",
                ["Replicate:StyleTuning:ProNumInferenceSteps"] = "34",
                ["Replicate:StyleTuning:CasualGuidanceScale"] = "2.3",
                ["Replicate:StyleTuning:CasualNumInferenceSteps"] = "40",
                ["Replicate:StyleTuning:ProStyles:0"] = "executive",
                ["Replicate:StyleTuning:CasualRelaxedStyles:0"] = "casual"
            })
            .Build();
    }

    private static ApplicationDbContext BuildDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task SeedStylesAsync(ApplicationDbContext db)
    {
        db.Styles.AddRange(
            new Style
            {
                Id = 1000,
                Name = "executive",
                Description = "Executive leadership portrait",
                PromptTemplate = "A photo of {subject}, executive leadership portrait of {gender} {ethnicity}, formal attire",
                NegativePromptTemplate = "casual clothes, unprofessional, waxy skin, plastic skin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Style
            {
                Id = 1002,
                Name = "casual",
                Description = "Casual style",
                PromptTemplate = "A photo of {subject}, casual portrait",
                NegativePromptTemplate = "formal business attire, suit, tie, waxy skin, plastic skin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        await db.SaveChangesAsync();
    }
}
