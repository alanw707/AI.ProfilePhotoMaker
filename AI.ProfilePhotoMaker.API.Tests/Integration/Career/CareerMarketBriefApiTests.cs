using System.Net.Http.Json;
using System.Text.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Services.Career;
using AI.ProfilePhotoMaker.API.Tests.Services.Career;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration.Career;

/// <summary>A clock the test can move, so data ageing is checked at exact dates.</summary>
public sealed class MovableClock : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public void Set(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;
}

/// <summary>No model registered (briefs need none), optionally with a chosen reference or clock.</summary>
public class CareerMarketFactory : CareerAgentFactory
{
    public IMarketReference? Reference { get; init; }
    public MovableClock? Clock { get; init; }

    public CareerMarketFactory()
    {
        RegisterModel = false;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            if (Reference != null)
            {
                services.AddSingleton(Reference);
            }
            if (Clock != null)
            {
                services.AddSingleton<TimeProvider>(Clock);
            }
        });
    }
}

/// <summary>Market briefs end to end (#382): runs, cited sections, honest unavailable states, staleness, isolation.</summary>
public class CareerMarketBriefApiTests : IClassFixture<CareerMarketFactory>
{
    private readonly CareerMarketFactory _factory;

    public CareerMarketBriefApiTests(CareerMarketFactory factory)
    {
        _factory = factory;
    }

    private static readonly string[] SoftwareDuties =
    {
        "Designed and developed backend software systems and REST APIs for an invoicing platform",
        "Modified existing software to correct errors and improve performance",
        "Wrote documentation and developed software testing and validation procedures",
        "Analyzed user needs and software requirements to determine feasibility of design"
    };

    private static object Profile(string title) => new
    {
        currentTitle = title,
        industry = "Technology",
        yearsExperience = 5,
        location = "Austin, TX",
        summary = "Reliable and curious.",
        skills = new[] { "Programming", "Systems Analysis" },
        highlights = SoftwareDuties,
        workArrangement = "hybrid",
        confirmed = true
    };

    private static object Goal(string? location) => new
    {
        targetRole = "Senior software developer",
        targetLocation = location,
        workArrangement = "hybrid",
        desiredPayMin = 120000,
        desiredPayMax = 160000,
        weeklyEffortHours = 5,
        confirmed = true
    };

