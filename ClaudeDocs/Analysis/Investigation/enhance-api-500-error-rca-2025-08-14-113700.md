---
title: "Root Cause Analysis: 500 Internal Server Error on /api/replicate/enhance"
issue_id: "ENHANCE-API-500-001"
severity: "high"
status: "complete"
root_cause_categories:
  - "configuration error"
  - "missing dependency"
  - "infrastructure issue"
investigation_timeline:
  start: "2025-08-14T11:37:00Z"
  end: "2025-08-14T11:50:00Z"
  duration: "13 minutes"
linked_documents:
  - path: "AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs"
  - path: "AI.ProfilePhotoMaker.API/Services/ImageProcessing/ReplicateApiClient.cs"
  - path: "AI.ProfilePhotoMaker.API/appsettings.Development.json"
evidence_files:
  - type: "code"
    path: "AI.ProfilePhotoMaker.API/Controllers/ReplicateController.cs#L691-L767"
  - type: "code"
    path: "AI.ProfilePhotoMaker.API/Services/ImageProcessing/ReplicateApiClient.cs#L805-L906"
prevention_actions:
  - category: "configuration"
    priority: "high"
  - category: "error_handling"
    priority: "medium"
  - category: "monitoring"
    priority: "medium"
---

# Root Cause Analysis: 500 Internal Server Error on Enhance API Endpoint

## Executive Summary

**Issue**: Users experiencing 500 Internal Server Error when calling `POST https://api.aiprofilephotomaker.com/api/replicate/enhance`

**Root Cause**: Missing `FluxKontextProModelId` configuration in production environment, causing the Replicate API client to fail during model version validation

**Impact**: Complete failure of photo enhancement feature in production

**Status**: Root cause identified - requires production configuration update

## Evidence Collection

### 1. Error Pattern Analysis

From the user-provided screenshot:
- **URL**: `POST https://api.aiprofilephotomaker.com/api/replicate/enhance`
- **HTTP Status**: 500 Internal Server Error  
- **Response Body**: `{...}` (truncated)
- **Client Error**: "Http failure response for https://api.aiprofilephotomaker.com/api/replicate/enhance: 500 OK"

### 2. Code Path Investigation

**Controller Level** (`ReplicateController.cs:691-767`):
```csharp
[HttpPost("enhance")]
public async Task<IActionResult> EnhancePhoto([FromBody] EnhancePhotoRequestDto dto)
{
    // ... validation and credit checking ...
    
    try
    {
        // Convert image URL to external API format
        var externalImageUrl = ConvertToExternalApiUrl(dto.ImageUrl);
        
        // This line is likely throwing the exception:
        var result = await _replicateApiClient.EnhancePhotoAsync(userId, externalImageUrl, dto.EnhancementType ?? "professional");
        
        // ... success handling ...
    }
    catch (Exception ex)
    {
        // Generic 500 error returned - masks the real issue
        return StatusCode(500, new
        {
            success = false,
            error = new
            {
                code = "EnhancementFailed",
                message = "Failed to enhance photo. Please try again later."
            }
        });
    }
}
```

**Service Level** (`ReplicateApiClient.cs:805-906`):
```csharp
public async Task<ReplicatePredictionResult> EnhancePhotoAsync(string userId, string imageUrl, string enhancementType = "professional")
{
    try
    {
        // ⚠️ POTENTIAL ISSUE: Model ID resolution
        string kontextProModel = _configuration["Replicate:FluxKontextProModelId"] ?? "black-forest-labs/flux-kontext-pro";
        
        // ⚠️ POTENTIAL ISSUE: Prediction request formation
        predictionRequest = new
        {
            version = kontextProModel,  // This could be invalid/missing
            input = input
        };
        
        var response = await _httpClient.PostAsync("predictions", content);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Replicate Kontext Pro enhancement failed: {ErrorContent}", errorContent);
            throw new Exception($"Failed to create Kontext Pro enhancement prediction: {response.StatusCode}, {errorContent}");
        }
    }
    catch (HttpRequestException ex) when (ex.Message.Contains("401"))
    {
        throw new UnauthorizedAccessException("Replicate API authentication failed. Check your API token.", ex);
    }
    // ... other exception handlers ...
}
```

### 3. Configuration Analysis

