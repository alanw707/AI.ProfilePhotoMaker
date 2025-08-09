# Code Patterns - AI Profile Photo Maker

## Angular Frontend Patterns

### Change Detection Strategy
**Pattern**: OnPush with Manual Change Detection
**Location**: PhotoEnhancementComponent and other performance-critical components
**Implementation**:
```typescript
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PhotoEnhancementComponent {
  constructor(private _cdr: ChangeDetectorRef) {}
  
  // Manual change detection after async operations
  async someAsyncOperation() {
    try {
      // ... async work
      this.someProperty = result;
      this._cdr.detectChanges(); // CRITICAL for OnPush
    } catch (error) {
      this.errorMessage = error.message;
      this._cdr.detectChanges(); // CRITICAL in error handling
    }
  }
}
```
**Usage**: All components handling large data or frequent updates
**Benefits**: Better performance, explicit change control

### Error Handling Pattern
**Pattern**: Comprehensive Error State Management
**Implementation**:
```typescript
catch (error: any) {
  // Detailed error analysis
  console.error('Full error details:', {
    error,
    status: error.status,
    message: error.message,
    body: error.error
  });
  
  // User-friendly error messages
  let errorMessage = 'Default fallback message';
  if (error.status === 401) {
    errorMessage = 'Authentication failed. Please log in again.';
  } else if (error.error?.message) {
    errorMessage = error.error.message;
  }
  
  this.errorMessage = errorMessage;
  this.isProcessing = false;
  this._cdr.detectChanges(); // Essential for OnPush
}
```
**Benefits**: Better UX, easier debugging, consistent error handling

### Service Pattern
**Pattern**: Centralized HTTP Service with Configuration
**Implementation**:
```typescript
@Injectable({
  providedIn: 'root'
})
export class ReplicateService {
  constructor(
    private http: HttpClient, 
    private config: ConfigService
  ) {}
  
  enhancePhoto(request: EnhancePhotoRequest): Observable<ApiResponse> {
    return this.http.post<ApiResponse>(
      this.config.getFullUrl('/replicate/enhance'), 
      request
    );
  }
}
```
**Benefits**: Centralized configuration, consistent API calls, typed responses

### State Management Pattern
**Pattern**: Reactive State with Subscription Management
**Implementation**:
```typescript
export class Component implements OnInit, OnDestroy {
  private _stateSubscription!: Subscription;
  
  ngOnInit() {
    this._stateSubscription = this._stateService.state$.subscribe(state => {
      this.localState = state.relevantData;
      this._cdr.detectChanges();
    });
  }
  
  ngOnDestroy() {
    if (this._stateSubscription) {
      this._stateSubscription.unsubscribe();
    }
  }
}
```
**Benefits**: Prevents memory leaks, reactive updates, clean lifecycle management

## ASP.NET Core Backend Patterns

### Controller Response Pattern
**Pattern**: Consistent API Response Structure
**Implementation**:
```csharp
public class BaseController : ControllerBase
{
    protected IActionResult SuccessResponse(object data, string? message = null)
    {
        return Ok(new { success = true, data, error = (object?)null, message });
    }
    
    protected IActionResult ErrorResponse(string code, string message, int statusCode = 400)
    {
        return StatusCode(statusCode, new { 
            success = false, 
            data = (object?)null, 
            error = new { code, message }
        });
    }
}
```
**Usage**: All API controllers inherit from BaseController
**Benefits**: Consistent API contract, predictable error handling

### Service Registration Pattern
**Pattern**: Scoped Service Registration with Interfaces
**Implementation**:
```csharp
// Register services with proper lifetime
builder.Services.AddScoped<IAsyncFileService, AsyncFileService>();
builder.Services.AddScoped<IAsyncZipService, AsyncZipService>();
builder.Services.AddScoped<IReplicateApiClient, ReplicateApiClient>();

// Configuration binding
builder.Services.Configure<ReplicateOptions>(
    builder.Configuration.GetSection("Replicate")
);
```
**Benefits**: Proper DI, testability, configuration separation

### Authentication Pattern
**Pattern**: JWT with User Context Extraction
**Implementation**:
```csharp
[ApiController]
[Authorize]
public class SomeController : BaseController
{
    public async Task<IActionResult> SomeAction()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." }});
        
        // Proceed with authenticated operation
    }
}
```
**Benefits**: Consistent auth, user context available, secure operations

### Error Handling Pattern
**Pattern**: Structured Error Responses with Logging
**Implementation**:
```csharp
try
{
    var result = await _someService.DoWorkAsync();
    return Ok(new { success = true, data = result });
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error in {Action} for user {UserId}", nameof(SomeAction), userId);
    return StatusCode(500, new { 
        success = false, 
        error = new { 
            code = "OperationFailed", 
            message = "Operation failed. Please try again later." 
        }
    });
}
```
**Benefits**: Proper logging, consistent errors, security (no detail leakage)

### Database Access Pattern
**Pattern**: Repository Pattern with Entity Framework
**Implementation**:
```csharp
public class UserProfileRepository : IUserProfileRepository
{
    private readonly ApplicationDbContext _context;
    
    public async Task<UserProfile?> GetByUserIdAsync(string userId)
    {
        return await _context.UserProfiles
            .FirstOrDefaultAsync(u => u.UserId == userId);
    }
}
```
**Benefits**: Testable data access, separation of concerns, async operations

## Configuration Patterns

