using System.Text.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Tests.Services.Career;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration.Career;

/// <summary>Market comparison and the saved location preference end to end (#385, ADR 0014).</summary>
public class CareerMarketComparisonApiTests : IClassFixture<CareerMarketFactory>
{
    private readonly CareerMarketFactory _factory;

    public CareerMarketComparisonApiTests(CareerMarketFactory factory)
    {
        _factory = factory;
    }

    private static object Goal() => new
    {
        targetRole = "Senior software developer",
        targetLocation = "Austin, TX",
        workArrangement = "hybrid",
        desiredPayMin = 120000,
        desiredPayMax = 160000,
        weeklyEffortHours = 5,
        confirmed = true
    };

    /// <summary>A user with a goal (version 1), optionally with the software developer occupation set on it.</summary>
    private async Task<(CareerClient User, string GoalId)> UserAsync(bool occupation = true)
    {
        var user = new CareerClient(_factory);
        var goal = await CareerClient.ReadDataAsync(await user.PostGoalAsync(Goal()), 201);
        if (occupation)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var version = db.CareerGoalVersions.Single(v => v.OwnerId == user.UserId);
            version.OccupationCode = "15-1252.00";
            version.OccupationTitle = "Software Developers";
            version.OccupationReferenceRelease = "30.2";
            await db.SaveChangesAsync();
        }
        return (user, goal.GetProperty("id").GetString()!);
    }

    private static Task<HttpResponseMessage> Save(CareerClient user, object body, string? ifMatch = "\"goal-v1\"") =>
        user.SendAsync(HttpMethod.Post, "/api/career/markets/preference", body, ifMatch);

    private static object Denver(bool? confirmed = true) => new { areaCode = "19740", level = "metro", confirmed };

    // ---- Metrics and comparison -------------------------------------------------

    [Fact]
    public async Task MetricsEndpointListsFiveMetrics()
    {
        var data = await CareerClient.ReadDataAsync(await new CareerClient(_factory).GetAsync("/api/career/markets/metrics"), 200);

        var metrics = data.GetProperty("metrics").EnumerateArray().ToList();
        metrics.Should().HaveCount(5);
        var projected = metrics.Single(m => m.GetProperty("key").GetString() == "projected_change");
        projected.GetProperty("supported").GetBoolean().Should().BeFalse();
        projected.GetProperty("reason").GetString().Should().Be("national_only_source");
        projected.GetProperty("geographyLevels").EnumerateArray().Select(l => l.GetString()).Should().Equal("national");
        metrics.Where(m => m.GetProperty("supported").GetBoolean()).Should()
            .OnlyContain(m => m.GetProperty("geographyLevels").GetArrayLength() >= 2);
    }

    [Fact]
    public async Task StateComparisonReturnsTheSnapshotValuesAndTheDisclosure()
    {
        var (user, _) = await UserAsync();

        var data = await CareerClient.ReadDataAsync(
            await user.GetAsync("/api/career/markets/compare?metric=median_wage&level=state&areas=08,06"), 200);

        data.GetProperty("occupation").GetProperty("code").GetString().Should().Be("15-1252.00");
        data.GetProperty("occupation").GetProperty("publishedCode").GetString().Should().Be("15-1252");
        var areas = data.GetProperty("areas").EnumerateArray().ToList();
        areas.Should().HaveCount(51);
        var colorado = areas.Single(a => a.GetProperty("areaCode").GetString() == "08");
        colorado.GetProperty("value").GetDouble().Should().Be(138390);
        colorado.GetProperty("selected").GetBoolean().Should().BeTrue();
        data.GetProperty("national").GetProperty("value").GetDouble().Should().Be(135980);
        data.GetProperty("selectionLimit").GetInt32().Should().Be(3);
        areas.Count(a => a.GetProperty("selected").GetBoolean()).Should().Be(2);

        var oews = RawSnapshot.Root.GetProperty("sources").GetProperty("oews");
        var reference = data.GetProperty("reference");
        reference.GetProperty("release").GetString().Should().Be(oews.GetProperty("referencePeriod").GetString());
        reference.GetProperty("publishedOn").GetString().Should().Be(oews.GetProperty("publishedOn").GetString());
        reference.GetProperty("coverage").GetString().Should().Be(oews.GetProperty("coverage").GetString()).And.NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task MetroComparisonIncludesAlaskaAndHawaiiAndFiltersByQuery()
    {
        var (user, _) = await UserAsync();

        var all = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/markets/compare?metric=employment&level=metro"), 200);
        var titles = all.GetProperty("areas").EnumerateArray().Select(a => a.GetProperty("areaTitle").GetString()).ToList();
        titles.Should().Contain(new[] { "Anchorage, AK", "Urban Honolulu, HI" });
        titles.Should().NotContain(t => t!.Contains("nonmetropolitan", StringComparison.OrdinalIgnoreCase));

        var found = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/markets/compare?metric=employment&level=metro&q=ANCHOR"), 200);
        found.GetProperty("areas").EnumerateArray().Select(a => a.GetProperty("areaTitle").GetString()).Should().Equal("Anchorage, AK");
    }

    [Theory]
    [InlineData("metric=nope&level=state", 400, "ValidationError")]
    [InlineData("metric=median_wage&level=county", 400, "ValidationError")]
    [InlineData("level=state", 400, "ValidationError")]
    [InlineData("metric=projected_change&level=state", 409, "CareerMetricUnsupported")]
    public async Task BadOrUnsupportedRequestsUseTheStableCodes(string query, int status, string code)
    {
        var (user, _) = await UserAsync();

        var error = await CareerClient.ReadErrorAsync(await user.GetAsync($"/api/career/markets/compare?{query}"), status);

        error.GetProperty("code").GetString().Should().Be(code);
    }

    [Fact]
    public async Task UnsupportedMetricCarriesItsReason()
    {
        var (user, _) = await UserAsync();

        var error = await CareerClient.ReadErrorAsync(
            await user.GetAsync("/api/career/markets/compare?metric=projected_change&level=state"), 409);

        error.GetProperty("message").GetString().Should().Be("national_only_source");
    }

    [Fact]
    public async Task ComparingWithoutAConfirmedOccupationOrAGoalIs409()
    {
        var (noOccupation, _) = await UserAsync(occupation: false);
        var noGoal = new CareerClient(_factory);

        foreach (var user in new[] { noOccupation, noGoal })
        {
            (await CareerClient.ReadErrorAsync(await user.GetAsync("/api/career/markets/compare?metric=median_wage&level=state"), 409))
                .GetProperty("code").GetString().Should().Be("CareerOccupationRequired");
        }
    }

    // ---- Preference -------------------------------------------------------------

    [Fact]
    public async Task SavingAPreferenceWritesANewGoalVersionWithTheAreaTitle()
    {
        var (user, _) = await UserAsync();

        var response = await Save(user, Denver());
        var goal = await CareerClient.ReadDataAsync(response, 200);

        response.Headers.ETag!.Tag.Should().Be("\"goal-v2\"");
        goal.GetProperty("version").GetInt32().Should().Be(2);
        goal.GetProperty("goal").GetProperty("targetLocation").GetString().Should().Be("Denver-Aurora-Centennial, CO");
        goal.GetProperty("goal").GetProperty("targetRole").GetString().Should().Be("Senior software developer");
        var area = goal.GetProperty("preferredArea");
        (area.GetProperty("code").GetString(), area.GetProperty("title").GetString(), area.GetProperty("level").GetString())
            .Should().Be(("19740", "Denver-Aurora-Centennial, CO", "metro"));
        goal.GetProperty("occupation").GetProperty("code").GetString().Should().Be("15-1252.00");

        var current = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/goals"), 200);
        current.GetProperty("preferredArea").GetProperty("code").GetString().Should().Be("19740");
    }

    [Fact]
    public async Task APreferenceCanBeSavedForAStateToo()
    {
        var (user, _) = await UserAsync();

        var goal = await CareerClient.ReadDataAsync(await Save(user, new { areaCode = "08", level = "state", confirmed = true }), 200);

        goal.GetProperty("goal").GetProperty("targetLocation").GetString().Should().Be("Colorado");
        goal.GetProperty("preferredArea").GetProperty("level").GetString().Should().Be("state");
    }

    [Fact]
    public async Task AnUnknownAreaOrTheWrongLevelIs404()
    {
        var (user, _) = await UserAsync();

        foreach (var body in new object[] { new { areaCode = "00000", level = "metro", confirmed = true }, new { areaCode = "08", level = "metro", confirmed = true } })
        {
            (await CareerClient.ReadErrorAsync(await Save(user, body), 404)).GetProperty("code").GetString().Should().Be("CareerAreaNotFound");
        }
        (await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/goals"), 200)).GetProperty("version").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task APreferenceNeedsExplicitConfirmationAndValidFields()
    {
        var (user, _) = await UserAsync();

        foreach (var body in new object[]
                 {
                     Denver(confirmed: false), Denver(confirmed: null), new { areaCode = "19740", level = "metro" },
                     new { areaCode = "", level = "metro", confirmed = true }, new { areaCode = "19740", level = "county", confirmed = true }
                 })
        {
            (await CareerClient.ReadErrorAsync(await Save(user, body), 400)).GetProperty("code").GetString().Should().Be("ValidationError");
        }
        (await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/goals"), 200)).GetProperty("version").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task APreferenceWithoutAGoalIs409()
    {
        var error = await CareerClient.ReadErrorAsync(await Save(new CareerClient(_factory), Denver()), 409);

        error.GetProperty("code").GetString().Should().Be("CareerGoalRequired");
    }

    [Fact]
    public async Task StaleOrMissingIfMatchIs412Or428AndChangesNothing()
    {
        var (user, _) = await UserAsync();
        (await Save(user, Denver())).EnsureSuccessStatusCode();

        var stale = await CareerClient.ReadErrorAsync(await Save(user, Denver(), "\"goal-v1\""), 412);
        var missing = await CareerClient.ReadErrorAsync(await Save(user, Denver(), null), 428);

        stale.GetProperty("code").GetString().Should().Be("CareerVersionConflict");
        stale.GetProperty("currentVersion").GetInt32().Should().Be(2);
        missing.GetProperty("code").GetString().Should().Be("CareerPreconditionRequired");
        (await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/goals"), 200)).GetProperty("version").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task ThePreferenceIsCarriedForwardByEditsAndRestoresOfAVersionThatHasIt()
    {
        var (user, goalId) = await UserAsync();
        (await Save(user, Denver())).EnsureSuccessStatusCode();

        var patched = await CareerClient.ReadDataAsync(await user.PatchGoalAsync(goalId, GoalAt("Denver-Aurora-Centennial, CO"), "\"goal-v2\""), 200);
        patched.GetProperty("version").GetInt32().Should().Be(3);
        patched.GetProperty("preferredArea").GetProperty("code").GetString().Should().Be("19740");
        patched.GetProperty("goal").GetProperty("targetLocation").GetString().Should().Be("Denver-Aurora-Centennial, CO");

        var restored = await CareerClient.ReadDataAsync(
            await user.SendAsync(HttpMethod.Post, $"/api/career/goals/{goalId}/versions/2/restore", null, "\"goal-v3\""), 200);
        restored.GetProperty("version").GetInt32().Should().Be(4);
        restored.GetProperty("preferredArea").GetProperty("code").GetString().Should().Be("19740");

        var back = await CareerClient.ReadDataAsync(
            await user.SendAsync(HttpMethod.Post, $"/api/career/goals/{goalId}/versions/1/restore", null, "\"goal-v4\""), 200);
        back.GetProperty("preferredArea").ValueKind.Should().Be(JsonValueKind.Null);
    }

    private static object GoalAt(string? location) => new
    {
        targetRole = "Senior software developer",
        targetLocation = location,
        workArrangement = "hybrid",
        desiredPayMin = 120000,
        desiredPayMax = 160000,
        weeklyEffortHours = 5,
        confirmed = true
    };

    [Fact]
    public async Task EditingTheLocationByHandClearsThePreferredArea()
    {
        var (user, goalId) = await UserAsync();
        (await Save(user, Denver())).EnsureSuccessStatusCode();

        var patched = await CareerClient.ReadDataAsync(await user.PatchGoalAsync(goalId, GoalAt("Seattle, WA"), "\"goal-v2\""), 200);

        patched.GetProperty("preferredArea").ValueKind.Should().Be(JsonValueKind.Null);
        patched.GetProperty("goal").GetProperty("targetLocation").GetString().Should().Be("Seattle, WA");
    }

    [Fact]
    public async Task ResavingTheSameLocationIgnoringCaseAndPaddingKeepsThePreferredArea()
    {
        var (user, goalId) = await UserAsync();
        var saved = await CareerClient.ReadDataAsync(await Save(user, Denver()), 200);
        var title = saved.GetProperty("preferredArea").GetProperty("title").GetString()!;

        var patched = await CareerClient.ReadDataAsync(
            await user.PatchGoalAsync(goalId, GoalAt("  " + title.ToUpperInvariant() + " "), "\"goal-v2\""), 200);

        patched.GetProperty("preferredArea").GetProperty("code").GetString().Should().Be("19740");
    }

    [Fact]
    public async Task APreferenceNeverTouchesAnotherUsersGoal()
    {
        var (owner, _) = await UserAsync();
        var (other, _) = await UserAsync();
        (await Save(owner, Denver())).EnsureSuccessStatusCode();

        var untouched = await CareerClient.ReadDataAsync(await other.GetAsync("/api/career/goals"), 200);

        untouched.GetProperty("version").GetInt32().Should().Be(1);
        untouched.GetProperty("preferredArea").ValueKind.Should().Be(JsonValueKind.Null);
        untouched.GetProperty("goal").GetProperty("targetLocation").GetString().Should().Be("Austin, TX");
    }

    [Fact]
    public async Task UnauthenticatedRequestsAre401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Unauthenticated", "true");

        ((int)(await client.GetAsync("/api/career/markets/metrics")).StatusCode).Should().Be(401);
        ((int)(await client.GetAsync("/api/career/markets/compare?metric=median_wage&level=state")).StatusCode).Should().Be(401);
        ((int)(await client.PostAsync("/api/career/markets/preference", null)).StatusCode).Should().Be(401);
    }
}
