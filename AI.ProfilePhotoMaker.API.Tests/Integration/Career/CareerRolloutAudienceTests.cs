using System.Net;
using System.Security.Claims;
using System.Text.Json;
using AI.ProfilePhotoMaker.API.Services.Career;
using AI.ProfilePhotoMaker.API.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration.Career;

/// <summary>Staged rollout: the flag on with an allowlist audience opens career only to listed accounts.</summary>
public class CareerAllowlistFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Features:CareerWorkspace"] = "true",
                ["Features:CareerWorkspaceAudience"] = "Allowlist",
                ["Features:CareerWorkspaceAllowedEmails"] = "owner@example.test; Beta@Example.test"
            }));
    }
}

public class CareerRolloutAudienceTests : IClassFixture<CareerAllowlistFactory>, IClassFixture<CareerWorkspaceEnabledFactory>, IClassFixture<CareerWorkspaceDisabledFactory>
{
    private readonly CareerAllowlistFactory _allowlist;
    private readonly CareerWorkspaceEnabledFactory _everyone;
    private readonly CareerWorkspaceDisabledFactory _off;

    public CareerRolloutAudienceTests(CareerAllowlistFactory allowlist, CareerWorkspaceEnabledFactory everyone, CareerWorkspaceDisabledFactory off)
    {
        _allowlist = allowlist;
        _everyone = everyone;
        _off = off;
    }

    private static CareerClient User(CustomWebApplicationFactory f, string? email) =>
        new(f, headers: email == null ? null : new Dictionary<string, string> { ["X-Test-Email"] = email });

    private static async Task<bool> PublicFlag(CustomWebApplicationFactory f)
    {
        var json = JsonDocument.Parse(await f.CreateClient().GetStringAsync("/api/config/client"));
        return json.RootElement.GetProperty("data").GetProperty("features").GetProperty("careerWorkspace").GetBoolean();
    }

    private static async Task<bool> Access(CareerClient user)
    {
        var response = await user.GetAsync("/api/config/career-access");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await CareerClient.ReadDataAsync(response, 200)).GetProperty("enabled").GetBoolean();
    }

    [Theory]
    [InlineData("owner@example.test")]
    [InlineData("beta@example.test")]
    public async Task ListedAccountsUseCareerCaseInsensitively(string email)
    {
        var user = User(_allowlist, email);
        (await user.GetAsync("/api/career/journey")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Access(user)).Should().BeTrue();
    }

    [Theory]
    [InlineData("someone@example.test")]
    [InlineData(null)]
    public async Task UnlistedAccountsGetTheDisabledAnswer(string? email)
    {
        var user = User(_allowlist, email);
        var error = await CareerClient.ReadErrorAsync(await user.GetAsync("/api/career/journey"), 403);
        error.GetProperty("code").GetString().Should().Be("CareerWorkspaceDisabled");
        (await Access(user)).Should().BeFalse();
    }

    [Fact]
    public async Task PublicConfigStaysOffDuringAnAllowlistRollout() => (await PublicFlag(_allowlist)).Should().BeFalse();

    [Fact]
    public async Task EveryoneAudienceIsTheDefaultWhenTheFlagIsOn()
    {
        (await PublicFlag(_everyone)).Should().BeTrue();
        (await Access(User(_everyone, null))).Should().BeTrue();
    }

    [Fact]
    public async Task FlagOffClosesCareerForListedAccountsToo()
    {
        (await PublicFlag(_off)).Should().BeFalse();
        (await Access(User(_off, "owner@example.test"))).Should().BeFalse();
    }

    [Fact]
    public async Task CareerAccessNeedsASignedInUser()
    {
        var anon = _allowlist.CreateAuthenticatedClient();
        anon.DefaultRequestHeaders.Add("X-Test-Unauthenticated", "true");
        (await anon.GetAsync("/api/config/career-access")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

public class CareerFeatureGateAudienceTests
{
    private static CareerFeatureGate Gate(params (string Key, string Value)[] settings) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => (string?)s.Value)).Build());

    private static ClaimsPrincipal Principal(string? email) =>
        new(new ClaimsIdentity(email == null ? [] : [new Claim(ClaimTypes.Email, email)], "test"));

    [Fact]
    public void AnUnknownAudienceFailsClosed() =>
        Gate(("Features:CareerWorkspace", "true"), ("Features:CareerWorkspaceAudience", "Staff"))
            .IsEnabledFor(Principal("owner@example.test")).Should().BeFalse();

    [Fact]
    public void AnEmptyAllowlistOpensForNobody() =>
        Gate(("Features:CareerWorkspace", "true"), ("Features:CareerWorkspaceAudience", "Allowlist"))
            .IsEnabledFor(Principal("owner@example.test")).Should().BeFalse();

    [Fact]
    public void TheSystemStaysOnDuringAnAllowlistRolloutSoTheWorkerRuns() =>
        Gate(("Features:CareerWorkspace", "true"), ("Features:CareerWorkspaceAudience", "Allowlist")).IsEnabled.Should().BeTrue();
}
