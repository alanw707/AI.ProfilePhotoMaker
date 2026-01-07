using System.Net;
using System.Net.Http.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration;

public class EnhancementCreditDeductionIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string UserId = "test-user-1";
    private readonly CustomWebApplicationFactory _factory;

    public EnhancementCreditDeductionIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OpenAiEnhancement_Succeeds_Deducts_One_Credit()
    {
        await SeedUserAsync(credits: 5);
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/enhancement/enhance", new
        {
            imageUrl = FakeOpenAiHttpMessageHandler.SuccessImageUrl,
            enhancementType = "pixar_3d",
            turnstileToken = "test"
        });

        if (response.StatusCode != HttpStatusCode.OK)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"OpenAI enhancement failed with {response.StatusCode}: {errorContent}");
        }

        var profile = await GetProfileAsync();
        Assert.Equal(4, profile.Credits);
    }

    [Fact]
    public async Task OpenAiEnhancement_Failure_Refunds_Credits()
    {
        await SeedUserAsync(credits: 5);
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/enhancement/enhance", new
        {
            imageUrl = FakeOpenAiHttpMessageHandler.FailureImageUrl,
            enhancementType = "pixar_3d",
            turnstileToken = "test"
        });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        var profile = await GetProfileAsync();
        Assert.Equal(5, profile.Credits);
    }

    [Fact]
    public async Task ReplicateEnhancement_Succeeds_Deducts_One_Credit()
    {
        await SeedUserAsync(credits: 5);
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/replicate/enhance", new
        {
            imageUrl = "https://example.com/replicate-enhance.png",
            enhancementType = "professional",
            turnstileToken = "test"
        });

        if (response.StatusCode != HttpStatusCode.OK)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Replicate enhancement failed with {response.StatusCode}: {errorContent}");
        }

        var profile = await GetProfileAsync();
        Assert.Equal(4, profile.Credits);
    }

    private async Task SeedUserAsync(int credits)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == UserId);
        if (user == null)
        {
            user = new ApplicationUser
            {
                Id = UserId,
                UserName = "test-user",
                NormalizedUserName = "TEST-USER",
                Email = "test-user@example.com",
                NormalizedEmail = "TEST-USER@EXAMPLE.COM",
                EmailConfirmed = true
            };
            db.Users.Add(user);
        }
        else
        {
            user.EmailConfirmed = true;
        }

        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == UserId);
        if (profile == null)
        {
            profile = new UserProfile
            {
                UserId = UserId,
                User = user,
                SubscriptionTier = SubscriptionTier.Basic,
                Credits = credits,
                LastCreditReset = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.UserProfiles.Add(profile);
        }
        else
        {
            profile.User = user;
            profile.SubscriptionTier = SubscriptionTier.Basic;
            profile.Credits = credits;
            profile.LastCreditReset = DateTime.UtcNow;
            profile.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    private async Task<UserProfile> GetProfileAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.UserProfiles.FirstAsync(p => p.UserId == UserId);
    }
}
