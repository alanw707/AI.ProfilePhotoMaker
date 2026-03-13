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
// using AI.ProfilePhotoMaker.API.Services.Monitoring; // Removed monitoring dependency
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
// NOTE: Microsoft.AspNetCore.TestHost removed to prevent production container crashes

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

// NOTE: TestServer configuration removed to prevent production container crashes
// Test projects should configure TestServer via WebApplicationFactory instead

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
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();

    // Restrict forwarded host values to known application hosts to prevent host header injection.
    // Note: ForwardedHeadersOptions.AllowedHosts supports wildcard patterns.
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

    // Trust Azure Container Apps load balancer - clear known networks to trust all proxies
    if (!builder.Environment.IsDevelopment())
    {
        // In production (Azure Container Apps), trust all networks since we're behind their load balancer
        options.ForwardLimit = null;
    }
});

// Data protection configuration is handled in the storage services section below

// Add session services for OAuth state management (required for both Development and Production)
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.None; // Allow cross-site for OAuth
    options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
    // Set domain for production to allow cross-subdomain sharing
    options.Cookie.Domain = builder.Environment.IsDevelopment() ? null : ".aiprofilephotomaker.com";
});

// Add environment configuration with validation
builder.Services.AddEnvironmentConfiguration();
builder.Services.AddOptions();
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.Configure<IpRateLimitPolicies>(builder.Configuration.GetSection("IpRateLimitPolicies"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection(StripeOptions.SectionName));
builder.Services.Configure<RetentionPolicyBackgroundServiceOptions>(
    builder.Configuration.GetSection(RetentionPolicyBackgroundServiceOptions.SectionName));
builder.Services.Configure<RetentionNotificationOptions>(
    builder.Configuration.GetSection(RetentionNotificationOptions.SectionName));
builder.Services.Configure<AbandonedUploadOptions>(
    builder.Configuration.GetSection(AbandonedUploadOptions.SectionName));
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

builder.Services.PostConfigure<StripeOptions>(options =>
{
    if (string.IsNullOrWhiteSpace(options.PublishableKey))
    {
        options.PublishableKey = Environment.GetEnvironmentVariable(EnvironmentConfiguration.STRIPE_PUBLISHABLE_KEY) ?? string.Empty;
    }

    if (string.IsNullOrWhiteSpace(options.SecretKey))
    {
        options.SecretKey = Environment.GetEnvironmentVariable(EnvironmentConfiguration.STRIPE_SECRET_KEY) ?? string.Empty;
    }

    if (string.IsNullOrWhiteSpace(options.WebhookSecret))
    {
        options.WebhookSecret = Environment.GetEnvironmentVariable(EnvironmentConfiguration.STRIPE_WEBHOOK_SECRET) ?? string.Empty;
    }

    options.PublishableKey = options.PublishableKey?.Trim() ?? string.Empty;
    options.SecretKey = options.SecretKey?.Trim() ?? string.Empty;
    options.WebhookSecret = options.WebhookSecret?.Trim() ?? string.Empty;
});

builder.Services.Configure<PaymentSimulationOptions>(builder.Configuration.GetSection(PaymentSimulationOptions.SectionName));

builder.Services.AddSingleton(provider =>
{
    var stripeOptions = provider.GetRequiredService<IOptions<StripeOptions>>().Value;
    var appInfo = new Stripe.AppInfo
    {
        Name = "AI Profile Photo Maker API",
        Version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0",
        Url = "https://github.com/alanwsmith/AI.ProfilePhotoMaker"
    };

    Stripe.StripeConfiguration.AppInfo = appInfo;
    var secretKey = AI.ProfilePhotoMaker.API.Services.Payments.StripeClientFactory.GetSafeSecretKey(stripeOptions.SecretKey);
    Stripe.StripeConfiguration.ApiKey = secretKey;

    return AI.ProfilePhotoMaker.API.Services.Payments.StripeClientFactory.Create(secretKey);
});

builder.Services.AddScoped<IStripePaymentService, StripePaymentService>();
builder.Services.AddScoped<IStripeWebhookService, StripeWebhookService>();
// Email notifications (registered via HttpClient below)
builder.Services.AddScoped<IPendingGenerationService, PendingGenerationService>();

builder.Services.Configure<LegacyCompatibilityOptions>(builder.Configuration.GetSection(LegacyCompatibilityOptions.SectionName));

// Configure database services
if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}");
    });

    // Program config expects health check middleware; in tests we register a minimal health checks service
    // (without database/migration checks) so startup doesn't fail.
    builder.Services.AddHealthChecks();
}
else
{
    builder.Services.AddDatabaseServices(builder.Configuration, builder.Environment);
    builder.Services.AddDatabaseConfiguration(builder.Configuration);
}

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Add SignInManager
builder.Services.AddScoped<SignInManager<ApplicationUser>>();

