using System.Text.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models.Career;
using AI.ProfilePhotoMaker.API.Services.Career;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Roadmaps = AI.ProfilePhotoMaker.API.Tests.Integration.Career.CareerRoadmapApiTests;

namespace AI.ProfilePhotoMaker.API.Tests.Integration.Career;

/// <summary>Roadmap tracking and replans end to end (#388, ADR 0017).</summary>
public class CareerRoadmapTrackingApiTests
{
    private static async Task<(CareerClient User, string Id)> AcceptedAsync(CareerPayFactory host, CareerClient? existing = null)
    {
        var user = existing ?? await Roadmaps.UserAsync(host);
        var id = (await Roadmaps.RoadmapAsync(user, host)).GetProperty("id").GetString()!;
        await CareerClient.ReadDataAsync(await user.SendAsync(HttpMethod.Post, $"/api/career/roadmaps/{id}/accept", new { optionKey = "closest_fit" }, "\"goal-v2\""), 200);
        return (user, id);
    }

    private static async Task<List<JsonElement>> ProgressAsync(CareerClient user, string id) =>
        (await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/roadmaps/{id}/progress"), 200)).GetProperty("tasks").EnumerateArray().ToList();

    private static JsonElement Find(IEnumerable<JsonElement> tasks, string titlePrefix) =>
        tasks.Single(t => t.GetProperty("title").GetString()!.StartsWith(titlePrefix, StringComparison.Ordinal));

    private static Task<HttpResponseMessage> Put(CareerClient user, string id, string taskId, object body, string? etag = "\"task-v0\"") =>
        user.SendAsync(HttpMethod.Put, $"/api/career/roadmaps/{id}/progress/{taskId}", body, etag);

    private static async Task<JsonElement> ReplanAsync(CareerClient user, string id) =>
        await CareerClient.ReadDataAsync(await user.SendAsync(HttpMethod.Post, $"/api/career/roadmaps/{id}/replan"), 201);

    private static async Task<HttpResponseMessage> ApplyAsync(CareerClient user, string replanId, params string[] accepted) =>
        await user.SendAsync(HttpMethod.Post, $"/api/career/replans/{replanId}/apply", new { acceptedChangeIds = accepted });

    private static string[] ChangeIds(JsonElement replan, Func<JsonElement, bool>? where = null) =>
        replan.GetProperty("changes").EnumerateArray().Where(where ?? (_ => true)).Select(c => c.GetProperty("id").GetString()!).ToArray();

    // ---- Progress -------------------------------------------------------------------

    [Fact]
    public async Task OnlyAcceptedRoadmapsAreTrackable()
    {
        using var host = new CareerPayFactory();
        var user = await Roadmaps.UserAsync(host);
        var id = (await Roadmaps.RoadmapAsync(user, host)).GetProperty("id").GetString()!;

        Assert.Equal("CareerRoadmapNotAccepted", (await CareerClient.ReadErrorAsync(await user.GetAsync($"/api/career/roadmaps/{id}/progress"), 409)).GetProperty("code").GetString());
        await CareerClient.ReadErrorAsync(await Put(user, id, "t1", new { status = "done" }), 409);
        await CareerClient.ReadErrorAsync(await user.SendAsync(HttpMethod.Post, $"/api/career/roadmaps/{id}/tasks", new { title = "x", effortHours = 1, milestoneDay = 0, dependsOn = Array.Empty<string>() }), 409);
        await CareerClient.ReadErrorAsync(await user.SendAsync(HttpMethod.Post, $"/api/career/roadmaps/{id}/replan"), 409);

        await user.SendAsync(HttpMethod.Post, $"/api/career/roadmaps/{id}/dismiss");
        await CareerClient.ReadErrorAsync(await user.GetAsync($"/api/career/roadmaps/{id}/progress"), 409);
    }

    [Fact]
    public async Task ProgressListsEveryTaskWithHelpAnEtagAndBlockedTasksNameTheirBlockers()
    {
        using var host = new CareerPayFactory();
        var (user, id) = await AcceptedAsync(host);

        var data = await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/roadmaps/{id}/progress"), 200);
        var tasks = data.GetProperty("tasks").EnumerateArray().ToList();

        Assert.Equal(id, data.GetProperty("roadmapId").GetString());
        Assert.Equal(1, data.GetProperty("version").GetInt32());
        Assert.All(tasks, t =>
        {
            Assert.Equal("generated", t.GetProperty("origin").GetString());
            Assert.Equal("\"task-v0\"", t.GetProperty("etag").GetString());
            Assert.False(string.IsNullOrWhiteSpace(t.GetProperty("help").GetString()));
            Assert.Equal("not_started", t.GetProperty("status").GetString());
            Assert.False(t.GetProperty("linkedMaterialMissing").GetBoolean());
        });
        var examples = Find(tasks, "Write one recent example");
        Assert.Equal("blocked", examples.GetProperty("effectiveStatus").GetString());
        Assert.StartsWith("Read the duty list for", Assert.Single(examples.GetProperty("blockedBy").EnumerateArray()).GetString());
        var read = Find(tasks, "Read the duty list");
        Assert.Equal("not_started", read.GetProperty("effectiveStatus").GetString());
        Assert.Empty(read.GetProperty("blockedBy").EnumerateArray());

        // Finishing the blocker releases the dependent task.
        await CareerClient.ReadDataAsync(await Put(user, id, read.GetProperty("taskId").GetString()!, new { status = "done" }), 200);
        var after = Find(await ProgressAsync(user, id), "Write one recent example");
        Assert.Equal("not_started", after.GetProperty("effectiveStatus").GetString());
        Assert.Empty(after.GetProperty("blockedBy").EnumerateArray());
    }

    [Fact]
    public async Task SimultaneousEditsWithTheSameEtagLetOnlyOneWin()
    {
        using var host = new CareerPayFactory();
        var (user, id) = await AcceptedAsync(host);
        var taskId = Find(await ProgressAsync(user, id), "Read the duty list").GetProperty("taskId").GetString()!;

        await CareerClient.ReadDataAsync(await Put(user, id, taskId, new { status = "in_progress" }), 200);
        var responses = await Task.WhenAll(
            Put(user, id, taskId, new { status = "in_progress", outputNote = "writer one" }, "\"task-v1\""),
            Put(user, id, taskId, new { status = "done", outputNote = "writer two" }, "\"task-v1\""));

        var codes = responses.Select(r => (int)r.StatusCode).OrderBy(c => c).ToArray();
        Assert.Equal(new[] { 200, 412 }, codes);
        var loser = responses.Single(r => (int)r.StatusCode == 412);
        Assert.Equal("CareerVersionConflict", (await CareerClient.ReadErrorAsync(loser, 412)).GetProperty("code").GetString());
        var winner = await CareerClient.ReadDataAsync(responses.Single(r => (int)r.StatusCode == 200), 200);
        Assert.Equal("\"task-v2\"", winner.GetProperty("etag").GetString());
        // The stored value is the winner's, never a blend.
        Assert.Equal(winner.GetProperty("outputNote").GetString(), Find(await ProgressAsync(user, id), "Read the duty list").GetProperty("outputNote").GetString());
    }

    [Fact]
    public async Task EditsNeedIfMatchAndAStaleOrOddTagIs412()
    {
        using var host = new CareerPayFactory();
        var (user, id) = await AcceptedAsync(host);
        var taskId = Find(await ProgressAsync(user, id), "Read the duty list").GetProperty("taskId").GetString()!;

        Assert.Equal("CareerPreconditionRequired", (await CareerClient.ReadErrorAsync(await Put(user, id, taskId, new { status = "done" }, null), 428)).GetProperty("code").GetString());
        var ok = await Put(user, id, taskId, new { status = "in_progress" });
        Assert.Equal("\"task-v1\"", ok.Headers.ETag?.Tag);
        await CareerClient.ReadDataAsync(ok, 200);
        await CareerClient.ReadErrorAsync(await Put(user, id, taskId, new { status = "done" }, "\"task-v0\""), 412);
        await CareerClient.ReadErrorAsync(await Put(user, id, taskId, new { status = "done" }, "*"), 412);
        await CareerClient.ReadErrorAsync(await Put(user, id, taskId, new { status = "done" }, "\"goal-v1\""), 412);
        await CareerClient.ReadErrorAsync(await Put(user, id, "nope", new { status = "done" }, "\"task-v0\""), 404);
        Assert.Equal("done", (await CareerClient.ReadDataAsync(await Put(user, id, taskId, new { status = "done" }, "\"task-v1\""), 200)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task InvalidValuesAre400WithFieldErrors()
    {
        using var host = new CareerPayFactory();
        var (user, id) = await AcceptedAsync(host);
        var taskId = Find(await ProgressAsync(user, id), "Read the duty list").GetProperty("taskId").GetString()!;

        foreach (var (body, field) in new (object, string)[]
        {
            (new { status = "finished" }, "status"), (new { effortHours = 0.1 }, "effortHours"), (new { effortHours = 41 }, "effortHours"),
            (new { outputNote = new string('x', 2001) }, "outputNote"), (new { linkedMaterialId = "not-a-guid" }, "linkedMaterialId")
        })
        {
            Assert.Equal(field, (await CareerClient.ReadErrorAsync(await Put(user, id, taskId, body), 400)).GetProperty("fieldErrors").EnumerateObject().Single().Name);
        }
        // A rejected write changes nothing.
        Assert.Equal("\"task-v0\"", Find(await ProgressAsync(user, id), "Read the duty list").GetProperty("etag").GetString());
    }

    private static async Task<Guid> AddMaterialAsync(CareerPayFactory host, string ownerId)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var doc = new ResumeDocument
        {
            Id = Guid.NewGuid(), OwnerId = ownerId, StorageKey = $"career-private/resumes/{Guid.NewGuid()}", FileName = "r.pdf", Format = "pdf",
            Sha256 = new string('a', 64), ConsentVersion = "v1", State = ResumeState.Ready, ExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        db.CareerResumeDocuments.Add(doc);
        await db.SaveChangesAsync();
        return doc.Id;
    }

    [Fact]
    public async Task ALinkedMaterialMustBelongToTheCallerAndReadsMissingOnceDeleted()
    {
        using var host = new CareerPayFactory();
        var (user, id) = await AcceptedAsync(host);
        var other = await Roadmaps.UserAsync(host);
        var taskId = Find(await ProgressAsync(user, id), "Read the duty list").GetProperty("taskId").GetString()!;
        var theirs = await AddMaterialAsync(host, other.UserId);
        var mine = await AddMaterialAsync(host, user.UserId);

        Assert.Equal("CareerMaterialNotFound", (await CareerClient.ReadErrorAsync(await Put(user, id, taskId, new { linkedMaterialId = theirs }), 404)).GetProperty("code").GetString());
        await CareerClient.ReadErrorAsync(await Put(user, id, taskId, new { linkedMaterialId = Guid.NewGuid() }), 404);
        Assert.Equal("\"task-v0\"", Find(await ProgressAsync(user, id), "Read the duty list").GetProperty("etag").GetString());

        var linked = await CareerClient.ReadDataAsync(await Put(user, id, taskId, new { linkedMaterialId = mine }), 200);
        Assert.Equal(mine.ToString(), linked.GetProperty("linkedMaterialId").GetString());
        Assert.False(linked.GetProperty("linkedMaterialMissing").GetBoolean());

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.CareerResumeDocuments.Remove(await db.CareerResumeDocuments.SingleAsync(d => d.Id == mine));
            await db.SaveChangesAsync();
        }
        var after = Find(await ProgressAsync(user, id), "Read the duty list");
        Assert.True(after.GetProperty("linkedMaterialMissing").GetBoolean());
        Assert.Equal(mine.ToString(), after.GetProperty("linkedMaterialId").GetString());

        var cleared = await CareerClient.ReadDataAsync(await Put(user, id, taskId, new { linkedMaterialId = "" }, "\"task-v1\""), 200);
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("linkedMaterialId").ValueKind);
        Assert.False(cleared.GetProperty("linkedMaterialMissing").GetBoolean());
    }

    [Fact]
    public async Task CompletingATaskLeavesPayAnalysesAndTheGoalUntouched()
    {
        using var host = new CareerPayFactory();
        var user = await Roadmaps.UserAsync(host);
        await Roadmaps.RunAsync(user, host, "pay_analysis");
        var (_, id) = await AcceptedAsync(host, user);
        var payBefore = (await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/pay-analyses"), 200)).GetRawText();
        var goalBefore = (await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/goals"), 200)).GetRawText();

        foreach (var task in await ProgressAsync(user, id))
        {
            await CareerClient.ReadDataAsync(await Put(user, id, task.GetProperty("taskId").GetString()!, new { status = "done", effortHours = 3 }), 200);
        }

        Assert.Equal(payBefore, (await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/pay-analyses"), 200)).GetRawText());
        var goalAfter = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/goals"), 200);
        Assert.Equal(goalBefore, goalAfter.GetRawText());
        Assert.Equal(2, goalAfter.GetProperty("version").GetInt32());
        Assert.All(await ProgressAsync(user, id), t => Assert.Equal("done", t.GetProperty("effectiveStatus").GetString()));
    }

    // ---- Human tasks ------------------------------------------------------------------

    [Fact]
    public async Task HumanTasksAreAddedWithDependenciesAndBlockUntilTheirDependenciesAreDone()
    {
        using var host = new CareerPayFactory();
        var (user, id) = await AcceptedAsync(host);
        var read = Find(await ProgressAsync(user, id), "Read the duty list");
        var readId = read.GetProperty("taskId").GetString()!;
        var path = $"/api/career/roadmaps/{id}/tasks";

        foreach (var bad in new object[]
        {
            new { title = "", effortHours = 1, milestoneDay = 0, dependsOn = Array.Empty<string>() },
            new { title = "x", effortHours = 0, milestoneDay = 0, dependsOn = Array.Empty<string>() },
            new { title = "x", effortHours = 1, milestoneDay = 45, dependsOn = Array.Empty<string>() },
            new { title = "x", effortHours = 1, milestoneDay = 0, dependsOn = new[] { "ghost" } }
        })
        {
            await CareerClient.ReadErrorAsync(await user.SendAsync(HttpMethod.Post, path, bad), 400);
        }

        var added = await CareerClient.ReadDataAsync(await user.SendAsync(HttpMethod.Post, path,
            new { title = "Ask my manager for a stretch project", effortHours = 2, milestoneDay = 30, dependsOn = new[] { readId } }), 201);

        Assert.Equal("human", added.GetProperty("origin").GetString());
        Assert.Equal("h1", added.GetProperty("taskId").GetString());
        Assert.Equal(30, added.GetProperty("milestoneDay").GetInt32());
        Assert.Equal("blocked", added.GetProperty("effectiveStatus").GetString());
        Assert.Equal(new[] { read.GetProperty("title").GetString() }, added.GetProperty("blockedBy").EnumerateArray().Select(b => b.GetString()));
        Assert.Equal("\"task-v1\"", added.GetProperty("etag").GetString());
        Assert.False(string.IsNullOrWhiteSpace(added.GetProperty("help").GetString()));

        await Put(user, id, readId, new { status = "done" });
        var released = (await ProgressAsync(user, id)).Single(t => t.GetProperty("taskId").GetString() == "h1");
        Assert.Equal("not_started", released.GetProperty("effectiveStatus").GetString());
        // A human task can depend on another human task, and both read in a stable order.
        var second = await CareerClient.ReadDataAsync(await user.SendAsync(HttpMethod.Post, path,
            new { title = "Book time with a mentor", effortHours = 1, milestoneDay = 60, dependsOn = new[] { "h1" } }), 201);
        Assert.Equal("h2", second.GetProperty("taskId").GetString());
        Assert.Equal(new[] { "h1", "h2" }, (await ProgressAsync(user, id)).Select(t => t.GetProperty("taskId").GetString()!).Where(t => t.StartsWith('h')));
    }

    [Fact]
    public async Task ADependencyCycleInTheStoredGraphIsRejectedWhenAddingATask()
    {
        using var host = new CareerPayFactory();
        var (user, id) = await AcceptedAsync(host);
        var roadmapId = Guid.Parse(id);
        var ids = Roadmaps.Tasks((await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/roadmaps/{id}"), 200)).GetProperty("options")[0])
            .Select(t => t.GetProperty("id").GetString()!).ToList();

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = await db.CareerRoadmaps.SingleAsync(r => r.Id == roadmapId);
            var options = JsonSerializer.Deserialize<List<RoadmapOption>>(row.OptionsJson, RoadmapJson.Options)!;
            var all = options[0].ThisWeek.Concat(options[0].Milestones.SelectMany(m => m.Tasks)).ToList();
            RoadmapTask Cyclic(RoadmapTask t) => t.Id == all[0].Id ? t with { DependsOn = new[] { all[1].Id } } : t.Id == all[1].Id ? t with { DependsOn = new[] { all[0].Id } } : t;
            options[0] = options[0] with
            {
                ThisWeek = options[0].ThisWeek.Select(Cyclic).ToList(),
                Milestones = options[0].Milestones.Select(m => m with { Tasks = m.Tasks.Select(Cyclic).ToList() }).ToList()
            };
            row.OptionsJson = JsonSerializer.Serialize(options, RoadmapJson.Options);
            await db.SaveChangesAsync();
        }

        var error = await CareerClient.ReadErrorAsync(await user.SendAsync(HttpMethod.Post, $"/api/career/roadmaps/{id}/tasks",
            new { title = "x", effortHours = 1, milestoneDay = 0, dependsOn = new[] { ids[^1] } }), 409);
        Assert.Equal("CareerRoadmapCycle", error.GetProperty("code").GetString());
        Assert.DoesNotContain(await ProgressAsync(user, id), t => t.GetProperty("origin").GetString() == "human");
    }

    // ---- Replans ------------------------------------------------------------------------

    /// <summary>Running a market brief and a pay analysis makes the "open a brief / open a pay analysis" tasks obsolete.</summary>
    private static async Task MakeBriefAndPayTasksObsoleteAsync(CareerClient user, CareerPayFactory host)
    {
        await Roadmaps.RunAsync(user, host, "market_brief");
        await Roadmaps.RunAsync(user, host, "pay_analysis");
    }

    [Fact]
    public async Task AReplanWithUnchangedEvidenceHasNoChangesAndStillApplies()
    {
        using var host = new CareerPayFactory();
        var (user, id) = await AcceptedAsync(host);

        var replan = await ReplanAsync(user, id);

        Assert.Equal(1, replan.GetProperty("baseVersion").GetInt32());
        Assert.Empty(replan.GetProperty("changes").EnumerateArray());
        var applied = await CareerClient.ReadDataAsync(await ApplyAsync(user, replan.GetProperty("id").GetString()!), 200);
        Assert.Equal(2, applied.GetProperty("version").GetInt32());
        Assert.Equal("accepted", applied.GetProperty("status").GetString());
    }

    [Fact]
    public async Task AReplanProposesFieldLevelChangesWithRationaleAndPreservesDoneOutputAndHumanTasks()
    {
        using var host = new CareerPayFactory();
        var (user, id) = await AcceptedAsync(host);
        var tasks = await ProgressAsync(user, id);
        var brief = Find(tasks, RoadmapTemplates.OpenBrief).GetProperty("taskId").GetString()!;
        var pay = Find(tasks, RoadmapTemplates.OpenPay).GetProperty("taskId").GetString()!;
        await Put(user, id, brief, new { status = "done" });
        await Put(user, id, pay, new { outputNote = "Compared the figures" });
        await user.SendAsync(HttpMethod.Post, $"/api/career/roadmaps/{id}/tasks", new { title = "Mine", effortHours = 1, milestoneDay = 0, dependsOn = Array.Empty<string>() });
        await MakeBriefAndPayTasksObsoleteAsync(user, host);

        var replan = await ReplanAsync(user, id);

        Assert.Equal(new[] { "done", "has_output", "human" }, replan.GetProperty("preserved").EnumerateArray().Select(p => p.GetProperty("reason").GetString()!).OrderBy(r => r));
        Assert.Equal(new[] { brief, pay, "h1" }.OrderBy(x => x), replan.GetProperty("preserved").EnumerateArray().Select(p => p.GetProperty("taskId").GetString()!).OrderBy(x => x));
        var changes = replan.GetProperty("changes").EnumerateArray().ToList();
        Assert.DoesNotContain(changes, c => new[] { brief, pay, "h1" }.Contains(c.GetProperty("taskId").GetString()));
        Assert.All(changes, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.GetProperty("rationale").GetString()));
            Assert.Contains(c.GetProperty("kind").GetString(), new[] { "added", "removed", "changed" });
            Assert.Contains(c.GetProperty("fields").EnumerateArray(), f => f.GetProperty("field").GetString() is "title" or "effort" or "milestoneDay" or "dependencies");
        });
        Assert.Equal(replan.GetRawText(), (await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/replans/{replan.GetProperty("id").GetString()}"), 200)).GetRawText());
    }

    [Fact]
    public async Task ApplyingAllChangesKeepsPreservedTasksAndCarriesProgressFaithfully()
    {
        using var host = new CareerPayFactory();
        var (user, id) = await AcceptedAsync(host);
        var tasks = await ProgressAsync(user, id);
        var brief = Find(tasks, RoadmapTemplates.OpenBrief).GetProperty("taskId").GetString()!;
        var pay = Find(tasks, RoadmapTemplates.OpenPay).GetProperty("taskId").GetString()!;
        var read = Find(tasks, "Read the duty list").GetProperty("taskId").GetString()!;
        await Put(user, id, brief, new { status = "done", effortHours = 4 });
        await Put(user, id, pay, new { outputNote = "Compared the figures" });
        await Put(user, id, read, new { status = "in_progress" });
        await user.SendAsync(HttpMethod.Post, $"/api/career/roadmaps/{id}/tasks", new { title = "Mine", effortHours = 1, milestoneDay = 0, dependsOn = new[] { read } });
        await MakeBriefAndPayTasksObsoleteAsync(user, host);
        var replan = await ReplanAsync(user, id);

        var applied = await CareerClient.ReadDataAsync(await ApplyAsync(user, replan.GetProperty("id").GetString()!, ChangeIds(replan)), 200);

        var newId = applied.GetProperty("id").GetString()!;
        Assert.NotEqual(id, newId);
        var after = await ProgressAsync(user, newId);
        Assert.Equal("done", after.Single(t => t.GetProperty("taskId").GetString() == brief).GetProperty("status").GetString());
        Assert.Equal(4, after.Single(t => t.GetProperty("taskId").GetString() == brief).GetProperty("effortHours").GetDouble());
        Assert.Equal("Compared the figures", after.Single(t => t.GetProperty("taskId").GetString() == pay).GetProperty("outputNote").GetString());
        Assert.Equal("in_progress", after.Single(t => t.GetProperty("taskId").GetString() == read).GetProperty("status").GetString());
        Assert.Equal("\"task-v1\"", after.Single(t => t.GetProperty("taskId").GetString() == read).GetProperty("etag").GetString());
        var human = after.Single(t => t.GetProperty("taskId").GetString() == "h1");
        Assert.Equal(new[] { read }, human.GetProperty("dependsOn").EnumerateArray().Select(d => d.GetString()));
        // The earlier version is untouched, and the replan is now closed.
        Assert.Equal(2, applied.GetProperty("version").GetInt32());
        Assert.Equal(1, (await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/roadmaps/{id}"), 200)).GetProperty("version").GetInt32());
        Assert.Equal("CareerReplanClosed", (await CareerClient.ReadErrorAsync(await ApplyAsync(user, replan.GetProperty("id").GetString()!), 409)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task ApplyingASubsetAppliesOnlyThoseChangesAndKeepsProgress()
    {
        using var host = new CareerPayFactory();
        var (user, id) = await AcceptedAsync(host);
        var tasks = await ProgressAsync(user, id);
        var brief = Find(tasks, RoadmapTemplates.OpenBrief).GetProperty("taskId").GetString()!;
        var pay = Find(tasks, RoadmapTemplates.OpenPay).GetProperty("taskId").GetString()!;
        var read = Find(tasks, "Read the duty list").GetProperty("taskId").GetString()!;
        await Put(user, id, read, new { status = "in_progress", effortHours = 2.5 });
        await MakeBriefAndPayTasksObsoleteAsync(user, host);
        var replan = await ReplanAsync(user, id);
        var removeBrief = replan.GetProperty("changes").EnumerateArray().Single(c => c.GetProperty("kind").GetString() == "removed" && c.GetProperty("taskId").GetString() == brief);
        Assert.Contains(replan.GetProperty("changes").EnumerateArray(), c => c.GetProperty("kind").GetString() == "removed" && c.GetProperty("taskId").GetString() == pay);

        var applied = await CareerClient.ReadDataAsync(await ApplyAsync(user, replan.GetProperty("id").GetString()!, removeBrief.GetProperty("id").GetString()!), 200);

        var after = await ProgressAsync(user, applied.GetProperty("id").GetString()!);
        Assert.DoesNotContain(after, t => t.GetProperty("taskId").GetString() == brief);
        Assert.Contains(after, t => t.GetProperty("taskId").GetString() == pay);
        var carried = after.Single(t => t.GetProperty("taskId").GetString() == read);
        Assert.Equal("in_progress", carried.GetProperty("status").GetString());
        Assert.Equal(2.5, carried.GetProperty("effortHours").GetDouble());
        Assert.Equal(tasks.Count - 1, after.Count);
        Assert.Equal("accepted", applied.GetProperty("status").GetString());
    }

    [Fact]
    public async Task RejectingAllLeavesTheRoadmapVersionUnchangedAndClosesTheReplan()
    {
        using var host = new CareerPayFactory();
        var (user, id) = await AcceptedAsync(host);
        await MakeBriefAndPayTasksObsoleteAsync(user, host);
        var replan = await ReplanAsync(user, id);
        var replanId = replan.GetProperty("id").GetString()!;
        Assert.NotEmpty(replan.GetProperty("changes").EnumerateArray());
        var listBefore = (await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/roadmaps"), 200)).GetRawText();
        var progressBefore = (await ProgressAsync(user, id)).Select(t => t.GetRawText()).ToList();

        var rejected = await CareerClient.ReadDataAsync(await user.SendAsync(HttpMethod.Post, $"/api/career/replans/{replanId}/reject"), 200);

        Assert.Equal("rejected", rejected.GetProperty("status").GetString());
        Assert.Equal(listBefore, (await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/roadmaps"), 200)).GetRawText());
        Assert.Equal(progressBefore, (await ProgressAsync(user, id)).Select(t => t.GetRawText()).ToList());
        Assert.Equal("CareerReplanClosed", (await CareerClient.ReadErrorAsync(await ApplyAsync(user, replanId), 409)).GetProperty("code").GetString());
        Assert.Equal("CareerReplanClosed", (await CareerClient.ReadErrorAsync(await user.SendAsync(HttpMethod.Post, $"/api/career/replans/{replanId}/reject"), 409)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task ApplyingWithNoAcceptedChangesStillWritesACurrentVersionAndLeavesTheTasksAsTheyWere()
    {
        using var host = new CareerPayFactory();
        var (user, id) = await AcceptedAsync(host);
        var before = (await ProgressAsync(user, id)).Select(t => t.GetProperty("taskId").GetString()).ToList();
        await MakeBriefAndPayTasksObsoleteAsync(user, host);
        var replan = await ReplanAsync(user, id);

        var applied = await CareerClient.ReadDataAsync(await ApplyAsync(user, replan.GetProperty("id").GetString()!), 200);

        Assert.Equal(before, (await ProgressAsync(user, applied.GetProperty("id").GetString()!)).Select(t => t.GetProperty("taskId").GetString()).ToList());
        Assert.False(applied.GetProperty("stale").GetBoolean());
    }

    [Fact]
    public async Task AStaleReplanIs409AndAnUnknownChangeIdIs400()
    {
        using var host = new CareerPayFactory();
        var (user, id) = await AcceptedAsync(host);
        await MakeBriefAndPayTasksObsoleteAsync(user, host);
        var first = await ReplanAsync(user, id);
        var second = await ReplanAsync(user, id);

        var unknown = await CareerClient.ReadErrorAsync(await ApplyAsync(user, first.GetProperty("id").GetString()!, "c999"), 400);
        Assert.Equal("acceptedChangeIds", unknown.GetProperty("fieldErrors").EnumerateObject().Single().Name);
        Assert.Equal("open", (await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/replans/{first.GetProperty("id").GetString()}"), 200)).GetProperty("status").GetString());

        await CareerClient.ReadDataAsync(await ApplyAsync(user, first.GetProperty("id").GetString()!, ChangeIds(first)), 200);
        Assert.Equal("CareerReplanStale", (await CareerClient.ReadErrorAsync(await ApplyAsync(user, second.GetProperty("id").GetString()!), 409)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task AnEffortEditWritesANewVersionThatCarriesProgressAndStalesOpenReplans()
    {
        using var host = new CareerPayFactory();
        var (user, id) = await AcceptedAsync(host);
        var read = Find(await ProgressAsync(user, id), "Read the duty list").GetProperty("taskId").GetString()!;
        await Put(user, id, read, new { status = "done" });
        var replan = await ReplanAsync(user, id);

        var edited = await CareerClient.ReadDataAsync(await user.SendAsync(HttpMethod.Put, $"/api/career/roadmaps/{id}/tasks/{read}", new { effortHours = 3 }), 200);

        Assert.Equal("done", Find(await ProgressAsync(user, edited.GetProperty("id").GetString()!), "Read the duty list").GetProperty("status").GetString());
        await CareerClient.ReadErrorAsync(await ApplyAsync(user, replan.GetProperty("id").GetString()!), 409);
    }

    // ---- Isolation, auth, deletion ----------------------------------------------------------

    [Fact]
    public async Task AnotherOwnersRoadmapAndReplanAre404ForEveryOperation()
    {
        using var host = new CareerPayFactory();
        var (user, id) = await AcceptedAsync(host);
        var read = Find(await ProgressAsync(user, id), "Read the duty list").GetProperty("taskId").GetString()!;
        var replanId = (await ReplanAsync(user, id)).GetProperty("id").GetString()!;
        var intruder = await Roadmaps.UserAsync(host);
        var material = await AddMaterialAsync(host, intruder.UserId);

        await CareerClient.ReadErrorAsync(await intruder.GetAsync($"/api/career/roadmaps/{id}/progress"), 404);
        await CareerClient.ReadErrorAsync(await Put(intruder, id, read, new { status = "done", linkedMaterialId = material }), 404);
        await CareerClient.ReadErrorAsync(await intruder.SendAsync(HttpMethod.Post, $"/api/career/roadmaps/{id}/tasks", new { title = "x", effortHours = 1, milestoneDay = 0, dependsOn = Array.Empty<string>() }), 404);
        await CareerClient.ReadErrorAsync(await intruder.SendAsync(HttpMethod.Post, $"/api/career/roadmaps/{id}/replan"), 404);
        Assert.Equal("CareerReplanNotFound", (await CareerClient.ReadErrorAsync(await intruder.GetAsync($"/api/career/replans/{replanId}"), 404)).GetProperty("code").GetString());
        await CareerClient.ReadErrorAsync(await ApplyAsync(intruder, replanId), 404);
        await CareerClient.ReadErrorAsync(await intruder.SendAsync(HttpMethod.Post, $"/api/career/replans/{replanId}/reject"), 404);

        // Untouched for the owner.
        Assert.Equal("\"task-v0\"", Find(await ProgressAsync(user, id), "Read the duty list").GetProperty("etag").GetString());
        Assert.Equal("open", (await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/replans/{replanId}"), 200)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task UnauthenticatedRequestsAre401()
    {
        using var host = new CareerPayFactory();
        var anon = host.CreateAuthenticatedClient();
        anon.DefaultRequestHeaders.Add("X-Test-Unauthenticated", "true");
        var id = Guid.NewGuid();

        Assert.Equal(401, (int)(await anon.GetAsync($"/api/career/roadmaps/{id}/progress")).StatusCode);
        Assert.Equal(401, (int)(await anon.PutAsync($"/api/career/roadmaps/{id}/progress/t1", null)).StatusCode);
        Assert.Equal(401, (int)(await anon.PostAsync($"/api/career/roadmaps/{id}/tasks", null)).StatusCode);
        Assert.Equal(401, (int)(await anon.PostAsync($"/api/career/roadmaps/{id}/replan", null)).StatusCode);
        Assert.Equal(401, (int)(await anon.GetAsync($"/api/career/replans/{id}")).StatusCode);
        Assert.Equal(401, (int)(await anon.PostAsync($"/api/career/replans/{id}/apply", null)).StatusCode);
        Assert.Equal(401, (int)(await anon.PostAsync($"/api/career/replans/{id}/reject", null)).StatusCode);
    }

    [Fact]
    public async Task OwnerDeletionRemovesProgressAndReplansOnlyForThatOwner()
    {
        using var host = new CareerPayFactory();
        var (user, id) = await AcceptedAsync(host);
        var (other, otherId) = await AcceptedAsync(host);
        foreach (var (u, rid) in new[] { (user, id), (other, otherId) })
        {
            var read = Find(await ProgressAsync(u, rid), "Read the duty list").GetProperty("taskId").GetString()!;
            await Put(u, rid, read, new { status = "done" });
            await ReplanAsync(u, rid);
        }

        using (var scope = host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ICareerPrivateDataService>().DeleteAllForOwnerAsync(user.UserId);
        }

        using var check = host.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, await db.CareerRoadmapTaskProgress.CountAsync(p => p.OwnerId == user.UserId));
        Assert.Equal(0, await db.CareerRoadmapReplans.CountAsync(r => r.OwnerId == user.UserId));
        Assert.Equal(1, await db.CareerRoadmapTaskProgress.CountAsync(p => p.OwnerId == other.UserId));
        Assert.Equal(1, await db.CareerRoadmapReplans.CountAsync(r => r.OwnerId == other.UserId));
        Assert.Contains(ApplicationCoveredTypes(), t => t == typeof(CareerRoadmapTaskProgress));
        Assert.Contains(ApplicationCoveredTypes(), t => t == typeof(CareerRoadmapReplan));
    }

    private static IReadOnlySet<Type> ApplicationCoveredTypes() => CareerPrivateDataService.CoveredEntityTypes;
}
