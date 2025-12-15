using System.Security.Claims;
using AI.ProfilePhotoMaker.API.Constants;
using AI.ProfilePhotoMaker.API.Controllers;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Unit;

public class ModelStatusCreditImpactTests
{
    [Fact]
    public async Task Get_ReturnsCorrelationBasedTrainingCreditImpact_WhenPresent()
    {
        var userId = "user-1";
        var requestId = "req-abc";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var db = new ApplicationDbContext(options);

        var profile = new UserProfile
        {
            UserId = userId,
            Credits = 0,
            PurchasedCredits = 0,
            SubscriptionTier = SubscriptionTier.Basic,
            LastCreditReset = DateTime.UtcNow,
            ProcessedImages = new List<ProcessedImage>()
        };

        for (var i = 0; i < 10; i++)
        {
            profile.ProcessedImages.Add(new ProcessedImage
            {
                Style = ImageConstants.OriginalStyle,
                CreatedAt = DateTime.UtcNow.AddMinutes(-30),
                UserProfile = profile
            });
        }

        db.UserProfiles.Add(profile);

        var failedRequest = new ModelCreationRequest
        {
            Id = requestId,
            UserId = userId,
            Status = ModelCreationStatus.Failed,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            CompletedAt = DateTime.UtcNow.AddMinutes(-4),
            ErrorMessage = "boom"
        };
        db.ModelCreationRequests.Add(failedRequest);

        // Add other nearby usage that would be caught by time-window logic, to prove correlation filtering is used.
        db.UsageLogs.Add(new UsageLog
        {
            UserId = userId,
            Action = "model_training",
            Details = "Consumed 15 credits; correlationId=other",
            CreditsCost = 15,
            CreditsRemaining = 0,
            CreatedAt = failedRequest.CreatedAt.AddSeconds(5)
        });

        db.UsageLogs.Add(new UsageLog
        {
            UserId = userId,
            Action = "model_training",
            Details = $"Consumed 15 credits; correlationId={requestId}",
            CreditsCost = 15,
            CreditsRemaining = 0,
            CreatedAt = failedRequest.CreatedAt.AddSeconds(10)
        });

        db.UsageLogs.Add(new UsageLog
        {
            UserId = userId,
            Action = "model_training_refund",
            Details = $"Refunded 15 credits; correlationId={requestId}",
            CreditsCost = -15,
            CreditsRemaining = 15,
            CreatedAt = failedRequest.CompletedAt!.Value.AddSeconds(5)
        });

        await db.SaveChangesAsync();

        var replicateMock = new Mock<IReplicateApiClient>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<ModelStatusController>>();
        var envMock = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();

        var controller = new ModelStatusController(db, loggerMock.Object, replicateMock.Object, envMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId)
                    }, "Test"))
                }
            }
        };

        var result = await controller.Get();
        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ModelStatusResponse>(ok.Value);

        Assert.Equal(UnifiedModelStatusCode.Failed, payload.StatusCode);
        Assert.NotNull(payload.CreditImpact);
        Assert.Equal(15, payload.CreditImpact!.ChargedCredits);
        Assert.Equal(15, payload.CreditImpact.RefundedCredits);
        Assert.Equal(0, payload.CreditImpact.NetCredits);
    }
}

