using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Controllers.Helpers;
using AI.ProfilePhotoMaker.API.Tests.Integration;
using AI.ProfilePhotoMaker.API.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using AI.ProfilePhotoMaker.API.Tests.Infrastructure;

namespace AI.ProfilePhotoMaker.API.Tests.Integration;

/// <summary>
/// Integration tests to verify the ModelCreationRequest version ID mismatch fix
/// </summary>
public class ModelVersionMismatchFixTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ModelVersionMismatchFixTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Replicate:Owner", "alanw707" }
            })
            .Build();
    }

    [Fact]
    public async Task ModelCreation_ShouldStoreModelIdWithoutOwnerPrefix()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Create a model creation request as would happen during training
        var modelRequest = new ModelCreationRequest
        {
            UserId = "test-user-version-fix",
            ModelName = "test-model-version-fix",
            ReplicateModelId = "test-model-version-fix", // Should be stored WITHOUT owner prefix
            TrainedModelVersion = "1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef", // Valid 64-char hex
            Status = ModelCreationStatus.Ready,
            CompletedAt = DateTime.UtcNow
        };

        dbContext.ModelCreationRequests.Add(modelRequest);
        await dbContext.SaveChangesAsync();

        // Act - Verify FormatModelVersion constructs correct format
        var configuration = CreateConfiguration();
        var result = ReplicateHelpers.FormatModelVersion(
            modelRequest.ReplicateModelId!,
            modelRequest.TrainedModelVersion!,
            configuration);

        // Assert
        result.Should().Be("alanw707/test-model-version-fix:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef");

        // Verify the format matches the expected pattern for Replicate API
        result.Should().MatchRegex(@"^alanw707\/[\w-]+:[a-fA-F0-9]{64}$");

        // Verify model ID is stored without owner prefix
        modelRequest.ReplicateModelId.Should().Be("test-model-version-fix");
        modelRequest.ReplicateModelId.Should().NotStartWith("alanw707/");

        // Verify version is a valid 64-character hex string
        modelRequest.TrainedModelVersion.Should().MatchRegex(@"^[a-fA-F0-9]{64}$");
    }

    [Fact]
    public void FormatModelVersion_WithLegacyOwnerPrefixData_ShouldStillWork()
    {
        // Test backward compatibility with existing data that might have owner prefix
        var configuration = CreateConfiguration();

        // Test with legacy data that has owner prefix (should still work)
        var result = ReplicateHelpers.FormatModelVersion(
            "alanw707/legacy-model-with-prefix",  // Legacy format with owner prefix
            "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcd",
            configuration);

        result.Should().Be("alanw707/legacy-model-with-prefix:abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcd");
    }

    [Fact]
    public void FormatModelVersion_WithNewCleanData_ShouldConstructCorrectFormat()
    {
        // Test with new clean data (no owner prefix)
        var configuration = CreateConfiguration();

        var result = ReplicateHelpers.FormatModelVersion(
            "clean-model-no-prefix",  // New format without owner prefix
            "fedcba0987654321fedcba0987654321fedcba0987654321fedcba0987654321",
            configuration);

        result.Should().Be("alanw707/clean-model-no-prefix:fedcba0987654321fedcba0987654321fedcba0987654321fedcba0987654321");
    }
}
