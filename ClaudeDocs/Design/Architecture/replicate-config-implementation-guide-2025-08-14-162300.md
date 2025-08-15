---
title: "Replicate Configuration Implementation Guide"
system_id: "replicate-config-impl"
complexity: "medium"
status: "implementation-ready"
architectural_patterns:
  - "configuration-management"
  - "dependency-injection"
  - "health-checks"
scalability_metrics:
  current_capacity: "3 Model IDs"
  target_capacity: "10+ Model IDs"
  scaling_approach: "horizontal"
technology_stack:
  - backend: "ASP.NET Core 8.0"
  - configuration: "IConfiguration, IOptions"
  - validation: "Health Checks, Startup Filters"
design_timeline:
  start: "2025-08-14T16:23:00Z"
  implementation: "2025-08-15T10:00:00Z"
  completion: "2025-08-15T16:00:00Z"
dependencies:
  - system: "Replicate API"
    type: "external"
  - system: "Azure Key Vault"
    type: "external"
quality_attributes:
  - attribute: "reliability"
    priority: "critical"
  - attribute: "maintainability"
    priority: "high"
---

# Replicate Configuration Implementation Guide

## Implementation Overview

This guide provides step-by-step instructions for implementing the Replicate configuration management architecture outlined in the main design document. All changes follow YAGNI principles and focus on production reliability.

## Current State Analysis

### Existing Configuration Locations

**File: `AI.ProfilePhotoMaker.API/Program.cs` (Lines 1000-1034)**
```csharp
// Current validation logic - needs enhancement
var fluxTrainingModelId = configuration["Replicate:FluxTrainingModelId"];
var fluxGenerationModelId = configuration["Replicate:FluxGenerationModelId"];
var fluxKontextProModelId = configuration["Replicate:FluxKontextProModelId"];
```

**File: `AI.ProfilePhotoMaker.API/Services/ImageProcessing/ReplicateApiClient.cs`**
```csharp
// Multiple hardcoded fallbacks throughout the file
string kontextProModel = _configuration["Replicate:FluxKontextProModelId"] ?? "black-forest-labs/flux-kontext-pro";
string baseFluxModel = _configuration["Replicate:FluxGenerationModelId"] ?? "black-forest-labs/flux-dev";
```

## Implementation Steps

### Step 1: Create Configuration Models

**File: `AI.ProfilePhotoMaker.API/Configuration/ReplicateConfiguration.cs`**

```csharp
namespace AI.ProfilePhotoMaker.API.Configuration;

public class ReplicateConfiguration
{
    public const string SectionName = "Replicate";
    
    public string ApiToken { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public ReplicateModelsConfiguration Models { get; set; } = new();
    public ReplicateValidationConfiguration Validation { get; set; } = new();
}

public class ReplicateModelsConfiguration
{
    public ReplicateModelGroup Training { get; set; } = new();
    public ReplicateModelGroup Generation { get; set; } = new();
    public ReplicateModelGroup Enhancement { get; set; } = new();
}

public class ReplicateModelGroup
{
    public string Primary { get; set; } = string.Empty;
    public string Fallback { get; set; } = string.Empty;
    public List<string> Additional { get; set; } = new();
}

public class ReplicateValidationConfiguration
{
    public bool EnableModelVersionCheck { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 30;
    public int RetryAttempts { get; set; } = 3;
    public bool FailFastOnStartup { get; set; } = true;
}

public enum ReplicateModelType
{
    Training,
    Generation,
    Enhancement
}
```

### Step 2: Create Configuration Service

**File: `AI.ProfilePhotoMaker.API/Services/Configuration/IReplicateConfigurationService.cs`**

```csharp
namespace AI.ProfilePhotoMaker.API.Services.Configuration;

public interface IReplicateConfigurationService
{
    Task<string> GetModelIdAsync(ReplicateModelType modelType);
    Task<ReplicateModelValidationResult> ValidateAllModelsAsync();
    Task<bool> IsModelAvailableAsync(string modelId);
    string GetApiToken();
    string GetWebhookSecret();
}

public class ReplicateModelValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public Dictionary<ReplicateModelType, string> ResolvedModels { get; set; } = new();
}
```

**File: `AI.ProfilePhotoMaker.API/Services/Configuration/ReplicateConfigurationService.cs`**

