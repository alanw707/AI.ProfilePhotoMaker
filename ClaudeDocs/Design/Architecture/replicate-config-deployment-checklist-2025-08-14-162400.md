---
title: "Replicate Configuration Deployment Checklist"
system_id: "replicate-config-deploy"
complexity: "low"
status: "deployment-ready"
architectural_patterns:
  - "deployment-validation"
  - "configuration-verification"
scalability_metrics:
  validation_time: "< 30 seconds"
  environments: "dev, staging, prod"
technology_stack:
  - validation: "PowerShell, Bash, curl"
  - monitoring: "Health Checks, Application Insights"
design_timeline:
  start: "2025-08-14T16:24:00Z"
  completion: "2025-08-14T16:30:00Z"
quality_attributes:
  - attribute: "reliability"
    priority: "critical"
  - attribute: "deployment-safety"
    priority: "high"
---

# Replicate Configuration Deployment Checklist

## Pre-Deployment Validation

### 1. Configuration File Validation

**Check required configuration sections exist:**

```bash
#!/bin/bash
# Script: validate-replicate-config.sh

echo "🔍 Validating Replicate Configuration..."

CONFIG_FILE="AI.ProfilePhotoMaker.API/appsettings.json"
DEV_CONFIG_FILE="AI.ProfilePhotoMaker.API/appsettings.Development.json"

# Check if configuration files exist
if [ ! -f "$CONFIG_FILE" ]; then
    echo "❌ Missing appsettings.json"
    exit 1
fi

if [ ! -f "$DEV_CONFIG_FILE" ]; then
    echo "❌ Missing appsettings.Development.json"
    exit 1
fi

# Validate JSON structure
echo "📋 Validating JSON structure..."
if ! jq empty "$CONFIG_FILE" 2>/dev/null; then
    echo "❌ Invalid JSON in appsettings.json"
    exit 1
fi

if ! jq empty "$DEV_CONFIG_FILE" 2>/dev/null; then
    echo "❌ Invalid JSON in appsettings.Development.json"
    exit 1
fi

# Check required Replicate configuration sections
echo "🔧 Checking Replicate configuration sections..."

REQUIRED_SECTIONS=(
    ".Replicate.Models.Training.Primary"
    ".Replicate.Models.Generation.Primary"
    ".Replicate.Models.Enhancement.Primary"
    ".Replicate.Validation"
)

for section in "${REQUIRED_SECTIONS[@]}"; do
    if ! jq -e "$section" "$CONFIG_FILE" >/dev/null 2>&1; then
        echo "❌ Missing configuration section: $section in appsettings.json"
        exit 1
    fi
    echo "✅ Found: $section"
done

echo "✅ All configuration sections present"
```

### 2. Environment Variable Validation

**PowerShell script for Windows/Azure:**

```powershell
# Script: Validate-ReplicateConfig.ps1

param(
    [Parameter(Mandatory=$true)]
    [string]$Environment = "Development",
    
    [switch]$CheckApiAccess = $false
)

Write-Host "🔍 Validating Replicate Configuration for $Environment..." -ForegroundColor Blue

# Required environment variables
$RequiredEnvVars = @(
    "REPLICATE_API_TOKEN",
    "REPLICATE_WEBHOOK_SECRET"
)

# Check environment variables
$MissingVars = @()
foreach ($var in $RequiredEnvVars) {
    $value = [Environment]::GetEnvironmentVariable($var)
    if ([string]::IsNullOrEmpty($value) -or $value.StartsWith("REPLACE_WITH_")) {
        $MissingVars += $var
        Write-Host "❌ Missing or placeholder: $var" -ForegroundColor Red
    } else {
        Write-Host "✅ Found: $var" -ForegroundColor Green
    }
}

if ($MissingVars.Count -gt 0) {
    Write-Host "❌ Missing environment variables: $($MissingVars -join ', ')" -ForegroundColor Red
    if ($Environment -eq "Production") {
        exit 1
    } else {
        Write-Host "⚠️  Warning: Missing variables in $Environment environment" -ForegroundColor Yellow
    }
}

# Check API access if requested
if ($CheckApiAccess) {
    Write-Host "🌐 Testing Replicate API access..." -ForegroundColor Blue
    
    $apiToken = [Environment]::GetEnvironmentVariable("REPLICATE_API_TOKEN")
    if (![string]::IsNullOrEmpty($apiToken) -and !$apiToken.StartsWith("REPLACE_WITH_")) {
        try {
            $headers = @{
                "Authorization" = "Token $apiToken"
                "User-Agent" = "AI.ProfilePhotoMaker/1.0"
            }
            
            $response = Invoke-RestMethod -Uri "https://api.replicate.com/v1/models/black-forest-labs/flux-dev" -Headers $headers -TimeoutSec 10
            Write-Host "✅ API access verified" -ForegroundColor Green
        }
        catch {
            Write-Host "❌ API access failed: $($_.Exception.Message)" -ForegroundColor Red
            if ($Environment -eq "Production") {
                exit 1
            }
        }
    }
}

Write-Host "✅ Replicate configuration validation completed" -ForegroundColor Green
```

