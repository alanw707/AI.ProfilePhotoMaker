namespace AI.ProfilePhotoMaker.API.Extensions;

/// <summary>
/// Extension methods for CORS service configuration
/// </summary>
public static class CorsServiceExtensions
{
    /// <summary>
    /// Add CORS services to the service collection with production, development, and debug policies
    /// </summary>
    public static IServiceCollection AddCorsServices(this IServiceCollection services, IConfiguration configuration)
    {
        var v1FrontendUrl = configuration["AppBaseUrl"] ?? "https://aiprofilemaker-web-v1.eastus.azurecontainerapps.io";

        services.AddCors(options =>
        {
            options.AddPolicy("V1Production", corsBuilder =>
            {
                var allowedOrigins = new List<string>
                {
                    "https://aiprofilephotomaker.com",
                    "https://test.profilephotomaker.com"
                };

                var envCorsOrigins = configuration["CORS_ALLOWED_ORIGINS"] ?? Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS");
                if (!string.IsNullOrEmpty(envCorsOrigins))
                {
                    var envOrigins = envCorsOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(o => o.Trim()).Where(o => !string.IsNullOrEmpty(o));
                    allowedOrigins.AddRange(envOrigins);
                }

                if (!string.IsNullOrEmpty(v1FrontendUrl))
                {
                    allowedOrigins.Add(v1FrontendUrl);
                }

                allowedOrigins.Add("https://aiprofilemaker-web-v1.eastus.azurecontainerapps.io");
                allowedOrigins.Add("https://aipm-web-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io");

                var finalOrigins = allowedOrigins.Distinct().ToArray();

                corsBuilder.WithOrigins(finalOrigins).AllowAnyMethod().AllowAnyHeader().AllowCredentials().SetPreflightMaxAge(TimeSpan.FromMinutes(10));
            });

            options.AddPolicy("AllowDevelopment", corsBuilder =>
            {
                corsBuilder.WithOrigins(
                    "http://localhost:4200",
                    "https://localhost:4200",
                    "https://clear-anteater-usually.ngrok-free.app"
                ).AllowAnyMethod().AllowAnyHeader().AllowCredentials();
            });

            options.AddPolicy("DebugAllowAll", corsBuilder =>
            {
                corsBuilder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
            });
        });

        return services;
    }
}
