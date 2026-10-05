using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration.Career;

/// <summary>Accepting and dismissing proposals (ADR 0007).</summary>
public class ResumeProposalTests : IClassFixture<ResumeImportFactory>
{
    private readonly ResumeImportFactory _factory;

    public ResumeProposalTests(ResumeImportFactory factory)
    {
        _factory = factory;
    }

    private static async Task<JsonElement> ProposalAsync(CareerClient user)
    {
        var resume = await user.UploadOkAsync(ResumeFixtures.Pdf(ResumeFixtures.MorganPages));
        return await CareerClient.ReadDataAsync(
            await user.GetAsync($"/api/career/profile/proposals/{resume.GetProperty("proposalId").GetString()}"), 200);
    }

    private static string[] Ids(JsonElement proposal, params string[] fields) =>
        proposal.GetProperty("items").EnumerateArray()
            .Where(i => fields.Contains(i.GetProperty("field").GetString()))
            .Select(i => i.GetProperty("id").GetString()!).ToArray();

    [Fact]
    public async Task AcceptMergesItemsAndRecordsProvenance()
    {
        var user = new CareerClient(_factory);
        await user.CreateProfileAsync("Data analyst"); // skills SQL, Tableau; location Denver, CO
        var proposal = await ProposalAsync(user);
        proposal.GetProperty("baseProfileVersion").GetInt32().Should().Be(1);

        var response = await user.AcceptAsync(proposal.GetProperty("id").GetString()!,
            Ids(proposal, "currentTitle", "skills", "yearsExperience"), "\"profile-v1\"");

        var profile = await CareerClient.ReadDataAsync(response, 200);
        response.Headers.ETag!.Tag.Should().Be("\"profile-v2\"");
        profile.GetProperty("version").GetInt32().Should().Be(2);
        profile.GetProperty("provenance").GetProperty("source").GetString().Should().Be("resume");
        profile.GetProperty("provenance").GetProperty("sourceProposalId").GetString()
            .Should().Be(proposal.GetProperty("id").GetString());

        var facts = profile.GetProperty("facts");
        facts.GetProperty("currentTitle").GetString().Should().Be("Senior Operations Lead");
        facts.GetProperty("yearsExperience").GetInt32().Should().Be(8);
        facts.GetProperty("skills").EnumerateArray().Select(s => s.GetString())
            .Should().Equal("SQL", "Tableau", "Process Improvement");
        // Unselected items leave existing facts alone.
        facts.GetProperty("industry").GetString().Should().Be("Healthcare");
        facts.GetProperty("highlights").GetArrayLength().Should().Be(1);

        var after = await CareerClient.ReadDataAsync(
            await user.GetAsync($"/api/career/profile/proposals/{proposal.GetProperty("id").GetString()}"), 200);
        after.GetProperty("status").GetString().Should().Be("accepted");
    }

    [Fact]
    public async Task AcceptWithoutExistingProfileCreatesVersionOne()
    {
        var user = new CareerClient(_factory);
        var proposal = await ProposalAsync(user);

        var profile = await CareerClient.ReadDataAsync(
            await user.AcceptAsync(proposal.GetProperty("id").GetString()!, Ids(proposal, "currentTitle", "location"), null), 200);

        profile.GetProperty("version").GetInt32().Should().Be(1);
        profile.GetProperty("facts").GetProperty("location").GetString().Should().Be("Denver, CO");
    }

