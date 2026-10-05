using System.Net;
using FluentAssertions;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration.Career;

/// <summary>
/// With <c>Features:CareerWorkspace</c> unset (the default everywhere), the server
/// refuses every career endpoint and leaves existing photo/account routes alone.
/// </summary>
public class CareerWorkspaceFlagOffTests : IClassFixture<CustomWebApplicationFactory>, IClassFixture<CareerWorkspaceEnabledFactory>
{
    private readonly CustomWebApplicationFactory _flagOff;
    private readonly CareerWorkspaceEnabledFactory _flagOn;

    public CareerWorkspaceFlagOffTests(CustomWebApplicationFactory flagOff, CareerWorkspaceEnabledFactory flagOn)
    {
        _flagOff = flagOff;
        _flagOn = flagOn;
    }

    public static TheoryData<string, string> CareerEndpoints => new()
    {
        { "GET", "/api/career/profile" },
        { "GET", "/api/career/journey" },
        { "GET", "/api/career/allowance" },
        { "PUT", "/api/career/profile" },
        { "GET", "/api/career/profile/versions" },
        { "GET", "/api/career/profile/versions/1" },
        { "POST", "/api/career/profile/versions/1/restore" },
        { "GET", "/api/career/goals" },
        { "POST", "/api/career/goals" },
        { "PATCH", $"/api/career/goals/{Guid.Empty}" },
        { "GET", $"/api/career/goals/{Guid.Empty}/versions" },
        { "GET", $"/api/career/goals/{Guid.Empty}/versions/1" },
        { "POST", $"/api/career/goals/{Guid.Empty}/versions/1/restore" },
        { "POST", "/api/career/resumes" },
        { "GET", "/api/career/resumes" },
        { "GET", $"/api/career/resumes/{Guid.Empty}" },
        { "GET", $"/api/career/resumes/{Guid.Empty}/file" },
        { "DELETE", $"/api/career/resumes/{Guid.Empty}" },
        { "POST", "/api/career/profile/proposals" },
        { "GET", $"/api/career/profile/proposals/{Guid.Empty}" },
        { "POST", $"/api/career/profile/proposals/{Guid.Empty}/accept" },
        { "POST", $"/api/career/profile/proposals/{Guid.Empty}/dismiss" },
        { "GET", "/api/career/photos" },
        { "PUT", "/api/career/photos/selection" },
        { "DELETE", "/api/career/photos/selection" },
        { "POST", "/api/career/runs" },
        { "GET", "/api/career/runs" },
        { "GET", $"/api/career/runs/{Guid.Empty}" },
        { "POST", $"/api/career/runs/{Guid.Empty}/answers" },
        { "POST", $"/api/career/runs/{Guid.Empty}/cancel" },
        { "GET", $"/api/career/occupation-matches/{Guid.Empty}" },
        { "POST", $"/api/career/occupation-matches/{Guid.Empty}/confirm" },
        { "POST", $"/api/career/occupation-matches/{Guid.Empty}/dismiss" },
        { "GET", "/api/career/occupations/reference" },
        { "GET", "/api/career/market-briefs" },
        { "GET", $"/api/career/market-briefs/{Guid.Empty}" },
        { "GET", "/api/career/market/reference" },
        { "GET", "/api/career/markets/metrics" },
        { "GET", "/api/career/markets/compare?metric=median_wage&level=state" },
        { "POST", "/api/career/markets/preference" },
        { "GET", "/api/career/jobs/observations" },
        { "GET", "/api/career/jobs/source" },
        { "GET", "/api/career/pay-analyses" },
        { "GET", $"/api/career/pay-analyses/{Guid.Empty}" },
        { "POST", $"/api/career/pay-analyses/{Guid.Empty}/recompute" },
        { "GET", "/api/career/pay/qualification" },
        { "GET", "/api/career/roadmaps" },
        { "GET", $"/api/career/roadmaps/{Guid.Empty}" },
        { "POST", $"/api/career/roadmaps/{Guid.Empty}/accept" },
        { "POST", $"/api/career/roadmaps/{Guid.Empty}/dismiss" },
        { "PUT", $"/api/career/roadmaps/{Guid.Empty}/tasks/t1" },
        { "GET", $"/api/career/roadmaps/{Guid.Empty}/progress" },
        { "PUT", $"/api/career/roadmaps/{Guid.Empty}/progress/t1" },
        { "POST", $"/api/career/roadmaps/{Guid.Empty}/tasks" },
        { "POST", $"/api/career/roadmaps/{Guid.Empty}/replan" },
        { "GET", $"/api/career/replans/{Guid.Empty}" },
        { "POST", $"/api/career/replans/{Guid.Empty}/apply" },
        { "POST", $"/api/career/replans/{Guid.Empty}/reject" },
        { "GET", "/api/career/materials?kind=resume" },
        { "GET", $"/api/career/materials/{Guid.Empty}" },
        { "PUT", $"/api/career/materials/{Guid.Empty}" },
        { "GET", $"/api/career/materials/{Guid.Empty}/versions" },
        { "GET", $"/api/career/materials/{Guid.Empty}/versions/1" },
        { "POST", $"/api/career/materials/{Guid.Empty}/versions/1/restore" },
        { "GET", $"/api/career/materials/{Guid.Empty}/proposals/{Guid.Empty}" },
        { "POST", $"/api/career/materials/{Guid.Empty}/proposals/{Guid.Empty}/apply" },
        { "POST", $"/api/career/materials/{Guid.Empty}/proposals/{Guid.Empty}/reject" },
        { "GET", "/api/career/materials?kind=summary" },
        { "POST", $"/api/career/materials/{Guid.Empty}/exports" },
        { "GET", $"/api/career/materials/{Guid.Empty}/exports" },
        { "GET", $"/api/career/exports/{Guid.Empty}" },
        { "GET", "/api/career/privacy/retention" },
        { "GET", "/api/career/privacy/export" },
        { "POST", "/api/career/privacy/deletions" },
        { "GET", $"/api/career/privacy/deletions/{Guid.Empty}" },
        { "POST", $"/api/career/privacy/deletions/{Guid.Empty}/retry" },
    };

