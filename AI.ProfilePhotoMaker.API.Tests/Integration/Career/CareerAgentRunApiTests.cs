using System.Net.Http.Json;
using System.Text.Json;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Services.Career;
using AI.ProfilePhotoMaker.API.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration.Career;

/// <summary>Career host with the fake model registered, as Development and Testing do in Program.cs.</summary>
public class CareerAgentFactory : CareerWorkspaceEnabledFactory
{
    public bool RegisterModel { get; init; } = true;
    public int MonthlyAllowance { get; init; } = 20;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Career:Agent:MonthlyRunAllowance"] = MonthlyAllowance.ToString(),
            // Existing suites start many runs for one user; the usage limits have their own tests (CareerUsageControlsTests).
            ["Career:Usage:PerMinuteRunLimit"] = "1000",
            ["Career:Usage:MaxConcurrentRunsPerUser"] = "1000"
        }));
        builder.ConfigureTestServices(services =>
        {
            if (RegisterModel)
            {
                services.AddSingleton<ICareerTextModel, FakeCareerTextModel>();
            }
        });
    }

    /// <summary>Plays the background worker: claims and runs until nothing is claimable.</summary>
    public async Task DrainWorkerAsync(string workerId = "test-worker")
    {
        for (var i = 0; i < 10; i++)
        {
            using var scope = Services.CreateScope();
            if (!await scope.ServiceProvider.GetRequiredService<ICareerAgentRunner>().RunOnceAsync(workerId))
            {
                return;
            }
        }
    }
}

public class CareerAgentRunApiTests : IClassFixture<CareerAgentFactory>
{
    private readonly CareerAgentFactory _factory;

    public CareerAgentRunApiTests(CareerAgentFactory factory)
    {
        _factory = factory;
    }

    private static string Key() => $"key-{Guid.NewGuid():N}";

    private static Task<HttpResponseMessage> StartAsync(CareerClient user, HttpClient http, string? key, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/career/runs")
        {
            Content = JsonContent.Create(body ?? new { task = "profile_summary" }, options: CareerClient.Json)
        };
        request.Headers.Add("X-Test-UserId", user.UserId);
        if (key != null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }
        return http.SendAsync(request);
    }

    private Task<HttpResponseMessage> StartAsync(CareerClient user, string? key, object? body = null) =>
        StartAsync(user, _factory.CreateAuthenticatedClient(), key, body);

    private async Task<JsonElement> StartOkAsync(CareerClient user, string? key = null)
    {
        var response = await StartAsync(user, key ?? Key());
        return await CareerClient.ReadDataAsync(response, 202);
    }

    private static string IdOf(JsonElement run) => run.GetProperty("id").GetString()!;

    private async Task<JsonElement> GetRunAsync(CareerClient user, string id) =>
        await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/runs/{id}"), 200);

    private async Task<CareerClient> UserWithProfileAndGoalAsync()
    {
        var user = new CareerClient(_factory);
        await user.CreateProfileAsync();
        await user.CreateGoalAsync();
        return user;
    }

    private int SummaryItemCount(Guid proposalId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return db.CareerProfileProposalItems.Count(i => i.ProposalId == proposalId && i.Field == "summary");
    }

    [Fact]
    public async Task HappyPathQueuesThenCompletesWithRealStepsAndAnAgentProposal()
    {
        var user = await UserWithProfileAndGoalAsync();
        var profileBefore = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/profile"), 200);

        var response = await StartAsync(user, Key());
        var queued = await CareerClient.ReadDataAsync(response, 202);
        response.Headers.Location!.ToString().Should().EndWith($"/api/career/runs/{IdOf(queued)}");
        queued.GetProperty("status").GetString().Should().Be("queued");
        queued.GetProperty("pinnedProfileVersion").GetInt32().Should().Be(1);
        queued.GetProperty("allowance").GetProperty("reserved").GetInt32().Should().Be(1);

        await _factory.DrainWorkerAsync();

        var done = await GetRunAsync(user, IdOf(queued));
        done.GetProperty("status").GetString().Should().Be("completed");
        done.GetProperty("steps").EnumerateArray().Select(s => s.GetProperty("name").GetString()).Should()
            .Equal("read_profile", "read_goal", "draft_summary", "save_proposal");
        done.GetProperty("steps")[0].GetProperty("label").GetString().Should().Be("Read your confirmed profile");
        done.GetProperty("profileChanged").GetBoolean().Should().BeFalse();
        done.GetProperty("allowance").GetProperty("used").GetInt32().Should().Be(1);
        done.GetProperty("allowance").GetProperty("reserved").GetInt32().Should().Be(0);

        var proposalId = done.GetProperty("proposalId").GetGuid();
        var proposal = await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/profile/proposals/{proposalId}"), 200);
        proposal.GetProperty("source").GetString().Should().Be("agent");
        proposal.GetProperty("items").GetArrayLength().Should().Be(1);
        proposal.GetProperty("items")[0].GetProperty("field").GetString().Should().Be("summary");
        proposal.GetProperty("items")[0].GetProperty("value").GetString().Should().Contain("Data analyst");
        SummaryItemCount(proposalId).Should().Be(1);

        // The agent proposes; it never changes a confirmed fact.
        var profileAfter = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/profile"), 200);
        profileAfter.GetProperty("version").GetInt32().Should().Be(profileBefore.GetProperty("version").GetInt32());
        profileAfter.GetProperty("facts").GetProperty("summary").GetString()
            .Should().Be(profileBefore.GetProperty("facts").GetProperty("summary").GetString());
    }

