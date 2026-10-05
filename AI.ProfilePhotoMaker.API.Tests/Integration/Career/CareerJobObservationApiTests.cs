using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Services.Career;
using AI.ProfilePhotoMaker.API.Tests.Services.Career;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration.Career;

/// <summary>Replaces the job source with a fake; nothing reaches the network.</summary>
public class CareerJobFactory : CareerAgentFactory
{
    public FakeJobObservationSource Source { get; } = new();

    public CareerJobFactory()
    {
        RegisterModel = false;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.AddSingleton<IJobObservationSource>(Source));
    }
}

/// <summary>Job observations end to end (#386, ADR 0015).</summary>
public class CareerJobObservationApiTests : IClassFixture<CareerJobFactory>
{
    private readonly CareerJobFactory _factory;

    public CareerJobObservationApiTests(CareerJobFactory factory)
    {
        _factory = factory;
        factory.Source.Raw.Clear();
        factory.Source.Failure = null;
        factory.Source.LastQuery = null;
        factory.Source.IsConfigured = true;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static RawJobObservation Job(string id, string city = "Denver", string state = "CO", JobRemoteStatus remote = JobRemoteStatus.Unknown) =>
        new(id, $"IT Specialist {id}", "Department of Veterans Affairs", new[] { new RawJobLocation(city, state) }, 98500, 128000,
            "usd_per_year", "annual", Today.AddDays(-2), Today.AddDays(10), remote, null, "2210", "GS-12", $"https://www.usajobs.gov/job/{id}");

    private async Task<CareerClient> UserAsync(bool occupation = true, bool preferDenver = false)
    {
        var user = new CareerClient(_factory);
        await CareerClient.ReadDataAsync(await user.PostGoalAsync(new
        {
            targetRole = "Software developer", targetLocation = "Denver, CO", workArrangement = "hybrid",
            desiredPayMin = 120000, desiredPayMax = 160000, weeklyEffortHours = 5, confirmed = true
        }), 201);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var version = db.CareerGoalVersions.Single(v => v.OwnerId == user.UserId);
        if (occupation)
        {
            version.OccupationCode = "15-1252.00";
            version.OccupationTitle = "Software Developers";
            version.OccupationReferenceRelease = "30.2";
        }
        if (preferDenver)
        {
            version.PreferredAreaCode = "19740";
            version.PreferredAreaTitle = "Denver-Aurora-Centennial, CO";
            version.PreferredAreaLevel = "metro";
        }
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<System.Text.Json.JsonElement> Get(CareerClient user, string query = "") =>
        await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/jobs/observations" + query), 200);

    [Fact]
    public async Task HappyPathReturnsTheContractShapeForTheGoalsOwnArea()
    {
        _factory.Source.Raw.Add(Job("1"));
        var data = await Get(await UserAsync());

        data.GetProperty("occupation").GetProperty("code").GetString().Should().Be("15-1252.00");
        data.GetProperty("area").GetProperty("code").GetString().Should().Be("19740");
        data.GetProperty("area").GetProperty("resolution").GetString().Should().Be("metro");
        var coverage = data.GetProperty("coverage");
        coverage.GetProperty("available").GetBoolean().Should().BeTrue();
        coverage.GetProperty("sourceName").GetString().Should().Be("USAJOBS");
        coverage.GetProperty("counts").GetProperty("shown").GetInt32().Should().Be(1);
        var o = data.GetProperty("observations").EnumerateArray().Single();
        o.GetProperty("observationId").GetString().Should().Be("usajobs:1");
        o.GetProperty("pay").GetProperty("unit").GetString().Should().Be("usd_per_year");
        o.GetProperty("postedOn").GetString().Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}$");
        o.GetProperty("locations")[0].GetProperty("match").GetString().Should().Be("user_area");
        data.GetProperty("truncated").GetBoolean().Should().BeFalse();
        _factory.Source.LastQuery!.OccupationCode.Should().Be("15-1252.00");
    }

    [Fact]
    public async Task NoKeyIsAnHonestUnavailableStateNotAnError()
    {
        _factory.Source.IsConfigured = false;
        _factory.Source.Raw.Add(Job("1"));

        var data = await Get(await UserAsync());

        data.GetProperty("coverage").GetProperty("available").GetBoolean().Should().BeFalse();
        data.GetProperty("coverage").GetProperty("reason").GetString().Should().Be("source_not_configured");
        data.GetProperty("observations").GetArrayLength().Should().Be(0);
        data.GetProperty("note").GetString().Should().Contain("not the labour market");
    }

    [Fact]
    public async Task ProviderFailureOrTimeoutIsSourceUnavailableWithAnEmptyList()
    {
        _factory.Source.Failure = new JobSourceUnavailableException("boom");

        var data = await Get(await UserAsync());

        data.GetProperty("coverage").GetProperty("available").GetBoolean().Should().BeFalse();
        data.GetProperty("coverage").GetProperty("reason").GetString().Should().Be("source_unavailable");
        data.GetProperty("observations").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task NoOccupationStillReturnsTheCoverageBlockWithAnEmptyListAndDoesNotCallTheSource()
    {
        _factory.Source.Raw.Add(Job("1"));

        var data = await Get(await UserAsync(occupation: false));

        data.GetProperty("coverage").GetProperty("sourceId").GetString().Should().Be("usajobs");
        data.GetProperty("observations").GetArrayLength().Should().Be(0);
        data.GetProperty("occupation").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        _factory.Source.LastQuery.Should().BeNull();
    }

    [Fact]
    public async Task FiltersApplyOverTheEndpoint()
    {
        _factory.Source.Raw.AddRange(new[]
        {
            Job("1", remote: JobRemoteStatus.Eligible), Job("2"), Job("3", city: "Austin", state: "TX")
        });
        var user = await UserAsync();

        (await Get(user)).GetProperty("observations").GetArrayLength().Should().Be(2);
        var eligible = await Get(user, "?eligibleOnly=true");
        eligible.GetProperty("observations").GetArrayLength().Should().Be(1);
        eligible.GetProperty("coverage").GetProperty("counts").GetProperty("remoteUnknownExcluded").GetInt32().Should().Be(1);
        (await Get(user, "?remote=unknown")).GetProperty("observations").GetArrayLength().Should().Be(1);
        (await Get(user, "?q=zzz")).GetProperty("observations").GetArrayLength().Should().Be(0);
        (await Get(user, "?area=" + "&q=IT%20Specialist%203")).GetProperty("observations").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task InvalidRemoteFilterIs400()
    {
        var user = await UserAsync();

        (await CareerClient.ReadErrorAsync(await user.GetAsync("/api/career/jobs/observations?remote=maybe"), 400))
            .GetProperty("code").GetString().Should().Be("ValidationError");
    }

    [Fact]
    public async Task StalePreferenceIsReportedWhenTheRequestedAreaDiffersFromTheSavedOne()
    {
        _factory.Source.Raw.Add(Job("1"));
        var user = await UserAsync(preferDenver: true);

        var same = (await Get(user, "?area=19740")).GetProperty("preferences");
        same.GetProperty("stalePreference").GetBoolean().Should().BeFalse();
        same.GetProperty("note").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        (await Get(user)).GetProperty("preferences").GetProperty("stalePreference").GetBoolean().Should().BeFalse();

        var other = await Get(user, "?area=12420");
        var prefs = other.GetProperty("preferences");
        prefs.GetProperty("areaCode").GetString().Should().Be("19740");
        prefs.GetProperty("stalePreference").GetBoolean().Should().BeTrue();
        prefs.GetProperty("note").GetString().Should().Contain("Denver").And.Contain("Austin");
        other.GetProperty("area").GetProperty("code").GetString().Should().Be("12420");
    }

    [Fact]
    public async Task SourceReferenceExplainsTheUnavailableStateWithoutCallingTheProvider()
    {
        _factory.Source.IsConfigured = false;

        var data = await CareerClient.ReadDataAsync(await new CareerClient(_factory).GetAsync("/api/career/jobs/source"), 200);

        data.GetProperty("sourceId").GetString().Should().Be("usajobs");
        data.GetProperty("configured").GetBoolean().Should().BeFalse();
        data.GetProperty("sourceUrl").GetString().Should().Be("https://www.usajobs.gov/");
        _factory.Source.LastQuery.Should().BeNull();
    }
}