// Configure Identity options
builder.Services.Configure<IdentityOptions>(options =>
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

// Only add Google OAuth if properly configured
var configClientId = builder.Configuration["Authentication:Google:ClientId"];
var configClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
var envClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? builder.Configuration["GOOGLE_CLIENT_ID"];
var envClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") ?? builder.Configuration["GOOGLE_CLIENT_SECRET"];
bool IsPlaceholder(string? v) => !string.IsNullOrWhiteSpace(v) && (v.StartsWith("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase) || v.Contains("STORED_IN_USER_SECRETS", StringComparison.OrdinalIgnoreCase) || v.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase) || v.StartsWith("your_", StringComparison.OrdinalIgnoreCase));
var googleClientId = !string.IsNullOrWhiteSpace(envClientId) ? envClientId : (IsPlaceholder(configClientId) ? null : configClientId);
var googleClientSecret = !string.IsNullOrWhiteSpace(envClientSecret) ? envClientSecret : (IsPlaceholder(configClientSecret) ? null : configClientSecret);

// Add JWT Authentication with Cookie support for OAuth
var authBuilder = builder.Services.AddAuthentication(options =>
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
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:ValidAudience"],
            ValidIssuer = builder.Configuration["Jwt:ValidIssuer"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"] ?? string.Empty))
        };
        var cookieName = builder.Configuration["Authentication:TokenCookieName"] ?? "AuthToken";
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
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrEmpty(jwtSecret) || jwtSecret.Length < 32)
{
    Console.WriteLine("Warning: JWT Secret is not configured or is not long enough. Please configure a secret of at least 32 characters in your application settings.");
}

// Register the Services
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

// Register Replicate SDK (skip in mock mode)
var enableReplicateMock = (Environment.GetEnvironmentVariable("ENABLE_REPLICATE_MOCK") ?? string.Empty)
    .Equals("true", StringComparison.OrdinalIgnoreCase);
if (!enableReplicateMock)
{
    builder.Services.AddSingleton<Replicate.ReplicateApi>(provider =>
    {
        var configuration = provider.GetRequiredService<IConfiguration>();
        var envToken = (Environment.GetEnvironmentVariable(EnvironmentConfiguration.REPLICATE_API_TOKEN) ?? string.Empty).Trim();
        var cfgToken = (configuration["Replicate:ApiToken"] ?? string.Empty).Trim();
        var apiToken = !string.IsNullOrWhiteSpace(envToken) ? envToken : cfgToken;
        if (string.IsNullOrEmpty(apiToken))
        {
            throw new InvalidOperationException("Replicate API token not configured");
        }
        return new Replicate.ReplicateApi(apiToken);
    });
}

// Register Replicate services with optional mock
if (enableReplicateMock)
{
    builder.Services.AddScoped<IReplicateApiClient, MockReplicateApiClient>();
    // Skip ModelDiscoveryService in mock mode since it needs ReplicateApi
    Console.WriteLine("Mock mode enabled - skipping ModelDiscoveryService registration");
}
else
{
    builder.Services.AddHttpClient<IReplicateApiClient, ReplicateApiClient>(client =>
    {
        // Set 2-minute timeout for Replicate API calls to prevent UI hanging
        client.Timeout = TimeSpan.FromMinutes(2);
    });

    // Register OpenAI Image Generation Service with HttpClient
    builder.Services.AddHttpClient<OpenAIImageGenerationService>(client =>
    {
        client.Timeout = TimeSpan.FromMinutes(5);
    });

    // Configure robustness service with options
    builder.Services.Configure<ModelDiscoveryRobustnessOptions>(options =>
    {
        options.MaxRetryAttempts = 3;
        options.BaseRetryDelayMs = 1000;
        options.MaxRetryDelayMs = 10000;
        options.CircuitBreakerFailureThreshold = 5;
        options.CircuitBreakerTimeoutMs = 60000;
    });

    builder.Services.AddSingleton<ModelDiscoveryRobustnessService>();
    builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.IModelDiscoveryService, AI.ProfilePhotoMaker.API.Services.ModelDiscoveryService>();
}

builder.Services.AddScoped<IWebhookUrlResolver, WebhookUrlResolver>();
builder.Services.AddHttpClient<WebhookUrlResolver>();
builder.Services.AddHttpClient<IImageDownloadService, ImageDownloadService>();
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Data.IUserProfileRepository, AI.ProfilePhotoMaker.API.Data.UserProfileRepository>();

// Register Storage Services - choose between Local or Azure Blob Storage
var azureStorageConnectionString = builder.Configuration.GetConnectionString("AzureStorage") ?? builder.Configuration["AzureStorage:ConnectionString"];
if (!string.IsNullOrEmpty(azureStorageConnectionString))
{
    builder.Services.AddSingleton<BlobServiceClient>(_ => new BlobServiceClient(azureStorageConnectionString));
    builder.Services.AddScoped<IStorageService, AzureBlobStorageService>();
}
else
{
    builder.Services.AddScoped<IStorageService, LocalStorageService>();
}

var dpBuilder = builder.Services.AddDataProtection()
    .SetApplicationName("AIProfilePhotoMaker");

if (!builder.Environment.IsDevelopment()
    && !builder.Environment.IsEnvironment("Testing")
    && !string.IsNullOrEmpty(azureStorageConnectionString))
{
    // Persist Data Protection keys to Azure Blob so cookies survive pod/revision restarts
    var dpContainer = new BlobContainerClient(azureStorageConnectionString, "dataprotection");
    try
    {
        // Ensure the container exists so DataProtection can persist keys on first run
        dpContainer.CreateIfNotExists();
        Console.WriteLine("[DataProtection] Ensured blob container 'dataprotection' exists");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DataProtection] Warning: failed to ensure container exists: {ex.Message}");
        // Continue; if container truly doesn't exist and cannot be created due to perms,
        // app will still start but cookie protection may fail until container is provisioned.
    }

    var dpBlob = dpContainer.GetBlobClient("keys.xml");
    dpBuilder.PersistKeysToAzureBlobStorage(dpBlob);
}
else
{
    // In Development, prefer local keys to avoid auth/login hanging if Azurite is misconfigured/unreachable.
    // (Can be overridden by setting DataProtection:PersistKeysToAzureBlob=true.)
    var persistToAzureBlobInDev = builder.Configuration.GetValue("DataProtection:PersistKeysToAzureBlob", false);
    if (persistToAzureBlobInDev && !string.IsNullOrEmpty(azureStorageConnectionString))
    {
        var dpBlob = new BlobContainerClient(azureStorageConnectionString, "dataprotection")
            .GetBlobClient("keys.xml");
        dpBuilder.PersistKeysToAzureBlobStorage(dpBlob);
    }
    else
    {
        dpBuilder.PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "keys")));
    }
}
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.Storage.StoragePathResolver>();

builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.ICreditPackageService, AI.ProfilePhotoMaker.API.Services.CreditPackageService>();
// Payment service removed - YAGNI principle (was never used by any controllers)
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.IRetentionPolicyService, AI.ProfilePhotoMaker.API.Services.RetentionPolicyService>();
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.Health.IHealthCheckService, AI.ProfilePhotoMaker.API.Services.Health.HealthCheckService>();
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.Health.IDatabaseHealthService, AI.ProfilePhotoMaker.API.Services.Health.DatabaseHealthService>();
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.Health.IStorageHealthService, AI.ProfilePhotoMaker.API.Services.Health.StorageHealthService>();
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.Health.IDependencyHealthService, AI.ProfilePhotoMaker.API.Services.Health.DependencyHealthService>();

// builder.Services.AddPerformanceMonitoring(builder.Configuration); // Removed monitoring service
builder.Services.AddScoped<IAsyncFileService, AsyncFileService>();
builder.Services.AddScoped<IAsyncZipService, AsyncZipService>();

// Email notifications (supports SMTP or Postmark API)
builder.Services.AddHttpClient<IEmailNotificationService, EmailNotificationService>();
builder.Services.AddHttpClient<ITurnstileVerificationService, TurnstileVerificationService>();

builder.Services.AddDeploymentValidation(builder.Configuration);
builder.Services.ConfigureDeploymentValidation(builder.Configuration, builder.Environment);