    [Fact]
    public async Task AcceptAfterProfileChangedIs412()
    {
        var user = new CareerClient(_factory);
        await user.CreateProfileAsync();
        var proposal = await ProposalAsync(user);
        await CareerClient.ReadDataAsync(await user.PutProfileAsync(CareerClient.ValidProfile("Analyst II"), "\"profile-v1\""), 200);

        // Even with the current ETag, the proposal was pinned to v1.
        var error = await CareerClient.ReadErrorAsync(
            await user.AcceptAsync(proposal.GetProperty("id").GetString()!, Ids(proposal, "currentTitle"), "\"profile-v2\""), 412);
        error.GetProperty("code").GetString().Should().Be("CareerVersionConflict");

        await CareerClient.ReadErrorAsync(
            await user.AcceptAsync(proposal.GetProperty("id").GetString()!, Ids(proposal, "currentTitle"), "\"profile-v1\""), 412);

        var stale = await CareerClient.ReadDataAsync(
            await user.GetAsync($"/api/career/profile/proposals/{proposal.GetProperty("id").GetString()}"), 200);
        stale.GetProperty("isStale").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task AcceptWithoutIfMatchWhenProfileExistsIs428()
    {
        var user = new CareerClient(_factory);
        await user.CreateProfileAsync();
        var proposal = await ProposalAsync(user);

        var error = await CareerClient.ReadErrorAsync(
            await user.AcceptAsync(proposal.GetProperty("id").GetString()!, Ids(proposal, "currentTitle"), null), 428);

        error.GetProperty("code").GetString().Should().Be("CareerPreconditionRequired");
    }

    [Fact]
    public async Task AcceptWithNoItemsIs400()
    {
        var user = new CareerClient(_factory);
        await user.CreateProfileAsync();
        var proposal = await ProposalAsync(user);

        var error = await CareerClient.ReadErrorAsync(
            await user.AcceptAsync(proposal.GetProperty("id").GetString()!, Array.Empty<string>(), "\"profile-v1\""), 400);

        error.GetProperty("code").GetString().Should().Be("ValidationError");
    }

    [Fact]
    public async Task AcceptCannotUseItemsFromAnotherProposal()
    {
        var user = new CareerClient(_factory);
        var first = await ProposalAsync(user);
        var second = await ProposalAsync(user);

        await CareerClient.ReadErrorAsync(
            await user.AcceptAsync(first.GetProperty("id").GetString()!, Ids(second, "currentTitle"), null), 400);
    }

    [Fact]
    public async Task AcceptedProposalCannotBeAcceptedOrDismissedAgain()
    {
        var user = new CareerClient(_factory);
        var proposal = await ProposalAsync(user);
        var id = proposal.GetProperty("id").GetString()!;
        await CareerClient.ReadDataAsync(await user.AcceptAsync(id, Ids(proposal, "currentTitle"), null), 200);

        await CareerClient.ReadErrorAsync(await user.AcceptAsync(id, Ids(proposal, "currentTitle"), "\"profile-v1\""), 409);
        await CareerClient.ReadErrorAsync(await user.SendAsync(HttpMethod.Post, $"/api/career/profile/proposals/{id}/dismiss"), 409);
    }

    [Fact]
    public async Task DismissMarksProposalAndLeavesProfileAlone()
    {
        var user = new CareerClient(_factory);
        await user.CreateProfileAsync();
        var proposal = await ProposalAsync(user);
        var id = proposal.GetProperty("id").GetString();

        var dismissed = await CareerClient.ReadDataAsync(
            await user.SendAsync(HttpMethod.Post, $"/api/career/profile/proposals/{id}/dismiss"), 200);

        dismissed.GetProperty("status").GetString().Should().Be("dismissed");
        (await CareerClient.ReadDataAsync(await user.GetAsync("/api/career/profile"), 200))
            .GetProperty("version").GetInt32().Should().Be(1);
        await CareerClient.ReadErrorAsync(
            await user.AcceptAsync(id!, Ids(proposal, "currentTitle"), "\"profile-v1\""), 409);
    }

    [Fact]
    public async Task AnotherOwnersProposalIs404ForGetAcceptAndDismiss()
    {
        var alice = new CareerClient(_factory);
        var bob = new CareerClient(_factory);
        var proposal = await ProposalAsync(alice);
        var id = proposal.GetProperty("id").GetString()!;

        await CareerClient.ReadErrorAsync(await bob.GetAsync($"/api/career/profile/proposals/{id}"), 404);
        await CareerClient.ReadErrorAsync(await bob.AcceptAsync(id, Ids(proposal, "currentTitle"), null), 404);
        await CareerClient.ReadErrorAsync(await bob.SendAsync(HttpMethod.Post, $"/api/career/profile/proposals/{id}/dismiss"), 404);
        await CareerClient.ReadErrorAsync(await bob.GetAsync("/api/career/profile"), 404);
    }

    [Fact]
    public async Task ProposalNeverChangesGoalsOrProfileUntilAccepted()
    {
        var user = new CareerClient(_factory);
        await ProposalAsync(user);

        (await user.GetAsync("/api/career/profile")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
