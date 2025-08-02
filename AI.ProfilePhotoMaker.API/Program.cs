using System.Text;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services.Authentication;
using AI.ProfilePhotoMaker.API.Services.Authentication.interfaces;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
using AI.ProfilePhotoMaker.API.Services.Payment;
using AI.ProfilePhotoMaker.API.Services.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Configure forwarded headers for ngrok proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                              Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto |
                              Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();

    // Trust ngrok proxy
    options.KnownProxies.Add(System.Net.IPAddress.Parse("127.0.0.1"));
});

// Configure data protection and session for OAuth state handling with ngrok
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
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Determine database provider based on connection string
bool isAzureSqlServer = !string.IsNullOrEmpty(connectionString) && 
                       (connectionString.Contains("azure") || 
                        connectionString.Contains("database.windows.net") || 
                        connectionString.Contains("SqlServer") ||
                        connectionString.Contains("Authentication=Active Directory"));

if (isAzureSqlServer)
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString, sqlServerOptionsAction: sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        }));
}
else
{
    // Use SQLite for local development
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(connectionString ?? "Data Source=ProfilePhotoMaker.db"));
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
        // IMPORTANT: Do not set DefaultChallengeScheme or DefaultScheme - this allows 
        // OAuth providers (Google, Facebook, etc.) to handle their own challenges with proper redirects
        // options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme; // REMOVED to fix OAuth
        // options.DefaultScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme; // REMOVED to fix OAuth
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
    
    Console.WriteLine("✅ Google OAuth configured successfully");
}
else
{
    Console.WriteLine("⚠️  Google OAuth not configured - skipping (ClientId or ClientSecret missing)");
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

builder.Services.AddHttpClient<IImageDownloadService, ImageDownloadService>();
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Data.IUserProfileRepository, AI.ProfilePhotoMaker.API.Data.UserProfileRepository>();

// Register Storage Services - choose between Local or Azure Blob Storage
var azureStorageConnectionString = builder.Configuration.GetConnectionString("AzureStorage") ?? 
                                  builder.Configuration["AzureStorage:ConnectionString"];

if (!string.IsNullOrEmpty(azureStorageConnectionString))
{
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
var stagingFrontendUrl = builder.Configuration["AppBaseUrl"] ?? 
                       "https://aiprofilemaker-web-staging.thankfulriver-68674ea3.eastus2.azurecontainerapps.io";

builder.Services.AddCors(options =>
{
    
    options.AddPolicy("AllowSpecificOrigins",
        corsBuilder =>
        {
            var allowedOrigins = new List<string>
            {
                "https://aiprofilephotomaker.com",
                "https://test.profilephotomaker.com"
            };
            
            // Add staging URL from configuration or fallback to current domain
            if (!string.IsNullOrEmpty(stagingFrontendUrl))
            {
                allowedOrigins.Add(stagingFrontendUrl);
            }
            
            corsBuilder.WithOrigins(allowedOrigins.ToArray())
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });

    options.AddPolicy("AllowDevelopment", corsBuilder =>
    {
        corsBuilder.WithOrigins(
                "http://localhost:4200",
                "https://localhost:4200",
                "https://awlocaldev.ngrok.app",
                "https://awlocaldev-api.ngrok.app"
            )
            .SetIsOriginAllowedToAllowWildcardSubdomains()
            .WithOrigins("https://*.ngrok.app", "https://*.ngrok.io")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });

    options.AddPolicy("AllowAll", corsBuilder =>
    {
        corsBuilder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

// TEST: Verify logging works after app.Build()
Console.WriteLine("🚨 CRITICAL TEST: App built successfully - checking if migration code executes");

// EMERGENCY FIX: Always run database migrations and validation
// Enhanced logging to debug migration failures using .NET logging infrastructure
try 
{
    Console.WriteLine("🚨 TEST: About to create service scope");
    using (var scope = app.Services.CreateScope())
    {
        Console.WriteLine("🚨 TEST: Service scope created successfully");
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    
    logger.LogCritical("=== MIGRATION DEBUG INFO ===");
    logger.LogCritical("Environment: {Environment}", app.Environment.EnvironmentName);
    logger.LogCritical("IsDevelopment: {IsDevelopment}", app.Environment.IsDevelopment());
    logger.LogCritical("IsStaging: {IsStaging}", app.Environment.IsStaging());
    logger.LogCritical("IsProduction: {IsProduction}", app.Environment.IsProduction());
    
    try
    {
        logger.LogCritical("=== DATABASE CONNECTION DEBUG ===");
        logger.LogCritical("Connection String (masked): Server={Server};...", connectionString?.Split(';')[0]?.Split('=')[1]);
        
        logger.LogCritical("Attempting database connection...");
        
        // Check if database is accessible
        if (dbContext.Database.CanConnectAsync().GetAwaiter().GetResult())
        {
            logger.LogCritical("✅ Database connection established successfully.");
            
            // Check if CreditPackages table exists
            var creditPackagesTableExists = false;
            try
            {
                dbContext.CreditPackages.Take(1).ToListAsync().GetAwaiter().GetResult();
                creditPackagesTableExists = true;
                logger.LogCritical("✅ CreditPackages table found and accessible");
            }
            catch (Exception tableCheckEx)
            {
                logger.LogCritical("❌ CreditPackages table check failed: {Message}", tableCheckEx.Message);
                creditPackagesTableExists = false;
            }
            
            logger.LogCritical("CreditPackages table exists: {TableExists}", creditPackagesTableExists);
            
            if (!creditPackagesTableExists)
            {
                logger.LogCritical("⚠️ CreditPackages table missing - forcing migration...");
                dbContext.Database.Migrate();
                logger.LogCritical("✅ Database migrations completed successfully.");
                
                // Verify CreditPackages table was created
                var tableCreated = false;
                try
                {
                    dbContext.CreditPackages.Take(1).ToListAsync().GetAwaiter().GetResult();
                    tableCreated = true;
                }
                catch
                {
                    tableCreated = false;
                }
                logger.LogCritical("CreditPackages table created: {TableCreated}", tableCreated);
                
                if (tableCreated)
                {
                    // Verify credit packages data exists
                    var creditPackageCount = dbContext.CreditPackages.CountAsync().GetAwaiter().GetResult();
                    logger.LogCritical("Credit packages count: {Count}", creditPackageCount);
                    
                    if (creditPackageCount == 0)
                    {
                        logger.LogCritical("⚠️ No credit packages found - they should have been seeded during migration");
                    }
                }
            }
            else
            {
                logger.LogCritical("✅ CreditPackages table already exists - running standard migration check...");
                dbContext.Database.Migrate();
                logger.LogCritical("✅ Database migrations completed successfully.");
            }
            
            // Verify styles exist after migration
            var styleCount = dbContext.Styles.CountAsync(s => s.IsActive).GetAwaiter().GetResult();
            logger.LogCritical("Active styles count: {Count}", styleCount);
            
            if (styleCount == 0)
            {
                logger.LogCritical("⚠️ Warning: No active styles found after migration. This may cause 'demo styles' fallback.");
            }
        }
        else
        {
            logger.LogCritical("❌ ERROR: Cannot connect to database. Database operations will fail.");
            logger.LogCritical("This will cause HTTP 500 errors for all database-dependent endpoints.");
        }
    }
    catch (Exception ex)
    {
        logger.LogCritical("❌ CRITICAL: Database migration failed!");
        logger.LogCritical("Error: {Message}", ex.Message);
        logger.LogCritical("Inner Exception: {InnerMessage}", ex.InnerException?.Message);
        logger.LogCritical("Stack trace: {StackTrace}", ex.StackTrace);
        
        // Additional debugging for SQL connection issues
        if (ex.Message.Contains("Login failed") || ex.Message.Contains("authentication"))
        {
            logger.LogCritical("🔑 Authentication issue detected - check managed identity permissions:");
            logger.LogCritical("- Ensure Container App has managed identity enabled");
            logger.LogCritical("- Verify managed identity has db_datareader, db_datawriter, db_ddladmin roles");
            logger.LogCritical("- Check SQL Server firewall allows Azure services");
        }
        
        // Don't fail the app startup, but make the error very visible
        logger.LogCritical("🚨 APPLICATION WILL START BUT DATABASE OPERATIONS WILL FAIL!");
    }
    }
}
catch (Exception outerEx)
{
    Console.WriteLine($"🚨 OUTER CATCH: Failed to create service scope or get services: {outerEx.Message}");
    Console.WriteLine($"🚨 OUTER CATCH: Stack trace: {outerEx.StackTrace}");
}

// Use forwarded headers for ngrok proxy
app.UseForwardedHeaders();

// Enable response compression early in the pipeline
app.UseResponseCompression();

// Use session middleware for OAuth state management
if (app.Environment.IsDevelopment())
{
    app.UseSession();
}

// In middleware pipeline - use appropriate CORS policy based on environment
var corsPolicy = app.Environment.IsDevelopment() ? "AllowDevelopment" : "AllowSpecificOrigins";
Console.WriteLine($"🔧 CORS Policy: Using '{corsPolicy}' for environment '{app.Environment.EnvironmentName}'");

if (app.Environment.IsDevelopment())
{
    Console.WriteLine("🔧 CORS: Development mode - allowing local origins and ngrok tunnels");
    app.UseCors("AllowDevelopment");
}
else
{
    Console.WriteLine($"🔧 CORS: Production/Staging mode - allowing specific origins including: {stagingFrontendUrl}");
    app.UseCors("AllowSpecificOrigins");
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

// Add debug middleware to log all requests
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLower();
    var method = context.Request.Method;

    Console.WriteLine($"🔍 Request: {method} {path}");

    // Special logging for OAuth-related paths
    if (path?.Contains("signin") == true || path?.Contains("oauth") == true || path?.Contains("auth") == true)
    {
        Console.WriteLine($"🔐 OAuth-related request detected: {method} {context.Request.Path}");
        Console.WriteLine($"   Query string: {context.Request.QueryString}");
        Console.WriteLine($"   User-Agent: {context.Request.Headers.UserAgent}");
        Console.WriteLine($"   Referer: {context.Request.Headers.Referer}");
    }

    await next();

    // Log response for OAuth paths
    if (path?.Contains("signin") == true || path?.Contains("oauth") == true || path?.Contains("auth") == true)
    {
        Console.WriteLine($"🔐 OAuth response: {context.Response.StatusCode}");
    }
});

// CRITICAL: Authentication middleware must come before static files to handle OAuth callbacks
app.UseAuthentication();
app.UseAuthorization();

// Serve static files from uploads directory
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "uploads")),
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

// Serve static files from training-zips directory
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "training-zips")),
    RequestPath = "/training-zips"
});

// Serve static files from style-previews directory
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "style-previews")),
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

// Serve static files from enhanced images directory
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "enhanced")),
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

// Serve static files from generated images directory
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "generated")),
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

        Console.WriteLine($"🔍 FALLBACK: Checking path: {path}");

        // Don't handle API or OAuth callback paths
        if (path?.StartsWith("/api/") == true ||
            path?.StartsWith("/signin-") == true ||
            path?.StartsWith("/swagger") == true)
        {
            Console.WriteLine($"🔍 FALLBACK: Skipping path: {path} (matches exclusion)");
            return Task.CompletedTask;
        }

        Console.WriteLine($"🔍 FALLBACK: Serving Angular for path: {path}");

        // Serve index.html for Angular routing
        context.Response.ContentType = "text/html";
        return context.Response.SendFileAsync(Path.Combine(angularPath, "index.html"));
    });
}

// OAuth callbacks are now handled by the standard middleware
// No custom debug routes needed


app.MapControllers();

app.Run();
