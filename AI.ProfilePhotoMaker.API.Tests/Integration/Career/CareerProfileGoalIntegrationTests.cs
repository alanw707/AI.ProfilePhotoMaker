using System.Net.Http.Headers;
using FluentAssertions;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration.Career;

/// <summary>
/// Observable API behaviour for ticket #378: manual career profile and goal,
/// owner-scoped, versioned, with ETag/If-Match concurrency (spec #376).
/// </summary>
public class CareerProfileGoalIntegrationTests : IClassFixture<CareerWorkspaceEnabledFactory>
{
    private readonly CareerWorkspaceEnabledFactory _factory;

    public CareerProfileGoalIntegrationTests(CareerWorkspaceEnabledFactory factory)
    {
        _factory = factory;
    }

    private CareerClient NewUser() => new(_factory);

    // ---- Profile ---------------------------------------------------------

    [Fact]
    public async Task Profile_DoesNotExistUntilCreated()
    {
        var user = NewUser();

        var error = await CareerClient.ReadErrorAsync(await user.GetAsync("/api/career/profile"), 404);

        error.GetProperty("code").GetString().Should().Be("CareerProfileNotFound");
    }

    [Fact]
    public async Task Profile_FirstSaveNeedsNoIfMatch_AndReturnsVersionOneEtag()
    {
        var user = NewUser();

        var response = await user.PutProfileAsync(CareerClient.ValidProfile());
        var data = await CareerClient.ReadDataAsync(response, 200);

        response.Headers.ETag!.ToString().Should().Be("\"profile-v1\"");
        data.GetProperty("version").GetInt32().Should().Be(1);
        data.GetProperty("etag").GetString().Should().Be("\"profile-v1\"");
        data.GetProperty("facts").GetProperty("currentTitle").GetString().Should().Be("Data analyst");
        data.GetProperty("provenance").GetProperty("source").GetString().Should().Be("manual");
        data.GetProperty("provenance").GetProperty("confirmedAt").GetDateTime().Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Profile_ReloadRestoresAcceptedFacts()
    {
        var user = NewUser();
        await user.CreateProfileAsync("Clinical data analyst");

        var data = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/profile"), 200);

        var facts = data.GetProperty("facts");
        facts.GetProperty("currentTitle").GetString().Should().Be("Clinical data analyst");
        facts.GetProperty("industry").GetString().Should().Be("Healthcare");
        facts.GetProperty("yearsExperience").GetInt32().Should().Be(4);
        facts.GetProperty("skills").EnumerateArray().Select(s => s.GetString()).Should().Equal("SQL", "Tableau");
        facts.GetProperty("highlights").EnumerateArray().Select(s => s.GetString()).Should().Equal("Built the weekly KPI report");
        facts.GetProperty("workArrangement").GetString().Should().Be("hybrid");
    }

    [Fact]
    public async Task Profile_MustBeExplicitlyConfirmed()
    {
        var user = NewUser();
        var body = new { currentTitle = "Data analyst", skills = Array.Empty<string>(), highlights = Array.Empty<string>(), confirmed = false };

        var error = await CareerClient.ReadErrorAsync(await user.PutProfileAsync(body), 400);

        error.GetProperty("code").GetString().Should().Be("ValidationError");
        error.GetProperty("fieldErrors").GetProperty("confirmed").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Profile_InvalidFieldsReturnFieldErrors()
    {
        var user = NewUser();
        var body = new
        {
            currentTitle = "  ",
            yearsExperience = 61,
            workArrangement = "on the moon",
            skills = new[] { new string('x', 81) },
            highlights = Array.Empty<string>(),
            confirmed = true
        };

        var error = await CareerClient.ReadErrorAsync(await user.PutProfileAsync(body), 400);

        var fields = error.GetProperty("fieldErrors");
        fields.TryGetProperty("currentTitle", out _).Should().BeTrue();
        fields.TryGetProperty("yearsExperience", out _).Should().BeTrue();
        fields.TryGetProperty("workArrangement", out _).Should().BeTrue();
        fields.TryGetProperty("skills", out _).Should().BeTrue();
        error.GetProperty("correlationId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Profile_SkillsAreTrimmedAndDeduplicatedCaseInsensitively()
    {
        var user = NewUser();
        var body = new
        {
            currentTitle = "Data analyst",
            skills = new[] { " SQL ", "sql", "Python" },
            highlights = Array.Empty<string>(),
            confirmed = true
        };

        var data = await CareerClient.ReadDataAsync(await user.PutProfileAsync(body), 200);

        data.GetProperty("facts").GetProperty("skills").EnumerateArray().Select(s => s.GetString())
            .Should().Equal("SQL", "Python");
    }

    [Fact]
    public async Task Profile_UpdateWithoutIfMatchReturns428()
    {
        var user = NewUser();
        await user.CreateProfileAsync();

        var error = await CareerClient.ReadErrorAsync(await user.PutProfileAsync(CareerClient.ValidProfile("Analyst II")), 428);

        error.GetProperty("code").GetString().Should().Be("CareerPreconditionRequired");
    }

    [Fact]
    public async Task Profile_StaleIfMatchReturns412_AndKeepsTheCurrentVersion()
    {
        var user = NewUser();
        await user.CreateProfileAsync();
        await CareerClient.ReadDataAsync(await user.PutProfileAsync(CareerClient.ValidProfile("Analyst II"), "\"profile-v1\""), 200);

        // A second tab still holding version 1 tries to save.
        var error = await CareerClient.ReadErrorAsync(await user.PutProfileAsync(CareerClient.ValidProfile("Stale tab"), "\"profile-v1\""), 412);

        error.GetProperty("code").GetString().Should().Be("CareerVersionConflict");
        error.GetProperty("currentVersion").GetInt32().Should().Be(2);
        var current = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/profile"), 200);
        current.GetProperty("facts").GetProperty("currentTitle").GetString().Should().Be("Analyst II");
    }

    [Fact]
    public async Task Profile_EditsCreateNewVersions_AndOldVersionsStayReadable()
    {
        var user = NewUser();
        await user.CreateProfileAsync("Analyst I");
        var updated = await CareerClient.ReadDataAsync(await user.PutProfileAsync(CareerClient.ValidProfile("Analyst II"), "\"profile-v1\""), 200);
        updated.GetProperty("version").GetInt32().Should().Be(2);

        var versions = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/profile/versions"), 200);
        versions.EnumerateArray().Select(v => v.GetProperty("version").GetInt32()).Should().Equal(2, 1);
        versions[0].GetProperty("isActive").GetBoolean().Should().BeTrue();
        versions[1].GetProperty("isActive").GetBoolean().Should().BeFalse();

        var v1 = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/profile/versions/1"), 200);
        v1.GetProperty("facts").GetProperty("currentTitle").GetString().Should().Be("Analyst I");
        v1.GetProperty("isActive").GetBoolean().Should().BeFalse();

        await CareerClient.ReadErrorAsync(await user.GetAsync("/api/career/profile/versions/9"), 404);
    }

    [Fact]
    public async Task Profile_RestoringAnOldVersionAppendsACopy()
    {
        var user = NewUser();
        await user.CreateProfileAsync("Analyst I");
        await CareerClient.ReadDataAsync(await user.PutProfileAsync(CareerClient.ValidProfile("Analyst II"), "\"profile-v1\""), 200);

        var noPrecondition = await user.SendAsync(HttpMethod.Post, "/api/career/profile/versions/1/restore");
        await CareerClient.ReadErrorAsync(noPrecondition, 428);

        var response = await user.SendAsync(HttpMethod.Post, "/api/career/profile/versions/1/restore", ifMatch: "\"profile-v2\"");
        var restored = await CareerClient.ReadDataAsync(response, 200);

        restored.GetProperty("version").GetInt32().Should().Be(3);
        restored.GetProperty("facts").GetProperty("currentTitle").GetString().Should().Be("Analyst I");
        response.Headers.ETag!.ToString().Should().Be("\"profile-v3\"");
        var versions = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/profile/versions"), 200);
        versions.GetArrayLength().Should().Be(3);
    }

    // ---- Goal ------------------------------------------------------------

    [Fact]
    public async Task Goal_CreateReadAndDuplicate()
    {
        var user = NewUser();
        await CareerClient.ReadErrorAsync(await user.GetAsync("/api/career/goals"), 404);

        var response = await user.PostGoalAsync(CareerClient.ValidGoal());
        var created = await CareerClient.ReadDataAsync(response, 201);
        response.Headers.ETag!.ToString().Should().Be("\"goal-v1\"");
        created.GetProperty("goal").GetProperty("targetRole").GetString().Should().Be("Senior data analyst");
        created.GetProperty("goal").GetProperty("desiredPayMax").GetInt32().Should().Be(120000);
        created.GetProperty("basedOnProfileVersion").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        created.GetProperty("isStale").GetBoolean().Should().BeFalse();

        var read = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/goals"), 200);
        read.GetProperty("id").GetString().Should().Be(created.GetProperty("id").GetString());

        var duplicate = await CareerClient.ReadErrorAsync(await user.PostGoalAsync(CareerClient.ValidGoal("Other")), 409);
        duplicate.GetProperty("code").GetString().Should().Be("CareerGoalAlreadyExists");
    }

    [Fact]
    public async Task Goal_PayRangeAndEffortAreValidated()
    {
        var user = NewUser();
        var body = new { targetRole = "Analyst", desiredPayMin = 120000, desiredPayMax = 90000, weeklyEffortHours = 41, confirmed = true };

        var error = await CareerClient.ReadErrorAsync(await user.PostGoalAsync(body), 400);

        error.GetProperty("fieldErrors").TryGetProperty("desiredPayMax", out _).Should().BeTrue();
        error.GetProperty("fieldErrors").TryGetProperty("weeklyEffortHours", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Goal_UpdateNeedsCurrentIfMatch()
    {
        var user = NewUser();
        var goal = await user.CreateGoalAsync();
        var id = goal.GetProperty("id").GetString()!;

        await CareerClient.ReadErrorAsync(await user.PatchGoalAsync(id, CareerClient.ValidGoal("Lead analyst"), null), 428);

        var updated = await CareerClient.ReadDataAsync(await user.PatchGoalAsync(id, CareerClient.ValidGoal("Lead analyst"), "\"goal-v1\""), 200);
        updated.GetProperty("version").GetInt32().Should().Be(2);

        var stale = await CareerClient.ReadErrorAsync(await user.PatchGoalAsync(id, CareerClient.ValidGoal("Stale"), "\"goal-v1\""), 412);
        stale.GetProperty("currentVersion").GetInt32().Should().Be(2);

        var versions = await CareerClient.ReadDataAsync(await user.GetAsync($"/api/career/goals/{id}/versions"), 200);
        versions.EnumerateArray().Select(v => v.GetProperty("targetRole").GetString()).Should().Equal("Lead analyst", "Senior data analyst");
    }

    [Fact]
    public async Task Goal_BecomesStaleWhenProfileChanges_UntilReconfirmed()
    {
        var user = NewUser();
        await user.CreateProfileAsync();
        var goal = await user.CreateGoalAsync();
        var id = goal.GetProperty("id").GetString()!;
        goal.GetProperty("basedOnProfileVersion").GetInt32().Should().Be(1);
        goal.GetProperty("isStale").GetBoolean().Should().BeFalse();

        await CareerClient.ReadDataAsync(await user.PutProfileAsync(CareerClient.ValidProfile("Analyst II"), "\"profile-v1\""), 200);

        var afterProfileEdit = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/goals"), 200);
        afterProfileEdit.GetProperty("isStale").GetBoolean().Should().BeTrue();

        var reconfirmed = await CareerClient.ReadDataAsync(await user.PatchGoalAsync(id, CareerClient.ValidGoal(), "\"goal-v1\""), 200);
        reconfirmed.GetProperty("isStale").GetBoolean().Should().BeFalse();
        reconfirmed.GetProperty("basedOnProfileVersion").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Goal_CreatedBeforeProfileBecomesStaleOnceFactsAreSaved()
    {
        var user = NewUser();
        await user.CreateGoalAsync();

        await user.CreateProfileAsync();

        var goal = await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/goals"), 200);
        goal.GetProperty("isStale").GetBoolean().Should().BeTrue();
    }

    // ---- Ownership -------------------------------------------------------

    [Fact]
    public async Task AnotherUsersCareerDataIsNeverReturned()
    {
        var alice = NewUser();
        var bob = NewUser();
        await alice.CreateProfileAsync("Alice's title");
        var aliceGoal = await alice.CreateGoalAsync("Alice's goal");
        var aliceGoalId = aliceGoal.GetProperty("id").GetString()!;

        await CareerClient.ReadErrorAsync(await bob.GetAsync("/api/career/profile"), 404);
        await CareerClient.ReadErrorAsync(await bob.GetAsync("/api/career/profile/versions/1"), 404);
        await CareerClient.ReadErrorAsync(await bob.GetAsync("/api/career/goals"), 404);
        await CareerClient.ReadErrorAsync(await bob.GetAsync($"/api/career/goals/{aliceGoalId}/versions"), 404);
        var patch = await CareerClient.ReadErrorAsync(await bob.PatchGoalAsync(aliceGoalId, CareerClient.ValidGoal("Hijack"), "\"goal-v1\""), 404);
        patch.GetProperty("code").GetString().Should().Be("CareerGoalNotFound");

        var bobVersions = await CareerClient.ReadDataAsync(await bob.GetAsync("/api/career/profile/versions"), 200);
        bobVersions.GetArrayLength().Should().Be(0);

        // Bob creating his own profile does not touch Alice's.
        await bob.CreateProfileAsync("Bob's title");
        var aliceProfile = await CareerClient.ReadDataAsync(await alice.GetAsync("/api/career/profile"), 200);
        aliceProfile.GetProperty("facts").GetProperty("currentTitle").GetString().Should().Be("Alice's title");
        var aliceGoalAfter = await CareerClient.ReadDataAsync(await alice.GetAsync("/api/career/goals"), 200);
        aliceGoalAfter.GetProperty("goal").GetProperty("targetRole").GetString().Should().Be("Alice's goal");
    }

    [Fact]
    public async Task UnauthenticatedRequestsAreRejected()
    {
        var client = _factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("X-Test-Unauthenticated", "true");

        var response = await client.GetAsync("/api/career/profile");

        ((int)response.StatusCode).Should().Be(401);
    }
}
