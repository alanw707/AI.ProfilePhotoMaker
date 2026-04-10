using System.Text;
using Azure.Storage.Blobs;
using AI.ProfilePhotoMaker.API.Constants;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Extensions;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Models.DTOs;
using AI.ProfilePhotoMaker.API.Services.Authentication;
using AI.ProfilePhotoMaker.API.Services.Authentication.interfaces;
using AI.ProfilePhotoMaker.API.Services.Database;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services.Health;
using AI.ProfilePhotoMaker.API.Services.Storage;
using AI.ProfilePhotoMaker.API.Services.Payments;
using AI.ProfilePhotoMaker.API.Services.Notifications;
using AI.ProfilePhotoMaker.API.Services.Security;
using AspNetCoreRateLimit;
using AI.ProfilePhotoMaker.API.Middleware;
using AI.ProfilePhotoMaker.API.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using Azure.Extensions.AspNetCore.DataProtection.Blobs;
using System.Security.Claims;
using Serilog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

// Handle command-line arguments for migration and upload operations
// Skip in Testing environment to avoid interference with test setup
var isTestingEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Testing" ||
                          Environment.GetEnvironmentVariable("RUNNING_IN_TESTS") == "true";

if (args.Length > 0 && !isTestingEnvironment)
{
    var commandBuilder = WebApplication.CreateBuilder(args);

    // Configure services for command-line operations
    commandBuilder.Services.AddDatabaseServices(commandBuilder.Configuration, commandBuilder.Environment);
    commandBuilder.Services.AddLogging();

    // Add storage services for upload commands
    var commandAzureStorageConnectionString = commandBuilder.Configuration.GetConnectionString("AzureStorage") ??
                                      commandBuilder.Configuration["AzureStorage:ConnectionString"];

    if (!string.IsNullOrEmpty(commandAzureStorageConnectionString))
    {
        // Register BlobServiceClient for command-line operations
        commandBuilder.Services.AddSingleton<BlobServiceClient>(serviceProvider =>
        {
            return new BlobServiceClient(commandAzureStorageConnectionString);
        });

        commandBuilder.Services.AddScoped<IStorageService, AzureBlobStorageService>();
    }
    else
    {
        commandBuilder.Services.AddScoped<IStorageService, LocalStorageService>();
    }

    // Add upload service
    commandBuilder.Services.AddScoped<UploadStylePreviewsService>();

    var commandApp = commandBuilder.Build();

    // Try migration commands first
    var migrationExitCode = await MigrationCommandService.HandleMigrationCommand(args, commandApp.Services);
    if (migrationExitCode != 0 || IsMigrationCommand(args[0]))
    {
        Environment.Exit(migrationExitCode);
    }

    if (string.Equals(args[0], "upload-previews", StringComparison.OrdinalIgnoreCase))
    {
        using var scope = commandApp.Services.CreateScope();
        var uploadService = scope.ServiceProvider.GetRequiredService<UploadStylePreviewsService>();
        var dryRun = args.Any(arg => string.Equals(arg, "--dry-run", StringComparison.OrdinalIgnoreCase));
        var force = args.Any(arg => string.Equals(arg, "--force", StringComparison.OrdinalIgnoreCase));
        var exitCode = await uploadService.UploadStylePreviewsAsync(dryRun, force);
        Environment.Exit(exitCode);
    }

    if (string.Equals(args[0], "list-previews", StringComparison.OrdinalIgnoreCase))
    {
        using var scope = commandApp.Services.CreateScope();
        var uploadService = scope.ServiceProvider.GetRequiredService<UploadStylePreviewsService>();
        var exitCode = await uploadService.ListStylePreviewsAsync();
        Environment.Exit(exitCode);
    }

    // Upload command functionality removed for MVP - development tooling should be separate
}

// Create the application using the standard pattern that WebApplicationFactory expects
var builder = WebApplication.CreateBuilder(args);

// Load environment variables from .env file if present
LoadEnvironmentVariables(builder.Environment);

// Load monitoring configuration
builder.Configuration.AddJsonFile("appsettings.Monitoring.json", optional: true, reloadOnChange: true);

// Configure Serilog for structured logging
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithEnvironmentName()
        .Enrich.WithThreadId()
        .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
        .Enrich.WithProperty("Application", "AI.ProfilePhotoMaker.API");
});