### 3. Application Startup Validation

**Test startup configuration loading:**

```bash
#!/bin/bash
# Script: test-startup-config.sh

echo "🚀 Testing application startup configuration..."

# Build the application
echo "📦 Building application..."
if ! dotnet build AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj --configuration Release --no-restore; then
    echo "❌ Build failed"
    exit 1
fi

# Test configuration loading (dry run)
echo "🔧 Testing configuration loading..."
cd AI.ProfilePhotoMaker.API

# Set minimal required environment variables for test
export ASPNETCORE_ENVIRONMENT="Development"
export REPLICATE_API_TOKEN="test-token"
export REPLICATE_WEBHOOK_SECRET="test-secret"

# Test configuration loading without starting full application
if ! timeout 30s dotnet run --no-build --configuration Release --dry-run 2>&1 | grep -q "Configuration loaded successfully"; then
    echo "❌ Configuration loading test failed"
    exit 1
fi

echo "✅ Configuration loading test passed"
```

## Deployment Steps

### Step 1: Pre-Deployment Checklist

**Before deploying to any environment:**

- [ ] **Code Review Complete**
  - [ ] All new files reviewed
  - [ ] Configuration changes reviewed
  - [ ] Tests passing

- [ ] **Configuration Validation**
  - [ ] `validate-replicate-config.sh` passes
  - [ ] `Validate-ReplicateConfig.ps1` passes for target environment
  - [ ] JSON structure validation passes

- [ ] **Security Review**
  - [ ] No secrets in configuration files
  - [ ] Environment variables properly configured
  - [ ] Azure Key Vault references correct (if used)

### Step 2: Development Environment Deployment

```bash
# 1. Set user secrets (if not already set)
dotnet user-secrets set "Replicate:ApiToken" "your-dev-api-token" --project AI.ProfilePhotoMaker.API
dotnet user-secrets set "Replicate:WebhookSecret" "whsec_wUh0bvV+/jGsxbEqPsRgHI1tdct1Y+KM" --project AI.ProfilePhotoMaker.API

# 2. Test application startup
echo "🚀 Testing development environment..."
cd AI.ProfilePhotoMaker.API
dotnet run --environment Development &
APP_PID=$!

# Wait for startup
sleep 10

# Test health endpoint
if curl -f http://localhost:5032/health >/dev/null 2>&1; then
    echo "✅ Development environment health check passed"
else
    echo "❌ Development environment health check failed"
    kill $APP_PID
    exit 1
fi

# Test Replicate configuration health
if curl -f http://localhost:5032/health/ready | grep -q '"replicate-config":"Healthy"'; then
    echo "✅ Replicate configuration health check passed"
else
    echo "❌ Replicate configuration health check failed"
    kill $APP_PID
    exit 1
fi

kill $APP_PID
echo "✅ Development deployment validated"
```

### Step 3: Staging Environment Deployment

