using FluentAssertions;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration;

/// <summary>
/// The token-less email confirmation helper exists only for local environments
/// (Development, LocalDev). Any other environment must not expose it.
/// </summary>
public class DevConfirmEmailEnvironmentTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DevConfirmEmailEnvironmentTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DevConfirmEmailIsNotFoundOutsideLocalEnvironments()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsync("/api/auth/dev/confirm-email", content: null);

        ((int)response.StatusCode).Should().Be(404);
    }
}
