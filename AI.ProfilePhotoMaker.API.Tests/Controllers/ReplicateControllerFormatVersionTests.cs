using Xunit;
using FluentAssertions;
using System.Reflection;
using AI.ProfilePhotoMaker.API.Controllers;

namespace AI.ProfilePhotoMaker.API.Tests.Controllers;

/// <summary>
/// Tests for the FormatModelVersion helper method in ReplicateController
/// </summary>
public class ReplicateControllerFormatVersionTests
{
    /// <summary>
    /// Helper method to call private static FormatModelVersion method using reflection
    /// </summary>
    private static string FormatModelVersion(string replicateModelId, string trainedModelVersion)
    {
        var method = typeof(ReplicateController).GetMethod("FormatModelVersion", 
            BindingFlags.NonPublic | BindingFlags.Static);
        
        if (method == null)
            throw new InvalidOperationException("FormatModelVersion method not found");
            
        var result = method.Invoke(null, new object[] { replicateModelId, trainedModelVersion });
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