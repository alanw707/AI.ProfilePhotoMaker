using System.Net.Http.Json;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration;

public class RegisterUsernameCollisionIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public RegisterUsernameCollisionIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_AllowsSameEmailPrefixAcrossDomains()
    {
        // Previously usernames were derived from the email prefix, which caused collisions:
        // collision@example.com and collision@other.com would both attempt to create "collision" as UserName.
        var prefix = $"collision-{Guid.NewGuid():N}";

        var first = new RegisterDto(
            Email: $"{prefix}@example.com",
            Password: "Password123!",
            FirstName: "Test",
            LastName: "User",
            Gender: "male",
            Ethnicity: "asian",
            AgeConfirmed: true
        );

        var second = new RegisterDto(
            Email: $"{prefix}@other.com",
            Password: "Password123!",
            FirstName: "Test",
            LastName: "User",
            Gender: "male",
            Ethnicity: "asian",
            AgeConfirmed: true
        );

        var firstResponse = await _client.PostAsJsonAsync("/api/auth/register", first);
        firstResponse.EnsureSuccessStatusCode();

        var secondResponse = await _client.PostAsJsonAsync("/api/auth/register", second);
        secondResponse.EnsureSuccessStatusCode();
    }
}
