using System.Net;
using System.Text.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.Career;
using AI.ProfilePhotoMaker.API.Services.Career;
using AI.ProfilePhotoMaker.API.Services.Storage;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration.Career;

/// <summary>Career host with an inspectable private store and no wait between blob-delete rounds.</summary>
public class CareerPrivacyFactory : CareerWorkspaceEnabledFactory
{
    public InMemoryStorage Storage { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Career:Privacy:PurgeBackoffMilliseconds"] = "0"
        }));
        builder.ConfigureTestServices(services => services.AddSingleton<IStorageService>(Storage));
    }
}

public class CareerPrivacyApiTests : IClassFixture<CareerPrivacyFactory>
{
    private readonly CareerPrivacyFactory _factory;

    public CareerPrivacyApiTests(CareerPrivacyFactory factory)
    {
        _factory = factory;
    }

    private static CareerClient Fresh(CareerPrivacyFactory factory, double? ageMinutes = null, string? authTimeAgeMinutes = null, bool noIat = false)
    {
        var headers = new Dictionary<string, string>();
        if (ageMinutes != null) headers["X-Test-AuthAgeMinutes"] = ageMinutes.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (authTimeAgeMinutes != null) headers["X-Test-AuthTimeAgeMinutes"] = authTimeAgeMinutes;
        if (noIat) headers["X-Test-NoIat"] = "true";
        return new CareerClient(factory, headers: headers);
    }

    private async Task SeedAsync(string owner, DateTime? at = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        CareerPrivacySeed.SeedAll(db, owner, at ?? DateTime.UtcNow.AddHours(-1), _factory.Storage);
        await db.SaveChangesAsync();
    }

