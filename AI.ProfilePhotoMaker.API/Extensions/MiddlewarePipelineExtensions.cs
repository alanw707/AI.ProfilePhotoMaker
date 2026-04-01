using AI.ProfilePhotoMaker.API.Middleware;
using AspNetCoreRateLimit;

namespace AI.ProfilePhotoMaker.API.Extensions;

/// <summary>
/// Extension methods for configuring the HTTP middleware pipeline
/// </summary>
public static class MiddlewarePipelineExtensions
{
    /// <summary>
    /// Configure the middleware pipeline ordering for the application
    /// </summary>
    public static WebApplication UseMiddlewarePipeline(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseResponseCompression();
        // Storage proxy: enabled in dev or when configured for blob proxying
        if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Storage:ProxyBlobRequests"))
        {
            app.UseMiddleware<EnhancedStorageProxyMiddleware>();
        }
        // Inject JWT from secure cookie into Authorization header if present
        app.UseMiddleware<JwtCookieAuthMiddleware>();

        app.Use(async (context, next) =>
        {
            var blockSearchEngines = app.Configuration.GetValue<bool>("SearchEngineBlocking:Enabled", true);
            if (blockSearchEngines && !app.Environment.IsDevelopment())
            {
                context.Response.Headers.Append("X-Robots-Tag", "noindex, nofollow, noarchive, nosnippet");
            }
            await next();
        });

        app.UseSession();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "AIProfileMaker v1"));
        }
        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        var corsPolicy = app.Environment.IsDevelopment() ? "AllowDevelopment" : "V1Production";
        app.Logger.LogInformation($"🌐 CORS Policy Selected: {corsPolicy} (Environment: {app.Environment.EnvironmentName})");
        if (app.Environment.IsDevelopment())
        {
            app.Use(async (context, next) =>
            {
                var isOptionsRequest = context.Request.Method == "OPTIONS";
                var hasOrigin = context.Request.Headers.ContainsKey("Origin");
                if (isOptionsRequest || hasOrigin)
                {
                    app.Logger.LogDebug($"🌐 CORS Request: {context.Request.Method} {context.Request.Path} | Origin: {context.Request.Headers.Origin} | Environment: {app.Environment.EnvironmentName}");
                }
                await next();
                if (isOptionsRequest || hasOrigin)
                {
                    var responseHeaders = string.Join(", ", context.Response.Headers.Where(h => h.Key.StartsWith("Access-Control")).Select(h => $"{h.Key}: {h.Value}"));
                    app.Logger.LogDebug($"🌐 CORS Response Headers: {(string.IsNullOrEmpty(responseHeaders) ? "NONE" : responseHeaders)}");
                }
            });
        }
        app.UseCors(corsPolicy);
        if (!app.Environment.IsDevelopment())
        {
            app.UseIpRateLimiting();
        }
        // app.UsePerformanceMonitoring(); // Removed monitoring middleware
        app.UseAuthentication();
        app.UseAuthorization();

        // Map health endpoints for liveness/readiness checks
        app.UseHealthCheckEndpoints();

        return app;
    }
}
