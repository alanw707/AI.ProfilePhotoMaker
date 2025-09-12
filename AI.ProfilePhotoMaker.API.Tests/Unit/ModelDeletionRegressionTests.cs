using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;

namespace AI.ProfilePhotoMaker.API.Tests.Unit;

/// <summary>
/// Regression tests for model deletion functionality
/// Tests the new proactive deletion logic with always-fetch-versions approach
/// </summary>
public class ModelDeletionRegressionTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly Mock<ILogger<ReplicateApiClient>> _loggerMock;
    private readonly ApplicationDbContext _context;
    private bool _disposed;

    public ModelDeletionRegressionTests()
    {
        _httpHandlerMock = new Mock<HttpMessageHandler>();
        _loggerMock = new Mock<ILogger<ReplicateApiClient>>();
        _context = CreateInMemoryDbContext();
    }

    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private ReplicateApiClient CreateReplicateApiClient()
    {
        // Create a proper configuration mock with required settings
        var configurationMock = new Mock<IConfiguration>();
        configurationMock.Setup(x => x["Replicate:ApiToken"]).Returns("test-token");
        
        // Create mock webhook resolver
        var webhookResolver = new Mock<IWebhookUrlResolver>();
        
        // Create HttpClient with our mocked handler
        var httpClient = new HttpClient(_httpHandlerMock.Object);
        
        return new ReplicateApiClient(
            httpClient,
            configurationMock.Object,
            _loggerMock.Object,
            _context,
            webhookResolver.Object);
    }

    /// <summary>
    /// Tests new proactive flow: model with empty versions response is handled gracefully
    /// New behavior: always fetch versions first, then delete model directly (no retries)
    /// </summary>
    [Fact]
    public async Task DeleteModelAsync_WithEmptyVersionsResponse_ReturnsSuccess()
    {
        // Arrange
        var modelId = "test/model-with-empty-versions";
        
        // Mock the model exists check (returns 200)
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri != null &&
                    req.RequestUri.AbsolutePath.EndsWith($"/models/{modelId}") &&
                    !req.RequestUri.AbsolutePath.Contains("/versions")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        // NEW: Mock the proactive versions fetch (returns empty response)
        var versionsResponse = new HttpResponseMessage(HttpStatusCode.OK);
        versionsResponse.Content = new StringContent("", Encoding.UTF8, "application/json");
        
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri != null &&
                    req.RequestUri.AbsolutePath.EndsWith($"/models/{modelId}/versions")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(versionsResponse);

        // NEW: Mock the direct model deletion (returns 204 - no versions to clean up)
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Delete && 
                    req.RequestUri != null &&
                    req.RequestUri.AbsolutePath.EndsWith($"/models/{modelId}") &&
                    !req.RequestUri.AbsolutePath.Contains("/versions")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NoContent));

        // Create the client with proper dependencies
        var client = CreateReplicateApiClient();

        // Act
        var (success, errorMessage) = await client.DeleteModelAsync(modelId);

        // Assert
        Assert.True(success, $"Expected success=true but got success={success} with errorMessage='{errorMessage}'");
        Assert.Null(errorMessage);

        // UPDATED: Verify proactive logging occurred
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => string.Concat(v).Contains("proactive version cleanup")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // UPDATED: Verify empty response warning is logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => string.Concat(v).Contains($"Empty response when fetching versions for model {modelId}")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // UPDATED: Verify versions are always fetched (proactive approach)
        _httpHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => 
                req.Method == HttpMethod.Get && 
                req.RequestUri!.ToString().EndsWith($"models/{modelId}/versions")),
            ItExpr.IsAny<CancellationToken>());
    }

    /// <summary>
    /// Tests new proactive flow: malformed JSON in versions response is handled gracefully
    /// New behavior: returns empty list, proceeds with model deletion (no error propagation)
    /// </summary>
    [Fact]
    public async Task DeleteModelAsync_WithMalformedVersionsJson_ReturnsSuccess()
    {
        // Arrange
        var modelId = "test/model-malformed-versions";
        
        // Mock the model exists check (returns 200)
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri != null &&
                    req.RequestUri.AbsolutePath.EndsWith($"/models/{modelId}") &&
                    !req.RequestUri.AbsolutePath.Contains("/versions")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        // NEW: Mock versions endpoint returning malformed JSON
        var versionsResponse = new HttpResponseMessage(HttpStatusCode.OK);
        versionsResponse.Content = new StringContent("{ invalid json content", Encoding.UTF8, "application/json");
        
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri != null &&
                    req.RequestUri.AbsolutePath.EndsWith($"/models/{modelId}/versions")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(versionsResponse);

        // NEW: Mock model deletion succeeds (no versions to interfere)
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Delete && 
                    req.RequestUri != null &&
                    req.RequestUri.AbsolutePath.EndsWith($"/models/{modelId}") &&
                    !req.RequestUri.AbsolutePath.Contains("/versions")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NoContent));

        // Create the client with proper dependencies
        var client = CreateReplicateApiClient();

        // Act
        var (success, errorMessage) = await client.DeleteModelAsync(modelId);

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);

        // UPDATED: Verify JSON parsing error is logged but doesn't fail the operation
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => string.Concat(v).Contains("Invalid JSON response when fetching versions")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests successful model deletion without versions
    /// New behavior: Always checks for versions first, then deletes model
    /// </summary>
    [Fact]
    public async Task DeleteModelAsync_WithoutVersions_ReturnsSuccess()
    {
        // Arrange
        var modelId = "test/model-no-versions";
        
        // Mock the model exists check (returns 200)
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri != null &&
                    req.RequestUri.AbsolutePath.EndsWith($"/models/{modelId}") &&
                    !req.RequestUri.AbsolutePath.Contains("/versions")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        // NEW: Mock versions endpoint returns empty results array
        var versionsResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var versionsData = new { results = new object[0] };
        versionsResponse.Content = new StringContent(
            JsonSerializer.Serialize(versionsData), 
            Encoding.UTF8, "application/json");
        
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri != null &&
                    req.RequestUri.AbsolutePath.EndsWith($"/models/{modelId}/versions")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(versionsResponse);

        // Mock successful model deletion
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Delete && 
                    req.RequestUri != null &&
                    req.RequestUri.AbsolutePath.EndsWith($"/models/{modelId}") &&
                    !req.RequestUri.AbsolutePath.Contains("/versions")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NoContent));

        // Create the client with proper dependencies
        var client = CreateReplicateApiClient();

        // Act
        var (success, errorMessage) = await client.DeleteModelAsync(modelId);

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);

        // UPDATED: Verify proactive approach logs
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => (string.Concat(v).Contains("No versions found for model") && string.Concat(v).Contains("proceeding to model deletion"))),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteModelAsync_ModelNotFound_ReturnsSuccess()
    {
        // Arrange
        var modelId = "test/model-not-found";
        
        // Mock the model exists check (returns 404)
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri != null &&
                    req.RequestUri.AbsolutePath.EndsWith($"/models/{modelId}") &&
                    !req.RequestUri.AbsolutePath.Contains("/versions")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

        // Create the client with proper dependencies
        var client = CreateReplicateApiClient();

        // Act
        var (success, errorMessage) = await client.DeleteModelAsync(modelId);

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);

        // UPDATED: Verify versions are NOT fetched if model doesn't exist
        _httpHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.Is<HttpRequestMessage>(req => 
                req.Method == HttpMethod.Get && 
                req.RequestUri!.ToString().EndsWith($"models/{modelId}/versions")),
            ItExpr.IsAny<CancellationToken>());
    }

    /// <summary>
    /// Tests new proactive flow: model with valid versions gets cleaned up then deleted
    /// New behavior: Get versions → delete all versions → delete model (single flow, no retries)
    /// </summary>
    [Fact]
    public async Task DeleteModelAsync_WithValidVersions_ProactivelyDeletesVersionsAndModel()
    {
        // Arrange
        var modelId = "test/model-with-valid-versions";
        
        // Mock the model exists check (returns 200)
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri != null &&
                    req.RequestUri.AbsolutePath.EndsWith($"/models/{modelId}") &&
                    !req.RequestUri.AbsolutePath.Contains("/versions")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        // NEW: Mock the proactive versions fetch (returns valid versions)
        var versionsResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var versionsData = new { results = new[] { new { id = "version1" }, new { id = "version2" } } };
        versionsResponse.Content = new StringContent(
            JsonSerializer.Serialize(versionsData), 
            Encoding.UTF8, "application/json");
        
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri != null &&
                    req.RequestUri.AbsolutePath.EndsWith($"/models/{modelId}/versions")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(versionsResponse);

        // NEW: Mock version deletion (returns 204 for each version)
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Delete && 
                    req.RequestUri != null &&
                    req.RequestUri.AbsolutePath.Contains($"/models/{modelId}/versions/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NoContent));

        // NEW: Mock model deletion after versions cleanup (returns 204)
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Delete && 
                    req.RequestUri != null &&
                    req.RequestUri.AbsolutePath.EndsWith($"/models/{modelId}") &&
                    !req.RequestUri.AbsolutePath.Contains("/versions")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NoContent));

        // Create the client with proper dependencies
        var client = CreateReplicateApiClient();

        // Act
        var (success, errorMessage) = await client.DeleteModelAsync(modelId);

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);

        // UPDATED: Verify proactive version cleanup logging
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => (string.Concat(v).Contains("Found 2 versions for model") && string.Concat(v).Contains("cleaning up proactively"))),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // UPDATED: Verify version cleanup completion logging
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => string.Concat(v).Contains("Version cleanup complete: 2 versions deleted")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // UPDATED: Verify both versions were deleted (2 DELETE requests to versions endpoint)
        _httpHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.Is<HttpRequestMessage>(req => 
                req.Method == HttpMethod.Delete && 
                req.RequestUri!.ToString().Contains($"models/{modelId}/versions/")),
            ItExpr.IsAny<CancellationToken>());

        // UPDATED: Verify model deletion happened after version cleanup
        _httpHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => 
                req.Method == HttpMethod.Delete && 
                req.RequestUri != null &&
                req.RequestUri.AbsolutePath.EndsWith($"/models/{modelId}") &&
                !req.RequestUri.AbsolutePath.Contains("/versions")),
            ItExpr.IsAny<CancellationToken>());
    }

    /// <summary>
    /// Tests proactive flow when version deletion fails
    /// New behavior: Version cleanup failure prevents model deletion
    /// </summary>
    [Fact]
    public async Task DeleteModelAsync_WithVersionDeletionFailure_ReturnsError()
    {
        // Arrange
        var modelId = "test/model-version-delete-fails";
        
        // Mock the model exists check (returns 200)
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri!.ToString().EndsWith($"models/{modelId}") &&
                    !req.RequestUri.ToString().Contains("versions")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        // Mock versions fetch returns one version
        var versionsResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var versionsData = new { results = new[] { new { id = "failing-version" } } };
        versionsResponse.Content = new StringContent(
            JsonSerializer.Serialize(versionsData), 
            Encoding.UTF8, "application/json");
        
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri!.ToString().EndsWith($"models/{modelId}/versions")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(versionsResponse);

        // NEW: Mock version deletion failure (returns 400)
        var versionDeleteError = new HttpResponseMessage(HttpStatusCode.BadRequest);
        versionDeleteError.Content = new StringContent(
            JsonSerializer.Serialize(new { detail = "Version is still processing" }), 
            Encoding.UTF8, "application/json");
        
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Delete && 
                    req.RequestUri != null &&
                    req.RequestUri.AbsolutePath.Contains($"/models/{modelId}/versions/failing-version")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(versionDeleteError);

        // Create the client with proper dependencies
        var client = CreateReplicateApiClient();

        // Act
        var (success, errorMessage) = await client.DeleteModelAsync(modelId);

        // Assert
        Assert.False(success);
        Assert.NotNull(errorMessage);
        Assert.Contains("Failed to delete 1 versions", errorMessage);
        Assert.Contains("failing-version", errorMessage);

        // UPDATED: Verify version cleanup failure logging
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => string.Concat(v).Contains("Version cleanup incomplete: 1/1 failed")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // UPDATED: Verify model deletion was NOT attempted (version cleanup failed)
        _httpHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.Is<HttpRequestMessage>(req => 
                req.Method == HttpMethod.Delete && 
                req.RequestUri!.ToString().EndsWith($"models/{modelId}") &&
                !req.RequestUri.ToString().Contains("versions")),
            ItExpr.IsAny<CancellationToken>());
    }

    /// <summary>
    /// Tests unexpected model deletion failure after successful version cleanup
    /// New behavior: Clear error reporting for unexpected failures
    /// </summary>
    [Fact]
    public async Task DeleteModelAsync_WithUnexpectedModelDeletionFailure_ReturnsError()
    {
        // Arrange
        var modelId = "test/model-delete-fails";
        
        // Mock the model exists check (returns 200)
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri!.ToString().EndsWith($"models/{modelId}") &&
                    !req.RequestUri.ToString().Contains("versions")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        // Mock empty versions response
        var versionsResponse = new HttpResponseMessage(HttpStatusCode.OK);
        var versionsData = new { results = new object[0] };
        versionsResponse.Content = new StringContent(
            JsonSerializer.Serialize(versionsData), 
            Encoding.UTF8, "application/json");
        
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri!.ToString().EndsWith($"models/{modelId}/versions")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(versionsResponse);

        // NEW: Mock model deletion failure (unexpected 500 error)
        var modelDeleteError = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        modelDeleteError.Content = new StringContent("Internal server error", Encoding.UTF8, "text/plain");
        
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Delete && 
                    req.RequestUri != null &&
                    req.RequestUri.AbsolutePath.EndsWith($"/models/{modelId}") &&
                    !req.RequestUri.AbsolutePath.Contains("/versions")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(modelDeleteError);

        // Create the client with proper dependencies
        var client = CreateReplicateApiClient();

        // Act
        var (success, errorMessage) = await client.DeleteModelAsync(modelId);

        // Assert
        Assert.False(success);
        Assert.Equal("Internal server error", errorMessage);

        // UPDATED: Verify unexpected failure logging
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => (string.Concat(v).Contains("Unexpected deletion failure for") && string.Concat(v).Contains(modelId))),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _context?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Tests for ProfileController regression ensuring proper tuple handling
/// </summary>
public class ProfileControllerDeletionRegressionTests
{
    [Fact]
    public void ProfileController_DeleteModelCall_UsesProperTupleHandling()
    {
        // Arrange
        var expectedTuplePattern = "var (success, errorMessage) = await _replicateApiClient.DeleteModelAsync(trainedModel.ReplicateModelId)";
        
        // Act - Read the actual ProfileController source code
        var controllerPath = "/home/alanw/projects/AI.ProfilePhotoMaker/AI.ProfilePhotoMaker.API/Controllers/ProfileController.cs";
        var controllerCode = File.ReadAllText(controllerPath);
        
        // Assert - Verify the controller uses proper tuple destructuring
        Assert.True(controllerCode.Contains(expectedTuplePattern), 
            "ProfileController should use proper tuple destructuring for DeleteModelAsync call");
    }
}