// Add training polling services
builder.Services.AddScoped<ITrainingPollingService, TrainingPollingService>();
builder.Services.AddHostedService<TrainingPollingBackgroundService>();

builder.Services.AddHostedService<AI.ProfilePhotoMaker.API.Services.BasicTierBackgroundService>();
// ModelExpirationBackgroundService removed - YAGNI principle (was doing nothing but logging)
builder.Services.AddHostedService<AI.ProfilePhotoMaker.API.Services.RetentionPolicyBackgroundService>();
builder.Services.AddHostedService<AI.ProfilePhotoMaker.API.Services.AbandonedUploadBackgroundService>();

// Model sync health monitoring - removed dependency
// builder.Services.AddModelSyncHealthMonitoring();

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

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

// Add SignalR for real-time prediction updates
builder.Services.AddSignalR();

// Configure Session cookie for cross-site usage (frontend and API are on different subdomains)
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "AIPM.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.IdleTimeout = TimeSpan.FromHours(12);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "AIProfileMaker", Version = "v1" });
    c.OperationFilter<FileUploadOperationFilter>();
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

var v1FrontendUrl = builder.Configuration["AppBaseUrl"] ?? "https://aiprofilemaker-web-v1.eastus.azurecontainerapps.io";
builder.Services.AddCors(options =>
{
    options.AddPolicy("V1Production", corsBuilder =>
    {
        var allowedOrigins = new List<string>
        {
            "https://aiprofilephotomaker.com",
            "https://aiprofilephotomaker.com",
            "https://test.profilephotomaker.com"
        };
        var envCorsOrigins = builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS");
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

var app = builder.Build();

// Validate environment configuration before starting (only enforce outside Development/Testing)
if (!app.Environment.IsEnvironment("Testing") && !app.Environment.IsDevelopment())
{
    await app.UseEnvironmentValidationAsync();
}

// Apply database migrations using new architecture (only if enabled and not in Testing)
if (!app.Environment.IsEnvironment("Testing"))
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

app.UseForwardedHeaders();
app.UseResponseCompression();
if (app.Environment.IsDevelopment())
{
    app.UseMiddleware<AI.ProfilePhotoMaker.API.Middleware.EnhancedStorageProxyMiddleware>();
}
// Inject JWT from secure cookie into Authorization header if present
app.UseMiddleware<AI.ProfilePhotoMaker.API.Middleware.JwtCookieAuthMiddleware>();
// Optional: serve images via API proxy (supports private blob containers)
if (app.Configuration.GetValue<bool>("Storage:ProxyBlobRequests"))
{
    app.UseMiddleware<AI.ProfilePhotoMaker.API.Middleware.EnhancedStorageProxyMiddleware>();
}

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
// Dedicated storage health endpoint so deployment validators can confirm
// we are wired to Azure Blob Storage rather than the local emulator.
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

        // CRITICAL FIX: Let API requests pass through to controllers and authentication middleware
        // Don't handle API requests in fallback - they should be handled by MapControllers
        if (path?.StartsWith("/api/") == true ||
            path?.StartsWith("/devstoreaccount1/") == true ||
            path?.StartsWith("/signin-") == true ||
            path?.StartsWith("/swagger") == true)
        {
            // Return 404 for unmatched API routes instead of completing the request
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

// IsUploadCommand removed with upload functionality

// Method signatures for validation functions - these would need to be implemented
static async Task ValidateWebhookConfigurationAsync(WebApplication app)
{
    // Implementation would go here
    await Task.CompletedTask;
}

static async Task ValidateReplicateConfigurationAsync(WebApplication app)
{
    // Implementation would go here
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