```csharp
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Services.ImageProcessing;

namespace AI.ProfilePhotoMaker.API.Services.Configuration;

public class ReplicateConfigurationService : IReplicateConfigurationService
{
    private readonly IOptions<ReplicateConfiguration> _options;
    private readonly ILogger<ReplicateConfigurationService> _logger;
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    
    // Fallback model definitions - last resort if configuration is missing
    private static readonly Dictionary<ReplicateModelType, List<string>> FallbackModels = new()
    {
        [ReplicateModelType.Training] = new() 
        { 
            "ostris/flux-dev-lora-trainer",
            "replicate/fast-flux-trainer"
        },
        [ReplicateModelType.Generation] = new() 
        { 
            "black-forest-labs/flux-dev",
            "black-forest-labs/flux-schnell"
        },
        [ReplicateModelType.Enhancement] = new() 
        { 
            "black-forest-labs/flux-kontext-pro",
            "black-forest-labs/flux-dev"
        }
    };
    
    public ReplicateConfigurationService(
        IOptions<ReplicateConfiguration> options,
        ILogger<ReplicateConfigurationService> logger,
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory)
    {
        _options = options;
        _logger = logger;
        _cache = cache;
        _httpClientFactory = httpClientFactory;
    }
    
    public async Task<string> GetModelIdAsync(ReplicateModelType modelType)
    {
        var cacheKey = $"replicate_model_{modelType}";
        
        if (_cache.TryGetValue(cacheKey, out string? cachedModelId) && !string.IsNullOrEmpty(cachedModelId))
        {
            return cachedModelId;
        }
        
        var candidates = GetModelCandidates(modelType);
        
        foreach (var candidate in candidates)
        {
            if (await IsModelAvailableAsync(candidate))
            {
                _logger.LogInformation("Resolved {ModelType} model: {ModelId}", modelType, candidate);
                
                // Cache for 5 minutes
                _cache.Set(cacheKey, candidate, TimeSpan.FromMinutes(5));
                return candidate;
            }
            
            _logger.LogWarning("Model {ModelId} unavailable for {ModelType}, trying next candidate", 
                candidate, modelType);
        }
        
        throw new InvalidOperationException($"No available {modelType} models found");
    }
    
    public async Task<ReplicateModelValidationResult> ValidateAllModelsAsync()
    {
        var result = new ReplicateModelValidationResult();
        
        foreach (ReplicateModelType modelType in Enum.GetValues<ReplicateModelType>())
        {
            try
            {
                var modelId = await GetModelIdAsync(modelType);
                result.ResolvedModels[modelType] = modelId;
                _logger.LogInformation("✅ {ModelType} model validated: {ModelId}", modelType, modelId);
            }
            catch (Exception ex)
            {
                var error = $"{modelType} model validation failed: {ex.Message}";
                result.Errors.Add(error);
                _logger.LogError(ex, "❌ {Error}", error);
            }
        }
        
        // Validate API token and webhook secret
        if (string.IsNullOrEmpty(GetApiToken()))
        {
            result.Errors.Add("Replicate API token is missing");
        }
        
        if (string.IsNullOrEmpty(GetWebhookSecret()))
        {
            result.Warnings.Add("Replicate webhook secret is missing");
        }
        
        result.IsValid = result.Errors.Count == 0;
        return result;
    }
    
    public async Task<bool> IsModelAvailableAsync(string modelId)
    {
        if (string.IsNullOrEmpty(modelId))
            return false;
            
        var cacheKey = $"model_available_{modelId}";
        
        if (_cache.TryGetValue(cacheKey, out bool cachedResult))
        {
            return cachedResult;
        }
        
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Token {GetApiToken()}");
            
            // Parse model ID to extract owner and name
            var parts = modelId.Split('/');
            if (parts.Length != 2)
            {
                _logger.LogWarning("Invalid model ID format: {ModelId}", modelId);
                return false;
            }
            
            var modelName = parts[1].Split(':')[0]; // Remove version if present
            var response = await client.GetAsync($"https://api.replicate.com/v1/models/{parts[0]}/{modelName}");
            
            var isAvailable = response.IsSuccessStatusCode;
            
            // Cache result for 1 minute
            _cache.Set(cacheKey, isAvailable, TimeSpan.FromMinutes(1));
            
            return isAvailable;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check availability for model {ModelId}", modelId);
            return false;
        }
    }
    
    public string GetApiToken()
    {
        return _options.Value.ApiToken;
    }
    
    public string GetWebhookSecret()
    {
        return _options.Value.WebhookSecret;
    }
    
    private List<string> GetModelCandidates(ReplicateModelType modelType)
    {
        var candidates = new List<string>();
        var config = _options.Value.Models;
        
        // Add configured models first
        var modelGroup = modelType switch
        {
            ReplicateModelType.Training => config.Training,
            ReplicateModelType.Generation => config.Generation,
            ReplicateModelType.Enhancement => config.Enhancement,
            _ => throw new ArgumentException($"Unknown model type: {modelType}")
        };
        
        if (!string.IsNullOrEmpty(modelGroup.Primary))
            candidates.Add(modelGroup.Primary);
            
        if (!string.IsNullOrEmpty(modelGroup.Fallback))
            candidates.Add(modelGroup.Fallback);
            
        candidates.AddRange(modelGroup.Additional.Where(x => !string.IsNullOrEmpty(x)));
        
        // Add hardcoded fallbacks as last resort
        if (FallbackModels.TryGetValue(modelType, out var fallbacks))
        {
            candidates.AddRange(fallbacks.Where(f => !candidates.Contains(f)));
        }
        
        return candidates.Where(c => !string.IsNullOrEmpty(c)).ToList();
    }
}
```

