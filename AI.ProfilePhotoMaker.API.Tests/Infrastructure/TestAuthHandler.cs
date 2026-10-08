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

        // Tests are signed in "now" unless they ask otherwise: X-Test-AuthAgeMinutes sets how long ago
        // (iat), and X-Test-AuthTimeAgeMinutes sets an auth_time claim, which takes precedence over iat.
        var issuedAge = Request.Headers.TryGetValue("X-Test-AuthAgeMinutes", out var ageValue)
            ? double.Parse(ageValue.ToString(), System.Globalization.CultureInfo.InvariantCulture) : 0;
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, "Test User")
        };
        if (Request.Headers.TryGetValue("X-Test-Email", out var email))
        {
            claims.Add(new Claim(ClaimTypes.Email, email.ToString()));
        }
        if (!Request.Headers.ContainsKey("X-Test-NoIat"))
        {
            claims.Add(new Claim("iat", DateTimeOffset.UtcNow.AddMinutes(-issuedAge).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        if (Request.Headers.TryGetValue("X-Test-AuthTimeAgeMinutes", out var authAge))
        {
            claims.Add(new Claim("auth_time", DateTimeOffset.UtcNow.AddMinutes(-double.Parse(authAge.ToString(), System.Globalization.CultureInfo.InvariantCulture)).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        if (Request.Headers.TryGetValue("X-Test-Roles", out var rolesValue))
        {
            var roles = rolesValue.ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var roleClaims = roles.Select(role => new Claim(ClaimTypes.Role, role));
            claims.AddRange(roleClaims);
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