    [Fact]
    public async Task WithoutAGoalTheRunAsksWhoItIsForThenResumesWithTheAnswer()
    {
        var user = new CareerClient(_factory);
        await user.CreateProfileAsync();
        var run = await StartOkAsync(user);

        await _factory.DrainWorkerAsync();

        var waiting = await GetRunAsync(user, IdOf(run));
        waiting.GetProperty("status").GetString().Should().Be("needs_input");
        waiting.GetProperty("question").GetProperty("id").GetString().Should().Be("audience");
        waiting.GetProperty("question").GetProperty("text").GetString().Should().Be("Who should this summary speak to?");
        waiting.GetProperty("question").GetProperty("maxLength").GetInt32().Should().Be(200);
        waiting.GetProperty("steps").EnumerateArray().Select(s => s.GetProperty("name").GetString()).Should()
            .Equal("read_profile", "read_goal", "ask_audience");

        var answered = await CareerClient.ReadDataAsync(await user.SendAsync(
            HttpMethod.Post, $"/api/career/runs/{IdOf(run)}/answers", new { questionId = "audience", answer = "hiring managers in health tech" }), 202);
        answered.GetProperty("status").GetString().Should().Be("queued");

        await _factory.DrainWorkerAsync();

        var done = await GetRunAsync(user, IdOf(run));
        done.GetProperty("status").GetString().Should().Be("completed");
        var proposal = await CareerClient.ReadDataAsync(
            await user.GetAsync($"/api/career/profile/proposals/{done.GetProperty("proposalId").GetString()}"), 200);
        proposal.GetProperty("items")[0].GetProperty("value").GetString().Should().Contain("hiring managers in health tech");
    }

    [Fact]
    public async Task AnsweringARunThatIsNotWaitingIs409()
    {
        var user = await UserWithProfileAndGoalAsync();
        var run = await StartOkAsync(user);

        var response = await user.SendAsync(HttpMethod.Post, $"/api/career/runs/{IdOf(run)}/answers", new { questionId = "audience", answer = "anyone" });

        (await CareerClient.ReadErrorAsync(response, 409)).GetProperty("code").GetString().Should().Be("CareerRunNotWaiting");
    }

    [Theory]
    [InlineData("wrong", "text")]
    [InlineData("audience", "")]
    public async Task AnswerIsValidated(string questionId, string answer)
    {
        var user = new CareerClient(_factory);
        await user.CreateProfileAsync();
        var run = await StartOkAsync(user);
        await _factory.DrainWorkerAsync();

        var response = await user.SendAsync(HttpMethod.Post, $"/api/career/runs/{IdOf(run)}/answers", new { questionId, answer });

        (await CareerClient.ReadErrorAsync(response, 400)).GetProperty("code").GetString().Should().Be("ValidationError");
    }

    [Fact]
    public async Task ReplayWithTheSameKeyReturnsTheSameRunAndReservesOnce()
    {
        var user = await UserWithProfileAndGoalAsync();
        var key = Key();

        var first = await CareerClient.ReadDataAsync(await StartAsync(user, key), 202);
        var second = await CareerClient.ReadDataAsync(await StartAsync(user, key), 202);

        IdOf(second).Should().Be(IdOf(first));
        second.GetProperty("allowance").GetProperty("reserved").GetInt32().Should().Be(1);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.CareerAgentRuns.Count(r => r.OwnerId == user.UserId).Should().Be(1);
    }

