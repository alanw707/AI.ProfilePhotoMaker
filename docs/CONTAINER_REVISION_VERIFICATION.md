# Container Revision Verification System

## Overview

The Container Revision Verification System ensures that Azure Container Apps are running the expected Docker image tag and are healthy after deployment. This system addresses common deployment issues where infrastructure deployment "succeeds" but containers are still running old images or failing to start.

## Key Features

- **Image Tag Verification**: Confirms container apps are running the expected Docker image tag
- **Startup Failure Detection**: Analyzes logs for common failure patterns
- **Health Endpoint Monitoring**: Validates application health after deployment
- **Automatic Rollback**: Can automatically rollback to previous revision on failure
- **Detailed Reporting**: Generates comprehensive reports for troubleshooting

## Components

### 1. Verification Script

**Location**: `scripts/verify-container-revision.sh`

The main verification script that:
- Checks container app image tags against expected values
- Monitors revision deployment status
- Analyzes container logs for startup failures
- Performs health checks
- Provides rollback capability

### 2. GitHub Actions Integration

The verification is integrated into the deployment workflow at `.github/workflows/simple-deploy.yml`:
- Runs after infrastructure deployment
- Uses rollback-enabled mode for production safety
- Uploads verification artifacts for troubleshooting
- Provides detailed failure analysis

## Usage

### Automatic (GitHub Actions)

The verification runs automatically during deployment:

```yaml
- name: 🔍 Verify Container Revision and Startup Health
  run: |
    ./scripts/verify-container-revision.sh \
      --expected-tag "$IMAGE_TAG" \
      --rollback-on-failure \
      --verbose \
      --max-wait 600
```

### Manual Verification

You can run verification manually for troubleshooting:

```bash
# Basic verification
./scripts/verify-container-revision.sh

# Verify specific image tag
./scripts/verify-container-revision.sh --expected-tag "123-456"

# Enable automatic rollback on failure
./scripts/verify-container-revision.sh --rollback-on-failure

# Quick verification without health checks
./scripts/verify-container-revision.sh --no-health-wait --max-wait 300

# Verbose output for debugging
./scripts/verify-container-revision.sh --verbose
```

### Command Line Options

| Option | Description | Default |
|--------|-------------|---------|
| `--expected-tag TAG` | Expected Docker image tag | `latest` or `$IMAGE_TAG` |
| `--rollback-on-failure` | Auto-rollback to previous revision on failure | `false` |
| `--no-health-wait` | Skip health endpoint checks | `false` |
| `--verbose` | Enable detailed logging | `false` |
| `--max-wait SECONDS` | Maximum wait time for verification | `600` (10 min) |

## Verification Process

### 1. Image Tag Verification

The script checks that each container app is running the expected Docker image tag:

```bash
# Current: aipmcrv16j74jubocuukg.azurecr.io/aiprofilemaker-api:123-456
# Expected: 123-456
# Status: ✅ Match
```

### 2. Revision Status Monitoring

Monitors Azure Container App revision deployment:

```bash
# Active Revisions: 1
# Ready Revisions: 1/1
# Status: ✅ All revisions ready
```

### 3. Startup Failure Detection

Analyzes container logs for common failure patterns:

- Application startup exceptions
- Configuration errors
- Database/storage connection failures
- Port binding issues
- Out of memory conditions
- Crash loops

### 4. Health Endpoint Validation

Tests application health endpoints:

```bash
# Backend: https://api.aiprofilephotomaker.com/api/health
# Frontend: https://app.aiprofilephotomaker.com
# Status: ✅ Health checks passed
```

## Troubleshooting

### Common Issues

#### 1. Image Tag Mismatch

**Symptoms**: Current tag doesn't match expected tag
**Causes**:
- Images not pushed to ACR
- Wrong image tag in deployment
- Caching issues in Azure Container Apps

**Resolution**:
```bash
# Check images in ACR
az acr repository list --name aipmcrv16j74jubocuukg

# Rebuild and push images
./scripts/build-local.sh
./scripts/push-to-acr.sh

# Re-run deployment
```

#### 2. Startup Failures

**Symptoms**: Health checks fail, error patterns in logs
**Causes**:
- Missing environment variables
- Database connection issues
- Storage configuration problems
- Application configuration errors

**Resolution**:
1. Check container app environment variables
2. Verify database connectivity
3. Validate storage configuration
4. Review application logs

#### 3. Health Endpoint Issues

**Symptoms**: Health endpoints return errors or timeout
**Causes**:
- Application not fully started
- Port configuration issues
- Resource constraints
- Application bugs

**Resolution**:
1. Wait for application startup (may take 1-2 minutes)
2. Check resource limits (CPU/memory)
3. Review application startup logs
4. Verify health endpoint URLs

### Rollback Scenarios

The system can automatically rollback in these situations:

1. **New revision fails to start**: Application crashes during startup
2. **Health checks fail**: Health endpoints return errors consistently
3. **Startup failures detected**: Critical error patterns found in logs