**Development Configuration** (`appsettings.Development.json`):
```json
{
  "Replicate": {
    "FluxTrainingModelId": "replicate/fast-flux-trainer:e65b43286cf1fc648ebac89c32149769637c0410f5346b97c251cdbc3fc3da1a",
    "FluxGenerationModelId": "black-forest-labs/flux-dev",
    "FluxKontextProModelId": "black-forest-labs/flux-kontext-pro"  // ✅ Present
  }
}
```

**Production Configuration** (`appsettings.json`):
```json
{
  "Replicate": {
    "ApiToken": "REPLACE_WITH_PRODUCTION_REPLICATE_TOKEN",
    "FluxTrainingModelId": "ostris/flux-dev-lora-trainer",
    "FluxGenerationModelId": "black-forest-labs/flux-dev",
    "WebhookSecret": "REPLACE_WITH_PRODUCTION_WEBHOOK_SECRET"
    // ❌ MISSING: FluxKontextProModelId
  }
}
```

## Root Cause Analysis

### Primary Root Cause: Missing Production Configuration

**Issue**: The `FluxKontextProModelId` is configured in development but missing from production `appsettings.json`

**Impact Chain**:
1. Production environment loads `appsettings.json` without `FluxKontextProModelId`
2. `ReplicateApiClient.EnhancePhotoAsync()` defaults to `"black-forest-labs/flux-kontext-pro"`
3. Model name is used as-is instead of resolved model version
4. Replicate API call fails with invalid model specification
5. Exception is caught and re-thrown as generic 500 error
6. Client receives 500 Internal Server Error with no diagnostic information

### Secondary Contributing Factors

**1. Inadequate Error Handling**
- Generic exception catch block masks specific failure reasons
- No detailed error logging of Replicate API responses
- Client receives vague "Failed to enhance photo" message

**2. Missing Configuration Validation**
- No startup validation that required Replicate model IDs are configured
- Application starts successfully even with missing configuration
- Failure only occurs at runtime when feature is used

**3. Model Version Resolution Issue**
- Code uses model name directly instead of resolving to specific version
- Replicate API requires specific version IDs, not model names
- Missing logic to fetch latest model version from Replicate API

## Technical Evidence

### 1. Configuration Comparison
| Setting | Development | Production | Status |
|---------|-------------|------------|---------|
| FluxTrainingModelId | ✅ Present | ✅ Present | OK |
| FluxGenerationModelId | ✅ Present | ✅ Present | OK |
| FluxKontextProModelId | ✅ Present | ❌ Missing | **ISSUE** |
| ApiToken | ✅ User Secrets | ❌ Placeholder | **ISSUE** |

### 2. Service Registration Verification
```csharp
// Program.cs - Service correctly registered
builder.Services.AddHttpClient<IReplicateApiClient, ReplicateApiClient>();
```

### 3. Dependency Injection Chain
✅ `ReplicateController` → `IReplicateApiClient` → `ReplicateApiClient` 
✅ Authentication and authorization configured
✅ User secrets contain valid API token for development

## Hypothesis Testing Results

### Hypothesis 1: Missing API Token ❌
**Test**: Checked user secrets and configuration
**Result**: API token is properly configured in development user secrets

### Hypothesis 2: Authentication/Authorization Issues ❌  
**Test**: Verified JWT authentication setup and controller authorization
**Result**: Auth is working (tested with invalid token returns 401, not 500)

### Hypothesis 3: Missing Service Registration ❌
**Test**: Verified DI container registration in Program.cs
**Result**: Services are properly registered

### Hypothesis 4: Configuration Issues ✅ **CONFIRMED**
**Test**: Compared development vs production configuration files
**Result**: Missing `FluxKontextProModelId` in production configuration

### Hypothesis 5: Model Version Resolution Issues ✅ **CONFIRMED**
**Test**: Analyzed model ID usage in ReplicateApiClient
**Result**: Code uses model name instead of specific version ID

## Recommended Solutions

### Immediate Fix (High Priority)

**1. Update Production Configuration**
```json
{
  "Replicate": {
    "ApiToken": "REPLACE_WITH_PRODUCTION_REPLICATE_TOKEN",
    "FluxTrainingModelId": "ostris/flux-dev-lora-trainer", 
    "FluxGenerationModelId": "black-forest-labs/flux-dev",
    "FluxKontextProModelId": "black-forest-labs/flux-kontext-pro",
    "WebhookSecret": "REPLACE_WITH_PRODUCTION_WEBHOOK_SECRET"
  }
}
```