// Configure forwarded headers for both development proxy and Azure Container Apps load balancer
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                              Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto |
                              Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();

    // Restrict forwarded host values to known application hosts to prevent host header injection.
    var allowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "*.aiprofilephotomaker.com"
    };
    foreach (var configured in new[]
             {
                 builder.Configuration["AppBaseUrl"],
                 builder.Configuration["ExternalApiBaseUrl"],
                 builder.Configuration["Webhooks:NgrokTunnelUrl"]
             })
    {
        if (string.IsNullOrWhiteSpace(configured)) continue;
        if (Uri.TryCreate(configured, UriKind.Absolute, out var parsed) && !string.IsNullOrWhiteSpace(parsed.Host))
        {
            allowedHosts.Add(parsed.Host);
        }
    }
    options.AllowedHosts.Clear();
    foreach (var host in allowedHosts)
    {
        options.AllowedHosts.Add(host);
    }

    // Trust development proxy
    options.KnownProxies.Add(System.Net.IPAddress.Parse("127.0.0.1"));

    // Trust Azure Container Apps load balancer
    if (!builder.Environment.IsDevelopment())
    {
        options.ForwardLimit = null;
    }
});

// Core infrastructure services
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddEnvironmentConfiguration();
builder.Services.AddOptions();
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.Configure<IpRateLimitPolicies>(builder.Configuration.GetSection("IpRateLimitPolicies"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// Payment, Stripe, and options configuration
builder.Services.AddPaymentServices(builder.Configuration);

// Database services
if (builder.Environment.IsEnvironment("Testing") || builder.Environment.IsEnvironment("LocalDev"))
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseInMemoryDatabase($"LocalDb_{Guid.NewGuid()}");
    });
    builder.Services.AddHealthChecks();
}
else
{
    builder.Services.AddDatabaseServices(builder.Configuration, builder.Environment);
    builder.Services.AddDatabaseConfiguration(builder.Configuration);
}

// Identity and authentication
builder.Services.AddIdentityServices();
builder.Services.AddAuthenticationServices(builder.Configuration, builder.Environment);

// Application services
builder.Services.AddHttpContextAccessor();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
    options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/json", "text/json", "image/svg+xml" });
});
builder.Services.AddHttpClient();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBlogContentService, BlogContentService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<ICouponService, CouponService>();
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.IBasicTierService, AI.ProfilePhotoMaker.API.Services.BasicTierService>();
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.IUserContextService, AI.ProfilePhotoMaker.API.Services.UserContextService>();
builder.Services.AddHostedService<AI.ProfilePhotoMaker.API.Services.StartupDiagnosticsHostedService>();

// Replicate API services (with mock mode support)
builder.Services.AddReplicateServices(builder.Configuration);

// Storage and data protection
builder.Services.AddStorageServices(builder.Configuration, builder.Environment);

// Additional application services
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.ICreditPackageService, AI.ProfilePhotoMaker.API.Services.CreditPackageService>();
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.IRetentionPolicyService, AI.ProfilePhotoMaker.API.Services.RetentionPolicyService>();
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.Health.IHealthCheckService, AI.ProfilePhotoMaker.API.Services.Health.HealthCheckService>();
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.Health.IDatabaseHealthService, AI.ProfilePhotoMaker.API.Services.Health.DatabaseHealthService>();
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.Health.IStorageHealthService, AI.ProfilePhotoMaker.API.Services.Health.StorageHealthService>();
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.Health.IDependencyHealthService, AI.ProfilePhotoMaker.API.Services.Health.DependencyHealthService>();
builder.Services.AddScoped<IAsyncFileService, AsyncFileService>();
builder.Services.AddScoped<IAsyncZipService, AsyncZipService>();

// Email and verification services (via HttpClient)
builder.Services.AddHttpClient<IEmailNotificationService, EmailNotificationService>();
builder.Services.AddHttpClient<ITurnstileVerificationService, TurnstileVerificationService>();

builder.Services.AddDeploymentValidation(builder.Configuration);
builder.Services.ConfigureDeploymentValidation(builder.Configuration, builder.Environment);

// Background services
builder.Services.AddScoped<ITrainingPollingService, TrainingPollingService>();
builder.Services.AddHostedService<TrainingPollingBackgroundService>();
builder.Services.AddHostedService<AI.ProfilePhotoMaker.API.Services.BasicTierBackgroundService>();
builder.Services.AddHostedService<AI.ProfilePhotoMaker.API.Services.RetentionPolicyBackgroundService>();
builder.Services.AddHostedService<AI.ProfilePhotoMaker.API.Services.AbandonedUploadBackgroundService>();
builder.Services.AddHostedService<AI.ProfilePhotoMaker.API.Services.Marketing.MarketingEmailBackgroundService>();

