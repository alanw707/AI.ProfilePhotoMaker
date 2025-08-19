---
title: "Replicate Configuration Management Architecture"
system_id: "replicate-config-mgmt"
complexity: "medium"
status: "draft"
architectural_patterns:
  - "configuration-management"
  - "fail-fast-validation"
  - "environment-specific-config"
  - "secrets-management"
scalability_metrics:
  current_capacity: "3 Model IDs"
  target_capacity: "10+ Model IDs"
  scaling_approach: "horizontal"
technology_stack:
  - backend: "ASP.NET Core, IConfiguration"
  - secrets: "Azure Key Vault, dotnet user-secrets"
  - validation: "Startup validation, Health checks"
design_timeline:
  start: "2025-08-14T16:22:00Z"
  review: "2025-08-15T10:00:00Z"
  completion: "2025-08-16T12:00:00Z"
linked_documents:
  - path: "ClaudeDocs/Analysis/Investigation/enhance-api-500-error-rca-2025-08-14-113700.md"
  - path: "AI.ProfilePhotoMaker.API/Program.cs"
dependencies:
  - system: "Azure Key Vault"
    type: "external"
  - system: "Replicate API"
    type: "external"
quality_attributes:
  - attribute: "reliability"
    priority: "critical"
  - attribute: "security"
    priority: "high"
  - attribute: "maintainability"
    priority: "high"
---

# Replicate Configuration Management Architecture

## Executive Summary

This document outlines a comprehensive configuration management solution for Replicate model IDs and API tokens to prevent deployment failures and ensure consistent behavior across environments. The solution addresses the root cause of recent production issues where missing `FluxKontextProModelId` caused 500 errors.

## Problem Analysis

### Current Issues

#### 1. Configuration Audit Results

**Identified Replicate Model IDs:**
- `FluxTrainingModelId` - Used for custom model training
- `FluxGenerationModelId` - Used for basic image generation  
- `FluxKontextProModelId` - Used for photo enhancement (caused recent 500 error)

**Current Configuration State:**
```json
// appsettings.json (Production)
{
  "Replicate": {
    "FluxTrainingModelId": "ostris/flux-dev-lora-trainer",
    "FluxGenerationModelId": "black-forest-labs/flux-dev", 
    "FluxKontextProModelId": "black-forest-labs/flux-kontext-pro" // ✅ NOW PRESENT
  }
}

// appsettings.Development.json
{
  "Replicate": {
    "FluxTrainingModelId": "replicate/fast-flux-trainer:e65b43286cf1fc648ebac89c32149769637c0410f5346b97c251cdbc3fc3da1a",
    "FluxGenerationModelId": "black-forest-labs/flux-dev",
    "FluxKontextProModelId": "black-forest-labs/flux-kontext-pro"
  }
}
```

#### 2. Structural Problems

**Configuration Inconsistencies:**
- Development uses specific version IDs (`:e65b43286cf1fc648ebac89c32149769637c0410f5346b97c251cdbc3fc3da1a`)
- Production uses model names without versions
- No validation to ensure required configurations are present before deployment

**Runtime Issues:**
- Missing configurations cause 500 errors in production
- Error messages don't clearly indicate configuration problems
- No fail-fast validation during startup

**Secrets Management:**
- Model IDs mixed with sensitive tokens in same configuration section
- No clear distinction between public model IDs and sensitive API tokens

## Recommended Architecture

### 1. Configuration Structure Design

#### Hierarchical Configuration Model

```json
{
  "Replicate": {
    "ApiToken": "SENSITIVE_TOKEN_HERE",
    "WebhookSecret": "SENSITIVE_SECRET_HERE",
    "Models": {
      "Training": {
        "Primary": "replicate/fast-flux-trainer:e65b43286cf1fc648ebac89c32149769637c0410f5346b97c251cdbc3fc3da1a",
        "Fallback": "ostris/flux-dev-lora-trainer"
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
      "RetryAttempts": 3
    }
  }
}
```

#### Environment-Specific Overrides

**Development Environment:**
```json
{
  "Replicate": {
    "Models": {
      "Training": {
        "Primary": "replicate/fast-flux-trainer:e65b43286cf1fc648ebac89c32149769637c0410f5346b97c251cdbc3fc3da1a"
      }
    },
    "Validation": {
      "EnableModelVersionCheck": false
    }
  }
}
```

**Production Environment:**
```json
{
  "Replicate": {
    "Models": {
      "Training": {
        "Primary": "ostris/flux-dev-lora-trainer"
      }
    },
    "Validation": {
      "EnableModelVersionCheck": true
    }
  }
}
```

### 2. Configuration Service Architecture

#### ReplicateConfigurationService Design

