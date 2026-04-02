using Xunit;
using FluentAssertions;
using AI.ProfilePhotoMaker.API.Controllers.Helpers;
using Microsoft.Extensions.Configuration;

namespace AI.ProfilePhotoMaker.API.Tests.Controllers;

/// <summary>
/// Tests for the FormatModelVersion helper method
/// </summary>
public class ReplicateControllerFormatVersionTests
{
    private static IConfiguration CreateConfiguration(string owner = "alanw707")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Replicate:Owner"] = owner
            })
            .Build();
    }

    private static string FormatModelVersion(string replicateModelId, string trainedModelVersion)
    {
        return ReplicateHelpers.FormatModelVersion(replicateModelId, trainedModelVersion, CreateConfiguration());
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
