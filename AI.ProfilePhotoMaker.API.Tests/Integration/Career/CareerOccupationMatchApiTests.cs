using System.Net.Http.Json;
using System.Text.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Services.Career;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration.Career;

/// <summary>Host whose O*NET snapshot is unavailable, as after a tampered deploy.</summary>
public sealed class CareerReferenceUnavailableFactory : CareerAgentFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
            services.AddSingleton<IOccupationReference>(new EmbeddedOccupationReference(() => new MemoryStream(new byte[] { 1, 2, 3 }))));
    }
}

/// <summary>Occupation matches end to end (#381): runs, evidence, clarification, confirmation into the goal.</summary>
public class CareerOccupationMatchApiTests : IClassFixture<CareerAgentFactory>
{
    private readonly CareerAgentFactory _factory;

    public CareerOccupationMatchApiTests(CareerAgentFactory factory)
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

    private static readonly string[] NurseDuties =
    {
        "Assessed patients' health problems and needs and developed and implemented nursing care plans",
        "Administered medications to patients and monitored patients for reactions or side effects",
        "Recorded patients' medical information and vital signs",
        "Consulted and coordinated with healthcare team members to assess, plan, implement and evaluate patient care plans"
    };

    private static object Profile(string title, IEnumerable<string> duties, params string[] skills) => new
    {
        currentTitle = title,
        industry = "Technology",
        yearsExperience = 5,
        location = "Austin, TX",
        summary = "Reliable and curious.",
        skills,
        highlights = duties.ToArray(),
        workArrangement = "hybrid",
        confirmed = true
    };

    private async Task<CareerClient> UserAsync(IEnumerable<string>? duties = null, bool withGoal = true, CareerAgentFactory? factory = null)
    {
        var user = new CareerClient(factory ?? _factory);
        (await user.PutProfileAsync(Profile("Software Engineer", duties ?? SoftwareDuties, "Programming", "Systems Analysis"))).EnsureSuccessStatusCode();
        if (withGoal)
        {
            await user.CreateGoalAsync();
        }
        return user;
    }