    [Fact]
    public async Task SameKeyWithADifferentPayloadIs409()
    {
        var user = await UserWithProfileAndGoalAsync();
        var key = Key();
        await StartOkAsync(user, key);

        var response = await StartAsync(user, key, new { task = "something_else" });

        (await CareerClient.ReadErrorAsync(response, 409)).GetProperty("code").GetString().Should().Be("CareerIdempotencyMismatch");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("short")]
    public async Task MissingOrShortIdempotencyKeyIs400(string? key)
    {
        var user = await UserWithProfileAndGoalAsync();

        var error = await CareerClient.ReadErrorAsync(await StartAsync(user, key), 400);

        error.GetProperty("code").GetString().Should().Be("ValidationError");
        error.GetProperty("fieldErrors").TryGetProperty("idempotencyKey", out _).Should().BeTrue();
    }

    [Fact]
    public async Task UnknownTaskIs400()
    {
        var user = await UserWithProfileAndGoalAsync();

        var error = await CareerClient.ReadErrorAsync(await StartAsync(user, Key(), new { task = "write_my_novel" }), 400);

        error.GetProperty("fieldErrors").TryGetProperty("task", out _).Should().BeTrue();
    }

    [Fact]
    public async Task NoConfirmedProfileIs409AndReservesNothing()
    {
        var user = new CareerClient(_factory);

        var error = await CareerClient.ReadErrorAsync(await StartAsync(user, Key()), 409);

        error.GetProperty("code").GetString().Should().Be("CareerProfileRequired");
        var list = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/runs"), 200);
        list.GetProperty("allowance").GetProperty("reserved").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task ExhaustedAllowanceIs429AndReadsNeverChangeIt()
    {
        var factory = new CareerAgentFactory { MonthlyAllowance = 1 };
        var user = new CareerClient(factory);
        await user.CreateProfileAsync();
        await CareerClient.ReadDataAsync(await StartAsync(user, factory.CreateAuthenticatedClient(), Key()), 202);

        var error = await CareerClient.ReadErrorAsync(await StartAsync(user, factory.CreateAuthenticatedClient(), Key()), 429);

        error.GetProperty("code").GetString().Should().Be("CareerAllowanceExhausted");
        for (var i = 0; i < 3; i++)
        {
            var list = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/runs"), 200);
            list.GetProperty("allowance").GetProperty("reserved").GetInt32().Should().Be(1);
            list.GetProperty("allowance").GetProperty("used").GetInt32().Should().Be(0);
            list.GetProperty("allowance").GetProperty("limit").GetInt32().Should().Be(1);
            list.GetProperty("runs").GetArrayLength().Should().Be(1);
        }
    }

    [Fact]
    public async Task ListIsNewestFirstAndLimitedToTwenty()
    {
        var factory = new CareerAgentFactory { MonthlyAllowance = 100 };
        var user = new CareerClient(factory);
        await user.CreateProfileAsync();
        var ids = new List<string>();
        for (var i = 0; i < 22; i++)
        {
            var run = await CareerClient.ReadDataAsync(await StartAsync(user, factory.CreateAuthenticatedClient(), Key()), 202);
            ids.Add(IdOf(run));
            await Task.Delay(2);
        }

        var list = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/runs"), 200);

        var listed = list.GetProperty("runs").EnumerateArray().Select(IdOf).ToList();
        listed.Should().HaveCount(20);
        listed.First().Should().Be(ids.Last());
    }

    [Fact]
    public async Task AnotherOwnersRunIs404ForGetAnswerAndCancel()
    {
        var owner = await UserWithProfileAndGoalAsync();
        var run = await StartOkAsync(owner);
        var stranger = new CareerClient(_factory);

        foreach (var response in new[]
        {
            await stranger.GetAsync($"/api/career/runs/{IdOf(run)}"),
            await stranger.SendAsync(HttpMethod.Post, $"/api/career/runs/{IdOf(run)}/answers", new { questionId = "audience", answer = "x" }),
            await stranger.SendAsync(HttpMethod.Post, $"/api/career/runs/{IdOf(run)}/cancel")
        })
        {
            (await CareerClient.ReadErrorAsync(response, 404)).GetProperty("code").GetString().Should().Be("CareerRunNotFound");
        }

        (await GetRunAsync(owner, IdOf(run))).GetProperty("status").GetString().Should().Be("queued");
    }

    [Fact]
    public async Task UnauthenticatedRequestsAreRejected()
    {
        var client = _factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("X-Test-Unauthenticated", "true");
        client.DefaultRequestHeaders.Add("Idempotency-Key", Key());

        ((int)(await client.GetAsync("/api/career/runs")).StatusCode).Should().Be(401);
        ((int)(await client.PostAsJsonAsync("/api/career/runs", new { task = "profile_summary" })).StatusCode).Should().Be(401);
    }

    [Fact]
    public async Task CancelIsIdempotentAndStopsTheRun()
    {
        var user = await UserWithProfileAndGoalAsync();
        var run = await StartOkAsync(user);

        var first = await CareerClient.ReadDataAsync(await user.SendAsync(HttpMethod.Post, $"/api/career/runs/{IdOf(run)}/cancel"), 200);
        var second = await CareerClient.ReadDataAsync(await user.SendAsync(HttpMethod.Post, $"/api/career/runs/{IdOf(run)}/cancel"), 200);
        await _factory.DrainWorkerAsync();

        first.GetProperty("status").GetString().Should().Be("cancelled");
        second.GetProperty("status").GetString().Should().Be("cancelled");
        first.GetProperty("allowance").GetProperty("reserved").GetInt32().Should().Be(0);
        (await GetRunAsync(user, IdOf(run))).GetProperty("status").GetString().Should().Be("cancelled");
    }

    [Fact]
    public async Task CancellingAFinishedRunLeavesItUnchanged()
    {
        var user = await UserWithProfileAndGoalAsync();
        var run = await StartOkAsync(user);
        await _factory.DrainWorkerAsync();

        var cancelled = await CareerClient.ReadDataAsync(await user.SendAsync(HttpMethod.Post, $"/api/career/runs/{IdOf(run)}/cancel"), 200);

        cancelled.GetProperty("status").GetString().Should().Be("completed");
        cancelled.GetProperty("allowance").GetProperty("used").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task ProfileSavedAfterPinningMakesTheRunReportProfileChangedAndAcceptanceStale()
    {
        var user = await UserWithProfileAndGoalAsync();
        var run = await StartOkAsync(user);
        (await user.PutProfileAsync(CareerClient.ValidProfile("Lead analyst"), "\"profile-v1\"")).EnsureSuccessStatusCode();

        await _factory.DrainWorkerAsync();

        var done = await GetRunAsync(user, IdOf(run));
        done.GetProperty("status").GetString().Should().Be("completed");
        done.GetProperty("pinnedProfileVersion").GetInt32().Should().Be(1);
        done.GetProperty("profileChanged").GetBoolean().Should().BeTrue();
        var proposal = await CareerClient.ReadDataAsync(
            await user.GetAsync($"/api/career/profile/proposals/{done.GetProperty("proposalId").GetString()}"), 200);
        var itemId = proposal.GetProperty("items")[0].GetProperty("id").GetString()!;

        var accept = await user.AcceptAsync(done.GetProperty("proposalId").GetString()!, new[] { itemId }, "\"profile-v2\"");

        ((int)accept.StatusCode).Should().Be(412);
    }

    [Fact]
    public async Task AcceptingACompletedAgentProposalAddsTheSummaryAsANewVersion()
    {
        var user = await UserWithProfileAndGoalAsync();
        var run = await StartOkAsync(user);
        await _factory.DrainWorkerAsync();
        var done = await GetRunAsync(user, IdOf(run));
        var proposalId = done.GetProperty("proposalId").GetString()!;
        var proposal = await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/profile/proposals/{proposalId}"), 200);

        var accept = await user.AcceptAsync(proposalId, new[] { proposal.GetProperty("items")[0].GetProperty("id").GetString()! }, "\"profile-v1\"");

        var profile = await CareerClient.ReadDataAsync(accept, 200);
        profile.GetProperty("version").GetInt32().Should().Be(2);
        profile.GetProperty("provenance").GetProperty("source").GetString().Should().Be("agent");
    }

    [Fact]
    public async Task WithNoModelRegisteredStartingARunIs503()
    {
        var factory = new CareerAgentFactory { RegisterModel = false };
        var user = new CareerClient(factory);
        await user.CreateProfileAsync();

        var response = await StartAsync(user, factory.CreateAuthenticatedClient(), Key());

        (await CareerClient.ReadErrorAsync(response, 503)).GetProperty("code").GetString().Should().Be("CareerModelUnavailable");
    }
}
