using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AI.ProfilePhotoMaker.API.Constants;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using Xunit;

namespace AI.ProfilePhotoMaker.API.Tests.Integration;

public class LoginAgeGateIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public LoginAgeGateIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_RequiresAgeConfirmation()
    {
        var payload = new LoginDto(
            Email: "test@example.com",
            Password: "Password123!",
            AgeConfirmed: false
        );

        var response = await _client.PostAsJsonAsync("/api/auth/login", payload);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("isSuccess", out var isSuccess));
        Assert.False(isSuccess.GetBoolean());
        Assert.True(root.TryGetProperty("message", out var message));
        Assert.Equal(AuthValidationMessages.AgeConfirmationRequiredToSignIn, message.GetString());
    }
}
