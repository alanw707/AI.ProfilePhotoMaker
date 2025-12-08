using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Services;

public class PendingGenerationServiceTests
{
    [Fact]
    public async Task EnqueueAsync_Throws_OnInvalidOutputsPerStyle()
    {
        using var context = CreateContext();
        var service = CreateService(context);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.EnqueueAsync("user-1", "train-1", new[] { "style-1" }, 0));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.EnqueueAsync("user-1", "train-1", new[] { "style-1" }, 5));
    }

    [Fact]
    public async Task ProcessAsync_FailsRequest_WhenOutputsPerStyleInvalid()
    {
        using var context = CreateContext();
        var trainingId = "train-1";
        context.PendingGenerationRequests.Add(new PendingGenerationRequest
        {
            UserId = "user-1",
            TrainingRequestId = trainingId,
            StylesJson = "[\"corporate\"]",
            NumOutputsPerStyle = 0,
            Status = PendingGenerationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var replicate = new Mock<IReplicateApiClient>(MockBehavior.Strict);
        var basicTier = new Mock<IBasicTierService>(MockBehavior.Strict);
        var service = new PendingGenerationService(context, NullLogger<PendingGenerationService>.Instance, replicate.Object, basicTier.Object);

        await service.ProcessAsync(trainingId);

        var record = await context.PendingGenerationRequests.SingleAsync();
        Assert.Equal(PendingGenerationStatus.Failed, record.Status);
        Assert.Equal("Invalid outputs per style", record.ErrorMessage);

        replicate.VerifyNoOtherCalls();
        basicTier.VerifyNoOtherCalls();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new ApplicationDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static PendingGenerationService CreateService(ApplicationDbContext context)
    {
        return new PendingGenerationService(
            context,
            NullLogger<PendingGenerationService>.Instance,
            new Mock<IReplicateApiClient>(MockBehavior.Strict).Object,
            new Mock<IBasicTierService>(MockBehavior.Strict).Object);
    }
}
