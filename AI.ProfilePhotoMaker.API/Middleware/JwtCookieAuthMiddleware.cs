using Microsoft.Extensions.Options;

namespace AI.ProfilePhotoMaker.API.Middleware;

public class JwtCookieAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<JwtCookieAuthMiddleware> _logger;
    private readonly string _cookieName;

    public JwtCookieAuthMiddleware(RequestDelegate next, ILogger<JwtCookieAuthMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _cookieName = configuration["Authentication:TokenCookieName"] ?? "AuthToken";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // If Authorization header missing, but JWT cookie exists, inject it as a Bearer token
        if (!context.Request.Headers.ContainsKey("Authorization") && context.Request.Cookies.TryGetValue(_cookieName, out var token) && !string.IsNullOrWhiteSpace(token))
        {
            context.Request.Headers["Authorization"] = $"Bearer {token}";
        }

        await _next(context);
    }
}