    private static async Task<JsonElement> StartAsync(CareerClient user, CustomWebApplicationFactory factory, string task, int expected = 202)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/career/runs")
        {
            Content = JsonContent.Create(new { task }, options: CareerClient.Json)
        };
        request.Headers.Add("X-Test-UserId", user.UserId);
        request.Headers.Add("Idempotency-Key", $"key-{Guid.NewGuid():N}");
        var response = await factory.CreateAuthenticatedClient().SendAsync(request);
        return expected == 202 ? await CareerClient.ReadDataAsync(response, 202) : await CareerClient.ReadErrorAsync(response, expected);
    }

    private async Task<JsonElement> RunAsync(CareerClient user, string task, CareerMarketFactory? factory = null)
    {
        var host = factory ?? _factory;
        var run = await StartAsync(user, host, task);
        await host.DrainWorkerAsync();
        return await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/runs/{run.GetProperty("id").GetString()}"), 200);
    }

    /// <summary>A user with a profile, a goal at <paramref name="location"/> and (optionally) a confirmed software developer occupation.</summary>
    private async Task<CareerClient> UserAsync(string? location = "Denver, CO", bool confirmOccupation = true, CareerMarketFactory? factory = null)
    {
        var host = factory ?? _factory;
        var user = new CareerClient(host);
        (await user.PutProfileAsync(Profile("Software Engineer"))).EnsureSuccessStatusCode();
        await CareerClient.ReadDataAsync(await user.PostGoalAsync(Goal(location)), 201);
        if (confirmOccupation)
        {
            var run = await RunAsync(user, "occupation_match", host);
            var match = await CareerClient.ReadDataAsync(
                await user.GetAsync($"/api/career/occupation-matches/{run.GetProperty("occupationMatchId").GetString()}"), 200);
            (await user.SendAsync(HttpMethod.Post, $"/api/career/occupation-matches/{match.GetProperty("id").GetString()}/confirm",
                new { occupationCode = "15-1252.00" }, "\"goal-v1\"")).EnsureSuccessStatusCode();
        }
        return user;
    }

    private async Task<JsonElement> BriefAsync(CareerClient user, CareerMarketFactory? factory = null)
    {
        var run = await RunAsync(user, "market_brief", factory);
        run.GetProperty("status").GetString().Should().Be("completed");
        return await CareerClient.ReadDataAsync(
            await user.GetAsync($"/api/career/market-briefs/{run.GetProperty("marketBriefId").GetString()}"), 200);
    }

    private static JsonElement Section(JsonElement brief, string key) =>
        brief.GetProperty("sections").EnumerateArray().Single(s => s.GetProperty("key").GetString() == key);

    private static JsonElement Figure(JsonElement section, string key, string areaCode) =>
        section.GetProperty("figures").EnumerateArray().Single(f => f.GetProperty("key").GetString() == key && f.GetProperty("areaCode").GetString() == areaCode);

    // ---- Start ------------------------------------------------------------------

    [Fact]
    public async Task StartingWithoutAConfirmedOccupationIs409OccupationRequired()
    {
        var withGoal = await UserAsync(confirmOccupation: false);
        var noGoal = new CareerClient(_factory);
        (await noGoal.PutProfileAsync(Profile("Software Engineer"))).EnsureSuccessStatusCode();

        (await StartAsync(withGoal, _factory, "market_brief", 409)).GetProperty("code").GetString().Should().Be("CareerOccupationRequired");
        (await StartAsync(noGoal, _factory, "market_brief", 409)).GetProperty("code").GetString().Should().Be("CareerOccupationRequired");
        (await StartAsync(new CareerClient(_factory), _factory, "market_brief", 409)).GetProperty("code").GetString().Should().Be("CareerProfileRequired");
    }

    [Fact]
    public async Task BothSourcesUnavailableAnswers503AtStartAndOnTheReferenceEndpoint()
    {
        var factory = new CareerMarketFactory { Reference = FakeMarketReference.Neither() };
        var user = await UserAsync(factory: factory);

        (await StartAsync(user, factory, "market_brief", 503)).GetProperty("code").GetString().Should().Be("CareerReferenceUnavailable");
        (await CareerClient.ReadErrorAsync(await user.GetAsync("/api/career/market/reference"), 503))
            .GetProperty("code").GetString().Should().Be("CareerReferenceUnavailable");
    }

    // ---- Run and brief ----------------------------------------------------------

    [Fact]
    public async Task RunCompletesWithoutAModelOrAllowanceAndSavesACitedBrief()
    {
        var user = await UserAsync("Denver, CO");

        var run = await RunAsync(user, "market_brief");

        run.GetProperty("status").GetString().Should().Be("completed");
        run.GetProperty("task").GetString().Should().Be("market_brief");
        run.GetProperty("steps").EnumerateArray().Select(s => s.GetProperty("name").GetString()).Should()
            .Equal("read_goal", "look_up_wages", "look_up_outlook", "compare_alternatives", "save_brief");
        run.GetProperty("steps").EnumerateArray().Select(s => s.GetProperty("label").GetString()).Should().Equal(
            "Read your career goal", "Looked up wages and employment", "Looked up the job outlook", "Compared related occupations", "Saved your market brief");
        run.GetProperty("allowance").GetProperty("used").GetInt32().Should().Be(0);
        run.GetProperty("allowance").GetProperty("reserved").GetInt32().Should().Be(0);
        run.GetProperty("pinnedGoalVersion").GetInt32().Should().Be(2);

        var brief = await CareerClient.ReadDataAsync(
            await user.GetAsync($"/api/career/market-briefs/{run.GetProperty("marketBriefId").GetString()}"), 200);
        brief.GetProperty("runId").GetString().Should().Be(run.GetProperty("id").GetString());
        brief.GetProperty("status").GetString().Should().Be("complete");
        brief.GetProperty("occupation").GetProperty("code").GetString().Should().Be("15-1252.00");
        brief.GetProperty("occupation").GetProperty("title").GetString().Should().Be("Software Developers");
        brief.GetProperty("occupation").GetProperty("published").GetProperty("oews").GetProperty("match").GetString().Should().Be("exact");
        brief.GetProperty("location").GetProperty("input").GetString().Should().Be("Denver, CO");
        brief.GetProperty("location").GetProperty("resolution").GetString().Should().Be("metro");
        brief.GetProperty("location").GetProperty("local").GetProperty("code").GetString().Should().Be("19740");
        var pinned = brief.GetProperty("pinned");
        pinned.GetProperty("profileVersion").GetInt32().Should().Be(1);
        pinned.GetProperty("goalVersion").GetInt32().Should().Be(2);
        pinned.GetProperty("oewsRelease").GetString().Should().Be("2025-05");
        pinned.GetProperty("projectionsRelease").GetString().Should().Be("2025-2035");
        brief.GetProperty("stale").GetBoolean().Should().BeFalse();
        brief.GetProperty("staleReasons").GetArrayLength().Should().Be(0);
        brief.GetProperty("dataStale").GetBoolean().Should().BeFalse();

        brief.GetProperty("sections").EnumerateArray().Select(s => s.GetProperty("key").GetString()).Should()
            .Equal("wages", "employment", "outlook", "alternatives");
        var wages = Section(brief, "wages");
        wages.GetProperty("status").GetString().Should().Be("complete");
        wages.GetProperty("reason").ValueKind.Should().Be(JsonValueKind.Null);
        Figure(wages, "medianAnnual", "99").GetProperty("value").GetDouble().Should().Be(135980);
        Figure(wages, "medianAnnual", "19740").GetProperty("value").GetDouble().Should().Be(137610);
        Figure(wages, "medianDifferenceAnnual", "19740").GetProperty("value").GetDouble().Should().Be(1630);
        Figure(Section(brief, "outlook"), "annualOpenings", "99").GetProperty("value").GetDouble().Should().Be(95.3);
        var alternatives = Section(brief, "alternatives");
        alternatives.GetProperty("status").GetString().Should().Be("complete");
        var items = alternatives.GetProperty("items").EnumerateArray().ToList();
        items.Count.Should().BeInRange(1, 3);
        items.Select(i => i.GetProperty("code").GetString()).Should().NotContain("15-1252.00");
        brief.GetProperty("sources").EnumerateArray().Select(s => s.GetProperty("id").GetString()).Should().Equal("oews", "projections");
        brief.GetProperty("sources")[0].GetProperty("license").GetString().Should().Be("Public domain (U.S. government work)");
        brief.GetProperty("nextAction").GetProperty("route").GetString().Should().Be("/app/career/occupation");
        brief.GetProperty("nextAction").GetProperty("label").GetString().Should().Be("Check your target occupation");
    }

    [Fact]
    public async Task AnUnresolvedLocationMakesLocalFiguresUnavailableAndAsksForACityAndState()
    {
        var user = await UserAsync("Atlantis, ZZ");

        var brief = await BriefAsync(user);

        brief.GetProperty("status").GetString().Should().Be("complete");
        brief.GetProperty("location").GetProperty("resolution").GetString().Should().Be("unresolved");
        brief.GetProperty("location").GetProperty("local").ValueKind.Should().Be(JsonValueKind.Null);
        var wages = Section(brief, "wages");
        wages.GetProperty("status").GetString().Should().Be("unavailable");
        wages.GetProperty("reason").GetString().Should().Be("location_unresolved");
        wages.GetProperty("figures").EnumerateArray().Should().OnlyContain(f => f.GetProperty("areaCode").GetString() == "99");
        brief.GetProperty("nextAction").GetProperty("label").GetString().Should().Be("Add a city and state to your goal");
        brief.GetProperty("nextAction").GetProperty("route").GetString().Should().Be("/app/career/setup");
    }

    [Fact]
    public async Task AUnavailableProjectionsSourceGivesAPartialBriefThatKeepsWagesAndEmployment()
    {
        var factory = new CareerMarketFactory { Reference = FakeMarketReference.WithoutProjections() };
        var user = await UserAsync(factory: factory);

        var brief = await BriefAsync(user, factory);

        brief.GetProperty("status").GetString().Should().Be("partial");
        Section(brief, "wages").GetProperty("status").GetString().Should().Be("complete");
        Section(brief, "employment").GetProperty("status").GetString().Should().Be("complete");
        Figure(Section(brief, "wages"), "medianAnnual", "99").GetProperty("value").GetDouble().Should().Be(135980);
        var outlook = Section(brief, "outlook");
        outlook.GetProperty("status").GetString().Should().Be("failed");
        outlook.GetProperty("reason").GetString().Should().Be("CareerReferenceUnavailable");
        brief.GetProperty("pinned").GetProperty("projectionsRelease").ValueKind.Should().Be(JsonValueKind.Null);
        brief.GetProperty("sources").GetArrayLength().Should().Be(1);

        var reference = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/market/reference"), 200);
        reference.GetProperty("sources").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task ReferenceEndpointListsSourcesAndCounts()
    {
        var reference = await CareerClient.ReadDataAsync(await new CareerClient(_factory).GetAsync("/api/career/market/reference"), 200);

        reference.GetProperty("areaCount").GetInt32().Should().Be(445);
        reference.GetProperty("occupationCount").GetInt32().Should().Be(831);
        reference.GetProperty("sources").EnumerateArray().Select(s => s.GetProperty("id").GetString()).Should().Equal("oews", "projections");
        new CareerMarketOptions().DataStaleMonths.Should().Be(18);
    }

    // ---- Staleness --------------------------------------------------------------

    [Fact]
    public async Task ChangingTheProfileMakesTheBriefStaleWithoutChangingIt()
    {
        var user = await UserAsync();
        var before = await BriefAsync(user);

        (await user.PutProfileAsync(Profile("Engineer II"), "\"profile-v1\"")).EnsureSuccessStatusCode();

        var after = await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/market-briefs/{before.GetProperty("id").GetString()}"), 200);
        after.GetProperty("stale").GetBoolean().Should().BeTrue();
        after.GetProperty("staleReasons").EnumerateArray().Select(r => r.GetString()).Should().Equal("profile_changed");
        after.GetProperty("sections").GetRawText().Should().Be(before.GetProperty("sections").GetRawText());
        after.GetProperty("pinned").GetRawText().Should().Be(before.GetProperty("pinned").GetRawText());
    }

    [Fact]
    public async Task ChangingTheGoalOrItsOccupationReportsWhy()
    {
        var user = await UserAsync();
        var brief = await BriefAsync(user);
        var id = brief.GetProperty("id").GetString();
        var goal = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/goals"), 200);
        var goalId = goal.GetProperty("id").GetString()!;

        // A manual edit carries the occupation forward: only the goal moved.
        (await user.PatchGoalAsync(goalId, Goal("Boston, MA"), "\"goal-v2\"")).EnsureSuccessStatusCode();
        var edited = await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/market-briefs/{id}"), 200);
        edited.GetProperty("staleReasons").EnumerateArray().Select(r => r.GetString()).Should().Equal("goal_changed");

        // Restoring version 1 (no occupation) moves the occupation too.
        (await user.SendAsync(HttpMethod.Post, $"/api/career/goals/{goalId}/versions/1/restore", null, "\"goal-v3\"")).EnsureSuccessStatusCode();
        var restored = await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/market-briefs/{id}"), 200);
        restored.GetProperty("staleReasons").EnumerateArray().Select(r => r.GetString()).Should().Equal("goal_changed", "occupation_changed");
        restored.GetProperty("location").GetProperty("input").GetString().Should().Be("Denver, CO");
    }

    [Fact]
    public async Task DataIsFlaggedStaleExactlyWhenASourceIsOlderThanTheConfiguredMonths()
    {
        var clock = new MovableClock();
        clock.Set(new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero));
        var factory = new CareerMarketFactory { Clock = clock };
        var user = await UserAsync(factory: factory);
        var brief = await BriefAsync(user, factory);
        var path = $"/api/career/market-briefs/{brief.GetProperty("id").GetString()}";

        // OEWS was published 2026-05-15 and projections 2026-08-27; 18 months from the latter is 2028-02-27.
        clock.Set(new DateTimeOffset(2028, 2, 27, 0, 0, 0, TimeSpan.Zero));
        (await CareerClient.ReadDataAsync(await user.GetAsync(path), 200)).GetProperty("dataStale").GetBoolean().Should().BeTrue("OEWS is already past 18 months");
        clock.Set(new DateTimeOffset(2027, 11, 15, 0, 0, 0, TimeSpan.Zero));
        (await CareerClient.ReadDataAsync(await user.GetAsync(path), 200)).GetProperty("dataStale").GetBoolean().Should().BeFalse();
        clock.Set(new DateTimeOffset(2027, 11, 16, 0, 0, 0, TimeSpan.Zero));
        (await CareerClient.ReadDataAsync(await user.GetAsync(path), 200)).GetProperty("dataStale").GetBoolean().Should().BeTrue();
    }

    // ---- List, isolation, deletion ----------------------------------------------

    [Fact]
    public async Task ListIsNewestFirstWithSummaries()
    {
        var user = await UserAsync();
        var first = await BriefAsync(user);
        var second = await BriefAsync(user);
        (await user.PutProfileAsync(Profile("Engineer II"), "\"profile-v1\"")).EnsureSuccessStatusCode();

        var list = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/market-briefs"), 200);

        var briefs = list.GetProperty("briefs").EnumerateArray().ToList();
        briefs.Select(b => b.GetProperty("id").GetString()).Should().Equal(second.GetProperty("id").GetString(), first.GetProperty("id").GetString());
        var summary = briefs[0];
        summary.GetProperty("occupationCode").GetString().Should().Be("15-1252.00");
        summary.GetProperty("occupationTitle").GetString().Should().Be("Software Developers");
        summary.GetProperty("areaTitle").GetString().Should().Be("Denver-Aurora-Centennial, CO");
        summary.GetProperty("status").GetString().Should().Be("complete");
        summary.GetProperty("stale").GetBoolean().Should().BeTrue();
        summary.GetProperty("createdAt").ValueKind.Should().Be(JsonValueKind.String);
        (await CareerClient.ReadDataAsync(await new CareerClient(_factory).GetAsync("/api/career/market-briefs"), 200))
            .GetProperty("briefs").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task AnotherOwnersBriefIs404AndMissingIdsToo()
    {
        var owner = await UserAsync();
        var brief = await BriefAsync(owner);
        var intruder = new CareerClient(_factory);

        (await CareerClient.ReadErrorAsync(await intruder.GetAsync($"/api/career/market-briefs/{brief.GetProperty("id").GetString()}"), 404))
            .GetProperty("code").GetString().Should().Be("CareerMarketBriefNotFound");
        await CareerClient.ReadErrorAsync(await intruder.GetAsync($"/api/career/market-briefs/{Guid.NewGuid()}"), 404);
        (await CareerClient.ReadDataAsync(await owner.GetAsync($"/api/career/market-briefs/{brief.GetProperty("id").GetString()}"), 200))
            .GetProperty("status").GetString().Should().Be("complete");
    }

    [Fact]
    public async Task UnauthenticatedRequestsAre401()
    {
        var client = _factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("X-Test-Unauthenticated", "true");

        ((int)(await client.GetAsync("/api/career/market-briefs")).StatusCode).Should().Be(401);
        ((int)(await client.GetAsync($"/api/career/market-briefs/{Guid.NewGuid()}")).StatusCode).Should().Be(401);
        ((int)(await client.GetAsync("/api/career/market/reference")).StatusCode).Should().Be(401);
    }

    [Fact]
    public async Task OwnerDeletionRemovesBriefs()
    {
        var user = await UserAsync();
        await BriefAsync(user);
        var other = await UserAsync();
        await BriefAsync(other);

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ICareerPrivateDataService>().DeleteAllForOwnerAsync(user.UserId);
        }

        using var check = _factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.CareerMarketBriefs.CountAsync(b => b.OwnerId == user.UserId)).Should().Be(0);
        (await db.CareerMarketBriefs.CountAsync(b => b.OwnerId == other.UserId)).Should().Be(1);
    }
}