Manual rollback can be performed:

```bash
# List available revisions
az containerapp revision list --name aipm-api-v1 --resource-group aiprofilemaker-v1

# Activate previous revision
az containerapp revision activate \
  --name aipm-api-v1 \
  --resource-group aiprofilemaker-v1 \
  --revision <previous-revision-name>
```

## Verification Artifacts

The system generates several artifacts for analysis:

### 1. Verification Log
**File**: `container-verification-YYYYMMDD-HHMMSS.log`
**Content**: Detailed verification process log

### 2. Verification Report
**File**: `container-verification-report-YYYYMMDD-HHMMSS.json`
**Content**: Structured verification results

```json
{
  "verificationTimestamp": "2024-01-01T12:00:00Z",
  "environment": "v1",
  "expectedImageTag": "123-456",
  "overallSuccess": true,
  "containerApps": [
    {
      "appName": "aipm-api-v1",
      "currentImageTag": "123-456",
      "tagMatches": true,
      "isHealthy": true,
      "activeRevisions": 1
    }
  ]
}
```

### 3. Rollback Information
**File**: `rollback-YYYYMMDD-HHMMSS.json`
**Content**: Information for manual rollback if needed

## GitHub Actions Integration

### Workflow Steps

1. **Infrastructure Deployment**: Deploy Azure resources
2. **Secret Refresh**: Update container app secrets
3. **Revision Verification**: ⭐ **NEW** - Verify container revision
4. **Health Validation**: Additional health checks
5. **Artifact Upload**: Upload verification artifacts

### Outputs

The verification step provides these outputs:

- `verification-failed`: Boolean indicating if verification failed
- `verification-report`: Path to verification report
- `verification-log`: Path to verification log

### Failure Handling

When verification fails:

1. **Automatic Actions**:
   - Rollback to previous revision (if enabled)
   - Capture detailed logs and error analysis
   - Upload artifacts for troubleshooting

2. **Manual Actions Required**:
   - Review verification artifacts
   - Check container app logs
   - Verify image availability in ACR
   - Consider manual rollback if needed

## Best Practices

### 1. Deployment Safety

- Always use `--rollback-on-failure` in production
- Set appropriate timeout values (`--max-wait`)
- Enable verbose logging for troubleshooting

### 2. Monitoring

- Monitor verification artifacts after deployment
- Set up alerts for verification failures
- Review logs regularly for patterns

### 3. Testing

- Test verification script with known good/bad deployments
- Validate rollback functionality periodically
- Keep verification timeouts reasonable for your application

## Integration with Existing Systems

### Deployment Validation Service

The container revision verification complements the existing `DeploymentValidationService`:

- **Pre-deployment**: Configuration and readiness validation
- **Post-deployment**: Container revision and runtime validation

### Monitoring Services

Works with existing monitoring:

- **DeploymentMonitoringService**: Continuous health monitoring
- **PerformanceMonitoringService**: Performance metrics validation

## Future Enhancements

Planned improvements:

1. **Blue-Green Deployment Support**: Gradual traffic switching
2. **Performance Regression Detection**: Automated performance validation
3. **Custom Health Check Integration**: Application-specific health checks
4. **Slack/Teams Notifications**: Real-time deployment status updates
5. **Metric-Based Rollback**: Automatic rollback based on performance metrics

## Examples

### Successful Verification

```bash
$ ./scripts/verify-container-revision.sh --expected-tag "123-456" --verbose

🔍 [VERIFY] Verifying container revision deployment and startup health...
📝 [INFO] Expected Image Tag: 123-456
🔍 [STEP] Verifying container app: aipm-api-v1
📊 [INFO] Current image tag: 123-456
✅ [SUCCESS] Image tag matches expected tag
✅ [SUCCESS] All active revisions are ready
✅ [SUCCESS] Health check passed
✅ [SUCCESS] Verification completed successfully for aipm-api-v1

🎉 CONTAINER REVISION VERIFICATION SUCCESSFUL! 🎉
📊 All container apps are running the expected revision and are healthy
```

### Failed Verification with Rollback

```bash
$ ./scripts/verify-container-revision.sh --expected-tag "123-456" --rollback-on-failure

🔍 [VERIFY] Verifying container revision deployment and startup health...
📝 [INFO] Expected Image Tag: 123-456
🔍 [STEP] Verifying container app: aipm-api-v1
⚠️ [WARNING] Health check failed for aipm-api-v1
🚨 [ERROR] Startup failure patterns detected in aipm-api-v1:
  • Database connection failed
  • Configuration error
🔄 [STEP] Attempting rollback for aipm-api-v1...
✅ [SUCCESS] Rollback completed successfully for aipm-api-v1

❌ CONTAINER REVISION VERIFICATION FAILED ❌
⚠️ Some container apps failed verification but rollback succeeded
```

This system provides comprehensive verification and rollback capabilities to ensure reliable container deployments.