```csharp
public interface IReplicateConfigurationService
{
    Task<string> GetTrainingModelIdAsync();
    Task<string> GetGenerationModelIdAsync();
    Task<string> GetEnhancementModelIdAsync();
    Task<ValidationResult> ValidateAllModelsAsync();
    Task<bool> IsModelAvailableAsync(string modelId);
}

public class ReplicateConfigurationService : IReplicateConfigurationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReplicateConfigurationService> _logger;
    private readonly IReplicateApiClient _apiClient;
    private readonly IMemoryCache _cache;
    
    public async Task<string> GetTrainingModelIdAsync()
    {
        var primary = _configuration["Replicate:Models:Training:Primary"];
        if (!string.IsNullOrEmpty(primary))
        {
            if (await IsModelAvailableAsync(primary))
                return primary;
        }
        
        var fallback = _configuration["Replicate:Models:Training:Fallback"];
        if (!string.IsNullOrEmpty(fallback))
        {
            _logger.LogWarning("Primary training model unavailable, using fallback: {Fallback}", fallback);
            return fallback;
        }
        
        throw new ConfigurationException("No valid training model configured");
    }
}
```

### 3. Startup Validation System

#### Configuration Validation Pipeline

```csharp
public static class ReplicateConfigurationExtensions
{
    public static IServiceCollection AddReplicateConfiguration(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Register configuration service
        services.AddSingleton<IReplicateConfigurationService, ReplicateConfigurationService>();
        
        // Add validation
        services.AddSingleton<IStartupFilter, ReplicateConfigurationValidationFilter>();
        
        return services;
    }
}

public class ReplicateConfigurationValidationFilter : IStartupFilter
{
    private readonly IReplicateConfigurationService _configService;
    
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return async builder =>
        {
            // Validate all required configurations at startup
            var validationResult = await _configService.ValidateAllModelsAsync();
            
            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors);
                throw new ApplicationException($"Replicate configuration validation failed: {errors}");
            }
            
            next(builder);
        };
    }
}
```

### 4. Health Check Integration

#### Replicate Configuration Health Check

```csharp
public class ReplicateConfigurationHealthCheck : IHealthCheck
{
    private readonly IReplicateConfigurationService _configService;
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var validationResult = await _configService.ValidateAllModelsAsync();
            
            if (validationResult.IsValid)
            {
                return HealthCheckResult.Healthy("All Replicate models configured and available");
            }
            
            return HealthCheckResult.Degraded(
                $"Some Replicate models unavailable: {string.Join(", ", validationResult.Warnings)}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Replicate configuration validation failed", ex);
        }
    }
}
```

### 5. Secrets Management Strategy

#### Separation of Concerns

**Public Configuration (appsettings.json):**
```json
{
  "Replicate": {
    "Models": {
      "Training": {
        "Primary": "ostris/flux-dev-lora-trainer"
      }
    },
    "Endpoints": {
      "BaseUrl": "https://api.replicate.com/v1"
    }
  }
}
```

**Sensitive Configuration (Azure Key Vault/User Secrets):**
```json
{
  "Replicate:ApiToken": "r8_xxx_sensitive_token",
  "Replicate:WebhookSecret": "whsec_xxx_sensitive_secret"
}
```

#### Azure Key Vault Integration

**Environment Variables (Production):**
```bash
REPLICATE_API_TOKEN="@Microsoft.KeyVault(VaultName=aipm-kv-v1;SecretName=ReplicateApiToken)"
REPLICATE_WEBHOOK_SECRET="@Microsoft.KeyVault(VaultName=aipm-kv-v1;SecretName=ReplicateWebhookSecret)"
```

### 6. Error Handling and Fallback Strategy

#### Multi-Layer Fallback System

```csharp
public class ReplicateModelResolver
{
    public async Task<string> ResolveModelAsync(ModelType modelType)
    {
        var candidates = GetModelCandidates(modelType);
        
        foreach (var candidate in candidates)
        {
            if (await IsModelAvailable(candidate))
            {
                _logger.LogInformation("Using {ModelType} model: {ModelId}", modelType, candidate);
                return candidate;
            }
            
            _logger.LogWarning("Model {ModelId} unavailable, trying next candidate", candidate);
        }
        
        throw new InvalidOperationException($"No available {modelType} models found");
    }
    
    private List<string> GetModelCandidates(ModelType modelType)
    {
        return modelType switch
        {
            ModelType.Training => new[]
            {
                _configuration["Replicate:Models:Training:Primary"],
                _configuration["Replicate:Models:Training:Fallback"],
                "ostris/flux-dev-lora-trainer" // Hard-coded last resort
            }.Where(x => !string.IsNullOrEmpty(x)).ToList(),
            
            ModelType.Enhancement => new[]
            {
                _configuration["Replicate:Models:Enhancement:Primary"],
                _configuration["Replicate:Models:Enhancement:Fallback"],
                "black-forest-labs/flux-kontext-pro" // Hard-coded last resort
            }.Where(x => !string.IsNullOrEmpty(x)).ToList(),
            
            _ => throw new ArgumentException($"Unknown model type: {modelType}")
        };
    }
}
```

