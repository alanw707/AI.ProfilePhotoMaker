using System.Collections.Generic;
using System.Security.Claims;
using AI.ProfilePhotoMaker.API.Controllers;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Models.Replicate;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AI.ProfilePhotoMaker.API.Tests.Controllers;

public class ReplicateControllerAuthAndOwnershipTests
{
    private static ApplicationDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;
        var ctx = new ApplicationDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static ReplicateController CreateController(
        string userId,
        ApplicationDbContext db,
        Mock<IReplicateApiClient>? mockReplicate = null,
        Mock<IBasicTierService>? mockBasic = null,
        Mock<IPendingGenerationService>? mockPending = null,
        IConfiguration? configuration = null,
        Mock<AI.ProfilePhotoMaker.API.Services.Storage.IStorageService>? storage = null)
    {
        mockReplicate ??= new Mock<IReplicateApiClient>(MockBehavior.Strict);
        mockBasic ??= new Mock<IBasicTierService>(MockBehavior.Strict);
        mockPending ??= new Mock<IPendingGenerationService>(MockBehavior.Strict);
        storage ??= new Mock<AI.ProfilePhotoMaker.API.Services.Storage.IStorageService>(MockBehavior.Strict);

        var config = configuration ?? new Mock<IConfiguration>().Object;

        var controller = new ReplicateController(
            mockReplicate.Object,
            mockBasic.Object,
            db,
            config,
            new NullLogger<ReplicateController>(),
            mockPending.Object,
            storage.Object);

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            }, "TestAuth"))
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    [Fact]
    public async Task TrainModel_MismatchedUserIdInDto_ReturnsBadRequest()
    {
        using var db = CreateInMemoryDb();

        var mockReplicate = new Mock<IReplicateApiClient>(MockBehavior.Strict);
        var mockBasic = new Mock<IBasicTierService>(MockBehavior.Strict);

        // Credits sufficient so code reaches our InvalidUserContext gate
        mockBasic.Setup(s => s.GetCreditBreakdownAsync("user-123"))
                 .ReturnsAsync((999, 999));
        mockBasic.Setup(s => s.ConsumeCreditsAsync("user-123", "model_training", It.IsAny<string?>()))
                 .ReturnsAsync(CreditConsumptionResult.Succeeded("model_training", 0, CreditCostConfig.GetCreditCost("model_training")));
        mockBasic.Setup(s => s.RefundCreditsAsync("user-123", It.IsAny<CreditConsumptionResult>()))
                 .ReturnsAsync(true);

        var controller = CreateController("user-123", db, mockReplicate, mockBasic);

        var dto = new TrainModelRequestDto
        {
            UserId = "other-user",
            ImageZipUrl = "https://example.com/images.zip"
        };

        var result = await controller.TrainModel(dto) as BadRequestObjectResult;
        result.Should().NotBeNull();

        mockBasic.VerifyAll();
        mockReplicate.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GenerateImages_MismatchedUserIdInDto_ReturnsBadRequest()
    {
        using var db = CreateInMemoryDb();
        var mockReplicate = new Mock<IReplicateApiClient>(MockBehavior.Strict);
        var mockBasic = new Mock<IBasicTierService>(MockBehavior.Strict);

        // Enough credits
        mockBasic.Setup(s => s.GetCreditBreakdownAsync("user-123"))
                 .ReturnsAsync((999, 999));

        var controller = CreateController("user-123", db, mockReplicate, mockBasic);

        var dto = new GenerateImagesRequestDto
        {
            TrainedModelVersion = "owner/model:version",
            UserId = "other-user",
            Style = "corporate",
            NumOutputs = 1
        };

        var result = await controller.GenerateImages(dto) as BadRequestObjectResult;
        result.Should().NotBeNull();

        mockBasic.VerifyAll();
        mockReplicate.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GenerateBatchImages_MismatchedUserIdInDto_ReturnsBadRequest()
    {
        using var db = CreateInMemoryDb();
        var mockReplicate = new Mock<IReplicateApiClient>(MockBehavior.Strict);
        var mockBasic = new Mock<IBasicTierService>(MockBehavior.Strict);

        // Credits sufficient
        mockBasic.Setup(s => s.GetCreditBreakdownAsync("user-123"))
                 .ReturnsAsync((999, 999));

        var controller = CreateController("user-123", db, mockReplicate, mockBasic);

        var dto = new GenerateBatchImagesRequestDto
        {
            TrainedModelVersion = "owner/model:version",
            UserId = "other-user",
            Styles = new List<string> { "corporate" },
            NumOutputsPerStyle = 1
        };

        var result = await controller.GenerateBatchImages(dto) as BadRequestObjectResult;
        result.Should().NotBeNull();

        mockBasic.VerifyAll();
        mockReplicate.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetTrainingStatus_EnforcesOwnership()
    {
        using var db = CreateInMemoryDb();
        db.ModelCreationRequests.Add(new ModelCreationRequest
        {
            UserId = "user-123",
            ModelName = "user-user-123-123",
            ReplicateModelId = "owner/user-user-123-123",
            Status = ModelCreationStatus.Creating,
            PendingTrainingRequestId = "train-1",
            CreatedAt = DateTime.UtcNow
        });
        db.ModelCreationRequests.Add(new ModelCreationRequest
        {
            UserId = "other-user",
            ModelName = "user-other-1",
            ReplicateModelId = "owner/user-other-1",
            Status = ModelCreationStatus.Creating,
            PendingTrainingRequestId = "train-2",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var mockReplicate = new Mock<IReplicateApiClient>(MockBehavior.Strict);
        mockReplicate.Setup(c => c.GetTrainingStatusAsync("train-1"))
                     .ReturnsAsync(new ReplicateTrainingResult { Id = "train-1", Status = "succeeded", CreatedAt = DateTime.UtcNow });
        var mockBasic = new Mock<IBasicTierService>(MockBehavior.Strict);

        var controller = CreateController("user-123", db, mockReplicate, mockBasic);

        // Owned -> OK
        var ok = await controller.GetTrainingStatus("train-1") as ObjectResult;
        ok!.StatusCode.Should().Be(200);

        // Not owned -> NotFound
        var nf = await controller.GetTrainingStatus("train-2") as ObjectResult;
        nf!.StatusCode.Should().Be(404);

        mockReplicate.VerifyAll();
        mockBasic.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetPredictionStatus_EnforcesOwnership()
    {
        using var db = CreateInMemoryDb();
        db.Predictions.Add(new Prediction
        {
            Id = "pred-1",
            UserId = "user-123",
            Style = "corporate",
            CreatedAt = DateTime.UtcNow
        });
        db.Predictions.Add(new Prediction
        {
            Id = "pred-2",
            UserId = "other-user",
            Style = "corporate",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var mockReplicate = new Mock<IReplicateApiClient>(MockBehavior.Strict);
        mockReplicate.Setup(c => c.GetPredictionStatusAsync("pred-1"))
                     .ReturnsAsync(new ReplicatePredictionResult { Id = "pred-1", Status = "succeeded", CreatedAt = DateTime.UtcNow });
        var mockBasic = new Mock<IBasicTierService>(MockBehavior.Strict);

        var controller = CreateController("user-123", db, mockReplicate, mockBasic);

        // Owned -> OK
        var ok = await controller.GetPredictionStatus("pred-1") as ObjectResult;
        ok!.StatusCode.Should().Be(200);

        // Not owned -> NotFound
        var nf = await controller.GetPredictionStatus("pred-2") as ObjectResult;
        nf!.StatusCode.Should().Be(404);

        mockReplicate.VerifyAll();
        mockBasic.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task QueueGeneration_RejectsInvalidOutputsPerStyle()
    {
        using var db = CreateInMemoryDb();
        db.ModelCreationRequests.Add(new ModelCreationRequest
        {
            UserId = "user-123",
            PendingTrainingRequestId = "train-1",
            Status = ModelCreationStatus.Creating,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var pending = new Mock<IPendingGenerationService>(MockBehavior.Strict);
        var controller = CreateController("user-123", db, mockPending: pending);

        var badRequest = new QueueGenerationRequest
        {
            TrainingId = "train-1",
            Styles = new List<string> { "corporate" },
            NumOutputsPerStyle = 0
        };

        var result = await controller.QueueGeneration(badRequest) as BadRequestObjectResult;
        result.Should().NotBeNull();
        pending.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task QueueGeneration_Enqueues_OnValidRequest()
    {
        using var db = CreateInMemoryDb();
        db.ModelCreationRequests.Add(new ModelCreationRequest
        {
            UserId = "user-123",
            PendingTrainingRequestId = "train-1",
            Status = ModelCreationStatus.Creating,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var pending = new Mock<IPendingGenerationService>(MockBehavior.Strict);
        pending.Setup(p => p.EnqueueAsync("user-123", "train-1", It.IsAny<IEnumerable<string>>(), 2))
               .Returns(Task.CompletedTask)
               .Verifiable();

        var controller = CreateController("user-123", db, mockPending: pending);

        var goodRequest = new QueueGenerationRequest
        {
            TrainingId = "train-1",
            Styles = new List<string> { "corporate", "studio" },
            NumOutputsPerStyle = 2
        };

        var result = await controller.QueueGeneration(goodRequest) as OkObjectResult;
        result.Should().NotBeNull();
        pending.Verify();
    }

    [Fact]
    public async Task EnhancePhoto_FailsWhenCreditConsumptionFails()
    {
        using var db = CreateInMemoryDb();
        var userId = "user-123";

        var mockReplicate = new Mock<IReplicateApiClient>(MockBehavior.Strict);
        var mockBasic = new Mock<IBasicTierService>(MockBehavior.Strict);

        mockBasic.Setup(b => b.GetAvailableCreditsAsync(userId)).ReturnsAsync(1);
        mockBasic.Setup(b => b.ConsumeCreditsAsync(userId, It.IsAny<int>(), "photo_enhancement", It.IsAny<string?>()))
                 .ReturnsAsync(CreditConsumptionResult.Failed("photo_enhancement", "insufficient_credits"));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Replicate:FluxKontextProModelId"] = "owner/flux:version",
                ["ExternalApiBaseUrl"] = "https://example.com"
            })
            .Build();

        var controller = CreateController(userId, db, mockReplicate, mockBasic, configuration: config);
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "TestAuth"))
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var dto = new EnhancePhotoRequestDto { ImageUrl = "https://example.com/img.png", EnhancementType = "professional" };

        var result = await controller.EnhancePhoto(dto) as ObjectResult;
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(500);

        mockReplicate.VerifyNoOtherCalls();
        mockBasic.VerifyAll();
    }

    [Fact]
    public async Task EnhancePhoto_RefundsOnReplicateFailure()
    {
        using var db = CreateInMemoryDb();
        var userId = "user-123";

        var mockReplicate = new Mock<IReplicateApiClient>(MockBehavior.Strict);
        var mockBasic = new Mock<IBasicTierService>(MockBehavior.Strict);

        mockBasic.Setup(b => b.GetAvailableCreditsAsync(userId)).ReturnsAsync(2);
        var consumption = CreditConsumptionResult.Succeeded("photo_enhancement", 1, 0);
        mockBasic.Setup(b => b.ConsumeCreditsAsync(userId, It.IsAny<int>(), "photo_enhancement", It.IsAny<string?>()))
                 .ReturnsAsync(consumption);
        mockBasic.Setup(b => b.RefundCreditsAsync(userId, consumption)).ReturnsAsync(true);

        mockReplicate.Setup(r => r.EnhancePhotoAsync(userId, It.IsAny<string>(), It.IsAny<string>()))
                     .ThrowsAsync(new HttpRequestException("network"));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Replicate:FluxKontextProModelId"] = "owner/flux:version",
                ["ExternalApiBaseUrl"] = "https://example.com"
            })
            .Build();

        var controller = CreateController(userId, db, mockReplicate, mockBasic, configuration: config);
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "TestAuth"))
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var dto = new EnhancePhotoRequestDto { ImageUrl = "https://example.com/img.png", EnhancementType = "professional" };

        var result = await controller.EnhancePhoto(dto) as ObjectResult;
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(502);

        mockReplicate.VerifyAll();
        mockBasic.Verify(b => b.RefundCreditsAsync(userId, consumption), Times.Once);
    }
}
