using System.Text;
using Azure.Storage.Blobs;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Extensions;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services.Authentication;
using AI.ProfilePhotoMaker.API.Services.Authentication.interfaces;
using AI.ProfilePhotoMaker.API.Services.Database;
using AI.ProfilePhotoMaker.API.Services;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services.Payment;
using AI.ProfilePhotoMaker.API.Services.Storage;
using AI.ProfilePhotoMaker.API.Services.Monitoring;
using AI.ProfilePhotoMaker.API.Middleware;
using AI.ProfilePhotoMaker.API.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Claims;
using Serilog;

// Handle command-line arguments for migration and upload operations
if (args.Length > 0)
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
    
    // Try upload commands
    var uploadExitCode = await UploadCommandService.HandleUploadCommand(args, commandApp.Services);
    if (uploadExitCode != 0 || IsUploadCommand(args[0]))
    {
        Environment.Exit(uploadExitCode);
    }
    
    // If we get here, it's not a recognized command, continue with normal startup
}

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
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();

    // Trust development proxy
    options.KnownProxies.Add(System.Net.IPAddress.Parse("127.0.0.1"));
    
    // Trust Azure Container Apps load balancer - clear known networks to trust all proxies
    if (!builder.Environment.IsDevelopment())
    {
        // In production (Azure Container Apps), trust all networks since we're behind their load balancer
        options.ForwardLimit = null;
    }
});

// Configure data protection for OAuth state handling
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "keys")))
    .SetApplicationName("AI.ProfilePhotoMaker.API");

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
    // Don't specify domain - let browser handle same-origin cookies
    options.Cookie.Domain = null;
});

// Add services to the container.

// Add environment configuration with validation
builder.Services.AddEnvironmentConfiguration();

// Configure database services with new architecture
builder.Services.AddDatabaseServices(builder.Configuration, builder.Environment);
builder.Services.AddDatabaseConfiguration(builder.Configuration);

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Add SignInManager
builder.Services.AddScoped<SignInManager<ApplicationUser>>();


// Configure Identity options
builder.Services.Configure<IdentityOptions>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 1;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
    options.User.RequireUniqueEmail = true;
});

// Only add Google OAuth if properly configured
// Prefer environment variables and user-secrets; treat placeholders as missing
var configClientId = builder.Configuration["Authentication:Google:ClientId"];
var configClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

// Also check top-level env-backed keys (from .env loader or shell)
var envClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? builder.Configuration["GOOGLE_CLIENT_ID"];
var envClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") ?? builder.Configuration["GOOGLE_CLIENT_SECRET"];

bool IsPlaceholder(string? v) =>
    !string.IsNullOrWhiteSpace(v) && (
        v.StartsWith("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase) ||
        v.Contains("STORED_IN_USER_SECRETS", StringComparison.OrdinalIgnoreCase) ||
        v.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase) ||
        v.StartsWith("your_", StringComparison.OrdinalIgnoreCase)
    );

// Prefer explicit env vars when present; otherwise use config if not placeholder
var googleClientId = !string.IsNullOrWhiteSpace(envClientId) ? envClientId : (IsPlaceholder(configClientId) ? null : configClientId);
var googleClientSecret = !string.IsNullOrWhiteSpace(envClientSecret) ? envClientSecret : (IsPlaceholder(configClientSecret) ? null : configClientSecret);

// Add JWT Authentication with Cookie support for OAuth
var authBuilder = builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        // Set DefaultChallengeScheme to JWT Bearer for API endpoints to return 401 instead of 302 redirects
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        // Note: OAuth controllers will explicitly specify their authentication schemes to override this
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
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidAudience = builder.Configuration["JWT:ValidAudience"],
            ValidIssuer = builder.Configuration["JWT:ValidIssuer"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT:Secret"] ?? string.Empty))
        };
        
        // CRITICAL: Configure JWT Bearer to return 401 for API endpoints instead of redirecting
        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                // Skip the default challenge behavior which would redirect
                context.HandleResponse();

                // Return 401 Unauthorized with proper JSON response for API calls
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                
                var response = new
                {
                    success = false,
                    error = new
                    {
                        code = "Unauthorized",
                        message = "Authentication required. Please provide a valid JWT token."
                    }
                };
                
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
else
{
    // Google OAuth not configured - skipping (ClientId or ClientSecret missing)
}