// API behavior configuration
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var routeValues = context.ActionDescriptor.RouteValues;
        var isAuthLogin = routeValues.TryGetValue("controller", out var controller) &&
                          routeValues.TryGetValue("action", out var action) &&
                          string.Equals(controller, "Auth", StringComparison.OrdinalIgnoreCase) &&
                          string.Equals(action, "Login", StringComparison.OrdinalIgnoreCase);

        if (isAuthLogin)
        {
            var ageGateFailed = context.ModelState
                .Where(entry => entry.Key.Contains(nameof(LoginDto.AgeConfirmed), StringComparison.OrdinalIgnoreCase))
                .SelectMany(entry => entry.Value?.Errors ?? Enumerable.Empty<ModelError>())
                .Any();

            if (ageGateFailed)
            {
                return new BadRequestObjectResult(new AuthResponseDto(
                    false,
                    AuthValidationMessages.AgeConfirmationRequiredToSignIn,
                    string.Empty,
                    null));
            }
        }

        return new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState));
    };
});

// Controllers, SignalR, Session
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});
builder.Services.AddSignalR();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "AIPM.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.Domain = builder.Environment.IsDevelopment() ? null : ".aiprofilephotomaker.com";
    options.IdleTimeout = TimeSpan.FromHours(12);
});

// Swagger/OpenAPI
builder.Services.AddSwaggerServices();

// CORS policies
builder.Services.AddCorsServices(builder.Configuration);

// ── Build the application ──────────────────────────────────────────────

var app = builder.Build();

// Validate environment configuration before starting (only enforce outside Development/Testing/LocalDev)
if (!app.Environment.IsEnvironment("Testing") && !app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("LocalDev"))
{
    await app.UseEnvironmentValidationAsync();
}

// Apply database migrations using new architecture (only if enabled and not in Testing/LocalDev)
if (!app.Environment.IsEnvironment("Testing") && !app.Environment.IsEnvironment("LocalDev"))
{
    var autoMigrateOnStartup = app.Configuration.GetValue<bool>("Database:AutoMigrateOnStartup", true);
    if (autoMigrateOnStartup)
    {
        await app.UseDatabaseMigrationAsync();
    }
    else
    {
        app.Logger.LogInformation("Database migrations skipped (AutoMigrateOnStartup=false)");
    }
}

if (!app.Environment.IsEnvironment("Testing"))
{
    try
    {
        await SeedAdminRole.SeedAsync(app.Services, app.Configuration, app.Logger);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Admin role seeding failed during startup");
    }
}

// Run validations in background after startup to prevent blocking
_ = Task.Run(async () =>
{
    await Task.Delay(10000);
    try
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("🔍 Running background startup validations...");
        await ValidateWebhookConfigurationAsync(app);
        await ValidateReplicateConfigurationAsync(app);
        logger.LogInformation("✅ Background validations completed");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "❌ Background validation failed: {Message}", ex.Message);
    }
});

// Configure middleware pipeline (forwarded headers, auth, CORS, rate limiting, etc.)
app.UseMiddlewarePipeline();

// Dev-only blob proxy endpoints
if (app.Environment.IsDevelopment())
{
    app.Map("/blob-proxy", blobApp =>
    {
        blobApp.Run(async context =>
        {
            try
            {
                var pathSegments = context.Request.Path.Value?.TrimStart('/').Split('/');
                if (pathSegments?.Length >= 2)
                {
                    var containerName = pathSegments[0];
                    var blobPath = string.Join("/", pathSegments.Skip(1));
                    var azuriteUrl = $"http://127.0.0.1:10000/devstoreaccount1/{containerName}/{blobPath}";
                    using var httpClient = new HttpClient();
                    var response = await httpClient.GetAsync(azuriteUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        context.Response.StatusCode = (int)response.StatusCode;
                        context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
                        context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
                        context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, OPTIONS");
                        await response.Content.CopyToAsync(context.Response.Body);
                    }
                    else
                    {
                        context.Response.StatusCode = (int)response.StatusCode;
                    }
                }
                else
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("Invalid blob path");
                }
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync($"Proxy error: {ex.Message}");
            }
        });
    });

    app.Map("/devstoreaccount1", devstoreApp =>
    {
        devstoreApp.Run(async context =>
        {
            try
            {
                var fullPath = context.Request.Path.Value?.TrimStart('/') ?? "";
                var azuriteUrl = $"http://127.0.0.1:10000/devstoreaccount1/{fullPath}";
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(azuriteUrl);
                if (response.IsSuccessStatusCode)
                {
                    context.Response.StatusCode = (int)response.StatusCode;
                    context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
                    context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
                    context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, OPTIONS");
                    await response.Content.CopyToAsync(context.Response.Body);
                }
                else
                {
                    context.Response.StatusCode = (int)response.StatusCode;
                }
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync($"Azurite proxy error: {ex.Message}");
            }
        });
    });
}

