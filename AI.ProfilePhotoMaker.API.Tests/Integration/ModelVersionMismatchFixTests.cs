using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Controllers;
using AI.ProfilePhotoMaker.API.Tests.Integration;
using AI.ProfilePhotoMaker.API.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;

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

        // Act - Verify FormatModelVersion constructs correct format using reflection
        var formatMethod = typeof(ReplicateController).GetMethod("FormatModelVersion", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        
        var result = (string)formatMethod!.Invoke(null, new object[] 
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
        var formatMethod = typeof(ReplicateController).GetMethod("FormatModelVersion", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        
        // Test with legacy data that has owner prefix (should still work)
        var result = (string)formatMethod!.Invoke(null, new object[] 
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
        var formatMethod = typeof(ReplicateController).GetMethod("FormatModelVersion", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        
        var result = (string)formatMethod!.Invoke(null, new object[] 
        { 
            "clean-model-no-prefix",  // New format without owner prefix
            "fedcba0987654321fedcba0987654321fedcba0987654321fedcba0987654321" 
        })!;

        result.Should().Be("alanw707/clean-model-no-prefix:fedcba0987654321fedcba0987654321fedcba0987654321fedcba0987654321");
    }
}