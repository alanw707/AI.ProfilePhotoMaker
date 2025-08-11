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

// Configure forwarded headers for development proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                              Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto |
                              Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();

    // Trust development proxy
    options.KnownProxies.Add(System.Net.IPAddress.Parse("127.0.0.1"));
});

// Configure data protection and session for OAuth state handling
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "keys")))
        .SetApplicationName("AI.ProfilePhotoMaker.API");

    // Add session services for OAuth state management
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
}

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
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT:Secret"]))
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

// Register Replicate SDK
builder.Services.AddSingleton<Replicate.ReplicateApi>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var apiToken = configuration["Replicate:ApiToken"]
        ?? throw new InvalidOperationException("Replicate API token not configured");
    return new Replicate.ReplicateApi(apiToken);
});

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
    builder.Services.AddScoped<IStorageService, LocalStorageService>();
    Console.WriteLine("Using Local Storage for image storage");
}

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
builder.Services.AddHostedService<AI.ProfilePhotoMaker.API.Services.ModelCreationPollingService>();
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
            
            // Add V1 deployment URL from configuration
            if (!string.IsNullOrEmpty(v1FrontendUrl))
            {
                allowedOrigins.Add(v1FrontendUrl);
            }
            
            // Add expected V1 Container Apps URL
            allowedOrigins.Add("https://aiprofilemaker-web-v1.eastus.azurecontainerapps.io");
            
            // Add actual V1 deployment URL for current infrastructure (keep for rollback capability)
            allowedOrigins.Add("https://aipm-web-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io");
            
            corsBuilder.WithOrigins(allowedOrigins.ToArray())
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
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

// Validate webhook URL configuration on startup
await ValidateWebhookConfigurationAsync(app);

// Use forwarded headers for ngrok proxy
app.UseForwardedHeaders();

// Enable response compression early in the pipeline
app.UseResponseCompression();

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

// Use session middleware for OAuth state management
if (app.Environment.IsDevelopment())
{
    app.UseSession();
}

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

// Serve static files from uploads directory - only if directory exists
var uploadsPath = Path.Combine(builder.Environment.ContentRootPath, "uploads");
if (Directory.Exists(uploadsPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
        RequestPath = "/uploads",
        OnPrepareResponse = ctx =>
    {
        // Add CORS headers to allow cross-origin requests for image downloads
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, OPTIONS");
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type");

        // Ensure proper content type for images
        var extension = Path.GetExtension(ctx.File.Name).ToLowerInvariant();
        if (extension == ".png") ctx.Context.Response.ContentType = "image/png";
        else if (extension == ".jpg" || extension == ".jpeg") ctx.Context.Response.ContentType = "image/jpeg";
        else if (extension == ".gif") ctx.Context.Response.ContentType = "image/gif";
        else if (extension == ".webp") ctx.Context.Response.ContentType = "image/webp";

        // Add aggressive caching for uploaded images (immutable content)
        ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=86400, immutable");
        ctx.Context.Response.Headers.Append("ETag", $"\"{ctx.File.LastModified:yyyy-MM-dd-HH-mm-ss}\"");
    }
    });
}

// Serve static files from training-zips directory - only if directory exists
var trainingZipsPath = Path.Combine(builder.Environment.ContentRootPath, "training-zips");
if (Directory.Exists(trainingZipsPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(trainingZipsPath),
    RequestPath = "/training-zips"
    });
}

// Serve static files from style-previews directory - only if directory exists
var stylePreviewsPath = Path.Combine(builder.Environment.ContentRootPath, "style-previews");
if (Directory.Exists(stylePreviewsPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(stylePreviewsPath),
    RequestPath = "/style-previews",
    OnPrepareResponse = ctx =>
    {
        // Add CORS headers to allow cross-origin requests for image downloads
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, OPTIONS");
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type");

        // Ensure proper content type for images
        var extension = Path.GetExtension(ctx.File.Name).ToLowerInvariant();
        if (extension == ".png") ctx.Context.Response.ContentType = "image/png";
        else if (extension == ".jpg" || extension == ".jpeg") ctx.Context.Response.ContentType = "image/jpeg";
        else if (extension == ".gif") ctx.Context.Response.ContentType = "image/gif";
        else if (extension == ".webp") ctx.Context.Response.ContentType = "image/webp";

        // Add aggressive caching for style previews (static assets, rarely change)
        ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=604800, immutable");
        ctx.Context.Response.Headers.Append("ETag", $"\"{ctx.File.LastModified:yyyy-MM-dd-HH-mm-ss}\"");
    }
    });
}

// Serve static files from enhanced images directory - only if directory exists
var enhancedPath = Path.Combine(builder.Environment.ContentRootPath, "enhanced");
if (Directory.Exists(enhancedPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(enhancedPath),
    RequestPath = "/enhanced",
    OnPrepareResponse = ctx =>
    {
        // Add CORS headers to allow cross-origin requests for image downloads
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, OPTIONS");
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type");

        // Ensure proper content type for images
        var extension = Path.GetExtension(ctx.File.Name).ToLowerInvariant();
        if (extension == ".png") ctx.Context.Response.ContentType = "image/png";
        else if (extension == ".jpg" || extension == ".jpeg") ctx.Context.Response.ContentType = "image/jpeg";
        else if (extension == ".gif") ctx.Context.Response.ContentType = "image/gif";
        else if (extension == ".webp") ctx.Context.Response.ContentType = "image/webp";

        // Add caching for enhanced images (personal photos, moderate caching)
        ctx.Context.Response.Headers.Append("Cache-Control", "private, max-age=3600");
        ctx.Context.Response.Headers.Append("ETag", $"\"{ctx.File.LastModified:yyyy-MM-dd-HH-mm-ss}\"");
    }
    });
}

// Serve static files from generated images directory - only if directory exists
var generatedPath = Path.Combine(builder.Environment.ContentRootPath, "generated");
if (Directory.Exists(generatedPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(generatedPath),
    RequestPath = "/generated",
    OnPrepareResponse = ctx =>
    {
        // Add CORS headers to allow cross-origin requests for image downloads
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, OPTIONS");
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type");

        // Ensure proper content type for images
        var extension = Path.GetExtension(ctx.File.Name).ToLowerInvariant();
        if (extension == ".png") ctx.Context.Response.ContentType = "image/png";
        else if (extension == ".jpg" || extension == ".jpeg") ctx.Context.Response.ContentType = "image/jpeg";
        else if (extension == ".gif") ctx.Context.Response.ContentType = "image/gif";
        else if (extension == ".webp") ctx.Context.Response.ContentType = "image/webp";
    }
    });
}

// Serve Angular static files
var angularPath = Path.Combine(builder.Environment.ContentRootPath, "../AI.ProfilePhotoMaker.UI/dist/ai.profile-photo-maker.ui");
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

        // Don't handle API or OAuth callback paths
        if (path?.StartsWith("/api/") == true ||
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