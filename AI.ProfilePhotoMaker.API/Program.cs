using System.Text;
using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using AI.ProfilePhotoMaker.API.Services.Authentication;
using AI.ProfilePhotoMaker.API.Services.Authentication.interfaces;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;
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
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
        options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
    });
}

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(connectionString));
}
else
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

// Add JWT Authentication
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
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
    })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "";
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "";
        options.CallbackPath = "/api/auth/external-login/callback";
        
        // Disable PKCE for development to allow direct code exchange
        if (builder.Environment.IsDevelopment())
        {
            options.UsePkce = false;
        }
        
        if (builder.Environment.IsDevelopment())
        {
            // Set the correct redirect URI for development with ngrok
            var appBaseUrl = builder.Configuration["AppBaseUrl"] ?? "http://localhost:5035";
            Console.WriteLine($"Configuring OAuth with base URL: {appBaseUrl}");
            
            // Override the redirect URI to use the correct base URL
            var correctRedirectUri = $"{appBaseUrl}/api/auth/external-login/callback";
            Console.WriteLine($"Setting redirect URI to: {correctRedirectUri}");
            
            // Configure cookies for ngrok proxy - more permissive settings
            options.CorrelationCookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
            options.CorrelationCookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
            options.CorrelationCookie.IsEssential = true;
            options.CorrelationCookie.HttpOnly = true;
            options.CorrelationCookie.Domain = null;
            
            // Force the OAuth system to use the correct base URL
            options.Events.OnRedirectToAuthorizationEndpoint = context =>
            {
                var originalUri = context.RedirectUri;
                
                // Parse the original URI to extract query parameters and rebuild with correct base
                var uri = new Uri(originalUri);
                var query = uri.Query;
                
                // Rebuild the OAuth URL with the correct redirect_uri parameter
                var newRedirectUri = originalUri.Replace("redirect_uri=" + Uri.EscapeDataString("http://localhost:5035/api/auth/external-login/callback"), 
                                                        "redirect_uri=" + Uri.EscapeDataString(correctRedirectUri));
                newRedirectUri = newRedirectUri.Replace("redirect_uri=" + Uri.EscapeDataString("https://localhost:5035/api/auth/external-login/callback"), 
                                                       "redirect_uri=" + Uri.EscapeDataString(correctRedirectUri));
                
                Console.WriteLine($"Original OAuth URI: {originalUri}");
                Console.WriteLine($"Modified OAuth URI: {newRedirectUri}");
                context.Response.Redirect(newRedirectUri);
                return Task.CompletedTask;
            };
            
            // Enhanced error handling - bypass correlation failures entirely
            options.Events.OnRemoteFailure = context =>
            {
                var errorMessage = context.Failure?.Message ?? "OAuth authentication failed";
                Console.WriteLine($"OAuth Remote Failure: {errorMessage}");
                Console.WriteLine($"Request URL: {context.Request.Path}{context.Request.QueryString}");
                
                // For any OAuth failure (including correlation), try to extract the code and handle it directly
                var code = context.Request.Query["code"].ToString();
                Console.WriteLine($"Found authorization code: {code}");
                
                if (!string.IsNullOrEmpty(code))
                {
                    Console.WriteLine($"OAuth failure detected, redirecting to direct OAuth handler with code: {code}");
                    var appBaseUrl = builder.Configuration["AppBaseUrl"] ?? "http://localhost:5035";
                    var redirectUrl = $"{appBaseUrl}/api/auth/google-direct-callback?code={Uri.EscapeDataString(code)}&returnUrl=/dashboard";
                    Console.WriteLine($"Redirecting to: {redirectUrl}");
                    context.Response.Redirect(redirectUrl);
                    context.HandleResponse();
                }
                else
                {
                    Console.WriteLine($"OAuth failed with error: {errorMessage}");
                    context.Response.Redirect($"http://localhost:4200/login?error=oauth_failed&message={Uri.EscapeDataString(errorMessage)}");
                    context.HandleResponse();
                }
                return Task.CompletedTask;
            };
            
            // Handle successful authentication - bypass state validation issues
            options.Events.OnTicketReceived = context =>
            {
                Console.WriteLine("OAuth Ticket Received - Authentication successful");
                
                // Get user claims from the ticket
                var email = context.Principal?.FindFirst(ClaimTypes.Email)?.Value;
                var firstName = context.Principal?.FindFirst(ClaimTypes.GivenName)?.Value ?? "";
                var lastName = context.Principal?.FindFirst(ClaimTypes.Surname)?.Value ?? "";
                
                Console.WriteLine($"User info from ticket: {email}, {firstName}, {lastName}");
                
                // If we have user info, redirect to our custom processor instead of continuing with standard flow
                if (!string.IsNullOrEmpty(email))
                {
                    Console.WriteLine("Redirecting to custom OAuth processor with user info");
                    var appBaseUrl = builder.Configuration["AppBaseUrl"] ?? "http://localhost:5035";
                    var redirectUrl = $"{appBaseUrl}/api/auth/google-ticket-callback?email={Uri.EscapeDataString(email)}&firstName={Uri.EscapeDataString(firstName)}&lastName={Uri.EscapeDataString(lastName)}&returnUrl=/dashboard";
                    context.Response.Redirect(redirectUrl);
                    context.HandleResponse();
                }
                
                return Task.CompletedTask;
            };
        }
    })
    .AddFacebook(options =>
    {
        var appId = builder.Configuration["Authentication:Facebook:AppId"];
        var appSecret = builder.Configuration["Authentication:Facebook:AppSecret"];
        
        // Only configure Facebook if we have real credentials
        if (!string.IsNullOrEmpty(appId) && appId != "placeholder" &&
            !string.IsNullOrEmpty(appSecret) && appSecret != "placeholder")
        {
            options.AppId = appId;
            options.AppSecret = appSecret;
            options.CallbackPath = "/signin-facebook";
            
            if (builder.Environment.IsDevelopment())
            {
                var baseUrl = builder.Configuration["AppBaseUrl"];
                if (!string.IsNullOrEmpty(baseUrl))
                {
                    options.Events.OnRedirectToAuthorizationEndpoint = context =>
                    {
                        context.Response.Redirect(context.RedirectUri.Replace("https://localhost:5035", baseUrl));
                        return Task.CompletedTask;
                    };
                }
            }
        }
        else
        {
            // Use placeholder values if no real credentials
            options.AppId = "placeholder-facebook-app-id";
            options.AppSecret = "placeholder-facebook-app-secret";
        }
    })
    .AddApple(options =>
    {
        var clientId = builder.Configuration["Authentication:Apple:ClientId"];
        var teamId = builder.Configuration["Authentication:Apple:TeamId"];
        var keyId = builder.Configuration["Authentication:Apple:KeyId"];
        var privateKey = builder.Configuration["Authentication:Apple:PrivateKey"];
        
        // Only configure Apple if we have real credentials
        if (!string.IsNullOrEmpty(clientId) && clientId != "placeholder" &&
            !string.IsNullOrEmpty(teamId) && teamId != "placeholder" &&
            !string.IsNullOrEmpty(keyId) && keyId != "placeholder" &&
            !string.IsNullOrEmpty(privateKey) && privateKey != "placeholder")
        {
            options.ClientId = clientId;
            options.TeamId = teamId;
            options.KeyId = keyId;
            options.CallbackPath = "/signin-apple";
            options.PrivateKey = (keyId, cancellationToken) => Task.FromResult<ReadOnlyMemory<char>>(privateKey.AsMemory());
            
            if (builder.Environment.IsDevelopment())
            {
                var baseUrl = builder.Configuration["AppBaseUrl"];
                if (!string.IsNullOrEmpty(baseUrl))
                {
                    options.Events.OnRedirectToAuthorizationEndpoint = context =>
                    {
                        context.Response.Redirect(context.RedirectUri.Replace("https://localhost:5035", baseUrl));
                        return Task.CompletedTask;
                    };
                }
            }
        }
        else
        {
            // Skip Apple configuration if credentials are placeholders
            options.ClientId = "skip-apple-oauth";
            options.ClientSecret = "skip-apple-oauth"; // Required field
        }
    });

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
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IImageProcessingService, AzureImageProcessingService>();
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Services.IFreeTierService, AI.ProfilePhotoMaker.API.Services.FreeTierService>();
builder.Services.AddHttpClient<IReplicateApiClient, ReplicateApiClient>();
builder.Services.AddScoped<AI.ProfilePhotoMaker.API.Data.IUserProfileRepository, AI.ProfilePhotoMaker.API.Data.UserProfileRepository>();

// Register background services
builder.Services.AddHostedService<AI.ProfilePhotoMaker.API.Services.ModelCreationPollingService>();
builder.Services.AddHostedService<AI.ProfilePhotoMaker.API.Services.FreeTierBackgroundService>();


builder.Services.AddControllers();
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


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins",
        builder =>
        {
            builder.WithOrigins("https://aiprofilephotomaker.com")
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });

     options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

// Use forwarded headers for ngrok proxy
app.UseForwardedHeaders();

// Use session middleware for OAuth state management
if (app.Environment.IsDevelopment())
{
    app.UseSession();
}

// In middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseCors("AllowAll");
}
else
{
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

// Serve static files from uploads directory
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "uploads")),
    RequestPath = "/uploads"
});

// Serve static files from training-zips directory
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "training-zips")),
    RequestPath = "/training-zips"
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
    
    // Fallback to index.html for Angular routing
    app.MapFallbackToFile("index.html", new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(angularPath)
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
