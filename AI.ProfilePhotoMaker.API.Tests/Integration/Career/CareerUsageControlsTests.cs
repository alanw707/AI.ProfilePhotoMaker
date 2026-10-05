using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.Career;
using AI.ProfilePhotoMaker.API.Services.Career;
using AI.ProfilePhotoMaker.API.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration.Career;

public sealed class MutableClock : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>Fake model whose behaviour a test can script; defaults to the deterministic fake (1 cent, 120 tokens).</summary>
public sealed class ScriptedCareerTextModel : ICareerTextModel
{
    public Func<CareerModelResult>? Override { get; set; }

    public Task<CareerModelResult> CompleteAsync(CareerModelRequest request, CancellationToken ct = default) =>
        Override != null ? Task.FromResult(Override()) : new FakeCareerTextModel().CompleteAsync(request, ct);
}

/// <summary>Career host with a controllable clock, a scripted model and per-class policy values.</summary>
public class CareerUsageFactory : CareerJobFactory
{
    public MutableClock Clock { get; } = new();
    public ScriptedCareerTextModel Model { get; } = new();

    /// <summary>Generous by default so only the limit a class is about can bite.</summary>
    protected virtual Dictionary<string, string?> Policy => new();

    public CareerUsageFactory()
    {
        RegisterModel = false;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        var values = new Dictionary<string, string?>
        {
            ["Career:Usage:PerMinuteRunLimit"] = "1000",
            ["Career:Usage:MaxConcurrentRunsPerUser"] = "1000",
            ["Career:Usage:MaxQueuedRunsGlobal"] = "100000",
            ["Career:Usage:MonthlyModelCostCapUsd"] = "100000",
            ["Career:Usage:PerUserMonthlyModelCostCapUsd"] = "100000"
        };
        foreach (var (k, v) in Policy)
        {
            values[k] = v;
        }
        builder.ConfigureAppConfiguration(c => c.AddInMemoryCollection(values));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddSingleton<ICareerTextModel>(Model);
        });
    }

    public HttpClient Admin()
    {
        var http = CreateAuthenticatedClient();
        http.DefaultRequestHeaders.Add("X-Test-UserId", $"admin-{Guid.NewGuid():N}");
        http.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");
        return http;
    }

    public async Task<CareerClient> UserAsync()
    {
        var user = new CareerClient(this);
        await user.CreateProfileAsync();
        await user.CreateGoalAsync();
        return user;
    }

    public Task<HttpResponseMessage> StartAsync(CareerClient user, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/career/runs")
        {
            Content = JsonContent.Create(new { task = "profile_summary" }, options: CareerClient.Json)
        };
        request.Headers.Add("X-Test-UserId", user.UserId);
        request.Headers.Add("Idempotency-Key", key ?? $"key-{Guid.NewGuid():N}");
        return CreateAuthenticatedClient().SendAsync(request);
    }

    public async Task<JsonElement> AllowanceAsync(CareerClient user) =>
        await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/allowance"), 200);

    public T WithDb<T>(Func<ApplicationDbContext, T> work)
    {
        using var scope = Services.CreateScope();
        return work(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    public async Task<int> ReapAsync()
    {
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ICareerReservationReaper>().ReapAsync();
    }
}

public sealed class UsageAllowanceFactory : CareerUsageFactory
{
    protected override Dictionary<string, string?> Policy => new() { ["Career:Usage:MonthlyRunAllowance"] = "3" };
}

/// <summary>Allowance, settle/release, abandonment and the allowance endpoint (ADR 0022, ADR 0009).</summary>
public class CareerAllowanceUsageTests : IClassFixture<UsageAllowanceFactory>
{
    private readonly UsageAllowanceFactory _f;

    public CareerAllowanceUsageTests(UsageAllowanceFactory f) => _f = f;

    [Fact]
    public async Task AllowanceEndpointShowsReservedThenUsedAndAnExactResetAtTheNextUtcMonth()
    {
        var user = await _f.UserAsync();
        var fresh = await _f.AllowanceAsync(user);
        fresh.GetProperty("limit").GetInt32().Should().Be(3);
        fresh.GetProperty("remaining").GetInt32().Should().Be(3);
        fresh.GetProperty("policyVersion").GetString().Should().NotBeNullOrEmpty();
        var now = _f.Clock.GetUtcNow().UtcDateTime;
        var expected = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
        fresh.GetProperty("resetsAt").GetDateTime().ToUniversalTime().Should().Be(expected);

        await CareerClient.ReadDataAsync(await _f.StartAsync(user), 202);
        var reserved = await _f.AllowanceAsync(user);
        reserved.GetProperty("reserved").GetInt32().Should().Be(1);
        reserved.GetProperty("remaining").GetInt32().Should().Be(2);

        await _f.DrainWorkerAsync();
        var used = await _f.AllowanceAsync(user);
        (used.GetProperty("used").GetInt32(), used.GetProperty("reserved").GetInt32(), used.GetProperty("remaining").GetInt32())
            .Should().Be((1, 0, 2));
    }

    [Fact]
    public async Task ExhaustedAllowanceStillAllowsReadEditAndExport()
    {
        var user = await _f.UserAsync();
        for (var i = 0; i < 3; i++)
        {
            await CareerClient.ReadDataAsync(await _f.StartAsync(user), 202);
        }
        await _f.DrainWorkerAsync();

        var blocked = await CareerClient.ReadErrorAsync(await _f.StartAsync(user), 429);
        blocked.GetProperty("code").GetString().Should().Be("CareerAllowanceExhausted");

        (await user.GetAsync("/api/career/profile")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await user.GetAsync("/api/career/runs")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await user.PutProfileAsync(CareerClient.ValidProfile("Edited title"))).StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.PreconditionRequired);
        (await user.GetAsync("/api/career/privacy/export")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await user.GetAsync("/api/career/allowance")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SuccessAndFailureAfterAModelCallSpendTheUnitOnceAndNeverTwice()
    {
        var user = await _f.UserAsync();
        var ok = await CareerClient.ReadDataAsync(await _f.StartAsync(user), 202);
        await _f.DrainWorkerAsync();
        _f.Model.Override = () => throw new CareerModelException("refused", retryable: false, costCents: 2);
        try
        {
            await CareerClient.ReadDataAsync(await _f.StartAsync(user), 202);
            await _f.DrainWorkerAsync();
        }
        finally
        {
            _f.Model.Override = null;
        }

        var allowance = await _f.AllowanceAsync(user);
        (allowance.GetProperty("used").GetInt32(), allowance.GetProperty("reserved").GetInt32()).Should().Be((2, 0));

        // Cancelling a run that already finished must not settle again.
        await user.SendAsync(HttpMethod.Post, $"/api/career/runs/{ok.GetProperty("id").GetString()}/cancel");
        var after = await _f.AllowanceAsync(user);
        (after.GetProperty("used").GetInt32(), after.GetProperty("reserved").GetInt32()).Should().Be((2, 0));
    }

    [Fact]
    public async Task CancelBeforeAnyModelCallReleasesTheUnit()
    {
        var user = await _f.UserAsync();
        var run = await CareerClient.ReadDataAsync(await _f.StartAsync(user), 202);
        await user.SendAsync(HttpMethod.Post, $"/api/career/runs/{run.GetProperty("id").GetString()}/cancel");

        var allowance = await _f.AllowanceAsync(user);
        (allowance.GetProperty("used").GetInt32(), allowance.GetProperty("reserved").GetInt32()).Should().Be((0, 0));
    }

    [Fact]
    public async Task ReaperReleasesAbandonedReservationsOnceAndSpendsAStartedModelCall()
    {
        var idle = await _f.UserAsync();
        var started = await _f.UserAsync();
        var young = await _f.UserAsync();
        var idleRun = await CareerClient.ReadDataAsync(await _f.StartAsync(idle), 202);
        var startedRun = await CareerClient.ReadDataAsync(await _f.StartAsync(started), 202);
        _f.WithDb(db =>
        {
            db.CareerAgentRuns.Single(r => r.Id == startedRun.GetProperty("id").GetGuid()).ModelCalled = true;
            return db.SaveChanges();
        });

        _f.Clock.Advance(TimeSpan.FromMinutes(25));
        await CareerClient.ReadDataAsync(await _f.StartAsync(young), 202);
        _f.Clock.Advance(TimeSpan.FromMinutes(6));

        (await _f.ReapAsync()).Should().BeGreaterThanOrEqualTo(2);
        (await _f.ReapAsync()).Should().Be(0, "a reservation is settled exactly once");

        var run = await CareerClient.ReadDataAsync(await idle.GetAsync($"/api/career/runs/{idleRun.GetProperty("id").GetString()}"), 200);
        run.GetProperty("status").GetString().Should().Be("failed");
        var released = await _f.AllowanceAsync(idle);
        (released.GetProperty("used").GetInt32(), released.GetProperty("reserved").GetInt32()).Should().Be((0, 0));
        var spent = await _f.AllowanceAsync(started);
        (spent.GetProperty("used").GetInt32(), spent.GetProperty("reserved").GetInt32()).Should().Be((1, 0));
        (await _f.AllowanceAsync(young)).GetProperty("reserved").GetInt32().Should().Be(1, "6 minutes old is not abandoned");
    }

    [Fact]
    public async Task UsageEventsHoldNoResumeOrProfileText()
    {
        const string sentinel = "ZZSENTINELZZ";
        var user = new CareerClient(_f);
        await user.CreateProfileAsync(sentinel);
        await user.CreateGoalAsync();
        await CareerClient.ReadDataAsync(await _f.StartAsync(user), 202);
        await _f.DrainWorkerAsync();

        var events = _f.WithDb(db => db.CareerUsageEvents.AsNoTracking().Where(e => e.OwnerId == user.UserId).ToList());
        events.Should().ContainSingle();
        var e = events.Single();
        (e.Action, e.Outcome, e.Tokens, e.CostCents).Should().Be((CareerUsageActions.ModelStep, CareerUsageOutcomes.Ok, 120, 1));
        e.RunId.Should().NotBeNull();
        foreach (var property in typeof(CareerUsageEvent).GetProperties())
        {
            (property.GetValue(e)?.ToString() ?? string.Empty).Should().NotContain(sentinel, property.Name);
        }
        // The same run did carry the sentinel (so the assertion above is meaningful).
        _f.WithDb(db => db.CareerAgentSteps.Where(s => s.OwnerId == user.UserId).AsEnumerable().Any(s => (s.OutputJson ?? "").Contains(sentinel))).Should().BeTrue();
    }

    [Fact]
    public async Task PrivacyExportIncludesTheOwnersUsageEventsOnly()
    {
        var user = await _f.UserAsync();
        var other = await _f.UserAsync();
        _f.WithDb(db =>
        {
            db.CareerUsageEvents.Add(CareerUsage_Event(user.UserId));
            db.CareerUsageEvents.Add(CareerUsage_Event(other.UserId));
            return db.SaveChanges();
        });

        var body = await (await user.GetAsync("/api/career/privacy/export")).Content.ReadAsStringAsync();
        var rows = JsonDocument.Parse(body).RootElement.GetProperty("sections").GetProperty("CareerUsageEvent");
        rows.GetArrayLength().Should().Be(1);
        rows[0].GetProperty("ownerId").GetString().Should().Be(user.UserId);
    }

    private CareerUsageEvent CareerUsage_Event(string owner) => new()
    {
        Id = Guid.NewGuid(), OwnerId = owner, Action = CareerUsageActions.Export, Outcome = CareerUsageOutcomes.Ok, CreatedAt = _f.Clock.GetUtcNow().UtcDateTime
    };
}

public sealed class UsageConcurrencyFactory : CareerUsageFactory
{
    protected override Dictionary<string, string?> Policy => new() { ["Career:Usage:MaxConcurrentRunsPerUser"] = "2" };
}

public class CareerConcurrencyLimitTests : IClassFixture<UsageConcurrencyFactory>
{
    private readonly UsageConcurrencyFactory _f;

    public CareerConcurrencyLimitTests(UsageConcurrencyFactory f) => _f = f;

    [Fact]
    public async Task ASlotFreesWhenARunFinishes()
    {
        var user = await _f.UserAsync();
        await CareerClient.ReadDataAsync(await _f.StartAsync(user), 202);
        await CareerClient.ReadDataAsync(await _f.StartAsync(user), 202);
        var error = await CareerClient.ReadErrorAsync(await _f.StartAsync(user), 429);
        error.GetProperty("code").GetString().Should().Be("CareerConcurrencyLimit");

        await _f.DrainWorkerAsync();
        await CareerClient.ReadDataAsync(await _f.StartAsync(user), 202);
    }
}

public sealed class UsageRateFactory : CareerUsageFactory
{
    protected override Dictionary<string, string?> Policy => new() { ["Career:Usage:PerMinuteRunLimit"] = "3" };
}

public class CareerUsagePolicyDefaultsTests
{
    [Fact]
    public void DefaultsMatchTheProvisionalPolicyInAdr0022()
    {
        var policy = new CareerUsagePolicy();
        (policy.PerMinuteRunLimit, policy.MaxConcurrentRunsPerUser, policy.MaxQueuedRunsGlobal, policy.MonthlyModelCostCapUsd,
            policy.ControlsCacheSeconds, policy.AbandonedReservationMinutes).Should().Be((6, 2, 200, 50m, 30, 30));
        policy.MonthlyRunAllowance.Should().BeNull("the current Career:Agent value stays the default");
    }
}

public class CareerRateLimitTests : IClassFixture<UsageRateFactory>
{
    private readonly UsageRateFactory _f;

    public CareerRateLimitTests(UsageRateFactory f) => _f = f;

    [Fact]
    public async Task TheFourthRunInAMinuteIs429WithRetryAfterAndRecoversLater()
    {
        var user = await _f.UserAsync();
        for (var i = 0; i < 3; i++)
        {
            await CareerClient.ReadDataAsync(await _f.StartAsync(user), 202);
        }

        var response = await _f.StartAsync(user);
        var error = await CareerClient.ReadErrorAsync(response, 429);
        error.GetProperty("code").GetString().Should().Be("CareerRateLimited");
        response.Headers.RetryAfter!.Delta!.Value.TotalSeconds.Should().BeInRange(1, 60);

        // Another user is unaffected, and the window slides.
        await CareerClient.ReadDataAsync(await _f.StartAsync(await _f.UserAsync()), 202);
        _f.Clock.Advance(TimeSpan.FromSeconds(61));
        await CareerClient.ReadDataAsync(await _f.StartAsync(user), 202);
    }
}

public sealed class UsageBackpressureFactory : CareerUsageFactory
{
    protected override Dictionary<string, string?> Policy => new() { ["Career:Usage:MaxQueuedRunsGlobal"] = "2" };
}

public class CareerBackpressureTests : IClassFixture<UsageBackpressureFactory>
{
    private readonly UsageBackpressureFactory _f;

    public CareerBackpressureTests(UsageBackpressureFactory f) => _f = f;

    [Fact]
    public async Task AFullGlobalQueueAnswers503CareerBusyWithRetryAfter()
    {
        await CareerClient.ReadDataAsync(await _f.StartAsync(await _f.UserAsync()), 202);
        await CareerClient.ReadDataAsync(await _f.StartAsync(await _f.UserAsync()), 202);

        var response = await _f.StartAsync(await _f.UserAsync());
        var error = await CareerClient.ReadErrorAsync(response, 503);
        error.GetProperty("code").GetString().Should().Be("CareerBusy");
        response.Headers.RetryAfter.Should().NotBeNull();

        await _f.DrainWorkerAsync();
        await CareerClient.ReadDataAsync(await _f.StartAsync(await _f.UserAsync()), 202);
    }
}

public sealed class UsageCostFactory : CareerUsageFactory
{
    // The fake model costs 1 cent a call: 3 cents globally, 2 per user.
    protected override Dictionary<string, string?> Policy => new()
    {
        ["Career:Usage:MonthlyModelCostCapUsd"] = "0.03",
        ["Career:Usage:PerUserMonthlyModelCostCapUsd"] = "0.02"
    };
}

public class CareerCostCapTests : IClassFixture<UsageCostFactory>
{
    private readonly UsageCostFactory _f;

    public CareerCostCapTests(UsageCostFactory f) => _f = f;

    [Fact]
    public async Task UserThenGlobalCostCapsStopNewRuns()
    {
        var heavy = await _f.UserAsync();
        for (var i = 0; i < 2; i++)
        {
            await CareerClient.ReadDataAsync(await _f.StartAsync(heavy), 202);
            await _f.DrainWorkerAsync();
        }
        var userCap = await CareerClient.ReadErrorAsync(await _f.StartAsync(heavy), 429);
        userCap.GetProperty("code").GetString().Should().Be("CareerUserCostCapReached");

        var other = await _f.UserAsync();
        await CareerClient.ReadDataAsync(await _f.StartAsync(other), 202);
        await _f.DrainWorkerAsync();

        var response = await _f.StartAsync(await _f.UserAsync());
        var globalCap = await CareerClient.ReadErrorAsync(response, 503);
        globalCap.GetProperty("code").GetString().Should().Be("CareerCostCapReached");
        response.Headers.RetryAfter.Should().NotBeNull();
        (await _f.AllowanceAsync(heavy)).GetProperty("used").GetInt32().Should().Be(2);
    }
}

public class CareerKillSwitchTests : IClassFixture<CareerUsageFactory>
{
    private readonly CareerUsageFactory _f;

    public CareerKillSwitchTests(CareerUsageFactory f) => _f = f;

    private Task<HttpResponseMessage> SetAsync(HttpClient admin, bool generation, bool sources) =>
        admin.PutAsJsonAsync("/api/admin/career/controls", new { generationDisabled = generation, sourcesDisabled = sources }, CareerClient.Json);

    [Fact]
    public async Task GenerationSwitchBlocksOnlyNewRunsAndReplaysReadEditAndExportStillWork()
    {
        var user = await _f.UserAsync();
        var admin = _f.Admin();
        var existing = await CareerClient.ReadDataAsync(await _f.StartAsync(user, "replay-key-0001"), 202);
        try
        {
            (await SetAsync(admin, true, false)).StatusCode.Should().Be(HttpStatusCode.OK);

            var error = await CareerClient.ReadErrorAsync(await _f.StartAsync(user), 503);
            error.GetProperty("code").GetString().Should().Be("CareerGenerationPaused");

            (await user.GetAsync("/api/career/profile")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await user.GetAsync($"/api/career/runs/{existing.GetProperty("id").GetString()}")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await user.GetAsync("/api/career/privacy/export")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await user.GetAsync("/api/career/allowance")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await user.SendAsync(HttpMethod.Post, $"/api/career/runs/{existing.GetProperty("id").GetString()}/cancel")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await _f.StartAsync(user, "replay-key-0001")).StatusCode.Should().Be(HttpStatusCode.Accepted);
        }
        finally
        {
            await SetAsync(admin, false, false);
        }
        await CareerClient.ReadDataAsync(await _f.StartAsync(user), 202);
    }

    [Fact]
    public async Task SourcesSwitchPausesTheJobSourceOnly()
    {
        var user = await _f.UserAsync();
        var admin = _f.Admin();
        _f.Source.IsConfigured = true;
        try
        {
            await SetAsync(admin, false, true);
            var response = await user.GetAsync("/api/career/jobs/observations?occupation=15-1252.00");
            var error = await CareerClient.ReadErrorAsync(response, 503);
            error.GetProperty("code").GetString().Should().Be("CareerGenerationPaused");
            (await user.GetAsync("/api/career/profile")).StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            await SetAsync(admin, false, false);
        }
    }

    [Fact]
    public async Task ControlsAreCachedForThirtySecondsAndReadAgainAfterwards()
    {
        var controls = _f.Services.GetRequiredService<ICareerOperatorControls>();
        (await controls.GetAsync()).GenerationDisabled.Should().BeFalse();
        _f.WithDb(db =>
        {
            db.CareerOperatorStates.RemoveRange(db.CareerOperatorStates);
            db.CareerOperatorStates.Add(new CareerOperatorState { GenerationDisabled = true, UpdatedAt = DateTime.UtcNow });
            return db.SaveChanges();
        });
        try
        {
            (await controls.GetAsync()).GenerationDisabled.Should().BeFalse("served from cache");
            _f.Clock.Advance(TimeSpan.FromSeconds(31));
            (await controls.GetAsync()).GenerationDisabled.Should().BeTrue();
        }
        finally
        {
            await controls.UpdateAsync(false, false, null);
        }
    }
}

public class CareerAdminUsageTests : IClassFixture<CareerUsageFactory>
{
    private readonly CareerUsageFactory _f;

    public CareerAdminUsageTests(CareerUsageFactory f) => _f = f;

    [Theory]
    [InlineData("GET", "/api/admin/career/usage")]
    [InlineData("GET", "/api/admin/career/controls")]
    [InlineData("PUT", "/api/admin/career/controls")]
    public async Task NonAdminsGet403(string method, string path)
    {
        var user = await _f.UserAsync();
        var response = await user.SendAsync(new HttpMethod(method), path, method == "PUT" ? new { generationDisabled = true } : null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminGetsControlsAndReportMathOverSeededEvents()
    {
        var a = $"report-a-{Guid.NewGuid():N}";
        var b = $"report-b-{Guid.NewGuid():N}";
        var at = new DateTime(2020, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        CareerUsageEvent Row(string owner, string action, string outcome, int ms, int cents) => new()
        {
            Id = Guid.NewGuid(), OwnerId = owner, Action = action, Outcome = outcome, LatencyMs = ms, CostCents = cents, CreatedAt = at
        };
        _f.WithDb(db =>
        {
            db.CareerUsageEvents.AddRange(
                Row(a, CareerUsageActions.ModelStep, CareerUsageOutcomes.Ok, 100, 10),
                Row(a, CareerUsageActions.ModelStep, CareerUsageOutcomes.Ok, 200, 20),
                Row(a, CareerUsageActions.Export, CareerUsageOutcomes.Ok, 50, 0),
                Row(b, CareerUsageActions.ModelStep, CareerUsageOutcomes.Failed, 400, 5),
                Row(b, CareerUsageActions.ModelStep, CareerUsageOutcomes.Ok, 300, 15));
            return db.SaveChanges();
        });
        var admin = _f.Admin();

        var report = await CareerClient.ReadDataAsync(
            await admin.GetAsync("/api/admin/career/usage?from=2020-01-01T00:00:00Z&to=2020-02-01T00:00:00Z"), 200);

        report.GetProperty("totalEvents").GetInt32().Should().Be(5);
        report.GetProperty("failures").GetInt32().Should().Be(1);
        report.GetProperty("failureRate").GetDouble().Should().BeApproximately(0.2, 1e-9);
        report.GetProperty("totalCostUsd").GetDecimal().Should().Be(0.50m);
        var model = report.GetProperty("actions").EnumerateArray().Single(x => x.GetProperty("action").GetString() == "model_step");
        (model.GetProperty("count").GetInt32(), model.GetProperty("failures").GetInt32(), model.GetProperty("costUsd").GetDecimal(),
            model.GetProperty("latencyP50Ms").GetInt32(), model.GetProperty("latencyP95Ms").GetInt32())
            .Should().Be((4, 1, 0.50m, 200, 400));
        var users = report.GetProperty("perActiveUser");
        (users.GetProperty("activeUsers").GetInt32(), users.GetProperty("eventsP50").GetInt32(), users.GetProperty("eventsP95").GetInt32(), users.GetProperty("eventsMax").GetInt32())
            .Should().Be((2, 2, 3, 3));
        (users.GetProperty("costUsdP50").GetDecimal(), users.GetProperty("costUsdMax").GetDecimal()).Should().Be((0.20m, 0.30m));
        report.GetRawText().Should().NotContain(a).And.NotContain(b, "owners appear only as hashes");
        report.GetProperty("topUsers")[0].GetProperty("events").GetInt32().Should().Be(3);

        var controls = await CareerClient.ReadDataAsync(await admin.GetAsync("/api/admin/career/controls"), 200);
        controls.GetProperty("generationDisabled").GetBoolean().Should().BeFalse();
        (await admin.GetAsync("/api/admin/career/usage?from=2020-02-01&to=2020-01-01")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

/// <summary>
/// Real parallel creates against SQLite (the in-memory test database ignores concurrency tokens, so it cannot
/// show a race). Each request has its own context and connection, as in production.
/// </summary>
public sealed class CareerConcurrentCreateTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"career-usage-{Guid.NewGuid():N}.db");
    private DbContextOptions<ApplicationDbContext> Options => new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlite($"Data Source={_path}").Options;

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_path);
    }

    private async Task<string> SeedAsync()
    {
        var owner = $"race-{Guid.NewGuid():N}";
        await using var db = new ApplicationDbContext(Options);
        await db.Database.EnsureCreatedAsync();
        db.Users.Add(new ApplicationUser { Id = owner, UserName = owner });
        db.CareerProfiles.Add(new CareerProfile { Id = Guid.NewGuid(), OwnerId = owner, ActiveVersionNumber = 1 });
        await db.SaveChangesAsync();
        // The month's row exists up front, as it does after a user's first run.
        var now = DateTime.UtcNow;
        db.CareerAllowances.Add(new CareerAllowance
        {
            Id = Guid.NewGuid(), OwnerId = owner, PeriodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc), CreatedAt = now
        });
        await db.SaveChangesAsync();
        return owner;
    }

    private async Task<CareerOutcomeKind> CreateAsync(string owner, int allowance, int concurrent)
    {
        await using var db = new ApplicationDbContext(Options);
        var service = new CareerAgentRunService(
            db, Microsoft.Extensions.Options.Options.Create(new CareerAgentOptions { MonthlyRunAllowance = allowance }), TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CareerAgentRunService>.Instance, new FakeCareerTextModel(),
            usage: Microsoft.Extensions.Options.Options.Create(new CareerUsagePolicy { PerMinuteRunLimit = 1000, MaxConcurrentRunsPerUser = concurrent }));
        return (await service.CreateAsync(owner, new CreateCareerRunRequest { Task = "profile_summary" }, $"key-{Guid.NewGuid():N}")).Kind;
    }

    [Fact]
    public async Task ParallelCreatesNeverExceedTheAllowance()
    {
        var owner = await SeedAsync();
        var kinds = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => CreateAsync(owner, allowance: 3, concurrent: 100))));

        var accepted = kinds.Count(k => k == CareerOutcomeKind.Ok);
        accepted.Should().BeInRange(1, 3);
        await using var db = new ApplicationDbContext(Options);
        var row = await db.CareerAllowances.SingleAsync(a => a.OwnerId == owner);
        (row.Reserved + row.Used).Should().Be(accepted);
        (await db.CareerAgentRuns.CountAsync(r => r.OwnerId == owner)).Should().Be(accepted);
    }

    [Fact]
    public async Task ParallelCreatesNeverExceedTheConcurrencyLimit()
    {
        var owner = await SeedAsync();
        var kinds = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => CreateAsync(owner, allowance: 20, concurrent: 2))));

        kinds.Count(k => k == CareerOutcomeKind.Ok).Should().BeInRange(1, 2);
        await using var db = new ApplicationDbContext(Options);
        (await db.CareerAgentRuns.CountAsync(r => r.OwnerId == owner)).Should().BeLessThanOrEqualTo(2);
    }
}