    private async Task<int> TotalAsync(string owner)
    {
        using var scope = _factory.Services.CreateScope();
        return await CareerPrivacySeed.TotalAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(), owner);
    }

    [Theory]
    [InlineData("GET", "/api/career/privacy/retention")]
    [InlineData("GET", "/api/career/privacy/export")]
    [InlineData("POST", "/api/career/privacy/deletions")]
    [InlineData("GET", "/api/career/privacy/deletions/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/career/privacy/deletions/00000000-0000-0000-0000-000000000001/retry")]
    public async Task UnauthenticatedRequestsGet401(string method, string path)
    {
        var http = _factory.CreateAuthenticatedClient();
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Add("X-Test-Unauthenticated", "true");

        (await http.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RetentionStatesTheActualPolicy()
    {
        var data = await CareerClient.ReadDataAsync(await Fresh(_factory).GetAsync("/api/career/privacy/retention"), 200);

        var items = data.GetProperty("items").EnumerateArray().ToDictionary(i => i.GetProperty("key").GetString()!);
        items["exports"].GetProperty("retention").GetString().Should().Be("24 hours");
        items["career_data"].GetProperty("retention").GetString().Should().Contain("Until you delete");
        items["raw_documents"].GetProperty("retention").GetString().Should().Contain("30 days");
        items["backups"].GetProperty("retention").GetString().Should().Contain("35 days");
        items["backups"].GetProperty("notes").GetString().Should().Contain("replays your deletions");
        var processor = data.GetProperty("processors").EnumerateArray().Single();
        processor.GetProperty("name").GetString().Should().Be("OpenAI");
        processor.GetProperty("purpose").GetString().Should().Contain("not used to train");
    }

    [Fact]
    public async Task ExportIsAPrivateJsonAttachmentOfOnlyTheCallersData()
    {
        var alice = Fresh(_factory);
        var bob = Fresh(_factory);
        await SeedAsync(alice.UserId);
        await SeedAsync(bob.UserId);

        var response = await alice.GetAsync("/api/career/privacy/export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        response.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        response.Content.Headers.ContentDisposition.FileName.Should().MatchRegex(@"^career-export-\d{4}-\d{2}-\d{2}\.json$");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.CacheControl.Private.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(alice.UserId).And.NotContain(bob.UserId);
        JsonDocument.Parse(body).RootElement.GetProperty("sections").EnumerateObject().Should().HaveCount(CareerPrivateDataService.CoveredEntityTypes.Count);
    }

    [Theory]
    [InlineData(11.0, null, false, 401)]
    [InlineData(9.0, null, false, 202)]
    [InlineData(null, null, true, 401)]
    [InlineData(60.0, "5", false, 202)]
    [InlineData(1.0, "30", false, 401)]
    public async Task DeletionNeedsASignInFromTheLastTenMinutes(double? iatAge, string? authTimeAge, bool noIat, int expected)
    {
        var user = Fresh(_factory, iatAge, authTimeAge, noIat);

        var response = await user.SendAsync(HttpMethod.Post, "/api/career/privacy/deletions", new { scope = "raw_documents" });

        ((int)response.StatusCode).Should().Be(expected);
        if (expected == 401)
        {
            var error = await CareerClient.ReadErrorAsync(response, 401);
            error.GetProperty("code").GetString().Should().Be("CareerReauthRequired");
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.CareerTombstones.CountAsync(t => t.OwnerId == user.UserId)).Should().Be(0, "a refused request writes nothing");
        }
    }

    [Theory]
    [InlineData("everything")]
    [InlineData("")]
    [InlineData(null)]
    public async Task BadScopeIs400(string? scope)
    {
        var response = await Fresh(_factory).SendAsync(HttpMethod.Post, "/api/career/privacy/deletions", new { scope });

        var error = await CareerClient.ReadErrorAsync(response, 400);
        error.GetProperty("fieldErrors").GetProperty("scope").GetString().Should().Contain("raw_documents");
    }

    [Fact]
    public async Task RawDocumentsDeletionKeepsTheProfileAndRemovesFilesRowsAndExcerpts()
    {
        var user = Fresh(_factory);
        await user.CreateProfileAsync();
        await SeedAsync(user.UserId);
        var before = await TotalAsync(user.UserId);
        _factory.Storage.Keys.Count(k => k.StartsWith("career-private/resumes/")).Should().BeGreaterThan(0);

        var response = await user.SendAsync(HttpMethod.Post, "/api/career/privacy/deletions", new { scope = "raw_documents" });

        var data = await CareerClient.ReadDataAsync(response, 202);
        data.GetProperty("status").GetString().Should().Be("completed");
        data.GetProperty("scope").GetString().Should().Be("raw_documents");
        data.GetProperty("lastError").ValueKind.Should().Be(JsonValueKind.Null);
        response.Headers.Location!.ToString().Should().EndWith($"/api/career/privacy/deletions/{data.GetProperty("id").GetString()}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.CareerResumeDocuments.CountAsync(d => d.OwnerId == user.UserId)).Should().Be(0);
        (await db.CareerProfiles.CountAsync(p => p.OwnerId == user.UserId)).Should().BeGreaterThan(0);
        (await db.CareerProfileProposalItems.Where(i => i.OwnerId == user.UserId).ToListAsync()).Should()
            .OnlyContain(i => i.Excerpt == "");
        (await TotalAsync(user.UserId)).Should().Be(before - 1, "only the resume row goes");
        await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/profile"), 200);
    }

    [Fact]
    public async Task CareerProfileDeletionRemovesEveryCoveredRowButNotPhotoOrBillingData()
    {
        var user = Fresh(_factory);
        var other = Fresh(_factory);
        await SeedAsync(user.UserId);
        await SeedAsync(other.UserId);
        var otherBefore = await TotalAsync(other.UserId);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.Add(new ApplicationUser { Id = user.UserId, UserName = user.UserId, Email = $"{user.UserId}@example.com" });
            db.UserProfiles.Add(new UserProfile { UserId = user.UserId, Credits = 7, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, LastCreditReset = DateTime.UtcNow });
            db.CreditPurchases.Add(new CreditPurchase { UserId = user.UserId, PackageId = 1, CreditsAwarded = 5, AmountPaid = 9.99m, Status = PaymentStatus.Completed });
            await db.SaveChangesAsync();
        }

        var data = await CareerClient.ReadDataAsync(
            await user.SendAsync(HttpMethod.Post, "/api/career/privacy/deletions", new { scope = "career_profile" }), 202);

        data.GetProperty("status").GetString().Should().Be("completed");
        (await TotalAsync(user.UserId)).Should().Be(0);
        (await TotalAsync(other.UserId)).Should().Be(otherBefore);
        using var check = _factory.Services.CreateScope();
        var verify = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await verify.UserProfiles.CountAsync(p => p.UserId == user.UserId)).Should().Be(1);
        (await verify.CreditPurchases.CountAsync(p => p.UserId == user.UserId)).Should().Be(1);
        // The audit records outlive the purge they record.
        (await verify.CareerTombstones.CountAsync(t => t.OwnerId == user.UserId && t.Scope == "career_profile")).Should().Be(1);
        (await verify.CareerDeletionRequests.CountAsync(r => r.OwnerId == user.UserId)).Should().Be(1);
    }

    [Fact]
    public async Task StorageFailureEndsAsFailedWithAReasonAndRetryCompletesIt()
    {
        var user = Fresh(_factory);
        await SeedAsync(user.UserId);
        _factory.Storage.FailNextDeletes = int.MaxValue;
        string id;
        try
        {
            var failed = await CareerClient.ReadDataAsync(
                await user.SendAsync(HttpMethod.Post, "/api/career/privacy/deletions", new { scope = "career_profile" }), 202);
            id = failed.GetProperty("id").GetString()!;
            failed.GetProperty("status").GetString().Should().Be("failed");
            failed.GetProperty("lastError").GetString().Should().Be("storage_unavailable");
            failed.GetProperty("attempts").GetInt32().Should().Be(5);
            failed.GetProperty("completedAt").ValueKind.Should().Be(JsonValueKind.Null);
        }
        finally
        {
            _factory.Storage.FailNextDeletes = 0;
        }

        // Every row except the one that still owns an undeleted file is already gone.
        (await TotalAsync(user.UserId)).Should().Be(1);
        var status = await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/privacy/deletions/{id}"), 200);
        status.GetProperty("status").GetString().Should().Be("failed");

        var retried = await CareerClient.ReadDataAsync(await user.SendAsync(HttpMethod.Post, $"/api/career/privacy/deletions/{id}/retry"), 202);

        retried.GetProperty("status").GetString().Should().Be("completed");
        retried.GetProperty("lastError").ValueKind.Should().Be(JsonValueKind.Null);
        retried.GetProperty("completedAt").ValueKind.Should().Be(JsonValueKind.String);
        (await TotalAsync(user.UserId)).Should().Be(0);

        var again = await user.SendAsync(HttpMethod.Post, $"/api/career/privacy/deletions/{id}/retry");
        (await CareerClient.ReadErrorAsync(again, 409)).GetProperty("code").GetString().Should().Be("CareerDeletionCompleted");
    }

    [Fact]
    public async Task AFewFailedRoundsStillCompleteWithoutARetry()
    {
        var user = Fresh(_factory);
        await SeedAsync(user.UserId);
        _factory.Storage.FailNextDeletes = 2;

        var data = await CareerClient.ReadDataAsync(
            await user.SendAsync(HttpMethod.Post, "/api/career/privacy/deletions", new { scope = "raw_documents" }), 202);

        data.GetProperty("status").GetString().Should().Be("completed");
        data.GetProperty("attempts").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task AnotherOwnersDeletionIs404ForStatusAndRetry()
    {
        var alice = Fresh(_factory);
        var bob = Fresh(_factory);
        var created = await CareerClient.ReadDataAsync(
            await alice.SendAsync(HttpMethod.Post, "/api/career/privacy/deletions", new { scope = "career_profile" }), 202);
        var id = created.GetProperty("id").GetString();

        (await CareerClient.ReadErrorAsync(await bob.GetAsync($"/api/career/privacy/deletions/{id}"), 404))
            .GetProperty("code").GetString().Should().Be("CareerDeletionNotFound");
        await CareerClient.ReadErrorAsync(await bob.SendAsync(HttpMethod.Post, $"/api/career/privacy/deletions/{id}/retry"), 404);
        await CareerClient.ReadDataAsync(await alice.GetAsync($"/api/career/privacy/deletions/{id}"), 200);
        await CareerClient.ReadErrorAsync(await alice.GetAsync($"/api/career/privacy/deletions/{Guid.NewGuid()}"), 404);
    }

    [Fact]
    public async Task DeletingCareerDataLeavesNewDataTheUserCreatesAfterwards()
    {
        var user = Fresh(_factory);
        await user.CreateProfileAsync();
        await CareerClient.ReadDataAsync(await user.SendAsync(HttpMethod.Post, "/api/career/privacy/deletions", new { scope = "career_profile" }), 202);
        await CareerClient.ReadErrorAsync(await user.GetAsync("/api/career/profile"), 404);

        await user.CreateProfileAsync("Second career");

        var profile = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/profile"), 200);
        profile.GetProperty("facts").GetProperty("currentTitle").GetString().Should().Be("Second career");
    }
}

/// <summary>DELETE /api/profile/account purges career data first and aborts when the purge fails (ADR 0020).</summary>
public class CareerAccountDeletionApiTests : IClassFixture<CareerPrivacyFactory>
{
    private readonly CareerPrivacyFactory _factory;

    public CareerAccountDeletionApiTests(CareerPrivacyFactory factory)
    {
        _factory = factory;
    }

    private async Task<CareerClient> SeedAccountAsync()
    {
        var user = new CareerClient(_factory);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = new ApplicationUser { Id = user.UserId, UserName = user.UserId, Email = $"{user.UserId}@example.com" };
        db.Users.Add(account);
        db.UserProfiles.Add(new UserProfile { UserId = user.UserId, User = account, Credits = 3, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, LastCreditReset = DateTime.UtcNow });
        CareerPrivacySeed.SeedAll(db, user.UserId, DateTime.UtcNow.AddHours(-1), _factory.Storage);
        await db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task DeletingTheAccountPurgesCareerDataAndLeavesATombstone()
    {
        var user = await SeedAccountAsync();

        var response = await user.SendAsync(HttpMethod.Delete, "/api/profile/account");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await CareerPrivacySeed.TotalAsync(db, user.UserId)).Should().Be(0);
        (await db.UserProfiles.CountAsync(p => p.UserId == user.UserId)).Should().Be(0);
        (await db.CareerTombstones.CountAsync(t => t.OwnerId == user.UserId)).Should().Be(1);
    }

    [Fact]
    public async Task APurgeFailureAbortsAccountDeletion()
    {
        var user = await SeedAccountAsync();
        _factory.Storage.FailNextDeletes = int.MaxValue;
        HttpResponseMessage response;
        try
        {
            response = await user.SendAsync(HttpMethod.Delete, "/api/profile/account");
        }
        finally
        {
            _factory.Storage.FailNextDeletes = 0;
        }

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await response.Content.ReadAsStringAsync()).Should().Contain("CareerDataDeletionFailed");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.UserProfiles.CountAsync(p => p.UserId == user.UserId)).Should().Be(1);
        (await db.Users.CountAsync(u => u.Id == user.UserId)).Should().Be(1);
    }
}