### Step 3: Create Health Check

**File: `AI.ProfilePhotoMaker.API/HealthChecks/ReplicateConfigurationHealthCheck.cs`**

```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;
using AI.ProfilePhotoMaker.API.Services.Configuration;

namespace AI.ProfilePhotoMaker.API.HealthChecks;

public class ReplicateConfigurationHealthCheck : IHealthCheck
{
    private readonly IReplicateConfigurationService _configService;
    private readonly ILogger<ReplicateConfigurationHealthCheck> _logger;
    
    public ReplicateConfigurationHealthCheck(
        IReplicateConfigurationService configService,
        ILogger<ReplicateConfigurationHealthCheck> logger)
    {
        _configService = configService;
        _logger = logger;
    }
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = await _configService.ValidateAllModelsAsync();
            
            var data = new Dictionary<string, object>
            {
                ["resolved_models"] = validationResult.ResolvedModels,
                ["errors"] = validationResult.Errors,
                ["warnings"] = validationResult.Warnings
            };
            
            if (validationResult.IsValid)
            {
                if (validationResult.Warnings.Count > 0)
                {
                    return HealthCheckResult.Degraded(
                        $"Replicate configuration has warnings: {string.Join(", ", validationResult.Warnings)}", 
                        data: data);
                }
                
                return HealthCheckResult.Healthy("All Replicate models configured and available", data);
            }
            
            return HealthCheckResult.Unhealthy(
                $"Replicate configuration validation failed: {string.Join(", ", validationResult.Errors)}", 
                data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Replicate configuration health check failed");
            return HealthCheckResult.Unhealthy("Replicate configuration health check failed", ex);
        }
    }
}
```

### Step 4: Update Program.cs

**File: `AI.ProfilePhotoMaker.API/Program.cs`** - Replace existing validation (lines 1000-1034):

```csharp
// Add after existing service registrations
builder.Services.Configure<ReplicateConfiguration>(
    builder.Configuration.GetSection(ReplicateConfiguration.SectionName));

builder.Services.AddSingleton<IReplicateConfigurationService, ReplicateConfigurationService>();

// Add health check
builder.Services.AddHealthChecks()
    .AddCheck<ReplicateConfigurationHealthCheck>("replicate-config");

// ... existing code ...

// Replace the existing Replicate validation section with:
private static async Task ValidateReplicateConfiguration(
    IServiceProvider serviceProvider, 
    ILogger logger,
    List<string> configurationErrors,
    List<string> configurationWarnings)
{
    try
    {
        var configService = serviceProvider.GetRequiredService<IReplicateConfigurationService>();
        var validationResult = await configService.ValidateAllModelsAsync();
        
        if (!validationResult.IsValid)
        {
            configurationErrors.AddRange(validationResult.Errors);
        }
        
        configurationWarnings.AddRange(validationResult.Warnings);
        
        foreach (var (modelType, modelId) in validationResult.ResolvedModels)
        {
            logger.LogInformation("✅ {ModelType} Model: {ModelId}", modelType, modelId);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to validate Replicate configuration");
        configurationErrors.Add("Replicate configuration validation failed");
    }
}

// Update the main validation call:
await ValidateReplicateConfiguration(app.Services, logger, configurationErrors, configurationWarnings);
```

### Step 5: Update ReplicateApiClient

**File: `AI.ProfilePhotoMaker.API/Services/ImageProcessing/ReplicateApiClient.cs`** - Update constructor and methods:

