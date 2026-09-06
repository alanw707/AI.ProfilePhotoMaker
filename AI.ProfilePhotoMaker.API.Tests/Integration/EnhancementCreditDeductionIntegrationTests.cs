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
    public async Task OpenAiEnhancement_WithStoragePath_Succeeds_Deducts_One_Credit_WithoutHttpDownload()
    {
        await SeedUserAsync(credits: 5);
        var handler = _factory.Services.GetRequiredService<FakeOpenAiHttpMessageHandler>();
        handler.Reset();
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/enhancement/enhance", new
        {
            imageStoragePath = "testing/enhanced/test-user-1/source.png",
            imageUrl = "https://example.com/openai-success.png",
            enhancementType = "headshot",
            turnstileToken = "test"
        });

        if (response.StatusCode != HttpStatusCode.OK)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"OpenAI enhancement with storage path failed with {response.StatusCode}: {errorContent}");
        }

        Assert.Equal(0, handler.SourceImageGetCount);

        var profile = await GetProfileAsync();
        Assert.Equal(4, profile.Credits);
    }

    [Fact]
    public async Task OpenAiEnhancement_WithOwnedPrivateGeneratedSource_Succeeds()
    {
        await SeedUserAsync(credits: 5);
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/enhancement/enhance", new
        {
            imageStoragePath = "testing/generated-private/test-user-1/source.png",
            enhancementType = "headshot",
            turnstileToken = "test"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OpenAiEnhancement_MissingSource_ReturnsBadRequest()
    {
        await SeedUserAsync(credits: 5);
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/enhancement/enhance", new
        {
            enhancementType = "headshot",
            turnstileToken = "test"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OpenAiEnhancement_InvalidStoragePath_ReturnsBadRequest_WithoutDeductingCredits()
    {
        await SeedUserAsync(credits: 5);
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/enhancement/enhance", new
        {
            imageStoragePath = "testing/uploads/test-user-1/source.png",
            enhancementType = "headshot",
            turnstileToken = "test"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var profile = await GetProfileAsync();
        Assert.Equal(5, profile.Credits);
    }

    [Fact]
    public async Task OpenAiEnhancement_CrossUserStoragePath_ReturnsBadRequest_WithoutDeductingCredits()
    {
        await SeedUserAsync(credits: 5);
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/enhancement/enhance", new
        {
            imageStoragePath = "testing/enhanced/other-user/source.png",
            enhancementType = "headshot",
            turnstileToken = "test"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var profile = await GetProfileAsync();
        Assert.Equal(5, profile.Credits);
    }

    [Fact]
    public async Task OpenAiEnhancement_CrossUserPrivateGeneratedSource_ReturnsBadRequest_WithoutDeductingCredits()
    {
        await SeedUserAsync(credits: 5);
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/enhancement/enhance", new
        {
            imageStoragePath = "testing/generated-private/other-user/source.png",
            enhancementType = "headshot",
            turnstileToken = "test"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var profile = await GetProfileAsync();
        Assert.Equal(5, profile.Credits);
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
    public async Task LegacyReplicateEnhancementRoute_UsesOpenAi_Deducts_One_Credit()
    {
        await SeedUserAsync(credits: 5);
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/replicate/enhance", new
        {
            imageUrl = FakeOpenAiHttpMessageHandler.SuccessImageUrl,
            enhancementType = "professional",
            turnstileToken = "test"
        });

        if (response.StatusCode != HttpStatusCode.OK)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Legacy enhancement route failed with {response.StatusCode}: {errorContent}");
        }

        var profile = await GetProfileAsync();
        Assert.Equal(4, profile.Credits);
    }

    [Fact]
    public async Task ExpiredPremiumEntitlement_IsRejectedBeforeProviderOrCreditConsumption()
    {
        await SeedUserAsync(credits: 20);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.UserPackageEntitlements.RemoveRange(db.UserPackageEntitlements.Where(e => e.UserId == UserId));
            var package = await db.OutcomePackageDefinitions.SingleAsync(p => p.Code == "pro_package");
            db.UserPackageEntitlements.Add(new UserPackageEntitlement
            {
                UserId = UserId, OutcomePackageDefinitionId = package.Id,
                Status = PackageEntitlementStatus.Active, RemainingPremiumAugmentations = 1,
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
            });
            await db.SaveChangesAsync();
        }
        var response = await _factory.CreateAuthenticatedClient().PostAsJsonAsync("/api/enhancement/enhance", new
        {
            imageUrl = FakeOpenAiHttpMessageHandler.SuccessImageUrl, enhancementType = "relighting"
        });
        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.Equal(20, (await GetProfileAsync()).Credits);
    }

    [Theory]
    [InlineData("relighting")]
    [InlineData("professional_polish")]
    [InlineData("outfit_upgrade")]
    [InlineData("background_upgrade")]
    [InlineData("skin_tone_polish")]
    [InlineData("sharpen_detail")]
    [InlineData("skin_smoothing")]
    [InlineData("wrinkle_softening")]
    public async Task IncludedPremiumTypes_ConsumeOneAllowance_AndRejectExhaustion(string type)
    {
        await SeedUserAsync(credits: 20);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.UserPackageEntitlements.RemoveRange(db.UserPackageEntitlements.Where(e => e.UserId == UserId));
            var package = await db.OutcomePackageDefinitions.SingleAsync(p => p.Code == "pro_package");
            db.UserPackageEntitlements.Add(new UserPackageEntitlement
            {
                UserId = UserId, OutcomePackageDefinitionId = package.Id,
                Status = PackageEntitlementStatus.Active, RemainingPackageUses = 0,
                RemainingCandidates = 0, RemainingRefinements = 0, RemainingPremiumAugmentations = 1,
                PlatformExportKitAvailable = false
            });
            await db.SaveChangesAsync();
        }
        var client = _factory.CreateAuthenticatedClient();
        var request = new { imageUrl = FakeOpenAiHttpMessageHandler.SuccessImageUrl, enhancementType = type };
        var first = await client.PostAsJsonAsync("/api/enhancement/enhance", request);
        Assert.True(first.StatusCode == HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        var exhausted = await client.PostAsJsonAsync("/api/enhancement/enhance", request);
        Assert.Equal(HttpStatusCode.PaymentRequired, exhausted.StatusCode);
        Assert.Contains("Unlock a Pro Package", await exhausted.Content.ReadAsStringAsync());
        using var verify = _factory.Services.CreateScope();
        var remaining = await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().UserPackageEntitlements.SingleAsync(e => e.UserId == UserId);
        Assert.Equal(0, remaining.RemainingPremiumAugmentations);
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
