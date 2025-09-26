using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Controllers;
using AI.ProfilePhotoMaker.API.Tests.Integration;
using AI.ProfilePhotoMaker.API.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;

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

        // Act - Verify FormatModelVersion constructs correct format using instance reflection
        var controller = new ReplicateController(
            scope.ServiceProvider.GetRequiredService<IReplicateApiClient>(),
            scope.ServiceProvider.GetRequiredService<IBasicTierService>(),
            dbContext,
            scope.ServiceProvider.GetRequiredService<IConfiguration>(),
            scope.ServiceProvider.GetRequiredService<ILogger<ReplicateController>>()
        );

        var formatMethod = typeof(ReplicateController).GetMethod(
            name: "FormatModelVersion",
            bindingAttr: System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(string), typeof(string) },
            modifiers: null);

        var result = (string)formatMethod!.Invoke(controller, new object[]
        {
            modelRequest.ReplicateModelId!,
            modelRequest.TrainedModelVersion!
        })!;

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
        using var scopeLegacy = _factory.Services.CreateScope();
        var spLegacy = scopeLegacy.ServiceProvider;
        var controller = new ReplicateController(
            spLegacy.GetRequiredService<IReplicateApiClient>(),
            spLegacy.GetRequiredService<IBasicTierService>(),
            spLegacy.GetRequiredService<ApplicationDbContext>(),
            spLegacy.GetRequiredService<IConfiguration>(),
            spLegacy.GetRequiredService<ILogger<ReplicateController>>()
        );
        var formatMethod = typeof(ReplicateController).GetMethod(
            name: "FormatModelVersion",
            bindingAttr: System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(string), typeof(string) },
            modifiers: null);

        // Test with legacy data that has owner prefix (should still work)
        var result = (string)formatMethod!.Invoke(controller, new object[]
        {
            "alanw707/legacy-model-with-prefix",  // Legacy format with owner prefix
            "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcd"
        })!;

        result.Should().Be("alanw707/legacy-model-with-prefix:abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcd");
    }

    [Fact]
    public void FormatModelVersion_WithNewCleanData_ShouldConstructCorrectFormat()
    {
        // Test with new clean data (no owner prefix)
        using var scopeClean = _factory.Services.CreateScope();
        var spClean = scopeClean.ServiceProvider;
        var controllerClean = new ReplicateController(
            spClean.GetRequiredService<IReplicateApiClient>(),
            spClean.GetRequiredService<IBasicTierService>(),
            spClean.GetRequiredService<ApplicationDbContext>(),
            spClean.GetRequiredService<IConfiguration>(),
            spClean.GetRequiredService<ILogger<ReplicateController>>()
        );
        var formatMethodClean = typeof(ReplicateController).GetMethod(
            name: "FormatModelVersion",
            bindingAttr: System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(string), typeof(string) },
            modifiers: null);

        var result = (string)formatMethodClean!.Invoke(controllerClean, new object[]
        {
            "clean-model-no-prefix",  // New format without owner prefix
            "fedcba0987654321fedcba0987654321fedcba0987654321fedcba0987654321"
        })!;

        result.Should().Be("alanw707/clean-model-no-prefix:fedcba0987654321fedcba0987654321fedcba0987654321fedcba0987654321");
    }
}
