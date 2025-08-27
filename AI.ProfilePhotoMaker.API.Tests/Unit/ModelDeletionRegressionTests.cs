using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Moq;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Services;
using System.Net;
using System.Text;
using Xunit;
using Moq.Protected;

namespace AI.ProfilePhotoMaker.API.Tests.Unit;

/// <summary>
/// Tests to validate the August 26th model deletion regression fix
/// </summary>
public class ModelDeletionRegressionTests
{
    private readonly Mock<ILogger<ReplicateApiClient>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpHandlerMock;
    private readonly HttpClient _httpClient;

    public ModelDeletionRegressionTests()
    {
        _loggerMock = new Mock<ILogger<ReplicateApiClient>>();
        _httpHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpHandlerMock.Object);
    }

    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task DeleteModelAsync_WithVersionsError_ReturnsSpecificErrorMessage()
    {
        // Arrange
        var modelId = "test/model-with-versions";
        var expectedErrorMessage = "This model has existing versions and cannot be deleted";
        
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

        // Mock the delete request (returns 400 with specific error)
        var errorResponse = new HttpResponseMessage(HttpStatusCode.BadRequest);
        var errorJson = $"{{\"detail\":\"{expectedErrorMessage}\"}}";
        errorResponse.Content = new StringContent(errorJson, Encoding.UTF8, "application/json");
        
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
        
        var client = new ReplicateApiClient(
            _httpClient, 
            configuration.Object, 
            _loggerMock.Object, 
            context, 
            webhookResolver.Object);

        // Act
        var (success, errorMessage) = await client.DeleteModelAsync(modelId);

        // Assert
        Assert.False(success);
        Assert.Equal(expectedErrorMessage, errorMessage);

        // Verify proper logging occurred
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Failed to delete model {modelId}")),
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
        
        var client = new ReplicateApiClient(
            _httpClient, 
            configuration.Object, 
            _loggerMock.Object, 
            context, 
            webhookResolver.Object);

        // Act
        var (success, errorMessage) = await client.DeleteModelAsync(modelId);

        // Assert
        Assert.False(success);
        Assert.Equal($"Delete failed: {rawErrorMessage}", errorMessage);
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

        // Mock successful delete request
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Delete && 
                    req.RequestUri.ToString().Contains($"models/{modelId}")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        // Create the client with mocked dependencies
        var configuration = new Mock<IConfiguration>();
        var context = CreateInMemoryDbContext();
        var webhookResolver = new Mock<IWebhookUrlResolver>();
        
        var client = new ReplicateApiClient(
            _httpClient, 
            configuration.Object, 
            _loggerMock.Object, 
            context, 
            webhookResolver.Object);

        // Act
        var (success, errorMessage) = await client.DeleteModelAsync(modelId);

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);

        // Verify success logging
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Successfully deleted model {modelId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteModelAsync_ModelNotFound_ReturnsSuccessAsAlreadyDeleted()
    {
        // Arrange
        var modelId = "test/nonexistent-model";
        
        // Mock the model check returns 404
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

        // Create the client with mocked dependencies  
        var configuration = new Mock<IConfiguration>();
        var context = CreateInMemoryDbContext();
        var webhookResolver = new Mock<IWebhookUrlResolver>();
        
        var client = new ReplicateApiClient(
            _httpClient, 
            configuration.Object, 
            _loggerMock.Object, 
            context, 
            webhookResolver.Object);

        // Act
        var (success, errorMessage) = await client.DeleteModelAsync(modelId);

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);

        // Verify appropriate logging
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Model {modelId} not found, considering deletion successful")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }
}

/// <summary>
/// Integration tests to validate the ProfileController regression fix
/// </summary>
public class ProfileControllerDeletionRegressionTests
{
    [Fact]
    public void ProfileController_DeleteModelCall_UsesProperTupleHandling()
    {
        // This test validates that the ProfileController.DeleteProfile method
        // properly handles the tuple return value from DeleteModelAsync
        
        // The fix ensures line 901 uses:
        // var (success, errorMessage) = await _replicateApiClient.DeleteModelAsync(trainedModel.ReplicateModelId);
        // 
        // Instead of the old pattern:
        // await _replicateApiClient.DeleteModelAsync(trainedModel.ReplicateModelId);

        var controllerCode = File.ReadAllText("/home/alanw/projects/AI.ProfilePhotoMaker/AI.ProfilePhotoMaker.API/Controllers/ProfileController.cs");
        
        // Verify the fix is in place
        Assert.True(controllerCode.Contains("var (success, errorMessage) = await _replicateApiClient.DeleteModelAsync(trainedModel.ReplicateModelId)"));
        
        // Verify proper error handling is implemented
        Assert.True(controllerCode.Contains("if (!success)"));
        
        Assert.True(controllerCode.Contains("LogWarning(\"Failed to delete model {ModelId} from Replicate: {Error}\""));
    }
}