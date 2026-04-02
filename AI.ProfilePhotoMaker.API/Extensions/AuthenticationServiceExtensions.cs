using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;

namespace AI.ProfilePhotoMaker.API.Extensions;

/// <summary>
/// Extension methods for authentication and identity service configuration
/// </summary>
public static class AuthenticationServiceExtensions
{
    /// <summary>
    /// Add ASP.NET Core Identity services with default password, lockout, and user policies
    /// </summary>
    public static IServiceCollection AddIdentityServices(this IServiceCollection services)
    {
        // Add Identity
        services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        // Add SignInManager
        services.AddScoped<SignInManager<ApplicationUser>>();

        // Configure Identity options
        services.Configure<IdentityOptions>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequiredLength = 8;
            options.Password.RequiredUniqueChars = 1;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;
            options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
            options.User.RequireUniqueEmail = true;
        });

        return services;
    }

    /// <summary>
    /// Add authentication services including JWT bearer, cookie, and optional Google OAuth
    /// </summary>
    public static IServiceCollection AddAuthenticationServices(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        // Only add Google OAuth if properly configured
        var configClientId = configuration["Authentication:Google:ClientId"];
        var configClientSecret = configuration["Authentication:Google:ClientSecret"];
        var envClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? configuration["GOOGLE_CLIENT_ID"];
        var envClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") ?? configuration["GOOGLE_CLIENT_SECRET"];
        var googleClientId = !string.IsNullOrWhiteSpace(envClientId) ? envClientId : (IsPlaceholder(configClientId) ? null : configClientId);
        var googleClientSecret = !string.IsNullOrWhiteSpace(envClientSecret) ? envClientSecret : (IsPlaceholder(configClientSecret) ? null : configClientSecret);

        // Add JWT Authentication with Cookie support for OAuth
        var authBuilder = services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.LoginPath = "/login";
                options.LogoutPath = "/logout";
                options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                options.SlidingExpiration = true;
                options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
                options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
            })
            .AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.RequireHttpsMetadata = !environment.IsDevelopment();
                options.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidAudience = configuration["Jwt:ValidAudience"],
                    ValidIssuer = configuration["Jwt:ValidIssuer"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Secret"] ?? string.Empty))
                };
                var cookieName = configuration["Authentication:TokenCookieName"] ?? "AuthToken";
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // If no Authorization header, try JWT from HttpOnly cookie
                        if (!context.Request.Headers.ContainsKey("Authorization") &&
                            context.Request.Cookies.TryGetValue(cookieName, out var jwt) &&
                            !string.IsNullOrWhiteSpace(jwt))
                        {
                            context.Token = jwt;
                        }
                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = 401;
                        context.Response.ContentType = "application/json";
                        var response = new { success = false, error = new { code = "Unauthorized", message = "Authentication required. Please provide a valid JWT token." } };
                        return context.Response.WriteAsJsonAsync(response);
                    },
                    OnAuthenticationFailed = context =>
                    {
                        if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                        {
                            context.Response.Headers.Append("Token-Expired", "true");
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
        {
            authBuilder.AddGoogle(options =>
            {
                options.ClientId = googleClientId;
                options.ClientSecret = googleClientSecret;
                options.CallbackPath = "/signin-google";
                options.SaveTokens = true;
                options.Scope.Add("email");
                options.Scope.Add("profile");
            });
        }

        // Validate JWT Secret
        var jwtSecret = configuration["Jwt:Secret"];
        if (string.IsNullOrEmpty(jwtSecret) || jwtSecret.Length < 32)
        {
            Console.WriteLine("Warning: JWT Secret is not configured or is not long enough. Please configure a secret of at least 32 characters in your application settings.");
        }

        return services;
    }

    /// <summary>
    /// Determines whether a configuration value is a placeholder that should be ignored
    /// </summary>
    private static bool IsPlaceholder(string? v) =>
        !string.IsNullOrWhiteSpace(v) &&
        (v.StartsWith("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase) ||
         v.Contains("STORED_IN_USER_SECRETS", StringComparison.OrdinalIgnoreCase) ||
         v.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase) ||
         v.StartsWith("your_", StringComparison.OrdinalIgnoreCase));
}