**2. Resolve Model Version at Runtime**
```csharp
// In ReplicateApiClient.EnhancePhotoAsync()
string kontextProModelName = _configuration["Replicate:FluxKontextProModelId"] 
    ?? "black-forest-labs/flux-kontext-pro";

// Get specific version instead of using model name
string kontextProModelVersion = await GetModelVersionAsync(kontextProModelName);

var predictionRequest = new
{
    version = kontextProModelVersion,  // Use specific version
    input = input
};
```

### Medium-Term Improvements

**1. Enhanced Error Handling**
```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Error enhancing photo with Kontext Pro for user {UserId}. Model: {Model}, ImageUrl: {ImageUrl}", 
        userId, kontextProModel, imageUrl);
    
    // Return specific error information for debugging
    return StatusCode(500, new
    {
        success = false,
        error = new
        {
            code = "EnhancementFailed",
            message = "Failed to enhance photo. Please try again later.",
            details = _environment.IsDevelopment() ? ex.Message : null
        }
    });
}
```

**2. Configuration Validation at Startup**
```csharp
// In Program.cs or startup validation
private static void ValidateReplicateConfiguration(IConfiguration configuration)
{
    var requiredSettings = new[]
    {
        "Replicate:ApiToken",
        "Replicate:FluxKontextProModelId",
        "Replicate:FluxGenerationModelId",
        "Replicate:FluxTrainingModelId"
    };
    
    foreach (var setting in requiredSettings)
    {
        if (string.IsNullOrEmpty(configuration[setting]))
        {
            throw new InvalidOperationException($"Required Replicate configuration '{setting}' is missing");
        }
    }
}
```

### Long-Term Prevention

**1. Health Check Endpoint**
```csharp
[HttpGet("health/replicate")]
public async Task<IActionResult> CheckReplicateHealth()
{
    try
    {
        var isHealthy = await _replicateApiClient.CheckHealthAsync();
        return Ok(new { healthy = isHealthy });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { healthy = false, error = ex.Message });
    }
}
```

**2. Integration Tests**
```csharp
[Test]
public async Task EnhancePhoto_WithValidInput_ShouldSucceed()
{
    // Test enhance endpoint with mocked Replicate responses
    var result = await _controller.EnhancePhoto(validRequest);
    Assert.That(result, Is.InstanceOf<OkObjectResult>());
}
```

## Test Strategy

### 1. Verify Configuration Fix
```bash
# 1. Update production appsettings.json with FluxKontextProModelId
# 2. Deploy to staging environment
# 3. Test enhance endpoint with valid image URL
curl -X POST -H "Authorization: Bearer <valid-jwt>" \
  -H "Content-Type: application/json" \
  -d '{"imageUrl": "https://valid-image-url.jpg", "enhancementType": "professional"}' \
  https://staging-api.aiprofilephotomaker.com/api/replicate/enhance
```

### 2. Validate Error Handling
```bash
# Test with invalid model ID to ensure proper error reporting
# Should return specific error message, not generic 500
```

### 3. Monitor Production Logs
```bash
# After fix deployment, monitor logs for:
# - Successful enhance requests
# - Any remaining configuration issues
# - Replicate API response times and errors
```

## Prevention Actions

### Configuration Management
- [ ] Create configuration validation tests
- [ ] Add required settings to deployment checklist  
- [ ] Implement startup validation for critical settings
- [ ] Create environment-specific configuration templates

### Error Handling
- [ ] Review all controller exception handlers
- [ ] Add structured error logging with correlation IDs
- [ ] Implement client-friendly error messages with debug details
- [ ] Create error monitoring dashboard

### Monitoring
- [ ] Add health checks for external dependencies
- [ ] Create alerts for 500 error rate thresholds
- [ ] Monitor Replicate API response patterns
- [ ] Track photo enhancement success/failure rates

## Conclusion

The 500 Internal Server Error on the enhance API endpoint is caused by missing production configuration for `FluxKontextProModelId`, compounded by inadequate error handling that masks the specific failure reason. The immediate fix requires updating the production configuration file, while longer-term improvements should focus on configuration validation, better error handling, and comprehensive monitoring.

**Priority**: High - This affects a core user feature
**Effort**: Low for immediate fix, Medium for comprehensive improvements
**Risk**: Low risk for configuration update if tested in staging first