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

public class ModelStatusIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ModelStatusIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ModelStatus_NotStarted_WhenNoImagesAndNoRequests()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        await _factory.EnsureTestUserWithCreditsAsync();
        // Clean any prior state for the shared test user
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userId = "test-user-1";
            var modelReqs = db.ModelCreationRequests.Where(r => r.UserId == userId);
            db.ModelCreationRequests.RemoveRange(modelReqs);
            var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile != null)
            {
                var imgs = db.ProcessedImages.Where(i => i.UserProfileId == profile.Id);
                db.ProcessedImages.RemoveRange(imgs);
            }
            await db.SaveChangesAsync();
        }

        // Act
        var response = await client.GetAsync("/api/model-status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var root = await ReadJsonAsync(response);
        root.GetProperty("statusCode").GetString().Should().Be("NotStarted");
        root.GetProperty("canStartTraining").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task ModelStatus_ReadyForTraining_WithEnoughImages()
    {
        var client = _factory.CreateAuthenticatedClient();
        await _factory.EnsureTestUserWithCreditsAsync();

        // Ensure no trained model exists
        using (var cleanupScope = _factory.Services.CreateScope())
        {
            var cleanupDb = cleanupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cleanupUserId = "test-user-1";
            var modelReqs = cleanupDb.ModelCreationRequests.Where(r => r.UserId == cleanupUserId);
            cleanupDb.ModelCreationRequests.RemoveRange(modelReqs);
            await cleanupDb.SaveChangesAsync();
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userId = "test-user-1";
        var profile = await db.UserProfiles.FirstAsync(p => p.UserId == userId);

        for (int i = 0; i < 10; i++)
        {
            db.ProcessedImages.Add(new ProcessedImage
            {
                UserProfileId = profile.Id,
                Style = ImageConstants.OriginalStyle,
                OriginalImageUrl = $"uploads/{userId}/img_{i}.jpg",
                IsOriginalUpload = true,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await db.SaveChangesAsync();

        var response = await client.GetAsync("/api/model-status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var root = await ReadJsonAsync(response);
        root.GetProperty("statusCode").GetString().Should().Be("ReadyForTraining");
        root.GetProperty("canStartTraining").GetBoolean().Should().BeTrue();
        root.GetProperty("totalUploadedImages").GetInt32().Should().BeGreaterThanOrEqualTo(10);
    }

    [Fact]
    public async Task ModelStatus_Training_WhenLatestRequestInProgress()
    {
        var client = _factory.CreateAuthenticatedClient();
        await _factory.EnsureTestUserWithCreditsAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userId = "test-user-1";

        // Clean any prior requests
        var existing = db.ModelCreationRequests.Where(r => r.UserId == userId);
        db.ModelCreationRequests.RemoveRange(existing);

        db.ModelCreationRequests.Add(new ModelCreationRequest
        {
            UserId = userId,
            ModelName = "model-old",
            Status = ModelCreationStatus.Failed,
            ErrorMessage = "Some error",
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        });
        db.ModelCreationRequests.Add(new ModelCreationRequest
        {
            UserId = userId,
            ModelName = "model-new",
            Status = ModelCreationStatus.Creating,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var response = await client.GetAsync("/api/model-status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var root = await ReadJsonAsync(response);
        root.GetProperty("statusCode").GetString().Should().Be("Training");
    }

    [Fact]
    public async Task ModelStatus_ModelReady_WhenTrainedModelExists()
    {
        var client = _factory.CreateAuthenticatedClient();
        await _factory.EnsureTestUserWithCreditsAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userId = "test-user-1";

        // Clean any prior requests
        var existing = db.ModelCreationRequests.Where(r => r.UserId == userId);
        db.ModelCreationRequests.RemoveRange(existing);

        db.ModelCreationRequests.Add(new ModelCreationRequest
        {
            UserId = userId,
            ModelName = "model-ready",
            Status = ModelCreationStatus.Ready,
            ReplicateModelId = "user/model",
            TrainedModelVersion = "v1",
            CompletedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var response = await client.GetAsync("/api/model-status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var root = await ReadJsonAsync(response);
        root.GetProperty("statusCode").GetString().Should().Be("ModelReady");
        root.GetProperty("hasTrainedModel").GetBoolean().Should().BeTrue();

    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        // Clone to detach from the using scope
        return doc.RootElement.Clone();
    }
}