    [Theory]
    [MemberData(nameof(CareerEndpoints))]
    public async Task CareerEndpointsReturn403WithStableCodeWhenFlagIsOff(string method, string path)
    {
        var user = new CareerClient(_flagOff);
        var body = method is "PUT" or "POST" or "PATCH" ? CareerClient.ValidProfile() : null;

        var response = await user.SendAsync(new HttpMethod(method), path, body);

        var error = await CareerClient.ReadErrorAsync(response, 403);
        error.GetProperty("code").GetString().Should().Be("CareerWorkspaceDisabled");
    }

    [Fact]
    public async Task FlagOffWritesNothing()
    {
        var userId = $"career-user-{Guid.NewGuid():N}";
        await new CareerClient(_flagOff, userId).PutProfileAsync(CareerClient.ValidProfile());

        using var scope = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateScope(_flagOff.Services);
        var db = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<AI.ProfilePhotoMaker.API.Data.ApplicationDbContext>(scope.ServiceProvider);
        db.CareerProfiles.Any(p => p.OwnerId == userId).Should().BeFalse();
    }

    [Fact]
    public async Task ClientConfigAdvertisesTheFlag()
    {
        var off = await CareerClient.ReadDataAsync(await _flagOff.CreateAuthenticatedClient().GetAsync("/api/config/client"), 200);
        var on = await CareerClient.ReadDataAsync(await _flagOn.CreateAuthenticatedClient().GetAsync("/api/config/client"), 200);

        off.GetProperty("features").GetProperty("careerWorkspace").GetBoolean().Should().BeFalse();
        on.GetProperty("features").GetProperty("careerWorkspace").GetBoolean().Should().BeTrue();
    }

    /// <summary>
    /// Existing account, profile, photo workflow, generation, gallery and payment
    /// routes. Statuses are pinned to what they returned on commit 757c4027, before
    /// any career backend existed, and must be identical with the flag on or off.
    /// </summary>
    public static TheoryData<string, string, int> ExistingRoutes => new()
    {
        { "GET", "/api/auth/validate-session", 204 },
        { "GET", "/api/auth/account-status", 401 }, // no Identity user row in the test host
        { "GET", "/api/profile", 200 },
        { "GET", "/api/profile/data-stats", 200 },
        { "GET", "/api/credit/status", 200 },
        { "GET", "/api/credit/packages", 200 },
        { "GET", "/api/credit/history", 200 },
        { "GET", "/api/profilephotoworkflow/packages", 200 },
        { "GET", "/api/profilephotoworkflow/entitlements", 200 },
        { "GET", "/api/headshots/resumable-preview", 200 },
        { "GET", "/api/profile/styles", 200 },
        { "GET", "/api/credit/costs", 200 },
        { "GET", "/api/profilephotoworkflow/export-options", 200 },
    };

    [Theory]
    [MemberData(nameof(ExistingRoutes))]
    public async Task ExistingRoutesAreUnchangedByTheFlag(string method, string path, int pinnedStatus)
    {
        var offStatus = await StatusOf(_flagOff, method, path);
        var onStatus = await StatusOf(_flagOn, method, path);

        offStatus.Should().Be(pinnedStatus, $"{method} {path} with the career flag off");
        onStatus.Should().Be(offStatus, $"{method} {path} must not depend on the career flag");
    }

    private static async Task<int> StatusOf(CustomWebApplicationFactory factory, string method, string path)
    {
        await factory.EnsureTestUserWithCreditsAsync();
        var client = factory.CreateAuthenticatedClient();
        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
        return (int)response.StatusCode;
    }
}