```csharp
public class ReplicateApiClient : IReplicateApiClient
{
    private readonly IReplicateConfigurationService _configService;
    // ... existing fields ...
    
    public ReplicateApiClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ReplicateApiClient> logger,
        IReplicateConfigurationService configService) // Add this parameter
    {
        _configService = configService;
        // ... existing initialization ...
    }
    
    // Update methods to use configuration service:
    
    public async Task<ReplicatePredictionResult> GenerateBasicImageAsync(string userId, UserInfo? userInfo, string gender)
    {
        try
        {
            // Replace hardcoded model resolution
            string baseFluxModel = await _configService.GetModelIdAsync(ReplicateModelType.Generation);
            
            // ... rest of method unchanged ...
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Basic image generation failed for user {UserId}", userId);
            throw;
        }
    }
    
    public async Task<ReplicatePredictionResult> EnhancePhotoAsync(string userId, string imageUrl, string enhancementType = "professional")
    {
        try
        {
            // Replace hardcoded model resolution
            string kontextProModel = await _configService.GetModelIdAsync(ReplicateModelType.Enhancement);
            
            // ... rest of method unchanged ...
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Photo enhancement failed for user {UserId}", userId);
            throw;
        }
    }
    
    public async Task<ReplicateTrainingResult> CreateModelTrainingAsync(string userId, string imageZipUrl)
    {
        try
        {
            // Replace hardcoded model resolution
            var modelVersion = await _configService.GetModelIdAsync(ReplicateModelType.Training);
            
            // Update endpoint construction to handle both versioned and non-versioned models
            string endpoint;
            if (modelVersion.Contains(':'))
            {
                // Versioned model (e.g., "replicate/fast-flux-trainer:e65b43286cf1fc648ebac89c32149769637c0410f5346b97c251cdbc3fc3da1a")
                var parts = modelVersion.Split(':');
                endpoint = $"models/{parts[0]}/versions/{parts[1]}/trainings";
            }
            else
            {
                // Non-versioned model (e.g., "ostris/flux-dev-lora-trainer")
                endpoint = $"models/{modelVersion}/trainings";
            }
            
            // ... rest of method unchanged ...
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Model training creation failed for user {UserId}", userId);
            throw;
        }
    }
}
```

### Step 6: Update Configuration Files

**File: `AI.ProfilePhotoMaker.API/appsettings.json`** - Replace Replicate section:

```json
{
  "Replicate": {
    "ApiToken": "REPLACE_WITH_PRODUCTION_REPLICATE_TOKEN",
    "WebhookSecret": "REPLACE_WITH_PRODUCTION_WEBHOOK_SECRET",
    "Models": {
      "Training": {
        "Primary": "ostris/flux-dev-lora-trainer",
        "Fallback": "replicate/fast-flux-trainer"
      },
      "Generation": {
        "Primary": "black-forest-labs/flux-dev",
        "Fallback": "black-forest-labs/flux-schnell"
      },
      "Enhancement": {
        "Primary": "black-forest-labs/flux-kontext-pro",
        "Fallback": "black-forest-labs/flux-dev"
      }
    },
    "Validation": {
      "EnableModelVersionCheck": true,
      "TimeoutSeconds": 30,
      "RetryAttempts": 3,
      "FailFastOnStartup": true
    }
  }
}
```

**File: `AI.ProfilePhotoMaker.API/appsettings.Development.json`** - Replace Replicate section:

```json
{
  "Replicate": {
    "Models": {
      "Training": {
        "Primary": "replicate/fast-flux-trainer:e65b43286cf1fc648ebac89c32149769637c0410f5346b97c251cdbc3fc3da1a",
        "Fallback": "ostris/flux-dev-lora-trainer"
      },
      "Generation": {
        "Primary": "black-forest-labs/flux-dev"
      },
      "Enhancement": {
        "Primary": "black-forest-labs/flux-kontext-pro"
      }
    },
    "Validation": {
      "EnableModelVersionCheck": false,
      "FailFastOnStartup": false
    }
  }
}
```

## Testing Plan

### Unit Tests

**File: `AI.ProfilePhotoMaker.API.Tests/Services/ReplicateConfigurationServiceTests.cs`**

