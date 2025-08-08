# Code Patterns - AI Profile Photo Maker

## ASP.NET Core Authentication Patterns

### JWT Bearer Authentication Setup
**Pattern**: DefaultChallengeScheme configuration for API consistency
**Location**: `Program.cs` authentication configuration
**Implementation**:
```csharp
var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme; // Critical for API 401 responses
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme; // For OAuth
})
.AddJwtBearer(options => {
    // JWT configuration with proper error handling
    options.Events = new JwtBearerEvents
    {
        OnChallenge = context => {
            context.HandleResponse();
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsJsonAsync(standardErrorResponse);
        }
    };
});
```
**Key Learning**: Missing DefaultChallengeScheme causes API endpoints to return 302 redirects instead of 401 JSON responses

### Controller Authorization Pattern
**Pattern**: Consistent [Authorize] attribute usage
**Example**:
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize] // Uses DefaultChallengeScheme for consistent behavior
public class ProfileController : ControllerBase
{
    // Protected endpoints automatically get proper 401 handling
}
```

### API Error Response Standardization
**Pattern**: Consistent error response format across all endpoints
**Format**:
```csharp
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T Data { get; set; }
    public object Error { get; set; }
}

// Error format
{
    "success": false,
    "error": {
        "code": "Unauthorized",
        "message": "Authentication required. Please provide a valid JWT token."
    }
}
```

## Controller Implementation Patterns

### Azure Storage Integration Pattern
**Pattern**: Fallback strategy for missing resources
**Example**: StylePreviewController implementation
```csharp
[HttpGet("list")]
public async Task<IActionResult> GetStylePreviews()
{
    try 
    {
        // Try local resources first
        var localPreviews = GetLocalStylePreviews();
        if (localPreviews.Any())
            return Ok(new { success = true, data = localPreviews });
            
        // Fallback to Azure Blob Storage
        var azurePreviews = await GetAzureStylePreviews();
        return Ok(new { success = true, data = azurePreviews });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { success = false, error = new { code = "StorageError", message = ex.Message } });
    }
}
```

### Repository Pattern Usage
**Pattern**: Consistent repository pattern for data access
**Example**: UserProfileRepository integration
```csharp
public class ProfileController : ControllerBase
{
    private readonly IUserProfileRepository _userProfileRepository;
    
    public ProfileController(IUserProfileRepository userProfileRepository)
    {
        _userProfileRepository = userProfileRepository;
    }
    
    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var profile = await _userProfileRepository.GetUserProfileWithImagesAsync(userId);
        return Ok(new { success = true, data = profile });
    }
}
```

## Configuration Patterns

### Multi-Environment Configuration
**Pattern**: Environment-specific configuration with fallbacks
**Example**: CORS policy configuration
```csharp
var corsPolicy = app.Environment.IsDevelopment() ? "AllowDevelopment" : "V1Production";
Console.WriteLine($"🔧 CORS Policy: Using '{corsPolicy}' for environment '{app.Environment.EnvironmentName}'");
app.UseCors(corsPolicy);
```

### Port Standardization Pattern
**Pattern**: Consistent port allocation across environments
**Standard**:
- Frontend: Always port 4200 (Angular CLI default)
- Backend API: Always port 5032 (custom, non-conflicting)
**Configuration**: Enforced in multiple files (package.json, angular.json, launchSettings.json)

## Middleware Patterns

### Request Logging Middleware
**Pattern**: Comprehensive request logging with OAuth detection
```csharp
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLower();
    Console.WriteLine($"🔍 Request: {context.Request.Method} {path}");
    
    // Special handling for OAuth paths
    if (path?.Contains("oauth") == true || path?.Contains("auth") == true)
    {
        Console.WriteLine($"🔐 OAuth-related request detected");
        // Additional OAuth-specific logging
    }
    
    await next();
    Console.WriteLine($"🔐 Response: {context.Response.StatusCode}");
});
```

## Database Patterns

### Entity Framework Integration
**Pattern**: DbContext with repository pattern
**Usage**: Direct DbContext for simple operations, Repository for complex business logic
**Example**: Mixed usage in controllers for optimal performance

### Migration Pattern
**Pattern**: Automated migration on startup
**Implementation**: `await app.UseDatabaseMigrationAsync();`
**Location**: Program.cs startup configuration

## Storage Patterns

### Dual Storage Strategy
**Pattern**: Local storage for development, Azure Blob for production
**Configuration**:
```csharp
var azureConnectionString = builder.Configuration.GetConnectionString("AzureStorage");
if (!string.IsNullOrEmpty(azureConnectionString))
{
    builder.Services.AddScoped<IStorageService, AzureBlobStorageService>();
}
else
{
    builder.Services.AddScoped<IStorageService, LocalStorageService>();
}
```

## Error Handling Patterns

### Global Exception Handling
**Pattern**: Centralized error handling with environment-specific responses
**Development**: Detailed error information with stack traces
**Production**: Sanitized error responses with logging

### Validation Patterns
**Pattern**: Model validation with consistent error responses
**Implementation**: Data annotations with custom validation attributes

## Performance Patterns

### Response Compression
**Pattern**: GZIP compression for API responses
**Configuration**: Enabled for JSON, SVG content types
**Benefits**: Reduced bandwidth, improved performance over slow connections

### Caching Strategy
**Pattern**: Aggressive caching for static assets (style previews, images)
**Implementation**: Cache-Control headers with immutable flag
**Duration**: 7 days for style previews, 1 day for user uploads

## Development Patterns

### Proxy Configuration
**Pattern**: Angular CLI proxy for seamless development
**Configuration**: All `/api/*` requests proxied to backend
**Benefits**: Single origin, simplified authentication, no CORS issues in development

### Hot Reload Integration
**Pattern**: File watching and automatic restart capabilities
**Tools**: Angular CLI dev server, dotnet watch for API changes

---

*Patterns validated and working as of 2025-08-08*
*All patterns follow ASP.NET Core and Angular best practices*