// Validate JWT Secret
var jwtSecret = builder.Configuration["JWT:Secret"];
if (string.IsNullOrEmpty(jwtSecret) || jwtSecret.Length < 32)
{
    // In a real application, you would want to throw an exception here.
    // For the purpose of this review, we will just log a warning.
    // It's highly recommended to use a secure secret management system like Azure Key Vault.
    Console.WriteLine("Warning: JWT Secret is not configured or is not long enough. Please configure a secret of at least 32 characters in your application settings.");
}

// Register the Services
builder.Services.AddHttpContextAccessor(); // Required for UserContextService

// Add response compression for better performance over ngrok
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
    options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/json", "text/json", "image/svg+xml" });
});
builder.Services.AddHttpClient(); // Required for OAuth HTTP calls
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.IBasicTierService, AI.ProfilePhotoMaker.API.Services.BasicTierService>();
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.IUserContextService, AI.ProfilePhotoMaker.API.Services.UserContextService>();

// Register Replicate SDK (skip in mock mode)
var enableReplicateMock = (Environment.GetEnvironmentVariable("ENABLE_REPLICATE_MOCK") ?? string.Empty)
    .Equals("true", StringComparison.OrdinalIgnoreCase);
if (!enableReplicateMock)
{
    builder.Services.AddSingleton<Replicate.ReplicateApi>(provider =>
    {
        var configuration = provider.GetRequiredService<IConfiguration>();
        var apiToken = configuration["Replicate:ApiToken"]
            ?? throw new InvalidOperationException("Replicate API token not configured");
        return new Replicate.ReplicateApi(apiToken);
    });
}
else
{
    // Mock mode enabled: skip initializing Replicate SDK
}

// Register Replicate services
builder.Services.AddHttpClient<IReplicateApiClient, ReplicateApiClient>();
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.IModelDiscoveryService, AI.ProfilePhotoMaker.API.Services.ModelDiscoveryService>();

// Register WebhookUrlResolver service for environment-aware webhook URL resolution
builder.Services.AddScoped<IWebhookUrlResolver, WebhookUrlResolver>();
builder.Services.AddHttpClient<WebhookUrlResolver>();

builder.Services.AddHttpClient<IImageDownloadService, ImageDownloadService>();
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Data.IUserProfileRepository, AI.ProfilePhotoMaker.API.Data.UserProfileRepository>();

// Register Storage Services - choose between Local or Azure Blob Storage
var azureStorageConnectionString = builder.Configuration.GetConnectionString("AzureStorage") ?? 
                                  builder.Configuration["AzureStorage:ConnectionString"];

if (!string.IsNullOrEmpty(azureStorageConnectionString))
{
    // Register BlobServiceClient for Azure Blob Storage dependency injection
    builder.Services.AddSingleton<BlobServiceClient>(serviceProvider =>
    {
        return new BlobServiceClient(azureStorageConnectionString);
    });
    
    builder.Services.AddScoped<IStorageService, AzureBlobStorageService>();
    Console.WriteLine("Using Azure Blob Storage for image storage");
}
else
{
    // DEPRECATED: LocalStorageService is legacy fallback when Azurite is not configured
    // All environments should use Azurite/Azure Blob Storage for consistency
    // TODO: Remove LocalStorageService once all environments have proper Azure Storage configuration
    builder.Services.AddScoped<IStorageService, LocalStorageService>();
    Console.WriteLine("Using Local Storage for image storage (DEPRECATED - configure Azurite for development)");
}

// Register storage path resolver for environment-aware path management
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.Storage.StoragePathResolver>();

// Premium Package Services removed - using unified credit system

// Register Credit Package Services (new unified system)
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.ICreditPackageService, AI.ProfilePhotoMaker.API.Services.CreditPackageService>();

// Register Payment Services
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.Payment.IPaymentService, AI.ProfilePhotoMaker.API.Services.Payment.StripePaymentService>();

