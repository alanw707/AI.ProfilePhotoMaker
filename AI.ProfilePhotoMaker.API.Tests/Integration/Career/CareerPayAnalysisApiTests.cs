using System.Net.Http.Json;
using System.Text.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Services.Career;
using AI.ProfilePhotoMaker.API.Tests.Services.Career;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration.Career;

public class CareerPayFactory : CareerMarketFactory
{
    public IPayObservationSource? Source { get; init; }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        if (Source != null) builder.ConfigureTestServices(services => services.AddSingleton(Source));
    }
}

public class CareerPayAnalysisApiTests
{
    private sealed class FixtureSource : IPayObservationSource
    {
        private readonly int _count;
        public FixtureSource(int count) => _count = count;
        public string? SourceId => "synthetic-fixture";
        public IReadOnlyList<PayObservation> ObservationsFor(string occupationCode, string areaCode)
        {
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
                "Services", "Career", "PayEvidenceFixtures", "pay-evidence-fixtures.json")));
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            return JsonSerializer.Deserialize<List<PayObservation>>(json.RootElement.GetProperty("covered").GetRawText(), options)!
                .Where(r => r.Role == "software").Take(_count)
                // The adapter contract: rows are keyed by the requested occupation and area code.
                .Select(r => r with { Role = occupationCode, Geography = areaCode }).ToList();
        }
    }

    [Theory]
    [InlineData(12)]
    [InlineData(7)]
    public async Task ARealCohortIsCountedButNeverPublishedWhileNoProviderHasRights(int count)
    {
        var clock = new MovableClock();
        clock.Set(new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero));
        using var host = new CareerPayFactory { Source = new FixtureSource(count), Clock = clock };
        var user = await User(host);
        var (_, analysis) = await Analyze(user, host);
        var cohort = Section(analysis, "personalized");

        // The cohort is reported for transparency, but authorization is the gate's to give: with no
        // provider rights, no interval is published however good the data is.
        Assert.Equal("unavailable", cohort.GetProperty("status").GetString());
        Assert.Equal("provider_rights_unverified", cohort.GetProperty("reason").GetString());
        Assert.Equal(JsonValueKind.Null, cohort.GetProperty("interval").ValueKind);
        Assert.Equal(count, cohort.GetProperty("cohort").GetProperty("included").GetInt32());
        Assert.Contains("provider_rights_unverified",
            analysis.GetProperty("blockedReasons").EnumerateArray().Select(x => x.GetString()));
        if (count == 12)
        {
            Assert.Equal(6, cohort.GetProperty("cohort").GetProperty("employers").GetInt32());
        }
        var recompute = await CareerClient.ReadDataAsync(await user.SendAsync(HttpMethod.Post,
            $"/api/career/pay-analyses/{analysis.GetProperty("id").GetString()}/recompute"), 200);
        Assert.True(recompute.GetProperty("matches").GetBoolean());
    }

    private static readonly string[] Duties = {
        "Designed and developed backend software systems and REST APIs for an invoicing platform",
        "Modified existing software to correct errors and improve performance",
        "Wrote documentation and developed software testing and validation procedures",
        "Analyzed user needs and software requirements to determine feasibility of design"
    };
    private static object Profile(string title = "Software Engineer") => new {
        currentTitle = title, industry = "Technology", yearsExperience = 5, location = "Austin, TX",
        summary = "Reliable and curious.", skills = new[] { "Programming", "Systems Analysis" },
        highlights = Duties, workArrangement = "hybrid", confirmed = true };
    private static object Goal(string location = "Denver, CO", int? pay = 150000) => new {
        targetRole = "Senior software developer", targetLocation = location, workArrangement = "hybrid",
        desiredPayMin = pay, desiredPayMax = pay, weeklyEffortHours = 5, confirmed = true };
    private static async Task<JsonElement> Start(CareerClient user, CareerPayFactory host, string task, int status = 202)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/career/runs") {
            Content = JsonContent.Create(new { task }, options: CareerClient.Json) };
        request.Headers.Add("X-Test-UserId", user.UserId);
        request.Headers.Add("Idempotency-Key", $"key-{Guid.NewGuid():N}");
        var result = await host.CreateAuthenticatedClient().SendAsync(request);
        return status == 202 ? await CareerClient.ReadDataAsync(result, status) : await CareerClient.ReadErrorAsync(result, status);
    }
    private static async Task<CareerClient> User(CareerPayFactory host, bool confirm = true)
    {
        var user = new CareerClient(host);
        (await user.PutProfileAsync(Profile())).EnsureSuccessStatusCode();
        await CareerClient.ReadDataAsync(await user.PostGoalAsync(Goal()), 201);
        if (confirm)
        {
            var run = await Start(user, host, "occupation_match");
            await host.DrainWorkerAsync();
            var dto = await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/runs/{run.GetProperty("id").GetString()}"), 200);
            var id = dto.GetProperty("occupationMatchId").GetString();
            (await user.SendAsync(HttpMethod.Post, $"/api/career/occupation-matches/{id}/confirm",
                new { occupationCode = "15-1252.00" }, "\"goal-v1\"")).EnsureSuccessStatusCode();
        }
        return user;
    }
    private static async Task<(JsonElement Run, JsonElement Analysis)> Analyze(CareerClient user, CareerPayFactory host)
    {
        var queued = await Start(user, host, "pay_analysis");
        await host.DrainWorkerAsync();
        var run = await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/runs/{queued.GetProperty("id").GetString()}"), 200);
        var analysis = await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/pay-analyses/{run.GetProperty("payAnalysisId").GetString()}"), 200);
        return (run, analysis);
    }
    private static JsonElement Section(JsonElement analysis, string key) =>
        analysis.GetProperty("sections").EnumerateArray().Single(x => x.GetProperty("key").GetString() == key);

    [Fact]
    public async Task DefaultSourceCompletesWithoutModelAndPreservesBenchmark()
    {
        using var host = new CareerPayFactory();
        var user = await User(host);
        var (run, analysis) = await Analyze(user, host);
        Assert.Equal("completed", run.GetProperty("status").GetString());
        Assert.Equal(new[] { "read_goal", "read_benchmark", "evaluate_cohort", "build_scenario", "save_analysis" },
            run.GetProperty("steps").EnumerateArray().Select(s => s.GetProperty("name").GetString()));
        Assert.Equal(0, run.GetProperty("allowance").GetProperty("used").GetInt32());
        Assert.Equal(0, run.GetProperty("allowance").GetProperty("reserved").GetInt32());
        Assert.Equal("provider_rights_unverified", Section(analysis, "personalized").GetProperty("reason").GetString());
        Assert.Equal(JsonValueKind.Null, Section(analysis, "personalized").GetProperty("interval").ValueKind);
        Assert.Equal(135980, Section(analysis, "benchmark").GetProperty("figures")[0].GetProperty("value").GetInt32());
        Assert.Equal(12390, Section(analysis, "scenario").GetProperty("gapAnnual").GetDouble());
        var list = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/pay-analyses"), 200);
        Assert.Equal(analysis.GetProperty("id").GetString(), list.GetProperty("analyses")[0].GetProperty("id").GetString());
        Assert.False(list.GetProperty("analyses")[0].GetProperty("personalizedAvailable").GetBoolean());
        var check = await CareerClient.ReadDataAsync(await user.SendAsync(HttpMethod.Post,
            $"/api/career/pay-analyses/{analysis.GetProperty("id").GetString()}/recompute"), 200);
        Assert.True(check.GetProperty("matches").GetBoolean());
    }

    [Fact]
    public async Task PinnedEditsAreDetectedButNotPersistedAndReadsRemainPrivate()
    {
        using var host = new CareerPayFactory();
        var user = await User(host);
        var (_, analysis) = await Analyze(user, host);
        var id = analysis.GetProperty("id").GetString();
        var intruder = new CareerClient(host);
        Assert.Equal("CareerPayAnalysisNotFound", (await CareerClient.ReadErrorAsync(await intruder.GetAsync($"/api/career/pay-analyses/{id}"), 404)).GetProperty("code").GetString());
        await CareerClient.ReadErrorAsync(await intruder.SendAsync(HttpMethod.Post, $"/api/career/pay-analyses/{id}" + "/recompute"), 404);
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = await db.CareerPayAnalyses.SingleAsync(a => a.Id == Guid.Parse(id!));
            row.OccupationCode = "15-1299.08";
            row.AreaCode = "08";
            await db.SaveChangesAsync();
        }
        var changed = await CareerClient.ReadDataAsync(await user.SendAsync(HttpMethod.Post, $"/api/career/pay-analyses/{id}/recompute"), 200);
        Assert.False(changed.GetProperty("matches").GetBoolean());
        Assert.Contains(changed.GetProperty("differences").EnumerateArray(), d => d.GetString() == "occupationCode");
        Assert.Contains(changed.GetProperty("differences").EnumerateArray(), d => d.GetString() == "areaCode");
        Assert.Equal(analysis.GetProperty("inputHash").GetString(), changed.GetProperty("storedInputHash").GetString());
    }

    [Fact]
    public async Task ProfileAndGoalChangesMakeOldContentStaleAndDeletionRemovesIt()
    {
        using var host = new CareerPayFactory();
        var user = await User(host);
        var (_, analysis) = await Analyze(user, host);
        var id = analysis.GetProperty("id").GetString();
        (await user.PutProfileAsync(Profile("Engineer II"), "\"profile-v1\"")).EnsureSuccessStatusCode();
        var profileStale = await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/pay-analyses/{id}"), 200);
        Assert.Contains(profileStale.GetProperty("staleReasons").EnumerateArray(), r => r.GetString() == "profile_changed");
        var goal = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/goals"), 200);
        (await user.PatchGoalAsync(goal.GetProperty("id").GetString()!, Goal("Boston, MA"), "\"goal-v2\"")).EnsureSuccessStatusCode();
        var goalStale = await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/pay-analyses/{id}"), 200);
        Assert.Contains(goalStale.GetProperty("staleReasons").EnumerateArray(), r => r.GetString() == "goal_changed");
        Assert.Contains(goalStale.GetProperty("staleReasons").EnumerateArray(), r => r.GetString() == "area_changed");
        Assert.Equal(analysis.GetProperty("sections").GetRawText(), goalStale.GetProperty("sections").GetRawText());
        using (var scope = host.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ICareerPrivateDataService>().DeleteAllForOwnerAsync(user.UserId);
        using var check = host.Services.CreateScope();
        Assert.Equal(0, await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().CareerPayAnalyses.CountAsync(a => a.OwnerId == user.UserId));
    }

    [Fact]
    public async Task RequiresOccupationAndAuthAndExposesGateRows()
    {
        using var host = new CareerPayFactory();
        var user = await User(host, false);
        Assert.Equal("CareerOccupationRequired", (await Start(user, host, "pay_analysis", 409)).GetProperty("code").GetString());
        var gates = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/pay/qualification"), 200);
        Assert.False(gates.GetProperty("personalizedAllowed").GetBoolean());
        Assert.Equal(8, gates.GetProperty("gates").GetArrayLength());
        Assert.Equal("Unverified", gates.GetProperty("gates")[0].GetProperty("status").GetString());
        var anon = host.CreateAuthenticatedClient();
        anon.DefaultRequestHeaders.Add("X-Test-Unauthenticated", "true");
        Assert.Equal(401, (int)(await anon.GetAsync("/api/career/pay/qualification")).StatusCode);
        Assert.Equal(401, (int)(await anon.GetAsync("/api/career/pay-analyses")).StatusCode);
        Assert.Equal(401, (int)(await anon.GetAsync($"/api/career/pay-analyses/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(401, (int)(await anon.PostAsync($"/api/career/pay-analyses/{Guid.NewGuid()}/recompute", null)).StatusCode);
    }
}
