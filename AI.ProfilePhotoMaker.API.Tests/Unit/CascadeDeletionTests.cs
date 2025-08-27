using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Moq;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Data;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Unit;

/// <summary>
/// Tests to validate cascade deletion functionality using MockReplicateApiClient
/// </summary>
public class CascadeDeletionTests
{
    private readonly Mock<ILogger<MockReplicateApiClient>> _loggerMock;

    public CascadeDeletionTests()
    {
        _loggerMock = new Mock<ILogger<MockReplicateApiClient>>();
    }

    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task MockReplicateApiClient_CascadeDeletion_DeletesVersionsFirstThenModel()
    {
        // Arrange - Clear static state for test isolation
        MockReplicateApiClient.ClearStaticState();
        using var context = CreateInMemoryDbContext();
        var mockClient = new MockReplicateApiClient(context, _loggerMock.Object);

        // Create a model (this automatically creates 3 versions)
        var modelId = await mockClient.CreateModelAsync("test-user", "test-model");
        
        // Verify model has versions initially
        var initialVersions = await mockClient.GetModelVersionsAsync(modelId);
        Assert.True(initialVersions.Count > 0, "Model should have versions after creation");

        // Act - First attempt should fail due to existing versions
        var (initialSuccess, initialError) = await mockClient.DeleteModelAsync(modelId);

        // Assert - Should fail with "has existing versions" error
        Assert.False(initialSuccess);
        Assert.Contains("existing versions and cannot be deleted", initialError);

        // Act - Delete all versions manually
        foreach (var versionId in initialVersions)
        {
            var (versionSuccess, versionError) = await mockClient.DeleteModelVersionAsync(modelId, versionId);
            Assert.True(versionSuccess, $"Should successfully delete version {versionId}");
            Assert.Null(versionError);
        }

        // Verify no versions remain
        var remainingVersions = await mockClient.GetModelVersionsAsync(modelId);
        Assert.Empty(remainingVersions);

        // Act - Now delete the model (should succeed)
        var (finalSuccess, finalError) = await mockClient.DeleteModelAsync(modelId);

        // Assert - Should succeed after versions are deleted
        Assert.True(finalSuccess);
        Assert.Null(finalError);
    }

    [Fact]
    public async Task MockReplicateApiClient_GetModelVersions_ReturnsExpectedVersions()
    {
        // Arrange - Clear static state for test isolation
        MockReplicateApiClient.ClearStaticState();
        using var context = CreateInMemoryDbContext();
        var mockClient = new MockReplicateApiClient(context, _loggerMock.Object);

        // Create a model (automatically creates versions)
        var modelId = await mockClient.CreateModelAsync("test-user", "test-model");
        
        // Act
        var versions = await mockClient.GetModelVersionsAsync(modelId);

        // Assert
        Assert.NotEmpty(versions);
        Assert.True(versions.Count >= 3, "Should have at least 3 mock versions");
        Assert.All(versions, version => Assert.Contains(modelId, version));
    }

    [Fact]
    public async Task MockReplicateApiClient_DeleteModelVersion_ReducesVersionCount()
    {
        // Arrange - Clear static state for test isolation
        MockReplicateApiClient.ClearStaticState();
        using var context = CreateInMemoryDbContext();
        var mockClient = new MockReplicateApiClient(context, _loggerMock.Object);

        var modelId = await mockClient.CreateModelAsync("test-user", "test-model");
        var initialVersions = await mockClient.GetModelVersionsAsync(modelId);
        var versionToDelete = initialVersions.First();

        // Act
        var (success, error) = await mockClient.DeleteModelVersionAsync(modelId, versionToDelete);

        // Assert
        Assert.True(success);
        Assert.Null(error);

        // Verify version count decreased
        var remainingVersions = await mockClient.GetModelVersionsAsync(modelId);
        Assert.Equal(initialVersions.Count - 1, remainingVersions.Count);
        Assert.DoesNotContain(versionToDelete, remainingVersions);
    }
}