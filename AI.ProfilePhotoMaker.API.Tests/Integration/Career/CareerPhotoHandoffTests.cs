using System.Net;
using System.Text.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration.Career;

/// <summary>
/// Optional career photo handoff (#391, ADR 0008, docs/career/api-photo-handoff.md).
/// The career API reads photos and remembers a choice; it never spends anything.
/// </summary>
public class CareerPhotoHandoffTests : IClassFixture<CareerWorkspaceEnabledFactory>
{
    private const string RawPath = "generated-private/secret-user/headshot-raw-1.png";

    private readonly CareerWorkspaceEnabledFactory _factory;

    public CareerPhotoHandoffTests(CareerWorkspaceEnabledFactory factory)
    {
        _factory = factory;
    }

    private sealed record Seeded(CareerClient User, int Delivered, int Original, int Failed, int Preview, int Other, int Older);

    private async Task<Seeded> SeedAsync(int credits = 7)
    {
        var user = new CareerClient(_factory);
        var other = new CareerClient(_factory);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var mine = new UserProfile { UserId = user.UserId, Credits = credits };
        var theirs = new UserProfile { UserId = other.UserId, Credits = 3 };
        db.UserProfiles.AddRange(mine, theirs);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        ProcessedImage Image(UserProfile owner, string status, bool original, string? raw, DateTime at, string url) => new()
        {
            UserProfileId = owner.Id,
            ProcessedImageUrl = url,
            Style = "linkedin",
            IsGenerated = !original,
            IsOriginalUpload = original,
            GenerationStatus = status,
            RawImageStoragePath = raw,
            CreatedAt = at
        };

        var older = Image(mine, "succeeded", false, null, now.AddDays(-2), "https://cdn.test/older.png");
        var delivered = Image(mine, "Succeeded", false, null, now.AddHours(-1), "https://cdn.test/delivered.png");
        var original = Image(mine, "succeeded", true, null, now, "https://cdn.test/original.png");
        var failed = Image(mine, "failed", false, null, now, "https://cdn.test/failed.png");
        var preview = Image(mine, "succeeded", false, RawPath, now.AddMinutes(-5), "https://cdn.test/preview-watermarked.png");
        var foreign = Image(theirs, "succeeded", false, null, now, "https://cdn.test/foreign.png");
        db.ProcessedImages.AddRange(older, delivered, original, failed, preview, foreign);
        await db.SaveChangesAsync();

        return new Seeded(user, delivered.Id, original.Id, failed.Id, preview.Id, foreign.Id, older.Id);
    }

    private static Task<JsonElement> ListAsync(CareerClient user) =>
        user.GetAsync("/api/career/photos").ContinueWith(t => CareerClient.ReadDataAsync(t.Result, 200)).Unwrap();

    private static Task<HttpResponseMessage> Select(CareerClient user, object body) =>
        user.SendAsync(HttpMethod.Put, "/api/career/photos/selection", body);