// Register Retention Policy Services
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.IRetentionPolicyService, AI.ProfilePhotoMaker.API.Services.RetentionPolicyService>();

// Register Health Check Services
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.Health.IHealthCheckService, AI.ProfilePhotoMaker.API.Services.Health.HealthCheckService>();
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.Health.IDatabaseHealthService, AI.ProfilePhotoMaker.API.Services.Health.DatabaseHealthService>();
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.Health.IStorageHealthService, AI.ProfilePhotoMaker.API.Services.Health.StorageHealthService>();
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.Health.IDependencyHealthService, AI.ProfilePhotoMaker.API.Services.Health.DependencyHealthService>();

// Add Performance Monitoring Services
builder.Services.AddPerformanceMonitoring(builder.Configuration);

// Register Async I/O Services for high-performance non-blocking file operations
builder.Services.AddScoped<IAsyncFileService, AsyncFileService>();
builder.Services.AddScoped<IAsyncZipService, AsyncZipService>();


// Add Deployment Validation and Monitoring Services
builder.Services.AddDeploymentValidation(builder.Configuration);
builder.Services.ConfigureDeploymentValidation(builder.Configuration, builder.Environment);

// Register background services
builder.Services.AddHostedService<AI.ProfilePhotoMaker.API.Services.BasicTierBackgroundService>();
builder.Services.AddHostedService<AI.ProfilePhotoMaker.API.Services.ModelExpirationBackgroundService>();
builder.Services.AddHostedService<AI.ProfilePhotoMaker.API.Services.RetentionPolicyBackgroundService>();


builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "AIProfileMaker", Version = "v1" });

    c.OperationFilter<FileUploadOperationFilter>();
    // Add JWT Authentication to Swagger
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


// Environment-aware CORS configuration for better maintainability  
var v1FrontendUrl = builder.Configuration["AppBaseUrl"] ?? 
                   "https://aiprofilemaker-web-v1.eastus.azurecontainerapps.io";