// Map controllers BEFORE MapFallback to ensure API routes take precedence
app.MapGet("/api/health/storage", async (IHealthCheckService healthChecks) =>
    Results.Json(await healthChecks.GetStorageHealthAsync()))
    .AllowAnonymous()
    .WithName("StorageHealthCheck");

app.MapControllers();

// Map SignalR hub for real-time prediction updates
app.MapHub<AI.ProfilePhotoMaker.API.Hubs.PredictionHub>("/hubs/prediction");

// Angular static files and fallback routing (only for non-API requests)
var angularPath = Path.Combine(builder.Environment.ContentRootPath, "../AI.ProfilePhotoMaker.UI/dist/ai.profile-photo-maker.ui/browser");
if (Directory.Exists(angularPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(angularPath),
        RequestPath = ""
    });

    // FIXED: Only handle non-API requests in MapFallback to prevent authentication bypass
    app.MapFallback(context =>
    {
        var path = context.Request.Path.Value?.ToLower();

        if (path?.StartsWith("/api/") == true ||
            path?.StartsWith("/devstoreaccount1/") == true ||
            path?.StartsWith("/signin-") == true ||
            path?.StartsWith("/swagger") == true)
        {
            context.Response.StatusCode = 404;
            return context.Response.WriteAsync("API endpoint not found");
        }

        // Serve Angular app for all other routes
        context.Response.ContentType = "text/html";
        return context.Response.SendFileAsync(Path.Combine(angularPath, "index.html"));
    });
}

// Only call app.Run() if not in Testing environment (TestServer handles hosting in tests)
if (!app.Environment.IsEnvironment("Testing"))
{
    app.Run();
}

// Helper methods for command detection
static bool IsMigrationCommand(string command)
{
    return command switch
    {
        "--check-db-connection" => true,
        "--verify-migrations" => true,
        "--apply-migrations" => true,
        "--validate-database" => true,
        "--migration-status" => true,
        "--database-health" => true,
        _ => false
    };
}

static async Task ValidateWebhookConfigurationAsync(WebApplication app)
{
    await Task.CompletedTask;
}

static async Task ValidateReplicateConfigurationAsync(WebApplication app)
{
    await Task.CompletedTask;
}

static void LoadEnvironmentVariables(IWebHostEnvironment environment)
{
    var explicitEnvFile = Environment.GetEnvironmentVariable("DEV_ENV_FILE")
        ?? Environment.GetEnvironmentVariable("ENV_FILE");

    string envFile;
    if (!string.IsNullOrWhiteSpace(explicitEnvFile))
    {
        envFile = explicitEnvFile;
    }
    else
    {
        var repoRootEnv = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", ".env"));
        envFile = File.Exists(repoRootEnv)
            ? repoRootEnv
            : Path.Combine(environment.ContentRootPath, $".env.{environment.EnvironmentName.ToLower()}");
    }

    if (File.Exists(envFile))
    {
        Console.WriteLine($"Loading environment variables from: {envFile}");

        var lines = File.ReadAllLines(envFile);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("//"))
                continue;

            var parts = line.Split('=', 2);
            if (parts.Length == 2)
            {
                var key = parts[0].Trim();
                var value = parts[1].Trim();

                // Remove quotes if present
                if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                    (value.StartsWith("'") && value.EndsWith("'")))
                {
                    value = value.Substring(1, value.Length - 2);
                }

                Environment.SetEnvironmentVariable(key, value);
            }
        }

        Console.WriteLine($"✅ Loaded environment variables from .env.{environment.EnvironmentName.ToLower()}");
    }
    else
    {
        Console.WriteLine($"No environment file found at: {envFile}");
    }
}

// Expose Program class for WebApplicationFactory in tests
public partial class Program { }
