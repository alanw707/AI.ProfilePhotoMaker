using Xunit;
using FluentAssertions;
using System.Reflection;
using AI.ProfilePhotoMaker.API.Controllers;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Data;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Tests.Controllers;

/// <summary>
/// Tests for the FormatModelVersion helper method in ReplicateController
/// </summary>
public class ReplicateControllerFormatVersionTests
{
    /// <summary>
    /// Build a ReplicateController instance with minimal dependencies for reflection tests
    /// </summary>
    private static ReplicateController CreateController(string owner = "alanw707")
    {
        var client = new Mock<IReplicateApiClient>(MockBehavior.Strict).Object;
        var basic = new Mock<IBasicTierService>(MockBehavior.Strict).Object;

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;
        var db = new ApplicationDbContext(options);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["Replicate:Owner"] = owner
            })
            .Build();

        var logger = new Mock<ILogger<ReplicateController>>().Object;
        return new ReplicateController(client, basic, db, config, logger);
    }

    /// <summary>
    /// Helper to call the private instance FormatModelVersion via reflection
    /// </summary>
    private static string FormatModelVersion(string replicateModelId, string trainedModelVersion)
    {
        var controller = CreateController();
        var method = typeof(ReplicateController).GetMethod(
            name: "FormatModelVersion",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(string), typeof(string) },
            modifiers: null);

        if (method == null)
            throw new InvalidOperationException("FormatModelVersion method not found");

        var result = method.Invoke(controller, new object[] { replicateModelId, trainedModelVersion });
        return (string)result!;
    }

    [Theory]
    [InlineData("alanw707/user-12345", "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890", "alanw707/user-12345:abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890")]
    [InlineData("mock/user-test", "1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef", "alanw707/user-test:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef")]
    public void FormatModelVersion_WithVersionHashOnly_ShouldFormatCorrectly(
        string replicateModelId, 
        string trainedModelVersion, 
        string expected)
    {
        // Act
        var result = FormatModelVersion(replicateModelId, trainedModelVersion);
        
        // Assert
        result.Should().Be(expected);
        result.Should().MatchRegex(@"^alanw707\/[\w-]+:[a-fA-F0-9]{64}$");
    }

    [Theory]
    [InlineData("alanw707/user-existing:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef")]
    [InlineData("alanw707/another-model:abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890")]
    public void FormatModelVersion_WithAlreadyFormattedVersion_ShouldReturnAsIs(string alreadyFormatted)
    {
        // Act
        var result = FormatModelVersion("alanw707/some-model", alreadyFormatted);
        
        // Assert
        result.Should().Be(alreadyFormatted);
        result.Should().MatchRegex(@"^alanw707\/[\w-]+:[a-fA-F0-9]{64}$");
    }

    [Fact]
    public void FormatModelVersion_ExtractsModelNameCorrectly()
    {
        // Arrange
        var replicateModelId = "alanw707/user-complex-name-123";
        var versionHash = "1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
        
        // Act
        var result = FormatModelVersion(replicateModelId, versionHash);
        
        // Assert
        result.Should().Be("alanw707/user-complex-name-123:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef");
    }
}