### Environment-Specific Configuration
**Pattern**: Hierarchical Configuration with Environment Separation
**Structure**:
```
appsettings.json           # Base configuration
appsettings.Development.json   # Development overrides
appsettings.Test.json         # Test environment
appsettings.Production.json   # Production settings
```
**Implementation**:
```csharp
builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables(); // Override with environment variables
```

### URL Configuration Pattern
**Pattern**: Environment-Aware URL Generation
**Implementation**:
```csharp
private string GetAbsoluteUrl(string relativePath)
{
    // Priority: ngrok headers > configuration > request
    var ngrokUrl = GetNgrokUrl();
    if (!string.IsNullOrEmpty(ngrokUrl))
        return $"{ngrokUrl}{relativePath}";
    
    var baseUrl = _configuration["AppBaseUrl"];
    if (!string.IsNullOrEmpty(baseUrl))
        return $"{baseUrl}{relativePath}";
    
    return $"{Request.Scheme}://{Request.Host}{relativePath}";
}
```
**Benefits**: Flexible deployment, development-friendly, environment-aware

## Performance Patterns

### Async I/O Pattern
**Pattern**: Non-blocking File Operations
**Implementation**:
```csharp
public class AsyncFileService : IAsyncFileService
{
    public async Task<byte[]> ReadAllBytesAsync(string path)
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        var buffer = new byte[fileStream.Length];
        await fileStream.ReadAsync(buffer, 0, buffer.Length);
        return buffer;
    }
}
```
**Usage**: File uploads, image processing, ZIP operations
**Benefits**: Better scalability, non-blocking operations

### Credit System Pattern
**Pattern**: Post-Success Credit Consumption
**Implementation**:
```csharp
// 1. Check credits first
var hasCredits = await _creditService.HasAvailableCreditsAsync(userId);
if (!hasCredits) return BadRequest("Insufficient credits");

// 2. Perform API operation
var result = await _replicateApi.EnhancePhotoAsync(request);

// 3. Consume credits only after success
var creditConsumed = await _creditService.ConsumeCreditsAsync(userId, "photo_enhancement");
if (!creditConsumed) {
    _logger.LogError("API succeeded but credit consumption failed for user {UserId}", userId);
}

return Ok(result);
```
**Benefits**: Fair billing, no charges for failures

### Change Detection Optimization
**Pattern**: Multi-Stage Change Detection for Large Data
**Implementation**:
```typescript
// For large base64 images
if (isBase64) {
  // Multi-stage change detection for large base64 data
  this._cdr.detectChanges();
  setTimeout(() => {
    this._cdr.detectChanges();
  }, 50);
} else {
  this._cdr.detectChanges();
}
```
**Benefits**: Smooth UI updates with large data

## Security Patterns

### Input Validation Pattern
**Pattern**: Model State Validation with Custom Messages
**Implementation**:
```csharp
if (!ModelState.IsValid)
    return BadRequest(new { success = false, error = new { code = "InvalidModel", message = "Invalid input." }});
```

### Environment Variable Pattern
**Pattern**: Secure Configuration Management
**Implementation**:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=DB;User Id=sa;Password=${DB_PASSWORD};TrustServerCertificate=true;"
  }
}
```
**Benefits**: No credentials in source control, environment-specific values

## Integration Patterns

### External API Integration Pattern
**Pattern**: Resilient External API Calls with Error Handling
**Implementation**:
```csharp
public async Task<ReplicatePredictionResult> EnhancePhotoAsync(string imageUrl)
{
    try
    {
        var response = await _httpClient.PostAsync(endpoint, content);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ReplicatePredictionResult>(result);
    }
    catch (HttpRequestException ex)
    {
        _logger.LogError(ex, "Replicate API call failed");
        throw new ApplicationException("Photo enhancement service unavailable");
    }
}
```

### Polling Pattern
**Pattern**: Async Operation Status Polling
**Implementation**:
```typescript
private async pollForCompletion(predictionId: string): Promise<any> {
  const maxAttempts = 60; // 5 minutes max
  let attempts = 0;
  
  while (attempts < maxAttempts) {
    const statusResponse = await this._replicateService.getPredictionStatus(predictionId).toPromise();
    
    if (statusResponse.data.status === 'succeeded') {
      return statusResponse.data;
    } else if (statusResponse.data.status === 'failed') {
      throw new Error(statusResponse.data.error || 'Operation failed');
    }
    
    await new Promise(resolve => setTimeout(resolve, 5000));
    attempts++;
  }
  
  throw new Error('Operation timed out');
}
```

## Code Organization Patterns

### Project Structure Pattern
```
AI.ProfilePhotoMaker.API/
├── Controllers/          # API endpoints
├── Services/            # Business logic
│   ├── Authentication/  # Auth services
│   ├── Database/       # DB services
│   ├── ImageProcessing/ # Image services
│   └── Storage/        # File storage
├── Models/             # Data models
│   └── DTOs/          # Data transfer objects
├── Data/              # EF context
├── Extensions/        # Service extensions
└── Middleware/        # Custom middleware
```

### Naming Conventions
- **Controllers**: `{Entity}Controller` (e.g., `ReplicateController`)
- **Services**: `I{Service}` interface, `{Service}` implementation
- **DTOs**: `{Purpose}Dto` (e.g., `EnhancePhotoRequestDto`)
- **Private fields**: `_camelCase` with underscore prefix
- **Configuration sections**: Match class names (e.g., `Replicate` section)

These patterns provide consistency, maintainability, and performance optimization across the entire codebase.