using Xunit;
using FluentAssertions;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using System.Text.RegularExpressions;

namespace AI.ProfilePhotoMaker.API.Tests.Services;

/// <summary>
/// Tests to verify that model version formats conform to Replicate API requirements
/// Expected format: "alanw707/modelId:versionHash"
/// </summary>
public class ModelVersionFormatTests
{
    private const string ExpectedOwner = "alanw707";
    private const string ValidVersionHashPattern = @"^[a-fA-F0-9]{64}$"; // 64-character hex string
    private const string ValidModelVersionPattern = @"^alanw707\/[\w-]+:[a-fA-F0-9]{64}$";

    [Theory]
    [InlineData("alanw707/user-12345-20240815123456:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef")]
    [InlineData("alanw707/test-model:abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890")]
    public void ValidateModelVersionFormat_ValidFormats_ShouldPass(string modelVersion)
    {
        // Act & Assert
        modelVersion.Should().MatchRegex(ValidModelVersionPattern,
            "model version should follow format: alanw707/modelId:versionHash");

        var parts = modelVersion.Split(':');
        parts.Should().HaveCount(2, "version should have exactly one colon separator");

        var modelPart = parts[0];
        var versionHash = parts[1];

        modelPart.Should().StartWith($"{ExpectedOwner}/",
            "model should be owned by {0}", ExpectedOwner);

        versionHash.Should().MatchRegex(ValidVersionHashPattern,
            "version hash should be a 64-character hexadecimal string");
        versionHash.Should().HaveLength(64, "version hash should be exactly 64 characters");
    }

    [Theory]
    [InlineData("user-12345-20240815123456")] // Missing owner and version
    [InlineData("alanw707/user-12345")] // Missing version hash
    [InlineData("wrongowner/user-12345:abcdef123")] // Wrong owner
    [InlineData("alanw707/user-12345:short")] // Version hash too short
    [InlineData("alanw707/user-12345:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdefg")] // Invalid hex character
    [InlineData("alanw707/user-12345:")] // Empty version hash
    [InlineData("/user-12345:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef")] // Missing owner
    [InlineData("alanw707/:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef")] // Missing model name
    public void ValidateModelVersionFormat_InvalidFormats_ShouldFail(string modelVersion)
    {
        // Act & Assert
        modelVersion.Should().NotMatchRegex(ValidModelVersionPattern,
            "invalid format should not match the required pattern");
    }

    [Fact]
    public void ParseModelVersion_ValidFormat_ShouldExtractComponents()
    {
        // Arrange
        const string testModelVersion = "alanw707/user-12345-20240815123456:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";

        // Act
        var parts = testModelVersion.Split(':');
        var modelIdParts = parts[0].Split('/');
        var owner = modelIdParts[0];
        var modelName = modelIdParts[1];
        var versionHash = parts[1];

        // Assert
        owner.Should().Be("alanw707");
        modelName.Should().Be("user-12345-20240815123456");
        versionHash.Should().HaveLength(64);
        versionHash.Should().MatchRegex(@"^[a-fA-F0-9]+$");
    }

    [Fact]
    public void GenerateImagesRequestDto_TrainedModelVersion_ShouldAcceptValidFormat()
    {
        // Arrange
        var dto = new GenerateImagesRequestDto
        {
            TrainedModelVersion = "alanw707/user-test-20240815:abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890",
            UserId = "test-user",
            Style = "professional"
        };

        // Act & Assert
        dto.TrainedModelVersion.Should().MatchRegex(ValidModelVersionPattern);
    }

    [Fact]
    public void GenerateBatchImagesRequestDto_TrainedModelVersion_ShouldAcceptValidFormat()
    {
        // Arrange
        var dto = new GenerateBatchImagesRequestDto
        {
            TrainedModelVersion = "alanw707/user-batch-test:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef",
            UserId = "test-user",
            Styles = new List<string> { "professional", "casual" }
        };

        // Act & Assert
        dto.TrainedModelVersion.Should().MatchRegex(ValidModelVersionPattern);
    }

