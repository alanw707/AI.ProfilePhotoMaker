using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AI.ProfilePhotoMaker.API.Constants;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration;

public class ModelDeletionStatusIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ModelDeletionStatusIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DeleteAIModel_ClearsAllModels_And_StatusEndpointsReflectNoModel()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        await _factory.EnsureTestUserWithCreditsAsync();

        var userId = "test-user-1";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Clean previous requests
            var existing = db.ModelCreationRequests.Where(r => r.UserId == userId);
            db.ModelCreationRequests.RemoveRange(existing);

            // Seed two models: one Ready, one Failed (both with ReplicateModelId)
            db.ModelCreationRequests.Add(new ModelCreationRequest
            {
                UserId = userId,
                ModelName = "ready-model",
                Status = ModelCreationStatus.Ready,
                ReplicateModelId = "mock/ready-model",
                TrainedModelVersion = "v1",
                CompletedAt = DateTime.UtcNow.AddMinutes(-10),
            });
            db.ModelCreationRequests.Add(new ModelCreationRequest
            {
                UserId = userId,
                ModelName = "failed-model",
                Status = ModelCreationStatus.Failed,
                ReplicateModelId = "mock/failed-model",
                TrainedModelVersion = "v0",
                CompletedAt = DateTime.UtcNow.AddMinutes(-20),
                ErrorMessage = "previous failure"
            });

            await db.SaveChangesAsync();
        }

        // Act: delete AI model
        var deleteResponse = await client.DeleteAsync("/api/profile/data/model");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert DB changes
        using (var verifyScope = _factory.Services.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var requests = await db.ModelCreationRequests.Where(r => r.UserId == userId).ToListAsync();
            requests.Should().BeEmpty();
        }

        // Model status endpoint should no longer report ModelReady
        var modelStatusResponse = await client.GetAsync("/api/model-status");
        modelStatusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var modelStatus = await ReadJsonAsync(modelStatusResponse);
        modelStatus.GetProperty("statusCode").GetString().Should().NotBe("ModelReady");
        modelStatus.GetProperty("hasTrainedModel").GetBoolean().Should().BeFalse();

        // Training status should indicate no trained model
        var trainingStatusResponse = await client.GetAsync("/api/profile/training-status");
        trainingStatusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var ts = await ReadJsonAsync(trainingStatusResponse);
        ts.GetProperty("hasTrainedModel").GetBoolean().Should().BeFalse();
        ts.TryGetProperty("trainedModelId", out var _).Should().BeTrue();
        ts.GetProperty("trainedModelId").GetString().Should().BeNull();
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
