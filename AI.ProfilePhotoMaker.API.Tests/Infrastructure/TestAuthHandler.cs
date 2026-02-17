using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Linq;
using System;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AI.ProfilePhotoMaker.API.Tests.Infrastructure;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestAuth";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder)
    { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.TryGetValue("X-Test-Unauthenticated", out var unauthenticated)
            && string.Equals(unauthenticated.ToString(), "true", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail("Unauthenticated test request"));
        }

        var userId = Request.Headers.TryGetValue("X-Test-UserId", out var headerUserId)
            ? headerUserId.ToString()
            : "test-user-1";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, "Test User")
        };

        if (Request.Headers.TryGetValue("X-Test-Roles", out var rolesValue))
        {
            var roles = rolesValue.ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var roleClaims = roles.Select(role => new Claim(ClaimTypes.Role, role));
            claims = claims.Concat(roleClaims).ToArray();
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
