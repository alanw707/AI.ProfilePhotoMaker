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
/// Tests critical bug fixes for JSON parsing failures and cascade deletion logic
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

    /// <summary>
    /// Tests that model deletion with empty/malformed version response is handled gracefully
    /// This was the root cause of the production model deletion failures
    /// </summary>
    [Fact]
    public async Task DeleteModelAsync_WithVersionsError_ReturnsSpecificErrorMessage()
    {
        // Arrange
        var modelId = "test/model-with-versions";
        var expectedErrorMessage = "Model has versions but none could be retrieved";
        
        // Mock the model exists check (returns 200)
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri.ToString().Contains($"models/{modelId}")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        // Mock the delete request (returns 400 with "existing versions" error)
        var deleteErrorResponse = new HttpResponseMessage(HttpStatusCode.BadRequest);
        deleteErrorResponse.Content = new StringContent(
            JsonSerializer.Serialize(new { detail = "Model has existing versions and cannot be deleted" }), 
            Encoding.UTF8, "application/json");
        
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Delete && 
                    req.RequestUri.ToString().Contains($"models/{modelId}")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(deleteErrorResponse);

        // CRITICAL: Mock the versions endpoint to return empty response (this was the production bug)
        var versionsResponse = new HttpResponseMessage(HttpStatusCode.OK);
        versionsResponse.Content = new StringContent("", Encoding.UTF8, "application/json");
        
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri.ToString().Contains($"models/{modelId}/versions")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(versionsResponse);

        // Create the client with mocked dependencies
        var configuration = new Mock<IConfiguration>();
        var context = CreateInMemoryDbContext();
        var webhookResolver = new Mock<IWebhookUrlResolver>();
        var httpClient = new HttpClient(_httpHandlerMock.Object);
        
        var client = new ReplicateApiClient(
            httpClient,
            configuration.Object,
            _loggerMock.Object,
            context,
            webhookResolver.Object);

        // Act
        var (success, errorMessage) = await client.DeleteModelAsync(modelId);

        // Assert
        Assert.False(success);
        Assert.Equal(expectedErrorMessage, errorMessage);

        // UPDATED: Verify proper logging occurred - should be WARNING not ERROR for empty response
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,  // Changed from LogLevel.Error to LogLevel.Warning
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Empty response when fetching versions for model {modelId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteModelAsync_WithMalformedJsonError_ReturnsRawErrorMessage()
    {
        // Arrange
        var modelId = "test/model-malformed";
        var rawErrorMessage = "Internal server error";
        
        // Mock the model exists check (returns 200)
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri.ToString().Contains($"models/{modelId}")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        // Mock the delete request (returns 500 with malformed JSON)
        var errorResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        errorResponse.Content = new StringContent(rawErrorMessage, Encoding.UTF8, "text/plain");
        
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Delete && 
                    req.RequestUri.ToString().Contains($"models/{modelId}")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(errorResponse);

        // Create the client with mocked dependencies
        var configuration = new Mock<IConfiguration>();
        var context = CreateInMemoryDbContext();
        var webhookResolver = new Mock<IWebhookUrlResolver>();
        var httpClient = new HttpClient(_httpHandlerMock.Object);
        
        var client = new ReplicateApiClient(
            httpClient,
            configuration.Object,
            _loggerMock.Object,
            context,
            webhookResolver.Object);

        // Act
        var (success, errorMessage) = await client.DeleteModelAsync(modelId);

        // Assert
        Assert.False(success);
        // UPDATED: Expect raw error message, not prefixed (this is the correct behavior for malformed JSON)
        Assert.Equal(rawErrorMessage, errorMessage);
    }

    [Fact]
    public async Task DeleteModelAsync_SuccessfulDeletion_ReturnsSuccess()
    {
        // Arrange
        var modelId = "test/model-success";
        
        // Mock the model exists check (returns 200)
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri.ToString().Contains($"models/{modelId}")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        // Mock successful delete
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Delete && 
                    req.RequestUri.ToString().Contains($"models/{modelId}")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NoContent));

        // Create the client with mocked dependencies
        var configuration = new Mock<IConfiguration>();
        var context = CreateInMemoryDbContext();
        var webhookResolver = new Mock<IWebhookUrlResolver>();
        var httpClient = new HttpClient(_httpHandlerMock.Object);
        
        var client = new ReplicateApiClient(
            httpClient,
            configuration.Object,
            _loggerMock.Object,
            context,
            webhookResolver.Object);

        // Act
        var (success, errorMessage) = await client.DeleteModelAsync(modelId);

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);
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
                    req.RequestUri.ToString().Contains($"models/{modelId}")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

        // Create the client with mocked dependencies
        var configuration = new Mock<IConfiguration>();
        var context = CreateInMemoryDbContext();
        var webhookResolver = new Mock<IWebhookUrlResolver>();
        var httpClient = new HttpClient(_httpHandlerMock.Object);
        
        var client = new ReplicateApiClient(
            httpClient,
            configuration.Object,
            _loggerMock.Object,
            context,
            webhookResolver.Object);

        // Act
        var (success, errorMessage) = await client.DeleteModelAsync(modelId);

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);
    }

    [Fact]
    public async Task DeleteModelAsync_WithValidVersions_DeletesVersionsAndModel()
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
                    req.RequestUri.ToString().Contains($"models/{modelId}")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        // Mock the delete request (returns 400 with "existing versions" error)
        var deleteErrorResponse = new HttpResponseMessage(HttpStatusCode.BadRequest);
        deleteErrorResponse.Content = new StringContent(
            JsonSerializer.Serialize(new { detail = "Model has existing versions and cannot be deleted" }), 
            Encoding.UTF8, "application/json");
        
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Delete && 
                    req.RequestUri.ToString().Contains($"models/{modelId}")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(deleteErrorResponse);

        // Mock the versions endpoint to return valid versions
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
                    req.RequestUri.ToString().Contains($"models/{modelId}/versions")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(versionsResponse);

        // Mock version deletion (returns 204 for each version)
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Delete && 
                    req.RequestUri.ToString().Contains($"models/{modelId}/versions/")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NoContent));

        // Mock retry model deletion after versions are cleared (returns 204)
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Delete && 
                    req.RequestUri.ToString().Contains($"models/{modelId}") &&
                    !req.RequestUri.ToString().Contains("versions")))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NoContent));

        // Create the client with mocked dependencies
        var configuration = new Mock<IConfiguration>();
        var context = CreateInMemoryDbContext();
        var webhookResolver = new Mock<IWebhookUrlResolver>();
        var httpClient = new HttpClient(_httpHandlerMock.Object);
        
        var client = new ReplicateApiClient(
            httpClient,
            configuration.Object,
            _loggerMock.Object,
            context,
            webhookResolver.Object);

        // Act
        var (success, errorMessage) = await client.DeleteModelAsync(modelId);

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);
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
        Assert.True(controllerCode.Contains("var (success, errorMessage) = await _replicateApiClient.DeleteModelAsync(trainedModel.ReplicateModelId)"), 
            "ProfileController should use proper tuple destructuring for DeleteModelAsync call");
    }
}