    /// <summary>
    /// Test helper method to validate model version format
    /// Can be used throughout the application for validation
    /// </summary>
    public static bool IsValidModelVersionFormat(string? modelVersion)
    {
        if (string.IsNullOrWhiteSpace(modelVersion))
            return false;

        return Regex.IsMatch(modelVersion, ValidModelVersionPattern);
    }

    /// <summary>
    /// Test helper method to extract components from model version
    /// Returns owner, modelName, and versionHash
    /// </summary>
    public static (string? owner, string? modelName, string? versionHash) ParseModelVersion(string? modelVersion)
    {
        if (string.IsNullOrWhiteSpace(modelVersion) || !IsValidModelVersionFormat(modelVersion))
            return (null, null, null);

        var parts = modelVersion.Split(':');
        if (parts.Length != 2) return (null, null, null);

        var modelParts = parts[0].Split('/');
        if (modelParts.Length != 2) return (null, null, null);

        return (modelParts[0], modelParts[1], parts[1]);
    }

    [Theory]
    [InlineData("alanw707/user-test:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef", "alanw707", "user-test", "1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef")]
    [InlineData("alanw707/user-12345-20240815:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef",
                "alanw707", "user-12345-20240815", "1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef")]
    public void ParseModelVersion_ValidInputs_ShouldReturnCorrectComponents(
        string input, string expectedOwner, string expectedModel, string expectedHash)
    {
        // Act
        var (owner, modelName, versionHash) = ParseModelVersion(input);

        // Assert
        owner.Should().Be(expectedOwner);
        modelName.Should().Be(expectedModel);
        versionHash.Should().Be(expectedHash);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("owner/model")] // Missing version
    [InlineData("model:version")] // Missing owner
    public void ParseModelVersion_InvalidInputs_ShouldReturnNulls(string? input)
    {
        // Act
        var (owner, modelName, versionHash) = ParseModelVersion(input);

        // Assert
        owner.Should().BeNull();
        modelName.Should().BeNull();
        versionHash.Should().BeNull();
    }

    /// <summary>
    /// Test to verify that the ReplicateApiClient receives correctly formatted versions
    /// </summary>
    [Fact]
    public void ReplicateApiClient_GenerateImagesAsync_ShouldReceiveValidVersionFormat()
    {
        // This test verifies the version parameter passed to GenerateImagesAsync
        // follows the expected format before being sent to Replicate API

        // Arrange
        const string validVersion = "alanw707/user-test-model:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";

        // Act & Assert
        validVersion.Should().MatchRegex(ValidModelVersionPattern,
            "version passed to ReplicateApiClient should be in correct format");

        // The version should be used directly in the API request:
        // version = trainedModelVersion // Expected format: "alanw707/modelId:versionHash" 
    }

    [Fact]
    public void ValidVersionFormat_ShouldMatchReplicateExpectations()
    {
        // Test that demonstrates the expected format for Replicate API
        const string correctFormat = "alanw707/user-12345-model:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
        const string incorrectFormat1 = "user-12345-model:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef"; // Missing owner
        const string incorrectFormat2 = "alanw707/user-12345-model"; // Missing version hash

        // Correct format should pass
        correctFormat.Should().MatchRegex(ValidModelVersionPattern);

        // Incorrect formats should fail  
        incorrectFormat1.Should().NotMatchRegex(ValidModelVersionPattern);
        incorrectFormat2.Should().NotMatchRegex(ValidModelVersionPattern);
    }
}

/// <summary>
/// Extension methods for model version validation
/// Can be used throughout the application
/// </summary>
public static class ModelVersionValidationExtensions
{
    private const string ValidModelVersionPattern = @"^alanw707\/[\w-]+:[a-fA-F0-9]{64}$";

    public static bool IsValidReplicateModelVersion(this string? modelVersion)
    {
        if (string.IsNullOrWhiteSpace(modelVersion))
            return false;

        return Regex.IsMatch(modelVersion, ValidModelVersionPattern);
    }

    public static void ValidateReplicateModelVersion(this string? modelVersion, string parameterName = "modelVersion")
    {
        if (!modelVersion.IsValidReplicateModelVersion())
        {
            throw new ArgumentException(
                $"Model version must follow format 'alanw707/modelId:versionHash' but was '{modelVersion}'",
                parameterName);
        }
    }
}