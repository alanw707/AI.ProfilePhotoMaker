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
        Assert.Equal("formal business attire, suit, tie", negativePrompt.GetString());
    }
}

