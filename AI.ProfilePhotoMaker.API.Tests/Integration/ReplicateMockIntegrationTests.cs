using System.Net;
using System.Net.Http.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Tests.Integration;
using AI.ProfilePhotoMaker.API.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AI.ProfilePhotoMaker.API.Tests.Integration;

public class ReplicateMockIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ReplicateMockIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Train_Then_Check_Status_Completes_And_Persists_Model()
    {
        // Ensure fresh user setup with sufficient credits
        await _factory.EnsureTestUserWithCreditsAsync("test-user-1", 100);

        var client = _factory.CreateAuthenticatedClient();

        // Clear any existing models for clean test state
        using (var cleanupScope = _factory.Services.CreateScope())
        {
            var cleanupDb = cleanupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var existingModels = cleanupDb.ModelCreationRequests.Where(m => m.UserId == "test-user-1").ToList();
            cleanupDb.ModelCreationRequests.RemoveRange(existingModels);
            await cleanupDb.SaveChangesAsync();
        }

        // Start training
        var trainResponse = await client.PostAsJsonAsync("/api/replicate/train", new
        {
            UserId = "test-user-1", // Will be overridden by claims, but DTO expects this field
            ImageZipUrl = "https://example.com/mock-dataset.zip"
        });

        // Debug: Log the response content if it's not OK
        if (trainResponse.StatusCode != HttpStatusCode.OK)
        {
            var errorContent = await trainResponse.Content.ReadAsStringAsync();
            throw new Exception($"Training failed with {trainResponse.StatusCode}: {errorContent}");
        }

        trainResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var trainPayload = await trainResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        trainPayload.Should().NotBeNull();

        var data = trainPayload!["data"] as System.Text.Json.JsonElement?;
        data.HasValue.Should().BeTrue();
        var prediction = data!.Value.GetProperty("prediction");
        var trainingId = prediction.GetProperty("id").GetString();
        trainingId.Should().NotBeNullOrEmpty();

        // Trigger polling manually using the polling service since we don't run background service in tests
        using (var pollingScope = _factory.Services.CreateScope())
        {
            var pollingService = pollingScope.ServiceProvider.GetRequiredService<ITrainingPollingService>();
            await pollingService.StartPollingForTraining(trainingId!, "test-user-1");
        }

        // Poll status until succeeded (mock flips after processing)
        var succeeded = false;
        for (var i = 0; i < 10 && !succeeded; i++)
        {
            await Task.Delay(150);
            var statusResp = await client.GetAsync($"/api/replicate/train/status/{trainingId}");
            statusResp.StatusCode.Should().Be(HttpStatusCode.OK);
            var statusPayload = await statusResp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
            var statusData = (System.Text.Json.JsonElement)statusPayload!["data"];
            var status = statusData.GetProperty("status").GetString();
            if (string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase))
            {
                succeeded = true;
            }
        }

        succeeded.Should().BeTrue("mock training should complete quickly");

        // Verify DB updated to Ready with model version
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var modelReq = await db.ModelCreationRequests
            .Where(m => m.UserId == "test-user-1")
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync();

        modelReq.Should().NotBeNull();
        modelReq!.Status.Should().Be(ModelCreationStatus.Ready);
        modelReq.TrainedModelVersion.Should().NotBeNullOrEmpty();
        modelReq.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Generate_Then_Status_Succeeds_And_Ownership_Enforced()
    {
        // Ensure fresh user setup with sufficient credits
        await _factory.EnsureTestUserWithCreditsAsync("test-user-1", 100);

        var client = _factory.CreateAuthenticatedClient();

        // Clear existing models and ensure there is a Ready model for this test
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userId = "test-user-1";

            // Clear any existing models first
            var existingModels = db.ModelCreationRequests.Where(m => m.UserId == userId).ToList();
            db.ModelCreationRequests.RemoveRange(existingModels);
            await db.SaveChangesAsync();

            // Now seed a Ready model for this test
            db.ModelCreationRequests.Add(new ModelCreationRequest
            {
                UserId = userId,
                ModelName = "user-test",
                ReplicateModelId = "mock/user-test",
                TrainedModelVersion = "1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef",
                Status = ModelCreationStatus.Ready,
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Request generation (use empty string to use DB Ready model)
        var genResp = await client.PostAsJsonAsync("/api/replicate/generate", new
        {
            TrainedModelVersion = "", // Empty string to use DB model
            UserId = "test-user-1", // Will be overridden by claims
            Style = "corporate",
            NumOutputs = 2
        });

        // Debug: Log the response content if it's not OK
        if (genResp.StatusCode != HttpStatusCode.OK)
        {
            var errorContent = await genResp.Content.ReadAsStringAsync();
            throw new Exception($"Generation failed with {genResp.StatusCode}: {errorContent}");
        }

        genResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var genPayload = await genResp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var genData = (System.Text.Json.JsonElement)genPayload!["data"];
        var prediction = genData.GetProperty("prediction");
        var predictionId = prediction.GetProperty("id").GetString();
        predictionId.Should().NotBeNullOrEmpty();

        // Poll prediction status until succeeded and outputs present
        var done = false;
        System.Text.Json.JsonElement lastStatus = default;
        for (var i = 0; i < 10 && !done; i++)
        {
            await Task.Delay(150);
            var statusResp = await client.GetAsync($"/api/replicate/generate/status/{predictionId}");
            statusResp.StatusCode.Should().Be(HttpStatusCode.OK);
            var statusPayload = await statusResp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
            lastStatus = (System.Text.Json.JsonElement)statusPayload!["data"];
            var status = lastStatus.GetProperty("status").GetString();
            if (string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase))
            {
                done = true;
            }
        }

        done.Should().BeTrue("mock prediction should complete quickly");
        if (lastStatus.TryGetProperty("output", out var output))
        {
            output.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
            output.GetArrayLength().Should().Be(2);
        }

        // Ownership enforcement: unknown prediction should yield 404
        var nf = await client.GetAsync($"/api/replicate/generate/status/{Guid.NewGuid()}");
        nf.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}