    [Fact]
    public async Task ListReturnsOnlyTheCallersSucceededGeneratedImagesNewestFirst()
    {
        var seeded = await SeedAsync();

        var data = await ListAsync(seeded.User);

        var photos = data.GetProperty("photos").EnumerateArray().ToList();
        // Newest first: the preview (-5m), the delivered image (-1h), then the older one (-2d).
        photos.Select(p => p.GetProperty("id").GetInt32()).Should().Equal(seeded.Preview, seeded.Delivered, seeded.Older);
        data.GetProperty("selectedPhotoId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task ListNeverExposesRawOrPrivatePaths()
    {
        var seeded = await SeedAsync();

        var response = await seeded.User.GetAsync("/api/career/photos");
        var text = await response.Content.ReadAsStringAsync();

        text.Should().NotContain("generated-private");
        text.Should().NotContain("headshot-raw");
    }

    [Fact]
    public async Task WatermarkedPreviewIsListedButFlagged()
    {
        var seeded = await SeedAsync();

        var data = await ListAsync(seeded.User);

        var photos = data.GetProperty("photos").EnumerateArray().ToDictionary(p => p.GetProperty("id").GetInt32());
        photos[seeded.Preview].GetProperty("isWatermarkedPreview").GetBoolean().Should().BeTrue();
        photos[seeded.Delivered].GetProperty("isWatermarkedPreview").GetBoolean().Should().BeFalse();
        photos[seeded.Delivered].GetProperty("imageUrl").GetString().Should().Be("https://cdn.test/delivered.png");
        photos[seeded.Delivered].GetProperty("style").GetString().Should().Be("linkedin");
    }

    [Fact]
    public async Task RelativeStoragePathsResolveThroughStorage()
    {
        var user = new CareerClient(_factory);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var profile = new UserProfile { UserId = user.UserId };
            db.UserProfiles.Add(profile);
            await db.SaveChangesAsync();
            db.ProcessedImages.Add(new ProcessedImage
            {
                UserProfileId = profile.Id, ProcessedImageUrl = "generated/u/a.png", Style = "x",
                IsGenerated = true, GenerationStatus = "succeeded"
            });
            await db.SaveChangesAsync();
        }

        var data = await ListAsync(user);

        data.GetProperty("photos")[0].GetProperty("imageUrl").GetString().Should().StartWith("https://fake-storage.com/");
    }

    [Fact]
    public async Task EntitlementsAreReadOnlyCopiesOfActivePackages()
    {
        var seeded = await SeedAsync();
        await GrantAsync(seeded.User.UserId, PackageEntitlementStatus.Active, candidates: 2, refinements: 3);
        await GrantAsync(seeded.User.UserId, PackageEntitlementStatus.Consumed, candidates: 0, refinements: 0);

        var data = await ListAsync(seeded.User);

        var entitlements = data.GetProperty("entitlements").EnumerateArray().ToList();
        entitlements.Should().ContainSingle();
        entitlements[0].GetProperty("packageCode").GetString().Should().Be("starter_package");
        entitlements[0].GetProperty("remainingCandidates").GetInt32().Should().Be(2);
        entitlements[0].GetProperty("remainingRefinements").GetInt32().Should().Be(3);
        entitlements[0].GetProperty("platformExportKitAvailable").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task SelectingAnOwnedDeliveredPhotoIsRememberedAndListed()
    {
        var seeded = await SeedAsync();

        var selected = await CareerClient.ReadDataAsync(await Select(seeded.User, new { processedImageId = seeded.Delivered }), 200);
        var data = await ListAsync(seeded.User);

        selected.GetProperty("selectedPhotoId").GetInt32().Should().Be(seeded.Delivered);
        selected.GetProperty("careerGoalId").ValueKind.Should().Be(JsonValueKind.Null);
        data.GetProperty("selectedPhotoId").GetInt32().Should().Be(seeded.Delivered);
        data.GetProperty("selectedPhotoAvailable").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ChoosingAgainReplacesTheSingleSelection()
    {
        var seeded = await SeedAsync();

        await CareerClient.ReadDataAsync(await Select(seeded.User, new { processedImageId = seeded.Delivered }), 200);
        await CareerClient.ReadDataAsync(await Select(seeded.User, new { processedImageId = seeded.Older }), 200);

        (await ListAsync(seeded.User)).GetProperty("selectedPhotoId").GetInt32().Should().Be(seeded.Older);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.CareerPhotoSelections.CountAsync(s => s.OwnerId == seeded.User.UserId)).Should().Be(1);
    }

    [Fact]
    public async Task SelectionRecordsTheCurrentGoal()
    {
        var seeded = await SeedAsync();
        var goal = await seeded.User.CreateGoalAsync();

        var selected = await CareerClient.ReadDataAsync(await Select(seeded.User, new { processedImageId = seeded.Delivered }), 200);

        selected.GetProperty("careerGoalId").GetString().Should().Be(goal.GetProperty("id").GetString());
    }

    [Fact]
    public async Task AnotherUsersImageIsNotFound()
    {
        var seeded = await SeedAsync();

        var error = await CareerClient.ReadErrorAsync(await Select(seeded.User, new { processedImageId = seeded.Other }), 404);

        error.GetProperty("code").GetString().Should().Be("CareerPhotoNotFound");
    }

    [Theory]
    [InlineData("original")]
    [InlineData("failed")]
    [InlineData("missing")]
    public async Task IneligibleImagesAreNotFound(string which)
    {
        var seeded = await SeedAsync();
        var id = which switch { "original" => seeded.Original, "failed" => seeded.Failed, _ => 987654 };

        var error = await CareerClient.ReadErrorAsync(await Select(seeded.User, new { processedImageId = id }), 404);

        error.GetProperty("code").GetString().Should().Be("CareerPhotoNotFound");
    }

    [Fact]
    public async Task WatermarkedPreviewCannotBeChosen()
    {
        var seeded = await SeedAsync();

        var error = await CareerClient.ReadErrorAsync(await Select(seeded.User, new { processedImageId = seeded.Preview }), 409);

        error.GetProperty("code").GetString().Should().Be("CareerPhotoIsPreview");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public async Task NonPositiveIdIsAValidationError(int id)
    {
        var seeded = await SeedAsync();

        var error = await CareerClient.ReadErrorAsync(await Select(seeded.User, new { processedImageId = id }), 400);

        error.GetProperty("code").GetString().Should().Be("ValidationError");
        error.GetProperty("fieldErrors").TryGetProperty("processedImageId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task MissingIdIsAValidationError()
    {
        var seeded = await SeedAsync();

        var error = await CareerClient.ReadErrorAsync(await Select(seeded.User, new { }), 400);

        error.GetProperty("fieldErrors").TryGetProperty("processedImageId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteClearsTheSelectionAndIsIdempotent()
    {
        var seeded = await SeedAsync();
        await CareerClient.ReadDataAsync(await Select(seeded.User, new { processedImageId = seeded.Delivered }), 200);

        var first = await seeded.User.SendAsync(HttpMethod.Delete, "/api/career/photos/selection");
        var second = await seeded.User.SendAsync(HttpMethod.Delete, "/api/career/photos/selection");

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ListAsync(seeded.User)).GetProperty("selectedPhotoId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task DeletedPhotoReadsAsUnavailable()
    {
        var seeded = await SeedAsync();
        await CareerClient.ReadDataAsync(await Select(seeded.User, new { processedImageId = seeded.Delivered }), 200);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ProcessedImages.Remove(await db.ProcessedImages.SingleAsync(i => i.Id == seeded.Delivered));
            await db.SaveChangesAsync();
        }

        var data = await ListAsync(seeded.User);

        data.GetProperty("selectedPhotoId").GetInt32().Should().Be(seeded.Delivered);
        data.GetProperty("selectedPhotoAvailable").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task HandoffNeverSpendsCreditsAllowancesOrStartsGeneration()
    {
        var seeded = await SeedAsync(credits: 7);
        await GrantAsync(seeded.User.UserId, PackageEntitlementStatus.Active, candidates: 2, refinements: 3, premium: 1);
        var before = await SnapshotAsync(seeded.User.UserId);

        await ListAsync(seeded.User);
        await CareerClient.ReadDataAsync(await Select(seeded.User, new { processedImageId = seeded.Delivered }), 200);
        await Select(seeded.User, new { processedImageId = seeded.Preview });
        await seeded.User.SendAsync(HttpMethod.Delete, "/api/career/photos/selection");

        (await SnapshotAsync(seeded.User.UserId)).Should().Be(before);
    }

    [Fact]
    public async Task ExpiredSessionIsUnauthorized()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Unauthenticated", "true");

        (await client.GetAsync("/api/career/photos")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PutAsync("/api/career/photos/selection", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.DeleteAsync("/api/career/photos/selection")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OwnerDeletionRemovesThePhotoSelection()
    {
        var seeded = await SeedAsync();
        await CareerClient.ReadDataAsync(await Select(seeded.User, new { processedImageId = seeded.Delivered }), 200);

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<API.Services.Career.ICareerPrivateDataService>()
            .DeleteAllForOwnerAsync(seeded.User.UserId);

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.CareerPhotoSelections.CountAsync(s => s.OwnerId == seeded.User.UserId)).Should().Be(0);
        (await db.ProcessedImages.AnyAsync(i => i.Id == seeded.Delivered)).Should().BeTrue("career deletion never touches the photo workspace");
    }

    private sealed record Snapshot(int Credits, string Entitlements, int Operations, int Images);

    private async Task<Snapshot> SnapshotAsync(string userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var profile = await db.UserProfiles.SingleAsync(p => p.UserId == userId);
        var entitlements = await db.UserPackageEntitlements.Where(e => e.UserId == userId).OrderBy(e => e.Id).ToListAsync();
        return new Snapshot(
            profile.Credits,
            string.Join("|", entitlements.Select(e =>
                $"{e.Id}:{e.Status}:{e.RemainingPackageUses}:{e.RemainingCandidates}:{e.RemainingRefinements}:{e.RemainingPremiumAugmentations}:{e.PlatformExportKitAvailable}")),
            await db.HeadshotGenerationOperations.CountAsync(o => o.UserId == userId),
            await db.ProcessedImages.CountAsync(i => i.UserProfileId == profile.Id));
    }

    private async Task GrantAsync(string userId, PackageEntitlementStatus status, int candidates, int refinements, int premium = 0)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var package = await db.OutcomePackageDefinitions.FirstAsync(p => p.Code == "starter_package");
        db.UserPackageEntitlements.Add(new UserPackageEntitlement
        {
            UserId = userId,
            OutcomePackageDefinitionId = package.Id,
            Status = status,
            RemainingCandidates = candidates,
            RemainingRefinements = refinements,
            RemainingPremiumAugmentations = premium,
            PlatformExportKitAvailable = true
        });
        await db.SaveChangesAsync();
    }
}
