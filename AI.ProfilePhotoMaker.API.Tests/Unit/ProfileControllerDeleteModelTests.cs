using System.Security.Claims;
using AI.ProfilePhotoMaker.API.Controllers;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Unit;

public class ProfileControllerDeleteModelTests
{
    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task DeleteAIModel_DeletesAllRecords_AndSetsTimestamp_ComprehensiveTest()
    {
        // Arrange: in-memory db and real repository
        var db = CreateDb();
        var repo = new UserProfileRepository(db);
        var env = Mock.Of<IWebHostEnvironment>();
        var logger = Mock.Of<ILogger<ProfileController>>();
        var config = Mock.Of<IConfiguration>();

        // Replicate client mock: pretend models do not exist on remote to skip delete calls
        var replicate = new Mock<IReplicateApiClient>();
        replicate.Setup(x => x.CheckModelExistsAsync(It.IsAny<string>())).ReturnsAsync(false);

        var controller = new ProfileController(repo, db, env, logger, config, replicate.Object);

        var userId = "unit-user-comprehensive";
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId)
                }, "test"))
            }
        };

        // Seed profile
        var profile = new UserProfile { UserId = userId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.UserProfiles.Add(profile);
        await db.SaveChangesAsync();

        // Seed ALL types of model requests to ensure complete deletion
        db.ModelCreationRequests.AddRange(new[]
        {
            new ModelCreationRequest
            {
                UserId = userId,
                ModelName = "pending-model",
                Status = ModelCreationStatus.Pending,
                ReplicateModelId = null,
                CreatedAt = DateTime.UtcNow
            },
            new ModelCreationRequest
            {
                UserId = userId,
                ModelName = "creating-model",
                Status = ModelCreationStatus.Creating,
                ReplicateModelId = "mock/creating",
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            },
            new ModelCreationRequest
            {
                UserId = userId,
                ModelName = "ready-model",
                Status = ModelCreationStatus.Ready,
                ReplicateModelId = "mock/ready",
                TrainedModelVersion = "v1",
                CompletedAt = DateTime.UtcNow.AddMinutes(-10)
            },
            new ModelCreationRequest
            {
                UserId = userId,
                ModelName = "failed-model",
                Status = ModelCreationStatus.Failed,
                ReplicateModelId = "mock/failed",
                ErrorMessage = "Training failed",
                CompletedAt = DateTime.UtcNow.AddMinutes(-15)
            },
            new ModelCreationRequest
            {
                UserId = userId,
                ModelName = "legacy-deleted",
                Status = ModelCreationStatus.Failed,
                ReplicateModelId = "mock/legacy",
                ErrorMessage = "Model deleted by user", // Legacy deletion pattern
                CompletedAt = DateTime.UtcNow.AddMinutes(-20)
            }
        });
        await db.SaveChangesAsync();

        var initialCount = await db.ModelCreationRequests.CountAsync(m => m.UserId == userId);
        var beforeDeletion = DateTime.UtcNow;

        // Act
        var result = await controller.DeleteAIModel();

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // Verify ALL records are deleted regardless of status
        var remainingRecords = db.ModelCreationRequests.Where(m => m.UserId == userId).ToList();
        remainingRecords.Should().BeEmpty("All ModelCreationRequest records should be deleted regardless of status");

        // Verify profile was updated (UpdatedAt changed)
        var updatedProfile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        updatedProfile!.UpdatedAt.Should().BeAfter(beforeDeletion, "Profile UpdatedAt should be refreshed on deletion");

        // Log comprehensive validation
        Console.WriteLine($"✅ Comprehensive deletion test passed:");
        Console.WriteLine($"   - Deleted {initialCount} records of all statuses");
        Console.WriteLine($"   - Updated profile timestamp: {updatedProfile.UpdatedAt}");
    }

    [Fact]
    public async Task DeleteAIModel_MarksAllRequestsFailed_AndClearsIdentifiers()
    {
        // Arrange: in-memory db and real repository
        var db = CreateDb();
        var repo = new UserProfileRepository(db);
        var env = Mock.Of<IWebHostEnvironment>();
        var logger = Mock.Of<ILogger<ProfileController>>();
        var config = Mock.Of<IConfiguration>();

        // Replicate client mock: pretend models do not exist on remote to skip delete calls
        var replicate = new Mock<IReplicateApiClient>();
        replicate.Setup(x => x.CheckModelExistsAsync(It.IsAny<string>())).ReturnsAsync(false);

        var controller = new ProfileController(repo, db, env, logger, config, replicate.Object);

        var userId = "unit-user-1";
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId)
                }, "test"))
            }
        };

        // Seed profile and two model requests
        var profile = new UserProfile { UserId = userId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.UserProfiles.Add(profile);
        await db.SaveChangesAsync();

        db.ModelCreationRequests.Add(new ModelCreationRequest
        {
            UserId = userId,
            ModelName = "ready-model",
            Status = ModelCreationStatus.Ready,
            ReplicateModelId = "mock/ready",
            TrainedModelVersion = "v1",
            CompletedAt = DateTime.UtcNow
        });
        db.ModelCreationRequests.Add(new ModelCreationRequest
        {
            UserId = userId,
            ModelName = "failed-model",
            Status = ModelCreationStatus.Failed,
            ReplicateModelId = "mock/failed",
            TrainedModelVersion = "v0",
            CompletedAt = DateTime.UtcNow.AddMinutes(-1),
            ErrorMessage = "previous failure"
        });
        await db.SaveChangesAsync();

        // Act
        var result = await controller.DeleteAIModel();

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        db.ModelCreationRequests.Where(m => m.UserId == userId).ToList().Should().BeEmpty();
    }
}
