using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration;

public class ExternalLoginAgeGateIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ExternalLoginAgeGateIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task ExternalLogin_RequiresAgeConfirmation()
    {
        var response = await _client.GetAsync("/api/auth/external-login/google");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var location = response.Headers.Location!.ToString();
        Assert.Contains("/auth/login", location);
        Assert.Contains("age_confirmation_required", location);
    }
}