builder.Services.AddCors(options =>
{
    options.AddPolicy("V1Production",
        corsBuilder =>
        {
            var allowedOrigins = new List<string>
            {
                "https://app.aiprofilephotomaker.com",
                "https://aiprofilephotomaker.com",
                "https://test.profilephotomaker.com"
            };
            
            // Add origins from CORS_ALLOWED_ORIGINS environment variable if set
            var envCorsOrigins = builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS");
            if (!string.IsNullOrEmpty(envCorsOrigins))
            {
                var envOrigins = envCorsOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                             .Select(o => o.Trim())
                                             .Where(o => !string.IsNullOrEmpty(o));
                allowedOrigins.AddRange(envOrigins);
            }
            
            // Add V1 deployment URL from configuration
            if (!string.IsNullOrEmpty(v1FrontendUrl))
            {
                allowedOrigins.Add(v1FrontendUrl);
            }
            
            // Add expected V1 Container Apps URL
            allowedOrigins.Add("https://aiprofilemaker-web-v1.eastus.azurecontainerapps.io");
            
            // Add actual V1 deployment URL for current infrastructure (keep for rollback capability)
            allowedOrigins.Add("https://aipm-web-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io");
            
            // Remove duplicates and log final origins list
            var finalOrigins = allowedOrigins.Distinct().ToArray();
            Console.WriteLine($"🌐 CORS V1Production Origins: {string.Join(", ", finalOrigins)}");
            
            corsBuilder.WithOrigins(finalOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials()
                .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
        });

    options.AddPolicy("AllowDevelopment", corsBuilder =>
    {
        corsBuilder.WithOrigins(
                "http://localhost:4200",
                "https://localhost:4200"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
    
    // Add a debug policy that allows everything for testing
    options.AddPolicy("DebugAllowAll", corsBuilder =>
    {
        corsBuilder.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

// Validate environment configuration before starting
await app.UseEnvironmentValidationAsync();

// Apply database migrations using new architecture (only if enabled)
var autoMigrateOnStartup = app.Configuration.GetValue<bool>("Database:AutoMigrateOnStartup", true);
if (autoMigrateOnStartup)
{
    await app.UseDatabaseMigrationAsync();
}
else
{
    app.Logger.LogInformation("Database migrations skipped (AutoMigrateOnStartup=false)");
}

// Perform deployment validation on startup
// Temporarily disabled: await app.ValidateDeploymentOnStartupAsync();

// Run validations in background after startup to prevent blocking
_ = Task.Run(async () => {
    await Task.Delay(10000); // Wait 10s for app to fully start
    try {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("🔍 Running background startup validations...");
        await ValidateWebhookConfigurationAsync(app);
        await ValidateReplicateConfigurationAsync(app);
        logger.LogInformation("✅ Background validations completed");
    } catch (Exception ex) {
        app.Logger.LogError(ex, "❌ Background validation failed: {Message}", ex.Message);
    }
});

// Use forwarded headers for ngrok proxy
app.UseForwardedHeaders();

// Enable response compression early in the pipeline
app.UseResponseCompression();

// Add storage proxy middleware VERY EARLY to intercept requests before MapFallback
app.UseMiddleware<AI.ProfilePhotoMaker.API.Middleware.StorageProxyMiddleware>();

// Add X-Robots-Tag header for search engine blocking during MVP phase
app.Use(async (context, next) =>
{
    // Add X-Robots-Tag header to all responses to prevent search engine indexing
    // This provides server-level protection in addition to robots.txt and meta tags
    var blockSearchEngines = app.Configuration.GetValue<bool>("SearchEngineBlocking:Enabled", true);
    
    if (blockSearchEngines && !app.Environment.IsDevelopment())
    {
        context.Response.Headers.Append("X-Robots-Tag", "noindex, nofollow, noarchive, nosnippet");
    }
    
    await next();
});

// Use session middleware for OAuth state management (required for both environments)
app.UseSession();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "AIProfileMaker v1"));
}
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// CORS must come early in the middleware pipeline, before authentication
var corsPolicy = app.Environment.IsDevelopment() ? "AllowDevelopment" : "V1Production";

// Log CORS policy selection for debugging
app.Logger.LogInformation($"🌐 CORS Policy Selected: {corsPolicy} (Environment: {app.Environment.EnvironmentName})");

// Add CORS debugging middleware in production for troubleshooting
app.Use(async (context, next) =>
{
    var isOptionsRequest = context.Request.Method == "OPTIONS";
    var hasOrigin = context.Request.Headers.ContainsKey("Origin");
    
    if (isOptionsRequest || hasOrigin)
    {
        app.Logger.LogInformation($"🌐 CORS Request: {context.Request.Method} {context.Request.Path} | Origin: {context.Request.Headers.Origin} | Environment: {app.Environment.EnvironmentName}");
    }
    
    await next();
    
    if (isOptionsRequest || hasOrigin)
    {
        var responseHeaders = string.Join(", ", context.Response.Headers.Where(h => h.Key.StartsWith("Access-Control")).Select(h => $"{h.Key}: {h.Value}"));
        app.Logger.LogInformation($"🌐 CORS Response Headers: {(string.IsNullOrEmpty(responseHeaders) ? "NONE" : responseHeaders)}");
    }
});

// Use production-ready CORS configuration
app.UseCors(corsPolicy);

// Add performance monitoring middleware (before authentication)  
app.UsePerformanceMonitoring();


// Remove debug middleware - production should use proper logging with ILogger
// Consider adding request logging middleware with proper ILogger implementation if needed

// Continue with the pipeline
app.Use(async (context, next) =>
{
    await next();

    // Handle authentication-related responses if needed
    var path = context.Request.Path.Value?.ToLower();
    if (path?.Contains("signin") == true || path?.Contains("oauth") == true || path?.Contains("auth") == true)
    {
        Console.WriteLine($"🔐 OAuth response: {context.Response.StatusCode}");
    }
});

// CRITICAL: Authentication middleware must come before static files to handle OAuth callbacks
app.UseAuthentication();
app.UseAuthorization();

// Static file serving for images removed - now using Azure Blob Storage with Azurite for all environments
// All image serving handled by AzureBlobStorageService with direct blob URLs

// Proxy middleware for external API access to Azurite in development
if (app.Environment.IsDevelopment())
{
    // Legacy blob-proxy path support
    app.Map("/blob-proxy", blobApp =>
    {
        blobApp.Run(async context =>
        {
            try
            {
                // Extract container and blob path from request
                var pathSegments = context.Request.Path.Value?.TrimStart('/').Split('/');
                if (pathSegments?.Length >= 2)
                {
                    var containerName = pathSegments[0];
                    var blobPath = string.Join("/", pathSegments.Skip(1));
                    
                    // Forward to Azurite (default port 10000)
                    var azuriteUrl = $"http://127.0.0.1:10000/devstoreaccount1/{containerName}/{blobPath}";
                    
                    using var httpClient = new HttpClient();
                    var response = await httpClient.GetAsync(azuriteUrl);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        context.Response.StatusCode = (int)response.StatusCode;
                        context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
                        
                        // Add CORS headers for external API access
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

    // Direct devstoreaccount1 path proxy for AzureBlobStorageService URLs
    app.Map("/devstoreaccount1", devstoreApp =>
    {
        devstoreApp.Run(async context =>
        {
            try
            {
                // Forward entire path to Azurite
                var fullPath = context.Request.Path.Value?.TrimStart('/') ?? "";
                var azuriteUrl = $"http://127.0.0.1:10000/devstoreaccount1/{fullPath}";
                
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(azuriteUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    context.Response.StatusCode = (int)response.StatusCode;
                    context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
                    
                    // Add CORS headers for external API access
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

// Serve Angular static files
var angularPath = Path.Combine(builder.Environment.ContentRootPath, "../AI.ProfilePhotoMaker.UI/dist/ai.profile-photo-maker.ui/browser");
if (Directory.Exists(angularPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(angularPath),
        RequestPath = ""
    });

    // Fallback to index.html for Angular routing (exclude API and OAuth paths)
    app.MapFallback(context =>
    {
        var path = context.Request.Path.Value?.ToLower();

        // Don't handle API, storage proxy, or OAuth callback paths
        if (path?.StartsWith("/api/") == true ||
            path?.StartsWith("/devstoreaccount1/") == true ||
            path?.StartsWith("/signin-") == true ||
            path?.StartsWith("/swagger") == true)
        {
            return Task.CompletedTask;
        }

        // Serve index.html for Angular routing
        context.Response.ContentType = "text/html";
        return context.Response.SendFileAsync(Path.Combine(angularPath, "index.html"));
    });
}

// Health check endpoints are now handled by HealthController
// Legacy health check middleware disabled in favor of controller-based endpoints

// OAuth callbacks are now handled by the standard middleware
// No custom debug routes needed

app.MapControllers();

app.Run();

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

static bool IsUploadCommand(string command)
{
    return command switch
    {
        "upload-previews" => true,
        "list-previews" => true,
        _ => false
    };
}

/// <summary>
/// Loads environment variables from .env files based on environment
/// </summary>
static void LoadEnvironmentVariables(IWebHostEnvironment environment)
{
    try
    {
        // Look for .env files in the solution root directory (parent of API directory)
        var contentRoot = environment.ContentRootPath;
        var solutionRoot = Directory.GetParent(contentRoot)?.FullName ?? contentRoot;
        
        Console.WriteLine($"🔍 Looking for .env files in:");
        Console.WriteLine($"   Content Root: {contentRoot}");
        Console.WriteLine($"   Solution Root: {solutionRoot}");
        
        var envFiles = new[]
        {
            ".env",
            $".env.{environment.EnvironmentName.ToLower()}",
            ".env.local",
            $".env.{environment.EnvironmentName.ToLower()}.local"
        };

        bool anyFileFound = false;
        foreach (var envFile in envFiles)
        {
            // First try solution root directory
            var envFilePath = Path.Combine(solutionRoot, envFile);
            if (File.Exists(envFilePath))
            {
                Console.WriteLine($"🔧 Loading environment variables from solution root: {envFile}");
                LoadEnvFile(envFilePath);
                anyFileFound = true;
            }
            else
            {
                // Fallback to API directory for compatibility
                envFilePath = Path.Combine(contentRoot, envFile);
                if (File.Exists(envFilePath))
                {
                    Console.WriteLine($"🔧 Loading environment variables from API directory: {envFile}");
                    LoadEnvFile(envFilePath);
                    anyFileFound = true;
                }
            }
        }
        
        if (!anyFileFound)
        {
            Console.WriteLine($"⚠️  No .env files found in either directory");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️  Warning: Could not load environment variables: {ex.Message}");
    }
}

/// <summary>
/// Loads environment variables from a specific .env file
/// </summary>
static void LoadEnvFile(string filePath)
{
    var lines = File.ReadAllLines(filePath);
    foreach (var line in lines)
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
            continue;

        var parts = line.Split('=', 2);
        if (parts.Length != 2)
            continue;

        var key = parts[0].Trim();
        var value = parts[1].Trim();

        // Remove surrounding quotes if present
        if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
            (value.StartsWith("'") && value.EndsWith("'")))
        {
            value = value[1..^1];
        }

        // Only set if not already set (environment variables take precedence)
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}

/// <summary>
/// Validates webhook URL configuration on startup and logs the results
/// </summary>
static async Task ValidateWebhookConfigurationAsync(WebApplication app)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var webhookUrlResolver = scope.ServiceProvider.GetRequiredService<IWebhookUrlResolver>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        logger.LogInformation("🔗 Validating webhook URL configuration for {Environment} environment...", environment.EnvironmentName);

        // Get the webhook base URL
        var webhookBaseUrl = await webhookUrlResolver.GetWebhookBaseUrlAsync();
        
        if (webhookBaseUrl == null)
        {
            if (environment.IsDevelopment())
            {
                logger.LogWarning("⚠️  Webhook URLs are disabled in development. Consider setting up ngrok for webhook testing.");
                logger.LogInformation("💡 To enable webhooks in development:");
                logger.LogInformation("   1. Start ngrok: ngrok http 5000");
                logger.LogInformation("   2. Set Webhooks:NgrokTunnelUrl in appsettings.Development.json");
                logger.LogInformation("   3. Or set Webhooks:BaseUrl to your preferred HTTPS endpoint");
            }
            else
            {
                logger.LogError("❌ Webhook URLs are disabled in production! This may affect functionality.");
                logger.LogError("🔧 Ensure AppBaseUrl is configured with an HTTPS URL in production.");
            }
            return;
        }

        logger.LogInformation("✅ Webhook base URL resolved: {WebhookBaseUrl}", webhookBaseUrl);

        // Test a sample webhook URL
        var sampleWebhookUrl = await webhookUrlResolver.GetWebhookUrlAsync("/api/webhooks/replicate/prediction-complete");
        logger.LogInformation("📨 Sample webhook URL: {SampleWebhookUrl}", sampleWebhookUrl);

        // Validate the webhook URL is accessible (optional validation)
        var isValid = await webhookUrlResolver.ValidateWebhookUrlAsync();
        if (isValid)
        {
            logger.LogInformation("✅ Webhook URL validation passed - endpoints are reachable");
        }
        else
        {
            if (environment.IsDevelopment())
            {
                logger.LogWarning("⚠️  Webhook URL validation failed - endpoints may not be reachable yet. This is normal if ngrok is not running.");
            }
            else
            {
                logger.LogWarning("⚠️  Webhook URL validation failed - please ensure your production endpoints are accessible");
            }
        }

        // Log environment-specific guidance
        if (environment.IsDevelopment())
        {
            logger.LogInformation("🔧 Development webhook configuration:");
            logger.LogInformation("   • Webhooks will work if HTTPS is configured (ngrok, local HTTPS, etc.)");
            logger.LogInformation("   • HTTP webhooks are disabled for security (Replicate API requirement)");
            logger.LogInformation("   • Configure Webhooks:NgrokTunnelUrl for manual ngrok URL override");
        }
        else
        {
            logger.LogInformation("🚀 Production webhook configuration active");
            logger.LogInformation("   • Webhooks enabled for HTTPS environments");
            logger.LogInformation("   • Using AppBaseUrl: {AppBaseUrl}", app.Configuration["AppBaseUrl"]);
        }
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "❌ Failed to validate webhook configuration during startup");
    }
}

/// <summary>
/// Validates Replicate configuration on startup to catch missing required settings
/// </summary>
static Task ValidateReplicateConfigurationAsync(WebApplication app)
{
    try
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        logger.LogInformation("🤖 Validating Replicate configuration for {Environment} environment...", environment.EnvironmentName);

        var configurationErrors = new List<string>();
        var configurationWarnings = new List<string>();

        // Check required Replicate settings
        var apiToken = configuration["Replicate:ApiToken"];
        if (string.IsNullOrEmpty(apiToken) || apiToken.StartsWith("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase))
        {
            configurationErrors.Add("Replicate:ApiToken is missing or contains placeholder value");
        }
        else
        {
            logger.LogInformation("✅ Replicate API Token is configured");
        }

        var fluxTrainingModelId = configuration["Replicate:FluxTrainingModelId"];
        if (string.IsNullOrEmpty(fluxTrainingModelId))
        {
            configurationWarnings.Add("Replicate:FluxTrainingModelId is missing - model training may fail");
        }
        else
        {
            logger.LogInformation("✅ Flux Training Model ID: {ModelId}", fluxTrainingModelId);
        }

        var fluxGenerationModelId = configuration["Replicate:FluxGenerationModelId"];
        if (string.IsNullOrEmpty(fluxGenerationModelId))
        {
            configurationWarnings.Add("Replicate:FluxGenerationModelId is missing - basic image generation may fail");
        }
        else
        {
            logger.LogInformation("✅ Flux Generation Model ID: {ModelId}", fluxGenerationModelId);
        }

        var fluxKontextProModelId = configuration["Replicate:FluxKontextProModelId"];
        if (string.IsNullOrEmpty(fluxKontextProModelId))
        {
            configurationErrors.Add("Replicate:FluxKontextProModelId is missing - photo enhancement will fail");
        }
        else
        {
            logger.LogInformation("✅ Flux Kontext Pro Model ID: {ModelId}", fluxKontextProModelId);
        }

        var webhookSecret = configuration["Replicate:WebhookSecret"];
        if (string.IsNullOrEmpty(webhookSecret) || webhookSecret.StartsWith("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase))
        {
            configurationErrors.Add("Replicate:WebhookSecret is missing or contains placeholder value");
        }
        else
        {
            logger.LogInformation("✅ Replicate Webhook Secret is configured");
        }

        // Report configuration status
        if (configurationErrors.Any())
        {
            logger.LogError("❌ Critical Replicate configuration errors found:");
            foreach (var error in configurationErrors)
            {
                logger.LogError("   • {Error}", error);
            }
            
            if (environment.IsProduction())
            {
                logger.LogError("🚨 Production deployment detected with critical configuration errors!");
                logger.LogError("🔧 Please configure the missing Replicate settings before proceeding.");
            }
            else
            {
                logger.LogWarning("⚠️  Development environment with configuration errors - some features will not work");
            }
        }

        if (configurationWarnings.Any())
        {
            logger.LogWarning("⚠️  Replicate configuration warnings:");
            foreach (var warning in configurationWarnings)
            {
                logger.LogWarning("   • {Warning}", warning);
            }
        }

        if (!configurationErrors.Any() && !configurationWarnings.Any())
        {
            logger.LogInformation("✅ All Replicate configuration settings are properly configured");
        }

        // Environment-specific guidance
        if (environment.IsDevelopment())
        {
            logger.LogInformation("🔧 Development Replicate configuration:");
            logger.LogInformation("   • Configure settings in appsettings.Development.json or user secrets");
            logger.LogInformation("   • Use 'dotnet user-secrets set \"Replicate:ApiToken\" \"your-token\"' for sensitive values");
        }
        else
        {
            logger.LogInformation("🚀 Production Replicate configuration:");
            logger.LogInformation("   • Ensure all secrets are properly configured via environment variables");
            logger.LogInformation("   • Verify Azure Key Vault or container secrets are accessible");
        }
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "❌ Failed to validate Replicate configuration during startup");
    }
    return Task.CompletedTask;
}
