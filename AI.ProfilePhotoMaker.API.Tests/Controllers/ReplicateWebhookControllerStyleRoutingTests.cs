using System.Text.Json;
using AI.ProfilePhotoMaker.API.Controllers;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Hubs;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.Replicate;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services.Notifications;
using AI.ProfilePhotoMaker.API.Services.Storage;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AI.ProfilePhotoMaker.API.Tests.Controllers;

public class ReplicateWebhookControllerStyleRoutingTests
{
    [Fact]
    public async Task PredictionComplete_UsesPersistedResolvedStyleAndPredictionUser_WhenPayloadMismatches()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"WebhookStyleRouting_{Guid.NewGuid()}")
            .Options;

        await using var db = new ApplicationDbContext(options);
        db.UserProfiles.Add(new UserProfile
        {
            Id = 1,
            UserId = "user-db",
            CreatedAt = DateTime.UtcNow
        });
        db.Predictions.Add(new Prediction
        {
            Id = "pred-123",
            UserId = "user-db",
            Style = "linkedin",
            RequestedStyle = "casual",
            ResolvedStyle = "linkedin",
            ResolvedStyleId = 4,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var logger = Mock.Of<ILogger<ReplicateWebhookController>>();
        var replicateClient = new Mock<IReplicateApiClient>(MockBehavior.Strict);
        var imageDownload = new Mock<IImageDownloadService>(MockBehavior.Strict);
        var storage = new Mock<IStorageService>(MockBehavior.Strict);
        var hub = new Mock<IHubContext<PredictionHub>>(MockBehavior.Loose);
        var email = new Mock<IEmailNotificationService>(MockBehavior.Loose);

        imageDownload
            .Setup(s => s.DownloadImagesWithDetailsAsync(It.IsAny<List<string>>(), "user-db", "linkedin"))
            .ReturnsAsync(new List<ImageDownloadResult>
            {
                new() { Success = true, StoragePath = "generated/user-db/linkedin/1.png", FileName = "1.png" }
            });

        storage
            .Setup(s => s.GetImageUrl("generated/user-db/linkedin/1.png"))
            .Returns("https://cdn.example/generated/user-db/linkedin/1.png");

        var controller = new ReplicateWebhookController(
            logger,
            db,
            replicateClient.Object,
            imageDownload.Object,
            storage.Object,
            hub.Object,
            email.Object);

        var payload = new ReplicatePredictionResult
        {
            Id = "pred-123",
            Status = "succeeded",
            Input = new Dictionary<string, object>
            {
                ["user_id"] = "payload-user",
                ["style"] = "fitness"
            },
            Output = JsonSerializer.SerializeToElement(new[] { "https://example.com/out-1.png" }),
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };

        var result = await controller.PredictionComplete(payload) as ObjectResult;
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);

        var saved = await db.ProcessedImages.OrderByDescending(i => i.Id).FirstAsync();
        saved.Style.Should().Be("linkedin");
        saved.UserProfileId.Should().Be(1);

        imageDownload.VerifyAll();
        storage.VerifyAll();
    }
}