## Implementation Plan

### Phase 1: Core Configuration Service (Day 1)

1. **Create Configuration Service**
   - Implement `IReplicateConfigurationService`
   - Add model resolution with fallback logic
   - Create configuration validation

2. **Update Startup Validation**
   - Enhance existing validation in `Program.cs`
   - Add fail-fast validation for all required models
   - Improve error messages

3. **Refactor API Client**
   - Update `ReplicateApiClient` to use configuration service
   - Remove hard-coded model IDs
   - Add proper error handling

### Phase 2: Enhanced Validation (Day 2)

1. **Health Check Integration**
   - Implement `ReplicateConfigurationHealthCheck`
   - Add to health check pipeline
   - Create monitoring dashboard integration

2. **Configuration Structure Migration**
   - Update `appsettings.json` structure
   - Migrate to hierarchical model configuration
   - Test across all environments

### Phase 3: Secrets Management Optimization (Day 3)

1. **Azure Key Vault Integration**
   - Separate sensitive tokens from public model IDs
   - Update deployment scripts
   - Document secret management procedures

2. **Environment-Specific Configuration**
   - Create environment-specific overrides
   - Validate configuration consistency
   - Update deployment validation

## Deployment Checklist

### Pre-Deployment Validation

- [ ] **Configuration Validation**
  - [ ] All required Replicate model IDs configured
  - [ ] Model IDs validated against Replicate API
  - [ ] Fallback models available and tested

- [ ] **Secrets Management**
  - [ ] API tokens properly configured in Key Vault
  - [ ] Webhook secrets synchronized across environments
  - [ ] Environment variables correctly set

- [ ] **Health Checks**
  - [ ] Replicate configuration health check passing
  - [ ] All model endpoints accessible
  - [ ] Fallback mechanisms tested

### Post-Deployment Verification

- [ ] **Functional Testing**
  - [ ] Model training endpoint working
  - [ ] Image generation endpoint working
  - [ ] Photo enhancement endpoint working

- [ ] **Monitoring Setup**
  - [ ] Configuration health checks reporting
  - [ ] Error alerting configured
  - [ ] Performance metrics baseline established

## Risk Mitigation

### Configuration Risks

**Risk: Missing Model Configuration**
- **Mitigation**: Fail-fast startup validation
- **Fallback**: Multiple model candidates per category
- **Monitoring**: Health checks every 60 seconds

**Risk: Model API Changes**
- **Mitigation**: Version-specific model IDs where possible
- **Fallback**: Multiple model versions configured
- **Monitoring**: Model availability health checks

**Risk: Secrets Exposure**
- **Mitigation**: Separate sensitive from non-sensitive config
- **Fallback**: Azure Key Vault for production secrets
- **Monitoring**: Secret rotation policies

### Operational Risks

**Risk: Deployment Configuration Drift**
- **Mitigation**: Automated configuration validation in CI/CD
- **Fallback**: Environment-specific validation scripts
- **Monitoring**: Configuration comparison across environments

**Risk: API Rate Limits**
- **Mitigation**: Cached model availability checks
- **Fallback**: Circuit breaker pattern for validation
- **Monitoring**: API usage metrics and alerts

## Quality Standards Compliance

### 10x Growth Planning
- **Model Scaling**: Architecture supports 10+ model types
- **Environment Scaling**: Supports unlimited environment configurations
- **Performance**: Sub-100ms configuration resolution with caching

### Dependency Transparency
- **External Dependencies**: Replicate API clearly documented
- **Internal Dependencies**: Configuration service interfaces defined
- **Coupling Analysis**: Loose coupling through interface abstraction

### Decision Traceability
- **Model Selection**: Logged at INFO level with rationale
- **Fallback Usage**: Logged at WARNING level with context
- **Validation Results**: Comprehensive health check reporting

### Pattern Compliance
- **Configuration Pattern**: Hierarchical configuration with overrides
- **Health Check Pattern**: Standard ASP.NET Core health checks
- **Service Pattern**: Dependency injection with interfaces

### Scalability Validation
- **Horizontal Scaling**: Configuration service is stateless
- **Bottleneck Identification**: Replicate API calls are primary bottleneck
- **Scaling Strategy**: Caching and circuit breaker patterns implemented

## Conclusion

This architecture provides a robust, scalable solution for managing Replicate model configurations while preventing the type of production issues recently experienced. The solution follows YAGNI principles by focusing on the immediate need for reliable configuration management while providing a foundation for future growth.

The phased implementation approach ensures minimal disruption to current operations while delivering immediate value through improved reliability and maintainability.