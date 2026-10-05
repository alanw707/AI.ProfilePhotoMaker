using System.Net.Http.Json;
using System.Text.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Services.Career;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration.Career;

/// <summary>Roadmaps end to end (#387, ADR 0016): model-free runs, acceptance that never edits the goal, versions, staleness, isolation.</summary>
public class CareerRoadmapApiTests
{
    private static readonly string[] Duties =
    {
        "Designed and developed backend software systems and REST APIs for an invoicing platform",
        "Modified existing software to correct errors and improve performance",
        "Wrote documentation and developed software testing and validation procedures",
        "Analyzed user needs and software requirements to determine feasibility of design"
    };

    internal static object Profile(string title = "Software Engineer") => new
    {
        currentTitle = title, industry = "Technology", yearsExperience = 5, location = "Austin, TX",
        summary = "Reliable and curious.", skills = new[] { "Programming", "Systems Analysis" },
        highlights = Duties, workArrangement = "hybrid", confirmed = true
    };

    internal static object Goal(int hours = 6, string location = "Denver, CO") => new
    {
        targetRole = "Senior software developer", targetLocation = location, workArrangement = "hybrid",
        desiredPayMin = 150000, desiredPayMax = 150000, weeklyEffortHours = hours, confirmed = true
    };

    internal static async Task<JsonElement> StartAsync(CareerClient user, CareerPayFactory host, string task, int status = 202)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/career/runs") { Content = JsonContent.Create(new { task }, options: CareerClient.Json) };
        request.Headers.Add("X-Test-UserId", user.UserId);
        request.Headers.Add("Idempotency-Key", $"key-{Guid.NewGuid():N}");
        var result = await host.CreateAuthenticatedClient().SendAsync(request);
        return status == 202 ? await CareerClient.ReadDataAsync(result, status) : await CareerClient.ReadErrorAsync(result, status);
    }

    internal static async Task<JsonElement> RunAsync(CareerClient user, CareerPayFactory host, string task)
    {
        var queued = await StartAsync(user, host, task);
        await host.DrainWorkerAsync();
        return await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/runs/{queued.GetProperty("id").GetString()}"), 200);
    }

    internal static async Task<CareerClient> UserAsync(CareerPayFactory host, int hours = 6, bool confirm = true)
    {
        var user = new CareerClient(host);
        (await user.PutProfileAsync(Profile())).EnsureSuccessStatusCode();
        await CareerClient.ReadDataAsync(await user.PostGoalAsync(Goal(hours)), 201);
        if (confirm)
        {
            var run = await RunAsync(user, host, "occupation_match");
            (await user.SendAsync(HttpMethod.Post, $"/api/career/occupation-matches/{run.GetProperty("occupationMatchId").GetString()}/confirm",
                new { occupationCode = "15-1252.00" }, "\"goal-v1\"")).EnsureSuccessStatusCode();
        }
        return user;
    }

    internal static async Task<JsonElement> RoadmapAsync(CareerClient user, CareerPayFactory host)
    {
        var run = await RunAsync(user, host, "roadmap");
        Assert.Equal("completed", run.GetProperty("status").GetString());
        return await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/roadmaps/{run.GetProperty("roadmapId").GetString()}"), 200);
    }

    internal static IEnumerable<JsonElement> Tasks(JsonElement option) =>
        option.GetProperty("thisWeek").EnumerateArray()
            .Concat(option.GetProperty("milestones").EnumerateArray().SelectMany(m => m.GetProperty("tasks").EnumerateArray()));

    // ---- Run ----------------------------------------------------------------------

    [Fact]
    public async Task RunCompletesWithoutAModelAndReleasesTheAllowance()
    {
        using var host = new CareerPayFactory();
        var user = await UserAsync(host);

        var run = await RunAsync(user, host, "roadmap");

        Assert.Equal("roadmap", run.GetProperty("task").GetString());
        Assert.Equal(new[] { "read_goal", "read_evidence", "build_options", "plan_tasks", "save_roadmap" },
            run.GetProperty("steps").EnumerateArray().Select(s => s.GetProperty("name").GetString()));
        Assert.Equal(new[] { "Read your career goal", "Read your market and pay evidence", "Compared possible paths",
                "Planned tasks around your available time", "Saved your roadmap" },
            run.GetProperty("steps").EnumerateArray().Select(s => s.GetProperty("label").GetString()));
        Assert.Equal(0, run.GetProperty("allowance").GetProperty("used").GetInt32());
        Assert.Equal(0, run.GetProperty("allowance").GetProperty("reserved").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, run.GetProperty("roadmapId").ValueKind);
    }

    [Fact]
    public async Task RoadmapHasEvidenceGatedOptionsCitedFiguresAndNoGoalChange()
    {
        using var host = new CareerPayFactory();
        var user = await UserAsync(host);

        var roadmap = await RoadmapAsync(user, host);

        Assert.Equal(1, roadmap.GetProperty("version").GetInt32());
        Assert.Equal("proposed", roadmap.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, roadmap.GetProperty("selectedOption").ValueKind);
        Assert.False(roadmap.GetProperty("stale").GetBoolean());
        Assert.Equal(6, roadmap.GetProperty("weeklyEffortHours").GetDouble());
        var pinned = roadmap.GetProperty("pinned");
        Assert.Equal(2, pinned.GetProperty("goalVersion").GetInt32());
        Assert.Equal("15-1252.00", pinned.GetProperty("occupationCode").GetString());
        Assert.Equal(JsonValueKind.Null, pinned.GetProperty("marketBriefId").ValueKind);
        Assert.Equal(JsonValueKind.Null, pinned.GetProperty("payAnalysisId").ValueKind);

        var options = roadmap.GetProperty("options").EnumerateArray().ToList();
        var omitted = roadmap.GetProperty("omittedOptions").EnumerateArray().ToList();
        Assert.Equal("closest_fit", options[0].GetProperty("key").GetString());
        // Every option is either offered or omitted with a reason, never invented.
        var keys = options.Select(o => o.GetProperty("key").GetString()!).Concat(omitted.Select(o => o.GetProperty("key").GetString()!)).ToList();
        Assert.Equal(new[] { "closest_fit", "higher_ambition", "steadier_transition" }, keys.OrderBy(k => Array.IndexOf(new[] { "closest_fit", "higher_ambition", "steadier_transition" }, k)));
        Assert.All(omitted, o => Assert.False(string.IsNullOrEmpty(o.GetProperty("reason").GetString())));
        var closest = options[0];
        Assert.Equal("A scenario, not a promise.", closest.GetProperty("timelineNote").GetString());
        Assert.Contains(closest.GetProperty("rationale").EnumerateArray(),
            r => r.GetProperty("sourceId").GetString() == "oews" && !string.IsNullOrEmpty(r.GetProperty("release").GetString()) && r.GetProperty("text").GetString()!.Contains("$135,980"));
        Assert.Equal(new[] { 30, 60, 90 }, closest.GetProperty("milestones").EnumerateArray().Select(m => m.GetProperty("day").GetInt32()));
        Assert.True(Tasks(closest).Any());
        Assert.Equal(JsonValueKind.Null, roadmap.GetProperty("lowTimeNote").ValueKind);

        var list = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/roadmaps"), 200);
        var first = list.GetProperty("roadmaps")[0];
        Assert.Equal(roadmap.GetProperty("id").GetString(), first.GetProperty("id").GetString());
        Assert.Equal(options.Count, first.GetProperty("optionCount").GetInt32());
        Assert.False(first.GetProperty("stale").GetBoolean());
    }

    [Fact]
    public async Task AGoalWithoutAConfirmedOccupationIs409OccupationRequired()
    {
        using var host = new CareerPayFactory();
        var ambiguous = await UserAsync(host, confirm: false);
        var noGoal = new CareerClient(host);
        (await noGoal.PutProfileAsync(Profile())).EnsureSuccessStatusCode();

        Assert.Equal("CareerOccupationRequired", (await StartAsync(ambiguous, host, "roadmap", 409)).GetProperty("code").GetString());
        Assert.Equal("CareerOccupationRequired", (await StartAsync(noGoal, host, "roadmap", 409)).GetProperty("code").GetString());
        Assert.Equal("CareerProfileRequired", (await StartAsync(new CareerClient(host), host, "roadmap", 409)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task WithoutAPayAnalysisTheRoadmapNamesTheMissingEvidenceAndStillBuilds()
    {
        using var host = new CareerPayFactory();
        var user = await UserAsync(host);

        var sparse = await RoadmapAsync(user, host);
        Assert.Contains(sparse.GetProperty("options")[0].GetProperty("missingEvidence").EnumerateArray(),
            m => m.GetString()!.Contains("No pay analysis"));

        var pay = await RunAsync(user, host, "pay_analysis");
        var brief = await RunAsync(user, host, "market_brief");
        var full = await RoadmapAsync(user, host);
        Assert.Equal(pay.GetProperty("payAnalysisId").GetString(), full.GetProperty("pinned").GetProperty("payAnalysisId").GetString());
        Assert.Equal(brief.GetProperty("marketBriefId").GetString(), full.GetProperty("pinned").GetProperty("marketBriefId").GetString());
        Assert.DoesNotContain(full.GetProperty("options")[0].GetProperty("missingEvidence").EnumerateArray(),
            m => m.GetString()!.Contains("No pay analysis") || m.GetString()!.Contains("No market brief"));
        Assert.Equal(2, full.GetProperty("version").GetInt32());
        // The earlier roadmap now reads as stale: newer evidence exists.
        var old = await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/roadmaps/{sparse.GetProperty("id").GetString()}"), 200);
        Assert.Contains(old.GetProperty("staleReasons").EnumerateArray(), r => r.GetString() == "market_brief_changed");
    }

    [Fact]
    public async Task LowTimeAvailabilityIsStatedAndKeepsOneTaskPerWeek()
    {
        using var host = new CareerPayFactory();
        var user = await UserAsync(host, hours: 1);

        var roadmap = await RoadmapAsync(user, host);
        var option = roadmap.GetProperty("options")[0];

        Assert.NotEqual(JsonValueKind.Null, roadmap.GetProperty("lowTimeNote").ValueKind);
        Assert.Equal(1, option.GetProperty("thisWeek").GetArrayLength());
        Assert.True(option.GetProperty("milestones")[0].GetProperty("tasks").GetArrayLength() <= 3);
        Assert.True(option.GetProperty("milestones")[0].GetProperty("tasks").GetArrayLength() > 0);
    }

    [Fact]
    public async Task GeneratedTextHasNoPayUpliftPaidCourseOrCredentialWording()
    {
        using var host = new CareerPayFactory();
        var user = await UserAsync(host);
        var roadmap = await RoadmapAsync(user, host);

        var text = roadmap.GetProperty("options").GetRawText().ToLowerInvariant();
        foreach (var word in new[] { "raise", "salary increase", "course fee", "certified" })
        {
            Assert.DoesNotContain(word, text);
        }
    }

    // ---- Accept and dismiss ---------------------------------------------------------

    [Fact]
    public async Task AcceptRequiresIfMatchAndNeverChangesTheGoal()
    {
        using var host = new CareerPayFactory();
        var user = await UserAsync(host);
        var roadmap = await RoadmapAsync(user, host);
        var id = roadmap.GetProperty("id").GetString();
        var goalBefore = (await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/goals"), 200)).GetRawText();
        var body = new { optionKey = "closest_fit" };

        Assert.Equal("CareerPreconditionRequired", (await CareerClient.ReadErrorAsync(
            await user.SendAsync(HttpMethod.Post, $"/api/career/roadmaps/{id}/accept", body), 428)).GetProperty("code").GetString());
        Assert.Equal("CareerVersionConflict", (await CareerClient.ReadErrorAsync(
            await user.SendAsync(HttpMethod.Post, $"/api/career/roadmaps/{id}/accept", body, "\"goal-v1\""), 412)).GetProperty("code").GetString());
        await CareerClient.ReadErrorAsync(await user.SendAsync(HttpMethod.Post, $"/api/career/roadmaps/{id}/accept",
            new { optionKey = "nonsense" }, "\"goal-v2\""), 400);

        var accepted = await CareerClient.ReadDataAsync(
            await user.SendAsync(HttpMethod.Post, $"/api/career/roadmaps/{id}/accept", body, "\"goal-v2\""), 200);

        Assert.Equal("accepted", accepted.GetProperty("status").GetString());
        Assert.Equal("closest_fit", accepted.GetProperty("selectedOption").GetString());
        Assert.True(accepted.GetProperty("goalUnchanged").GetBoolean());
        Assert.Equal(goalBefore, (await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/goals"), 200)).GetRawText());
        var goal = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/goals"), 200);
        Assert.Equal(2, goal.GetProperty("version").GetInt32());
        Assert.Equal("15-1252.00", goal.GetProperty("occupation").GetProperty("code").GetString());

        // A second accept, or a dismiss of an accepted roadmap, is a state conflict.
        Assert.Equal("CareerRoadmapNotProposed", (await CareerClient.ReadErrorAsync(
            await user.SendAsync(HttpMethod.Post, $"/api/career/roadmaps/{id}/accept", body, "\"goal-v2\""), 409)).GetProperty("code").GetString());
        await CareerClient.ReadErrorAsync(await user.SendAsync(HttpMethod.Post, $"/api/career/roadmaps/{id}/dismiss"), 409);
    }

    [Fact]
    public async Task DismissIsIdempotentAndBlocksAcceptance()
    {
        using var host = new CareerPayFactory();
        var user = await UserAsync(host);
        var id = (await RoadmapAsync(user, host)).GetProperty("id").GetString();

        for (var i = 0; i < 2; i++)
        {
            var dismissed = await CareerClient.ReadDataAsync(await user.SendAsync(HttpMethod.Post, $"/api/career/roadmaps/{id}/dismiss"), 200);
            Assert.Equal("dismissed", dismissed.GetProperty("status").GetString());
        }
        Assert.Equal("CareerRoadmapNotProposed", (await CareerClient.ReadErrorAsync(
            await user.SendAsync(HttpMethod.Post, $"/api/career/roadmaps/{id}/accept", new { optionKey = "closest_fit" }, "\"goal-v2\""), 409))
            .GetProperty("code").GetString());
    }

    // ---- Effort edits ---------------------------------------------------------------

    [Fact]
    public async Task EditingEffortWritesANewVersionAndValidatesTheRange()
    {
        using var host = new CareerPayFactory();
        var user = await UserAsync(host);
        var roadmap = await RoadmapAsync(user, host);
        var id = roadmap.GetProperty("id").GetString();
        var task = Tasks(roadmap.GetProperty("options")[0]).First();
        var taskId = task.GetProperty("id").GetString();

        foreach (var bad in new[] { 0.4, 40.5, -1.0 })
        {
            Assert.Equal("effortHours", (await CareerClient.ReadErrorAsync(await user.SendAsync(HttpMethod.Put,
                $"/api/career/roadmaps/{id}/tasks/{taskId}", new { effortHours = bad }), 400)).GetProperty("fieldErrors").EnumerateObject().Single().Name);
        }
        await CareerClient.ReadErrorAsync(await user.SendAsync(HttpMethod.Put, $"/api/career/roadmaps/{id}/tasks/nope", new { effortHours = 2 }), 404);

        var edited = await CareerClient.ReadDataAsync(await user.SendAsync(HttpMethod.Put,
            $"/api/career/roadmaps/{id}/tasks/{taskId}", new { effortHours = 3 }), 200);

        Assert.NotEqual(id, edited.GetProperty("id").GetString());
        Assert.Equal(2, edited.GetProperty("version").GetInt32());
        Assert.Equal(3, Tasks(edited.GetProperty("options")[0]).Single(t => t.GetProperty("id").GetString() == taskId).GetProperty("effortHours").GetDouble());
        // The earlier version is untouched.
        var original = await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/roadmaps/{id}"), 200);
        Assert.Equal(task.GetProperty("effortHours").GetDouble(),
            Tasks(original.GetProperty("options")[0]).Single(t => t.GetProperty("id").GetString() == taskId).GetProperty("effortHours").GetDouble());
        var list = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/roadmaps"), 200);
        Assert.Equal(new[] { 2, 1 }, list.GetProperty("roadmaps").EnumerateArray().Select(r => r.GetProperty("version").GetInt32()));
        // Every task survives the refit.
        Assert.Equal(Tasks(roadmap.GetProperty("options")[0]).Count(), Tasks(edited.GetProperty("options")[0]).Count());
    }

    [Fact]
    public async Task AStoredDependencyCycleIsRejectedOnEdit()
    {
        using var host = new CareerPayFactory();
        var user = await UserAsync(host);
        var roadmap = await RoadmapAsync(user, host);
        var id = Guid.Parse(roadmap.GetProperty("id").GetString()!);
        var tasks = Tasks(roadmap.GetProperty("options")[0]).Select(t => t.GetProperty("id").GetString()!).ToList();

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = await db.CareerRoadmaps.SingleAsync(r => r.Id == id);
            var options = JsonSerializer.Deserialize<List<RoadmapOption>>(row.OptionsJson, RoadmapJson.Options)!;
            var option = options[0];
            var all = option.ThisWeek.Concat(option.Milestones.SelectMany(m => m.Tasks)).ToList();
            var a = all[0];
            var b = all[1];
            var cyclic = all.Select(t => t.Id == a.Id ? t with { DependsOn = new[] { b.Id } } : t.Id == b.Id ? t with { DependsOn = new[] { a.Id } } : t).ToList();
            options[0] = option with
            {
                ThisWeek = cyclic.Where(t => option.ThisWeek.Any(w => w.Id == t.Id)).ToList(),
                Milestones = option.Milestones.Select(m => m with { Tasks = cyclic.Where(t => m.Tasks.Any(x => x.Id == t.Id)).ToList() }).ToList()
            };
            row.OptionsJson = JsonSerializer.Serialize(options, RoadmapJson.Options);
            await db.SaveChangesAsync();
        }

        var error = await CareerClient.ReadErrorAsync(await user.SendAsync(HttpMethod.Put,
            $"/api/career/roadmaps/{id}/tasks/{tasks[^1]}", new { effortHours = 2 }), 409);
        Assert.Equal("CareerRoadmapCycle", error.GetProperty("code").GetString());
        var list = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/roadmaps"), 200);
        Assert.Equal(1, list.GetProperty("roadmaps").GetArrayLength());
    }

    // ---- Staleness, isolation, deletion, auth ------------------------------------------

    [Fact]
    public async Task ProfileAndGoalChangesMakeTheRoadmapStaleWithoutChangingItsContent()
    {
        using var host = new CareerPayFactory();
        var user = await UserAsync(host);
        var roadmap = await RoadmapAsync(user, host);
        var id = roadmap.GetProperty("id").GetString();

        (await user.PutProfileAsync(Profile("Engineer II"), "\"profile-v1\"")).EnsureSuccessStatusCode();
        var profileStale = await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/roadmaps/{id}"), 200);
        Assert.True(profileStale.GetProperty("stale").GetBoolean());
        Assert.Contains(profileStale.GetProperty("staleReasons").EnumerateArray(), r => r.GetString() == "profile_changed");

        var goal = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/goals"), 200);
        (await user.PatchGoalAsync(goal.GetProperty("id").GetString()!, Goal(6, "Boston, MA"), "\"goal-v2\"")).EnsureSuccessStatusCode();
        var goalStale = await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/roadmaps/{id}"), 200);
        Assert.Contains(goalStale.GetProperty("staleReasons").EnumerateArray(), r => r.GetString() == "goal_changed");
        Assert.Equal(roadmap.GetProperty("options").GetRawText(), goalStale.GetProperty("options").GetRawText());
        Assert.Equal(2, goalStale.GetProperty("pinned").GetProperty("goalVersion").GetInt32());
        var list = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/roadmaps"), 200);
        Assert.True(list.GetProperty("roadmaps")[0].GetProperty("stale").GetBoolean());
    }

    [Fact]
    public async Task AnotherOwnersRoadmapIs404ForEveryOperation()
    {
        using var host = new CareerPayFactory();
        var user = await UserAsync(host);
        var roadmap = await RoadmapAsync(user, host);
        var id = roadmap.GetProperty("id").GetString();
        var taskId = Tasks(roadmap.GetProperty("options")[0]).First().GetProperty("id").GetString();
        var intruder = await UserAsync(host);

        Assert.Equal("CareerRoadmapNotFound", (await CareerClient.ReadErrorAsync(await intruder.GetAsync($"/api/career/roadmaps/{id}"), 404)).GetProperty("code").GetString());
        await CareerClient.ReadErrorAsync(await intruder.SendAsync(HttpMethod.Post, $"/api/career/roadmaps/{id}/accept", new { optionKey = "closest_fit" }, "\"goal-v2\""), 404);
        await CareerClient.ReadErrorAsync(await intruder.SendAsync(HttpMethod.Post, $"/api/career/roadmaps/{id}/dismiss"), 404);
        await CareerClient.ReadErrorAsync(await intruder.SendAsync(HttpMethod.Put, $"/api/career/roadmaps/{id}/tasks/{taskId}", new { effortHours = 2 }), 404);
        Assert.Equal(0, (await CareerClient.ReadDataAsync(await new CareerClient(host).GetAsync("/api/career/roadmaps"), 200)).GetProperty("roadmaps").GetArrayLength());
        // Untouched for the owner.
        Assert.Equal("proposed", (await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/roadmaps/{id}"), 200)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task OwnerDeletionRemovesRoadmaps()
    {
        using var host = new CareerPayFactory();
        var user = await UserAsync(host);
        var other = await UserAsync(host);
        await RoadmapAsync(user, host);
        await RoadmapAsync(other, host);

        using (var scope = host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ICareerPrivateDataService>().DeleteAllForOwnerAsync(user.UserId);
        }

        using var check = host.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, await db.CareerRoadmaps.CountAsync(r => r.OwnerId == user.UserId));
        Assert.Equal(1, await db.CareerRoadmaps.CountAsync(r => r.OwnerId == other.UserId));
    }

    [Fact]
    public async Task UnauthenticatedRequestsAre401()
    {
        using var host = new CareerPayFactory();
        var anon = host.CreateAuthenticatedClient();
        anon.DefaultRequestHeaders.Add("X-Test-Unauthenticated", "true");
        var id = Guid.NewGuid();

        Assert.Equal(401, (int)(await anon.GetAsync("/api/career/roadmaps")).StatusCode);
        Assert.Equal(401, (int)(await anon.GetAsync($"/api/career/roadmaps/{id}")).StatusCode);
        Assert.Equal(401, (int)(await anon.PostAsync($"/api/career/roadmaps/{id}/accept", null)).StatusCode);
        Assert.Equal(401, (int)(await anon.PostAsync($"/api/career/roadmaps/{id}/dismiss", null)).StatusCode);
        Assert.Equal(401, (int)(await anon.PutAsync($"/api/career/roadmaps/{id}/tasks/t1", null)).StatusCode);
    }
}
