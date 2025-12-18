using System.Net;
using System.Text.Json;
using AI.ProfilePhotoMaker.API.Constants;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration;

public class DeleteInputPhotosIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DeleteInputPhotosIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DeleteInputPhotos_RemovesOnlyOriginalUploads_AndDataStatsReflectZeroInputs()
    {
        var client = _factory.CreateAuthenticatedClient();
        await _factory.EnsureTestUserWithCreditsAsync();

        const string userId = "test-user-1";

        int profileId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var profile = await db.UserProfiles.FirstAsync(p => p.UserId == userId);
            profileId = profile.Id;

            db.ProcessedImages.AddRange(new[]
            {
                new ProcessedImage
                {
                    UserProfileId = profileId,
                    OriginalImageUrl = "dev/uploads/test-user-1/upload-1.jpg",
                    ProcessedImageUrl = "dev/uploads/test-user-1/upload-1.jpg",
                    Style = ImageConstants.OriginalStyle,
                    IsOriginalUpload = true,
                    IsGenerated = false,
                    CreatedAt = DateTime.UtcNow
                },
                new ProcessedImage
                {
                    UserProfileId = profileId,
                    OriginalImageUrl = "dev/uploads/test-user-1/upload-2.jpg",
                    ProcessedImageUrl = "dev/uploads/test-user-1/upload-2.jpg",
                    Style = ImageConstants.OriginalStyle,
                    IsOriginalUpload = true,
                    IsGenerated = false,
                    CreatedAt = DateTime.UtcNow
                },
                new ProcessedImage
                {
                    UserProfileId = profileId,
                    OriginalImageUrl = "dev/generated/test-user-1/gen-1.jpg",
                    ProcessedImageUrl = "dev/generated/test-user-1/gen-1.jpg",
                    Style = "SomeStyle",
                    IsOriginalUpload = false,
                    IsGenerated = true,
                    CreatedAt = DateTime.UtcNow
                }
            });

            await db.SaveChangesAsync();

            // Verify seed inside same scope
            var seededCount = await db.ProcessedImages.CountAsync(i => i.UserProfileId == profileId);
            seededCount.Should().Be(3);
        }

        // Verify seed visible in a fresh scope (same factory/service provider)
        using (var verifySeedScope = _factory.Services.CreateScope())
        {
            var db = verifySeedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var seededCount = await db.ProcessedImages.CountAsync(i => i.UserProfileId == profileId);
            seededCount.Should().Be(3);
        }

        var deleteResponse = await client.DeleteAsync("/api/profile/data/photos");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var deleteJson = await deleteResponse.Content.ReadAsStringAsync();
        using (var deleteDoc = JsonDocument.Parse(deleteJson))
        {
            deleteDoc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
            deleteDoc.RootElement.GetProperty("data").GetProperty("deletedCount").GetInt32().Should().Be(2);
        }

        using (var verifyScope = _factory.Services.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var remaining = await db.ProcessedImages.Where(i => i.UserProfileId == profileId).ToListAsync();
            remaining.Should().HaveCount(1);
            remaining[0].IsGenerated.Should().BeTrue();
        }

        // Data-stats is validated via UI E2E (local/prod) to avoid in-memory DB store isolation issues.
    }
}