```bash
# Deploy to staging and validate
echo "🔄 Deploying to staging environment..."

# Build and deploy (using your existing deployment script)
./scripts/simple-deployment.sh staging

# Wait for deployment to complete
sleep 30

# Validate staging deployment
echo "🧪 Validating staging deployment..."

STAGING_URL="https://staging-api.aiprofilephotomaker.com"

# Health check
if ! curl -f "$STAGING_URL/health" >/dev/null 2>&1; then
    echo "❌ Staging health check failed"
    exit 1
fi

# Replicate configuration health check
HEALTH_RESPONSE=$(curl -s "$STAGING_URL/health")
if echo "$HEALTH_RESPONSE" | grep -q '"replicate-config":"Healthy"'; then
    echo "✅ Staging Replicate configuration healthy"
else
    echo "❌ Staging Replicate configuration unhealthy"
    echo "Health response: $HEALTH_RESPONSE"
    exit 1
fi

echo "✅ Staging deployment validated"
```

### Step 4: Production Environment Deployment

```bash
# Production deployment with extra validation
echo "🚀 Deploying to production environment..."

# Pre-production validation
echo "📋 Pre-production validation..."

# Check production secrets are available
if ! ./scripts/Validate-ReplicateConfig.ps1 -Environment Production -CheckApiAccess; then
    echo "❌ Production secrets validation failed"
    exit 1
fi

# Deploy to production
./scripts/simple-deployment.sh production

# Wait for deployment
sleep 60

# Post-deployment validation
echo "✅ Post-deployment validation..."

PROD_URL="https://api.aiprofilephotomaker.com"

# Health check with retries
for i in {1..5}; do
    if curl -f "$PROD_URL/health" >/dev/null 2>&1; then
        echo "✅ Production health check passed (attempt $i)"
        break
    else
        echo "⚠️  Production health check failed (attempt $i), retrying..."
        sleep 10
    fi
    
    if [ $i -eq 5 ]; then
        echo "❌ Production health check failed after 5 attempts"
        exit 1
    fi
done

# Replicate configuration validation
HEALTH_RESPONSE=$(curl -s "$PROD_URL/health")
if echo "$HEALTH_RESPONSE" | grep -q '"replicate-config":"Healthy"'; then
    echo "✅ Production Replicate configuration healthy"
else
    echo "❌ Production Replicate configuration unhealthy"
    echo "Health response: $HEALTH_RESPONSE"
    
    # Check if this is a degraded state (warnings only)
    if echo "$HEALTH_RESPONSE" | grep -q '"replicate-config":"Degraded"'; then
        echo "⚠️  Replicate configuration is degraded but functional"
    else
        exit 1
    fi
fi

echo "✅ Production deployment validated"
```

## Post-Deployment Validation

### Functional Tests

**Test each Replicate model type:**

```bash
#!/bin/bash
# Script: test-replicate-functionality.sh

API_BASE_URL="${1:-https://api.aiprofilephotomaker.com}"
echo "🧪 Testing Replicate functionality at $API_BASE_URL"

# Test enhancement endpoint (uses FluxKontextProModelId)
echo "🖼️  Testing photo enhancement..."
ENHANCE_RESPONSE=$(curl -s -w "%{http_code}" -o /tmp/enhance_response.json \
    -X POST "$API_BASE_URL/api/replicate/enhance" \
    -H "Content-Type: application/json" \
    -d '{"imageUrl":"https://example.com/test.jpg","enhancementType":"professional"}')

if [ "${ENHANCE_RESPONSE: -3}" = "200" ]; then
    echo "✅ Enhancement endpoint working"
elif [ "${ENHANCE_RESPONSE: -3}" = "401" ]; then
    echo "⚠️  Enhancement endpoint requires authentication (expected)"
else
    echo "❌ Enhancement endpoint failed with status: ${ENHANCE_RESPONSE: -3}"
    cat /tmp/enhance_response.json
fi

# Test basic generation endpoint (uses FluxGenerationModelId)
echo "👤 Testing basic image generation..."
GENERATE_RESPONSE=$(curl -s -w "%{http_code}" -o /tmp/generate_response.json \
    -X POST "$API_BASE_URL/api/replicate/generate-basic" \
    -H "Content-Type: application/json" \
    -d '{"gender":"male","style":"casual"}')

if [ "${GENERATE_RESPONSE: -3}" = "200" ]; then
    echo "✅ Basic generation endpoint working"
elif [ "${GENERATE_RESPONSE: -3}" = "401" ]; then
    echo "⚠️  Basic generation endpoint requires authentication (expected)"
else
    echo "❌ Basic generation endpoint failed with status: ${GENERATE_RESPONSE: -3}"
    cat /tmp/generate_response.json
fi

echo "✅ Functional tests completed"
```