    private async Task<JsonElement> StartMatchAsync(CareerClient user, CareerAgentFactory? factory = null, int expected = 202)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/career/runs")
        {
            Content = JsonContent.Create(new { task = "occupation_match" }, options: CareerClient.Json)
        };
        request.Headers.Add("X-Test-UserId", user.UserId);
        request.Headers.Add("Idempotency-Key", $"key-{Guid.NewGuid():N}");
        var response = await (factory ?? _factory).CreateAuthenticatedClient().SendAsync(request);
        return expected == 202
            ? await CareerClient.ReadDataAsync(response, 202)
            : await CareerClient.ReadErrorAsync(response, expected);
    }

    private async Task<JsonElement> GetRunAsync(CareerClient user, string id) =>
        await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/runs/{id}"), 200);

    /// <summary>Starts a run, drives the worker and returns the finished (or waiting) run.</summary>
    private async Task<JsonElement> MatchAsync(CareerClient user)
    {
        var run = await StartMatchAsync(user);
        await _factory.DrainWorkerAsync();
        return await GetRunAsync(user, run.GetProperty("id").GetString()!);
    }

    private async Task<JsonElement> MatchDoneAsync(CareerClient user)
    {
        var run = await MatchAsync(user);
        run.GetProperty("status").GetString().Should().Be("completed");
        return await CareerClient.ReadDataAsync(
            await user.GetAsync($"/api/career/occupation-matches/{run.GetProperty("occupationMatchId").GetString()}"), 200);
    }

    private static Task<HttpResponseMessage> ConfirmAsync(CareerClient user, string matchId, string? code, string? ifMatch) =>
        user.SendAsync(HttpMethod.Post, $"/api/career/occupation-matches/{matchId}/confirm", new { occupationCode = code }, ifMatch);

    private static string[] Codes(JsonElement match) =>
        match.GetProperty("candidates").EnumerateArray().Select(c => c.GetProperty("code").GetString()!).ToArray();

    // ---- Run -------------------------------------------------------------------

    [Fact]
    public async Task RunCompletesWithRealStepsAnAllowanceThatIsReleasedAndAMatchWithEvidence()
    {
        var user = await UserAsync();

        var run = await MatchAsync(user);

        run.GetProperty("status").GetString().Should().Be("completed");
        run.GetProperty("task").GetString().Should().Be("occupation_match");
        run.GetProperty("steps").EnumerateArray().Select(s => s.GetProperty("name").GetString()).Should()
            .Equal("read_profile", "read_goal", "match_occupations", "save_match");
        run.GetProperty("steps")[2].GetProperty("label").GetString().Should().Be("Compared your duties with occupation tasks");
        run.GetProperty("steps")[3].GetProperty("label").GetString().Should().Be("Saved the matches for your review");
        run.GetProperty("proposalId").ValueKind.Should().Be(JsonValueKind.Null);
        run.GetProperty("allowance").GetProperty("used").GetInt32().Should().Be(0);
        run.GetProperty("allowance").GetProperty("reserved").GetInt32().Should().Be(0);

        var match = await CareerClient.ReadDataAsync(
            await user.GetAsync($"/api/career/occupation-matches/{run.GetProperty("occupationMatchId").GetString()}"), 200);
        match.GetProperty("status").GetString().Should().Be("proposed");
        match.GetProperty("runId").GetString().Should().Be(run.GetProperty("id").GetString());
        match.GetProperty("matcherVersion").GetString().Should().Be("duty-overlap-2");
        match.GetProperty("pinnedProfileVersion").GetInt32().Should().Be(1);
        match.GetProperty("pinnedGoalVersion").GetInt32().Should().Be(1);
        match.GetProperty("profileChanged").GetBoolean().Should().BeFalse();
        match.GetProperty("reference").GetProperty("release").GetString().Should().Be("30.0");
        match.GetProperty("reference").GetProperty("license").GetString().Should().Be("CC BY 4.0");
        match.GetProperty("reference").GetProperty("attribution").GetString().Should().Contain("O*NET");
        match.GetProperty("clarification").ValueKind.Should().Be(JsonValueKind.Null);
        Codes(match).Should().Contain("15-1252.00");
        match.GetProperty("candidates").GetArrayLength().Should().BeLessThanOrEqualTo(5);

        var developer = match.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("code").GetString() == "15-1252.00");
        var evidence = developer.GetProperty("evidence").EnumerateArray().ToList();
        evidence.Should().Contain(e => e.GetProperty("kind").GetString() == "duty"
            && e.GetProperty("profileField").GetString() == "highlights"
            && e.GetProperty("referenceKind").GetString() == "task");
        evidence.Should().Contain(e => e.GetProperty("kind").GetString() == "skill" && e.GetProperty("profileText").GetString() == "Programming");
        developer.GetProperty("missingEvidence").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task OccupationRunsNeedNoModelButProfileSummaryStillDoes()
    {
        var factory = new CareerAgentFactory { RegisterModel = false };
        var user = await UserAsync(factory: factory);

        var run = await StartMatchAsync(user, factory);
        await factory.DrainWorkerAsync();

        var done = await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/runs/{run.GetProperty("id").GetString()}"), 200);
        done.GetProperty("status").GetString().Should().Be("completed");

        var summary = new HttpRequestMessage(HttpMethod.Post, "/api/career/runs")
        {
            Content = JsonContent.Create(new { task = "profile_summary" }, options: CareerClient.Json)
        };
        summary.Headers.Add("X-Test-UserId", user.UserId);
        summary.Headers.Add("Idempotency-Key", $"key-{Guid.NewGuid():N}");
        (await CareerClient.ReadErrorAsync(await factory.CreateAuthenticatedClient().SendAsync(summary), 503))
            .GetProperty("code").GetString().Should().Be("CareerModelUnavailable");
    }

    [Fact]
    public async Task RunNeedsAConfirmedProfile()
    {
        var error = await StartMatchAsync(new CareerClient(_factory), expected: 409);

        error.GetProperty("code").GetString().Should().Be("CareerProfileRequired");
    }

    [Fact]
    public async Task UnavailableReferenceAnswers503()
    {
        var factory = new CareerReferenceUnavailableFactory();
        var user = await UserAsync(factory: factory);

        (await StartMatchAsync(user, factory, 503)).GetProperty("code").GetString().Should().Be("CareerReferenceUnavailable");
        (await CareerClient.ReadErrorAsync(await user.GetAsync("/api/career/occupations/reference"), 503))
            .GetProperty("code").GetString().Should().Be("CareerReferenceUnavailable");
        (await CareerClient.ReadErrorAsync(await user.GetAsync($"/api/career/occupation-matches/{Guid.NewGuid()}"), 404))
            .GetProperty("code").GetString().Should().Be("CareerOccupationMatchNotFound");
    }

    // ---- Clarification ---------------------------------------------------------

    private static IEnumerable<string> Contradictory => NurseDuties.Concat(SoftwareDuties);

    [Fact]
    public async Task AmbiguousResultPausesWithChoicesAndAValidChoiceCompletesTheRun()
    {
        var user = await UserAsync(Contradictory);

        var waiting = await MatchAsync(user);

        waiting.GetProperty("status").GetString().Should().Be("needs_input");
        var question = waiting.GetProperty("question");
        question.GetProperty("id").GetString().Should().Be("occupation");
        question.GetProperty("text").GetString().Should().Be("Which of these is closest to the work you want analysed?");
        question.GetProperty("maxLength").GetInt32().Should().Be(20);
        var choices = question.GetProperty("choices").EnumerateArray().Select(c => c.GetProperty("value").GetString()!).ToList();
        choices.Should().Contain("15-1252.00").And.Contain("29-1141.00").And.EndWith("none");
        question.GetProperty("choices").EnumerateArray().Last().GetProperty("label").GetString().Should().Be("None of these");
        waiting.GetProperty("steps").EnumerateArray().Select(s => s.GetProperty("name").GetString()).Should()
            .Equal("read_profile", "read_goal", "match_occupations", "ask_occupation");
        waiting.GetProperty("steps")[3].GetProperty("label").GetString().Should().Be("Asked which occupation is closest");
        waiting.GetProperty("allowance").GetProperty("reserved").GetInt32().Should().Be(1);

        var runId = waiting.GetProperty("id").GetString();
        var invalid = await user.SendAsync(HttpMethod.Post, $"/api/career/runs/{runId}/answers", new { questionId = "occupation", answer = "11-1011.00" });
        (await CareerClient.ReadErrorAsync(invalid, 400)).GetProperty("fieldErrors").GetProperty("answer").GetString().Should().NotBeNullOrEmpty();

        await CareerClient.ReadDataAsync(await user.SendAsync(
            HttpMethod.Post, $"/api/career/runs/{runId}/answers", new { questionId = "occupation", answer = "29-1141.00" }), 202);
        await _factory.DrainWorkerAsync();

        var done = await GetRunAsync(user, runId!);
        done.GetProperty("status").GetString().Should().Be("completed");
        done.GetProperty("allowance").GetProperty("reserved").GetInt32().Should().Be(0);
        done.GetProperty("allowance").GetProperty("used").GetInt32().Should().Be(0);
        var match = await CareerClient.ReadDataAsync(
            await user.GetAsync($"/api/career/occupation-matches/{done.GetProperty("occupationMatchId").GetString()}"), 200);
        Codes(match).First().Should().Be("29-1141.00");
        match.GetProperty("status").GetString().Should().Be("proposed");
        match.GetProperty("clarification").GetProperty("answer").GetString().Should().Be("29-1141.00");
        match.GetProperty("clarification").GetProperty("question").GetString().Should().Contain("closest");
    }

    [Fact]
    public async Task AnsweringNoneMakesTheMatchUnsupportedAndNothingCanBeConfirmed()
    {
        var user = await UserAsync(Contradictory);
        var waiting = await MatchAsync(user);
        var runId = waiting.GetProperty("id").GetString();

        await user.SendAsync(HttpMethod.Post, $"/api/career/runs/{runId}/answers", new { questionId = "occupation", answer = "none" });
        await _factory.DrainWorkerAsync();

        var done = await GetRunAsync(user, runId!);
        var match = await CareerClient.ReadDataAsync(
            await user.GetAsync($"/api/career/occupation-matches/{done.GetProperty("occupationMatchId").GetString()}"), 200);
        match.GetProperty("status").GetString().Should().Be("unsupported");
        match.GetProperty("candidates").GetArrayLength().Should().Be(0);
        match.GetProperty("guidance").GetString().Should().Contain("highlights");

        var goal = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/goals"), 200);
        var confirm = await ConfirmAsync(user, match.GetProperty("id").GetString()!, "15-1252.00", goal.GetProperty("etag").GetString());
        (await CareerClient.ReadErrorAsync(confirm, 409)).GetProperty("code").GetString().Should().Be("CareerMatchNotConfirmable");
    }

    [Fact]
    public async Task ProfileWithOnlyATitleIsUnsupportedWithGuidance()
    {
        var user = await UserAsync(Array.Empty<string>());

        var run = await MatchAsync(user);

        run.GetProperty("status").GetString().Should().Be("completed");
        var match = await CareerClient.ReadDataAsync(
            await user.GetAsync($"/api/career/occupation-matches/{run.GetProperty("occupationMatchId").GetString()}"), 200);
        match.GetProperty("status").GetString().Should().Be("unsupported");
        match.GetProperty("guidance").GetString().Should().NotBeNullOrEmpty();
    }

    // ---- Confirm ---------------------------------------------------------------

    [Fact]
    public async Task ConfirmCreatesANewGoalVersionWithTheOccupationAndANewEtag()
    {
        var user = await UserAsync();
        var goalBefore = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/goals"), 200);
        var match = await MatchDoneAsync(user);

        var response = await ConfirmAsync(user, match.GetProperty("id").GetString()!, "15-1252.00", goalBefore.GetProperty("etag").GetString());

        var goal = await CareerClient.ReadDataAsync(response, 200);
        response.Headers.ETag!.Tag.Should().Be("\"goal-v2\"");
        goal.GetProperty("version").GetInt32().Should().Be(2);
        goal.GetProperty("provenance").GetProperty("source").GetString().Should().Be("occupation_match");
        goal.GetProperty("occupation").GetProperty("code").GetString().Should().Be("15-1252.00");
        goal.GetProperty("occupation").GetProperty("title").GetString().Should().Be("Software Developers");
        goal.GetProperty("occupation").GetProperty("referenceRelease").GetString().Should().Be("30.0");
        goal.GetProperty("occupation").GetProperty("matchId").GetString().Should().Be(match.GetProperty("id").GetString());
        goal.GetProperty("goal").GetProperty("targetRole").GetString().Should().Be("Senior data analyst");

        var after = await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/occupation-matches/{match.GetProperty("id").GetString()}"), 200);
        after.GetProperty("status").GetString().Should().Be("confirmed");
        after.GetProperty("confirmedCode").GetString().Should().Be("15-1252.00");
        after.GetProperty("confirmedIntoGoalVersion").GetInt32().Should().Be(2);
        after.GetProperty("decidedAt").ValueKind.Should().Be(JsonValueKind.String);

        // Manual edits and restores carry the confirmed occupation forward.
        var goalId = goal.GetProperty("id").GetString()!;
        var patched = await CareerClient.ReadDataAsync(await user.PatchGoalAsync(goalId, CareerClient.ValidGoal("Staff engineer"), "\"goal-v2\""), 200);
        patched.GetProperty("goal").GetProperty("targetRole").GetString().Should().Be("Staff engineer");
        patched.GetProperty("occupation").GetProperty("code").GetString().Should().Be("15-1252.00");
        patched.GetProperty("provenance").GetProperty("source").GetString().Should().Be("manual");

        var restored = await CareerClient.ReadDataAsync(await user.SendAsync(
            HttpMethod.Post, $"/api/career/goals/{goalId}/versions/1/restore", null, "\"goal-v3\""), 200);
        restored.GetProperty("occupation").ValueKind.Should().Be(JsonValueKind.Null);
        var restoredOccupation = await CareerClient.ReadDataAsync(await user.SendAsync(
            HttpMethod.Post, $"/api/career/goals/{goalId}/versions/2/restore", null, "\"goal-v4\""), 200);
        restoredOccupation.GetProperty("occupation").GetProperty("code").GetString().Should().Be("15-1252.00");
    }

    [Fact]
    public async Task ConfirmChecksThePreconditionAndTheCode()
    {
        var user = await UserAsync();
        var match = await MatchDoneAsync(user);
        var id = match.GetProperty("id").GetString()!;

        (await CareerClient.ReadErrorAsync(await ConfirmAsync(user, id, "15-1252.00", null), 428))
            .GetProperty("code").GetString().Should().Be("CareerPreconditionRequired");
        var stale = await CareerClient.ReadErrorAsync(await ConfirmAsync(user, id, "15-1252.00", "\"goal-v9\""), 412);
        stale.GetProperty("code").GetString().Should().Be("CareerVersionConflict");
        stale.GetProperty("currentVersion").GetInt32().Should().Be(1);
        (await CareerClient.ReadErrorAsync(await ConfirmAsync(user, id, "11-1011.00", "\"goal-v1\""), 409))
            .GetProperty("code").GetString().Should().Be("CareerMatchNotConfirmable");
        (await CareerClient.ReadErrorAsync(await ConfirmAsync(user, id, "nonsense", "\"goal-v1\""), 400))
            .GetProperty("fieldErrors").GetProperty("occupationCode").GetString().Should().NotBeNullOrEmpty();
        (await CareerClient.ReadErrorAsync(await ConfirmAsync(user, id, null, "\"goal-v1\""), 400))
            .GetProperty("code").GetString().Should().Be("ValidationError");
    }

    [Fact]
    public async Task ConfirmingTwiceIs409AndDismissingAConfirmedMatchIs409()
    {
        var user = await UserAsync();
        var match = await MatchDoneAsync(user);
        var id = match.GetProperty("id").GetString()!;
        (await ConfirmAsync(user, id, "15-1252.00", "\"goal-v1\"")).EnsureSuccessStatusCode();

        (await CareerClient.ReadErrorAsync(await ConfirmAsync(user, id, "15-1252.00", "\"goal-v2\""), 409))
            .GetProperty("code").GetString().Should().Be("CareerMatchNotConfirmable");
        (await CareerClient.ReadErrorAsync(await user.SendAsync(HttpMethod.Post, $"/api/career/occupation-matches/{id}/dismiss"), 409))
            .GetProperty("code").GetString().Should().Be("CareerMatchNotConfirmable");
    }

    [Fact]
    public async Task ConfirmAfterTheProfileChangedIs409Stale()
    {
        var user = await UserAsync();
        var match = await MatchDoneAsync(user);
        (await user.PutProfileAsync(Profile("Engineer II", SoftwareDuties), "\"profile-v1\"")).EnsureSuccessStatusCode();

        var read = await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/occupation-matches/{match.GetProperty("id").GetString()}"), 200);
        read.GetProperty("profileChanged").GetBoolean().Should().BeTrue();
        var response = await ConfirmAsync(user, match.GetProperty("id").GetString()!, "15-1252.00", "\"goal-v1\"");

        (await CareerClient.ReadErrorAsync(response, 409)).GetProperty("code").GetString().Should().Be("CareerMatchStale");
    }

    [Fact]
    public async Task ConfirmWithoutAGoalIs409GoalRequired()
    {
        var user = await UserAsync(withGoal: false);
        var match = await MatchDoneAsync(user);

        var response = await ConfirmAsync(user, match.GetProperty("id").GetString()!, "15-1252.00", "\"goal-v1\"");

        (await CareerClient.ReadErrorAsync(response, 409)).GetProperty("code").GetString().Should().Be("CareerGoalRequired");
    }

    [Fact]
    public async Task DismissIsIdempotentAndLeavesTheGoalAlone()
    {
        var user = await UserAsync();
        var match = await MatchDoneAsync(user);
        var path = $"/api/career/occupation-matches/{match.GetProperty("id").GetString()}/dismiss";

        var first = await CareerClient.ReadDataAsync(await user.SendAsync(HttpMethod.Post, path), 200);
        var second = await CareerClient.ReadDataAsync(await user.SendAsync(HttpMethod.Post, path), 200);

        first.GetProperty("status").GetString().Should().Be("dismissed");
        second.GetProperty("status").GetString().Should().Be("dismissed");
        (await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/goals"), 200)).GetProperty("version").GetInt32().Should().Be(1);
        var confirm = await ConfirmAsync(user, match.GetProperty("id").GetString()!, "15-1252.00", "\"goal-v1\"");
        (await CareerClient.ReadErrorAsync(confirm, 409)).GetProperty("code").GetString().Should().Be("CareerMatchNotConfirmable");
    }

    // ---- Isolation and independence --------------------------------------------

    [Fact]
    public async Task AnotherOwnersMatchIs404ForReadConfirmAndDismissAndTheirGoalIsUnchanged()
    {
        var owner = await UserAsync();
        var match = await MatchDoneAsync(owner);
        var id = match.GetProperty("id").GetString()!;
        var intruder = await UserAsync();
        var intruderGoal = await CareerClient.ReadDataAsync(await intruder.GetAsync("/api/career/goals"), 200);

        (await CareerClient.ReadErrorAsync(await intruder.GetAsync($"/api/career/occupation-matches/{id}"), 404))
            .GetProperty("code").GetString().Should().Be("CareerOccupationMatchNotFound");
        (await CareerClient.ReadErrorAsync(await ConfirmAsync(intruder, id, "15-1252.00", intruderGoal.GetProperty("etag").GetString()), 404))
            .GetProperty("code").GetString().Should().Be("CareerOccupationMatchNotFound");
        await CareerClient.ReadErrorAsync(await intruder.SendAsync(HttpMethod.Post, $"/api/career/occupation-matches/{id}/dismiss"), 404);

        var after = await CareerClient.ReadDataAsync(await intruder.GetAsync("/api/career/goals"), 200);
        after.GetProperty("version").GetInt32().Should().Be(1);
        after.GetProperty("occupation").ValueKind.Should().Be(JsonValueKind.Null);
        (await CareerClient.ReadDataAsync(await owner.GetAsync($"/api/career/occupation-matches/{id}"), 200))
            .GetProperty("status").GetString().Should().Be("proposed");
    }

    [Fact]
    public async Task GoalPayLocationAndArrangementCannotChangeTheCandidates()
    {
        var first = await UserAsync(withGoal: false);
        var second = await UserAsync(withGoal: false);
        await CareerClient.ReadDataAsync(await first.PostGoalAsync(new
        {
            targetRole = "Engineer", targetLocation = "Oslo, Norway", workArrangement = "onsite", desiredPayMin = 30000, desiredPayMax = 40000, confirmed = true
        }), 201);
        await CareerClient.ReadDataAsync(await second.PostGoalAsync(new
        {
            targetRole = "Principal wizard", targetLocation = "Remote", workArrangement = "remote", desiredPayMin = 300000, desiredPayMax = 500000, confirmed = true
        }), 201);

        var a = await MatchDoneAsync(first);
        var b = await MatchDoneAsync(second);

        a.GetProperty("candidates").GetRawText().Should().Be(b.GetProperty("candidates").GetRawText());
    }

    [Fact]
    public async Task ReferenceEndpointReturnsSourceAndCount()
    {
        var user = new CareerClient(_factory);

        var reference = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/occupations/reference"), 200);

        reference.GetProperty("occupationCount").GetInt32().Should().Be(893);
        reference.GetProperty("release").GetString().Should().Be("30.0");
        reference.GetProperty("taxonomy").GetString().Should().Be("O*NET-SOC 2019");
        reference.GetProperty("attribution").GetString().Should().Contain("CC BY 4.0");
    }

    [Fact]
    public async Task UnauthenticatedRequestsAre401()
    {
        var client = _factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("X-Test-Unauthenticated", "true");

        ((int)(await client.GetAsync($"/api/career/occupation-matches/{Guid.NewGuid()}")).StatusCode).Should().Be(401);
        ((int)(await client.PostAsJsonAsync($"/api/career/occupation-matches/{Guid.NewGuid()}/confirm", new { occupationCode = "15-1252.00" })).StatusCode).Should().Be(401);
        ((int)(await client.PostAsync($"/api/career/occupation-matches/{Guid.NewGuid()}/dismiss", null)).StatusCode).Should().Be(401);
        ((int)(await client.GetAsync("/api/career/occupations/reference")).StatusCode).Should().Be(401);
    }

    [Fact]
    public async Task OwnerDeletionRemovesMatches()
    {
        var user = await UserAsync();
        var match = await MatchDoneAsync(user);
        var other = await UserAsync();
        await MatchDoneAsync(other);

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ICareerPrivateDataService>().DeleteAllForOwnerAsync(user.UserId);
        }

        using var check = _factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.CareerOccupationMatches.CountAsync(m => m.OwnerId == user.UserId)).Should().Be(0);
        (await db.CareerOccupationMatches.CountAsync(m => m.OwnerId == other.UserId)).Should().Be(1);
        match.GetProperty("id").GetString().Should().NotBeNull();
    }
}