```csharp
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Xunit;
using FluentAssertions;
using AI.ProfilePhotoMaker.API.Configuration;
using AI.ProfilePhotoMaker.API.Services.Configuration;

namespace AI.ProfilePhotoMaker.API.Tests.Services;

public class ReplicateConfigurationServiceTests
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<ReplicateConfigurationService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    
    public ReplicateConfigurationServiceTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _logger = Substitute.For<ILogger<ReplicateConfigurationService>>();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
    }
    
    [Fact]
    public async Task GetModelIdAsync_WithValidPrimaryModel_ReturnsPrimaryModel()
    {
        // Arrange
        var config = new ReplicateConfiguration
        {
            Models = new ReplicateModelsConfiguration
            {
                Training = new ReplicateModelGroup
                {
                    Primary = "test/primary-model",
                    Fallback = "test/fallback-model"
                }
            }
        };
        
        var options = Options.Create(config);
        var service = new ReplicateConfigurationService(options, _logger, _cache, _httpClientFactory);
        
        // Act
        var result = await service.GetModelIdAsync(ReplicateModelType.Training);
        
        // Assert
        result.Should().Be("test/primary-model");
    }
    
    [Fact]
    public async Task ValidateAllModelsAsync_WithMissingModels_ReturnsErrors()
    {
        // Arrange
        var config = new ReplicateConfiguration(); // Empty config
        var options = Options.Create(config);
        var service = new ReplicateConfigurationService(options, _logger, _cache, _httpClientFactory);
        
        // Act
        var result = await service.ValidateAllModelsAsync();
        
        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }
}
```

### Integration Tests

**File: `AI.ProfilePhotoMaker.API.Tests/Integration/ReplicateConfigurationIntegrationTests.cs`**

```csharp
[Collection("Integration")]
public class ReplicateConfigurationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    
    public ReplicateConfigurationIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }
    
    [Fact]
    public async Task HealthCheck_ReplicateConfiguration_ReturnsHealthy()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.GetAsync("/health");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
    }
    
    [Fact]
    public async Task ReplicateConfigurationService_GetModelId_ReturnsValidModel()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var configService = scope.ServiceProvider.GetRequiredService<IReplicateConfigurationService>();
        
        // Act
        var trainingModel = await configService.GetModelIdAsync(ReplicateModelType.Training);
        var generationModel = await configService.GetModelIdAsync(ReplicateModelType.Generation);
        var enhancementModel = await configService.GetModelIdAsync(ReplicateModelType.Enhancement);
        
        // Assert
        trainingModel.Should().NotBeNullOrEmpty();
        generationModel.Should().NotBeNullOrEmpty();
        enhancementModel.Should().NotBeNullOrEmpty();
    }
}
```

## Deployment Steps

### Step 1: Backup Current Configuration

```bash
# Backup current appsettings files
cp AI.ProfilePhotoMaker.API/appsettings.json AI.ProfilePhotoMaker.API/appsettings.json.backup
cp AI.ProfilePhotoMaker.API/appsettings.Development.json AI.ProfilePhotoMaker.API/appsettings.Development.json.backup
```

### Step 2: Deploy Code Changes

1. **Commit and push code changes**
2. **Run tests to ensure no regressions**
3. **Deploy to staging environment first**

### Step 3: Update Configuration

```bash
# Update user secrets for development
dotnet user-secrets set "Replicate:ApiToken" "your-api-token-here" --project AI.ProfilePhotoMaker.API
dotnet user-secrets set "Replicate:WebhookSecret" "whsec_wUh0bvV+/jGsxbEqPsRgHI1tdct1Y+KM" --project AI.ProfilePhotoMaker.API
```

### Step 4: Validate Deployment

```bash
# Check health endpoint
curl https://api.aiprofilephotomaker.com/health

# Check specific Replicate health
curl https://api.aiprofilephotomaker.com/health/ready
```

## Monitoring and Alerts

### Health Check Monitoring

```json
{
  "healthChecks": {
    "replicate-config": {
      "alertOnUnhealthy": true,
      "alertOnDegraded": false,
      "checkIntervalSeconds": 300
    }
  }
}
```

### Application Insights Queries

```kusto
// Configuration errors
traces
| where customDimensions.CategoryName contains "ReplicateConfiguration"
| where severityLevel >= 3
| summarize count() by bin(timestamp, 5m), message

// Model resolution patterns
traces
| where message contains "Resolved" and message contains "model"
| summarize count() by bin(timestamp, 1h), tostring(customDimensions.ModelType)
```

## Rollback Plan

If issues arise after deployment:

1. **Immediate**: Restore backup configuration files
2. **Code rollback**: Revert to previous working version
3. **Database**: No database changes in this implementation
4. **Monitoring**: Verify health checks return to healthy state

## Success Criteria

- [ ] **No 500 errors** due to missing configuration
- [ ] **Health checks passing** for all Replicate models
- [ ] **Fallback models working** when primary models unavailable
- [ ] **Performance maintained** (sub-100ms configuration resolution)
- [ ] **Monitoring in place** with alerts for configuration issues

This implementation provides immediate value by preventing configuration-related production failures while establishing a foundation for future scalability.