### Performance Validation

**Check configuration resolution performance:**

```bash
#!/bin/bash
# Script: test-config-performance.sh

API_BASE_URL="${1:-https://api.aiprofilephotomaker.com}"
echo "⚡ Testing configuration resolution performance..."

# Measure health check response time
for i in {1..5}; do
    START_TIME=$(date +%s%N)
    curl -s "$API_BASE_URL/health" >/dev/null
    END_TIME=$(date +%s%N)
    
    DURATION_MS=$(( (END_TIME - START_TIME) / 1000000 ))
    echo "Health check $i: ${DURATION_MS}ms"
    
    if [ $DURATION_MS -gt 5000 ]; then
        echo "⚠️  Health check taking longer than 5 seconds"
    fi
done

echo "✅ Performance validation completed"
```

## Monitoring Setup

### Application Insights Queries

**Add these queries to your monitoring dashboard:**

```kusto
// Replicate configuration health
requests
| where name contains "health"
| extend HealthStatus = tostring(customDimensions.HealthStatus)
| where HealthStatus contains "replicate-config"
| summarize count() by bin(timestamp, 5m), HealthStatus

// Configuration resolution errors
traces
| where message contains "GetModelIdAsync" or message contains "ValidateAllModelsAsync"
| where severityLevel >= 3
| summarize count() by bin(timestamp, 5m), severityLevel

// Model fallback usage
traces
| where message contains "using fallback" or message contains "fallback model"
| summarize count() by bin(timestamp, 1h), tostring(customDimensions.ModelType)
```

### Alert Rules

**Set up alerts for:**

1. **Replicate Health Check Failures**
   - Condition: Health check returns "Unhealthy" for replicate-config
   - Frequency: Every 5 minutes
   - Action: Email/Slack notification

2. **Model Resolution Failures**
   - Condition: Log entries with "No available models found"
   - Frequency: Any occurrence
   - Action: Immediate notification

3. **Fallback Model Usage**
   - Condition: Frequent fallback model usage (>10% of requests)
   - Frequency: Every 15 minutes
   - Action: Warning notification

## Rollback Procedures

### Emergency Rollback

**If critical issues occur after deployment:**

```bash
#!/bin/bash
# Script: emergency-rollback.sh

echo "🚨 Initiating emergency rollback..."

# 1. Restore previous configuration
git checkout HEAD~1 -- AI.ProfilePhotoMaker.API/appsettings.json
git checkout HEAD~1 -- AI.ProfilePhotoMaker.API/appsettings.Development.json

# 2. Redeploy previous version
./scripts/simple-deployment.sh production --force

# 3. Verify rollback
sleep 30
if curl -f "https://api.aiprofilephotomaker.com/health" >/dev/null 2>&1; then
    echo "✅ Rollback successful"
else
    echo "❌ Rollback failed - manual intervention required"
    exit 1
fi
```

### Gradual Rollback

**For less critical issues:**

1. **Disable new configuration validation**
2. **Switch to fallback models only**
3. **Monitor for stability**
4. **Plan proper fix**

## Success Criteria

**Deployment is considered successful when:**

- [ ] **All health checks passing** (including replicate-config)
- [ ] **No 500 errors** in application logs
- [ ] **All model types resolving** (Training, Generation, Enhancement)
- [ ] **Performance within acceptable limits** (<100ms config resolution)
- [ ] **Monitoring alerts configured** and functioning
- [ ] **Functional tests passing** for all Replicate endpoints

## Documentation Updates

**After successful deployment, update:**

- [ ] **CLAUDE.md** with new configuration patterns
- [ ] **Environment setup docs** with new validation scripts
- [ ] **Troubleshooting guides** with new health check procedures
- [ ] **Team knowledge base** with configuration management procedures

This comprehensive checklist ensures reliable deployment of the Replicate configuration management system while maintaining production stability.