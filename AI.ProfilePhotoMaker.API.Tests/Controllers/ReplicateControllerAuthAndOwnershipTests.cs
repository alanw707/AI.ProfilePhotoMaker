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
        Mock<IBasicTierService>? mockBasic = null)
    {
        mockReplicate ??= new Mock<IReplicateApiClient>(MockBehavior.Strict);
        mockBasic ??= new Mock<IBasicTierService>(MockBehavior.Strict);

        var config = new Mock<IConfiguration>();

        var controller = new ReplicateController(
            mockReplicate.Object,
            mockBasic.Object,
            db,
            config.Object,
            new NullLogger<ReplicateController>());

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
                 .ReturnsAsync((weekly: 999, purchased: 999));

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
                 .ReturnsAsync((weekly: 999, purchased: 999));

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
                 .ReturnsAsync((weekly: 999, purchased: 999));

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
    public async Task GenerateBasicImage_ConsumesCreditAfterCreation()
    {
        using var db = CreateInMemoryDb();

        var mockReplicate = new Mock<IReplicateApiClient>(MockBehavior.Strict);
        var mockBasic = new Mock<IBasicTierService>(MockBehavior.Strict);

        // Available credits
        mockBasic.Setup(s => s.HasAvailableCreditsAsync("user-123")).ReturnsAsync(true);

        // Validate ordering: first Replicate call, then ConsumeCredits
        var sequence = new MockSequence();
        mockReplicate.InSequence(sequence)
                     .Setup(c => c.GenerateBasicImageAsync("user-123", It.IsAny<UserInfo?>(), "male"))
                     .ReturnsAsync(new ReplicatePredictionResult { Id = "pred-xyz", Status = "starting", CreatedAt = DateTime.UtcNow });

        mockBasic.InSequence(sequence)
                 .Setup(s => s.ConsumeCreditsAsync("user-123", "casual_headshot_generation"))
                 .ReturnsAsync(true);

        mockBasic.Setup(s => s.GetAvailableCreditsAsync("user-123")).ReturnsAsync(99);

        var controller = CreateController("user-123", db, mockReplicate, mockBasic);

        var dto = new GenerateBasicImageRequestDto { Gender = "male" };
        var result = await controller.GenerateBasicImage(dto) as ObjectResult;
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);

        mockReplicate.VerifyAll();
        mockBasic.VerifyAll();
    }
}
