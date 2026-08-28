using System.Net;
using System.Net.Http.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration;

public class HeadshotGenerationEndpointIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public HeadshotGenerationEndpointIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GenerateHeadshot_FreePreview_GeneratesOneStoredImageWithoutConsumingCredits()
    {
        var userId = $"headshot-user-{Guid.NewGuid():N}";
        await SeedUserAsync(userId, credits: 3);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);

        var response = await client.PostAsJsonAsync("/api/headshots/generate", new
        {
            imageStoragePath = $"testing/enhanced/{userId}/source.png",
            style = "professional",
            background = "auto",
            numOutputs = 1
        });

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, responseBody);
        var json = await response.Content.ReadFromJsonAsync<HeadshotApiResponse>();
        Assert.NotNull(json);
        Assert.True(json!.Success);
        Assert.NotNull(json.Data);
        Assert.Equal("openai", json.Data!.Provider);
        Assert.Equal("gpt-image-2", json.Data.Model);
        Assert.Equal(0, json.Data.CreditsCost);
        Assert.Equal(3, json.Data.RemainingCredits);
        Assert.NotEqual(0, json.Data.ProcessedImageId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var profile = db.UserProfiles.Single(p => p.UserId == userId);
        Assert.Equal(3, profile.Credits);
        var image = db.ProcessedImages.Single(i => i.Id == json.Data.ProcessedImageId);
        Assert.Equal("instant_headshot", image.GenerationMode);
        Assert.Equal("openai", image.Provider);
        Assert.Equal("gpt-image-2", image.ProviderModel);
        Assert.True(image.IsGenerated);
        Assert.Single(json.Data.Candidates);
    }

    [Fact]
    public async Task GetResumablePreview_ReturnsLatestOwnedRawPreviewWithPackageContinuation()
    {
        var userId = $"headshot-resume-{Guid.NewGuid():N}";
        await SeedUserAsync(userId, credits: 3);
        await GrantPackageEntitlementAsync(userId, "starter_package", candidates: 3, refinements: 1, premiumAugmentations: 0, exportKit: true);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);

        var generate = await client.PostAsJsonAsync("/api/headshots/generate", new
        {
            imageStoragePath = $"testing/enhanced/{userId}/source.png",
            style = "professional",
            background = "auto",
            numOutputs = 1
        });
        generate.EnsureSuccessStatusCode();

        var response = await client.GetAsync("/api/headshots/resumable-preview");

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, responseBody);
        var json = await response.Content.ReadFromJsonAsync<ResumablePreviewApiResponse>();
        Assert.NotNull(json?.Data);
        Assert.True(json!.Data!.HasRawPreview);
        Assert.True(json.Data.CanPromotePreview);
        Assert.Equal("starter_package", json.Data.ActivePackageCode);
        Assert.Equal(2, json.Data.RemainingCandidateCount);
        Assert.Equal($"testing/enhanced/{userId}/source.png", json.Data.SourceStoragePath);
    }

    [Fact]
    public async Task GetResumablePreview_ByIdRejectsAnotherUsersPreview()
    {
        var ownerUserId = $"headshot-owner-{Guid.NewGuid():N}";
        var otherUserId = $"headshot-other-{Guid.NewGuid():N}";
        await SeedUserAsync(ownerUserId, credits: 3);
        await SeedUserAsync(otherUserId, credits: 3);
        var ownerClient = _factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Add("X-Test-UserId", ownerUserId);
        var generate = await ownerClient.PostAsJsonAsync("/api/headshots/generate", new
        {
            imageStoragePath = $"testing/enhanced/{ownerUserId}/source.png",
            style = "professional",
            background = "auto",
            numOutputs = 1
        });
        generate.EnsureSuccessStatusCode();
        var generated = await generate.Content.ReadFromJsonAsync<HeadshotApiResponse>();

        var otherClient = _factory.CreateClient();
        otherClient.DefaultRequestHeaders.Add("X-Test-UserId", otherUserId);
        var response = await otherClient.GetAsync($"/api/headshots/resumable-preview?previewId={generated!.Data!.ProcessedImageId}");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<ResumablePreviewApiResponse>();
        Assert.Null(json!.Data);
    }

    [Fact]
    public async Task GenerateHeadshot_StarterPackage_RequiresEntitlementAndConsumesCandidateAllowance()
    {
        var userId = $"headshot-starter-{Guid.NewGuid():N}";
        await SeedUserAsync(userId, credits: 10);
        await GrantPackageEntitlementAsync(userId, "starter_package", candidates: 3, refinements: 1, premiumAugmentations: 0, exportKit: true);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);

        var response = await client.PostAsJsonAsync("/api/headshots/generate", new
        {
            imageStoragePath = $"testing/enhanced/{userId}/source.png",
            style = "professional",
            background = "auto",
            packageCode = "starter_package",
            numOutputs = 5
        });

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, responseBody);
        var json = await response.Content.ReadFromJsonAsync<HeadshotApiResponse>();
        Assert.NotNull(json?.Data);
        Assert.Equal(3, json!.Data!.Candidates.Count);
        Assert.Equal(3, json.Data.CreditsCost);
        Assert.Equal(7, json.Data.RemainingCredits);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entitlement = db.UserPackageEntitlements.Single(e => e.UserId == userId);
        Assert.Equal(0, entitlement.RemainingCandidates);
        Assert.Equal(3, db.ProcessedImages.Count(i => i.UserProfile.UserId == userId));
    }

    [Fact]
    public async Task PrivateRawPreviewProxyPath_IsRejectedWithoutAuthentication()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/profile-images/test/generated-private/user/raw.png");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GenerateHeadshot_PromotesPreviewAndServesRawBytesOnlyThroughAuthorizedEndpoint()
    {
        var userId = $"headshot-promotion-{Guid.NewGuid():N}";
        await SeedUserAsync(userId, credits: 10);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);
        var sourcePath = $"testing/enhanced/{userId}/source.png";

        var previewResponse = await client.PostAsJsonAsync("/api/headshots/generate", new
        {
            imageStoragePath = sourcePath,
            style = "professional",
            background = "auto",
            packageCode = "free_preview",
            numOutputs = 1
        });
        previewResponse.EnsureSuccessStatusCode();
        var preview = await previewResponse.Content.ReadFromJsonAsync<HeadshotApiResponse>();
        await GrantPackageEntitlementAsync(userId, "starter_package", candidates: 3, refinements: 1, premiumAugmentations: 0, exportKit: true);

        var paidResponse = await client.PostAsJsonAsync("/api/headshots/generate", new
        {
            imageStoragePath = sourcePath,
            style = "professional",
            background = "auto",
            packageCode = "starter_package",
            numOutputs = 1,
            reusedPreviewProcessedImageId = preview!.Data!.ProcessedImageId,
            clientRequestId = "promote-preview"
        });
        paidResponse.EnsureSuccessStatusCode();
        var paid = await paidResponse.Content.ReadFromJsonAsync<HeadshotApiResponse>();
        var promoted = paid!.Data!.Candidates.Single(candidate => candidate.StoragePath.Contains("generated-private"));

        Assert.Contains($"/api/headshots/images/{promoted.ProcessedImageId}/original", promoted.ImageUrl);
        var imageResponse = await client.GetAsync($"/api/headshots/images/{promoted.ProcessedImageId}/original");
        Assert.Equal(HttpStatusCode.OK, imageResponse.StatusCode);
        Assert.Equal("image/png", imageResponse.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await imageResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task GenerateHeadshot_OneCandidateBatchesResumeWithoutDuplicatesOrAllowanceLoss()
    {
        var userId = $"headshot-batches-{Guid.NewGuid():N}";
        await SeedUserAsync(userId, credits: 10);
        await GrantPackageEntitlementAsync(userId, "starter_package", candidates: 3, refinements: 1, premiumAugmentations: 0, exportKit: true);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId);

        async Task<HeadshotApiResponse> GenerateAsync(string clientRequestId)
        {
            var response = await client.PostAsJsonAsync("/api/headshots/generate", new
            {
                imageStoragePath = $"testing/enhanced/{userId}/source.png",
                style = "professional",
                background = "auto",
                packageCode = "starter_package",
                numOutputs = 1,
                clientRequestId
            });
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, body);
            return (await response.Content.ReadFromJsonAsync<HeadshotApiResponse>())!;
        }

        var first = await GenerateAsync("batch-1");
        var second = await GenerateAsync("batch-2");
        var third = await GenerateAsync("batch-3");
        var retry = await GenerateAsync("batch-3");

        Assert.Equal(first.Data!.ProcessedImageId, first.Data.Candidates.Single().ProcessedImageId);
        Assert.Equal(third.Data!.ProcessedImageId, retry.Data!.ProcessedImageId);
        Assert.Equal(3, new[] { first, second, third }.Select(r => r.Data!.ProcessedImageId).Distinct().Count());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entitlement = db.UserPackageEntitlements.Single(e => e.UserId == userId);
        Assert.Equal(0, entitlement.RemainingCandidates);
        Assert.Equal(3, db.ProcessedImages.Count(i => i.UserProfile.UserId == userId));
    }

    [Fact]
    public async Task GenerateHeadshot_UnauthenticatedRequest_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Unauthenticated", "true");

        var response = await client.PostAsJsonAsync("/api/headshots/generate", new
        {
            imageStoragePath = "dev/enhanced/test-user-1/source.png",
            style = "professional",
            background = "auto",
            numOutputs = 1
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task GrantPackageEntitlementAsync(string userId, string packageCode, int candidates, int refinements, int premiumAugmentations, bool exportKit)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var package = db.OutcomePackageDefinitions.Single(p => p.Code == packageCode);
        db.UserPackageEntitlements.Add(new UserPackageEntitlement
        {
            UserId = userId,
            OutcomePackageDefinitionId = package.Id,
            Status = PackageEntitlementStatus.Active,
            RemainingPackageUses = 1,
            RemainingCandidates = candidates,
            RemainingRefinements = refinements,
            RemainingPremiumAugmentations = premiumAugmentations,
            PlatformExportKitAvailable = exportKit,
            ActivatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedUserAsync(string userId, int credits)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"{userId}@example.com",
            NormalizedUserName = $"{userId}@EXAMPLE.COM",
            Email = $"{userId}@example.com",
            NormalizedEmail = $"{userId}@EXAMPLE.COM",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
        user.PasswordHash = hasher.HashPassword(user, "Password123!");

        if (!db.Styles.Any(s => s.Name == "linkedin"))
        {
            db.Styles.Add(new Style
            {
                Name = "linkedin",
                Description = "LinkedIn professional style",
                PromptTemplate = "professional portrait of a {gender} {ethnicity}, {subject}, clean neutral background, confident approachable expression",
                NegativePromptTemplate = "distorted face, unrealistic features",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        db.Users.Add(user);
        db.UserProfiles.Add(new UserProfile
        {
            UserId = userId,
            User = user,
            Credits = credits,
            LastCreditReset = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private sealed class HeadshotApiResponse
    {
        public bool Success { get; set; }
        public HeadshotApiData? Data { get; set; }
    }

    private sealed class ResumablePreviewApiResponse
    {
        public bool Success { get; set; }
        public ResumablePreviewApiData? Data { get; set; }
    }

    private sealed class ResumablePreviewApiData
    {
        public int ProcessedImageId { get; set; }
        public string SourceStoragePath { get; set; } = string.Empty;
        public string Style { get; set; } = string.Empty;
        public bool HasRawPreview { get; set; }
        public bool CanPromotePreview { get; set; }
        public string? ActivePackageCode { get; set; }
        public int RemainingCandidateCount { get; set; }
    }

    private sealed class HeadshotApiData
    {
        public string ImageUrl { get; set; } = string.Empty;
        public string StoragePath { get; set; } = string.Empty;
        public int ProcessedImageId { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int CreditsCost { get; set; }
        public int RemainingCredits { get; set; }
        public List<HeadshotCandidateData> Candidates { get; set; } = new();
    }

    private sealed class HeadshotCandidateData
    {
        public int ProcessedImageId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string StoragePath { get; set; } = string.Empty;
    }
}
