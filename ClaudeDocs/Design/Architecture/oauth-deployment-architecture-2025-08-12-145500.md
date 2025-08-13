---
title: "System Architecture: OAuth Secrets and Deployment Workflow"
system_id: "aiprofilemaker-oauth-deployment"
complexity: "medium"
status: "review"
architectural_patterns:
  - "infrastructure-as-code"
  - "secret-management"
  - "continuous-deployment"
  - "configuration-management"
scalability_metrics:
  current_capacity: "MVP"
  target_capacity: "Production"
  scaling_approach: "horizontal"
technology_stack:
  - backend: "ASP.NET Core, C#"
  - database: "Azure SQL Database"
  - infrastructure: "Azure Container Apps, Bicep"
  - ci_cd: "Local build, Azure CLI"
design_timeline:
  start: "2025-08-12T14:55:00Z"
  review: "2025-08-12T16:00:00Z"
  completion: "2025-08-12T18:00:00Z"
linked_documents:
  - path: "infrastructure/simple-deploy.bicep"
  - path: "scripts/fix-oauth-production.sh"
dependencies:
  - system: "google-oauth"
    type: "external"
  - system: "azure-container-apps"
    type: "infrastructure"
quality_attributes:
  - attribute: "security"
    priority: "critical"
  - attribute: "maintainability"
    priority: "high"
  - attribute: "deployability"
    priority: "high"
---

# OAuth Secrets and Deployment Workflow Architecture

## Executive Summary

The system currently faces an OAuth configuration gap where `GOOGLE_CLIENT_SECRET` is missing from production, causing authentication failures. This document presents a comprehensive solution that addresses both immediate fixes and long-term deployment sustainability through Infrastructure-as-Code (IaC) integration.

## Current State Analysis

### System Architecture
```
┌─────────────────────────────────────────────────────────────┐
│                     Production Environment                    │
├───────────────────────────────────────────────────────────────┤
│  Azure Container Apps                                         │
│  ├── aipm-api-v1 (Backend)                                   │
│  │   ├── ✅ GOOGLE_CLIENT_ID (manually added)               │
│  │   └── ❌ GOOGLE_CLIENT_SECRET (missing)                  │
│  └── aipm-web-v1 (Frontend)                                  │
├───────────────────────────────────────────────────────────────┤
│  Supporting Infrastructure                                    │
│  ├── Azure SQL Database                                      │
│  ├── Azure Storage Account                                   │
│  ├── Azure Container Registry                                │
│  └── Application Insights                                    │
└───────────────────────────────────────────────────────────────┘
```

### Configuration Drift Analysis

1. **Manual Configuration**: OAuth credentials added via Azure Portal
2. **IaC Template**: `simple-deploy.bicep` lacks OAuth configuration
3. **Result**: Configuration drift between manual state and IaC definition

### Deployment Workflow
```
Current Flow:
1. Local Build (scripts/build-local.sh)
2. Push to ACR (scripts/push-to-acr.sh)
3. Manual Bicep Deployment (infrastructure/simple-deploy.bicep)
4. Manual OAuth Configuration (Azure Portal/CLI)
```

## Architectural Decision Records (ADRs)

### ADR-001: OAuth Secret Management Strategy

**Status**: Proposed

**Context**: OAuth secrets need secure management and automated deployment

**Decision**: Implement two-phase approach:
1. **Phase 1**: Direct secrets in Bicep parameters (MVP approach)
2. **Phase 2**: Azure Key Vault integration (future enhancement)

**Rationale**: 
- Aligns with YAGNI principle for MVP
- Maintains simplicity while ensuring security
- Provides upgrade path for enterprise needs

**Consequences**:
- (+) Simple implementation
- (+) No additional Azure resources
- (-) Secrets in deployment parameters
- (-) Manual rotation required

### ADR-002: Configuration Management Pattern

**Status**: Proposed

**Context**: Need to prevent configuration drift between IaC and runtime

**Decision**: All configuration must be defined in Bicep templates

**Rationale**:
- Single source of truth
- Reproducible deployments
- Version-controlled configuration

**Consequences**:
- (+) Eliminates configuration drift
- (+) Auditable changes
- (-) Requires template updates for config changes

## Solution Architecture

### Phase 1: Immediate Fix (Priority: Critical)

```bash
# Immediate production fix
./scripts/fix-oauth-production.sh
```

This script:
1. Prompts for OAuth credentials
2. Updates Container App configuration
3. Validates OAuth endpoint functionality
4. Provides monitoring commands

### Phase 2: IaC Integration (Priority: High)

#### Updated Bicep Template Structure

```bicep
// OAuth Parameters (add to simple-deploy.bicep)
@secure()
@description('Google OAuth Client ID')
param googleClientId string

@secure()
@description('Google OAuth Client Secret')
param googleClientSecret string

// Container App Secrets
secrets: [
  // ... existing secrets ...
  {
    name: 'google-client-id'
    value: googleClientId
  }
  {
    name: 'google-client-secret'
    value: googleClientSecret
  }
]

// Environment Variables
env: [
  // ... existing env vars ...
  {
    name: 'GOOGLE_CLIENT_ID'
    secretRef: 'google-client-id'
  }
  {
    name: 'GOOGLE_CLIENT_SECRET'
    secretRef: 'google-client-secret'
  }
]
```

### Deployment Workflow Enhancement

```
Enhanced Flow:
1. Build Images Locally
   └── scripts/build-local.sh
2. Push to ACR
   └── scripts/push-to-acr.sh
3. Deploy Infrastructure with OAuth
   └── az deployment group create \
       --template-file infrastructure/simple-deploy.bicep \
       --parameters @deployment-params.json
4. Validate Deployment
   └── scripts/validate-deployment.sh
```

## Implementation Plan

### Step 1: Immediate Production Fix
```bash
# Execute OAuth fix script
./scripts/fix-oauth-production.sh

# Verify OAuth endpoint
curl -I https://api.aiprofilephotomaker.com/api/auth/external-login/google
# Expected: HTTP 302 (redirect to Google)
```

### Step 2: Update Bicep Template
```bash
# Update simple-deploy.bicep with OAuth configuration
# (Changes as specified in Phase 2 above)
```

### Step 3: Create Deployment Parameters File
```json
// deployment-params.json
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "sqlAdminPassword": {
      "reference": {
        "keyVault": {
          "id": "/subscriptions/.../resourceGroups/.../providers/Microsoft.KeyVault/vaults/..."
        },
        "secretName": "sql-admin-password"
      }
    },
    "jwtSecret": {
      "value": "your-jwt-secret"
    },
    "replicateApiToken": {
      "value": "your-replicate-token"
    },
    "googleClientId": {
      "value": "your-google-client-id"
    },
    "googleClientSecret": {
      "value": "your-google-client-secret"
    }
  }
}
```

### Step 4: Deployment Validation

```bash
# Validation checklist
✓ OAuth endpoint returns 302 redirect
✓ No 500 errors in application logs
✓ Google login flow completes successfully
✓ User tokens are generated correctly
✓ Infrastructure template includes OAuth config
```

## Security Considerations

### Secret Management Best Practices

1. **Never commit secrets to version control**
2. **Use Azure CLI parameter files with `.gitignore`**
3. **Rotate secrets regularly**
4. **Monitor access logs**

### OAuth Security Checklist

- ✓ Client secret stored as Container App secret
- ✓ HTTPS-only redirect URIs
- ✓ State parameter for CSRF protection
- ✓ Nonce validation for replay attacks
- ✓ Token validation on backend

## Monitoring and Observability

### Key Metrics
- OAuth endpoint response time
- Authentication success rate
- Failed authentication attempts
- Secret rotation compliance

### Log Queries
```kusto
// OAuth errors
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == "aipm-api-v1"
| where Message contains "OAuth" or Message contains "Google"
| where Level == "Error"
| top 50 by TimeGenerated desc

// Authentication metrics
requests
| where name contains "auth/external-login"
| summarize 
    TotalRequests = count(),
    SuccessRate = countif(success == true) * 100.0 / count(),
    AvgDuration = avg(duration)
  by bin(timestamp, 5m)
```

## Risk Assessment

### Identified Risks

1. **Configuration Drift**
   - Mitigation: All config in IaC
   - Status: Addressed in Phase 2

2. **Secret Exposure**
   - Mitigation: Use secure parameters
   - Status: Implemented

3. **Deployment Failures**
   - Mitigation: Validation scripts
   - Status: In place

4. **OAuth Downtime**
   - Mitigation: Blue-green deployment
   - Status: Future enhancement

## Future Enhancements

### Short-term (1-2 weeks)
- ✓ Complete OAuth fix in production
- ✓ Update Bicep templates
- ✓ Document deployment process

### Medium-term (1-2 months)
- Implement Azure Key Vault integration
- Add automated secret rotation
- Create CI/CD pipeline

### Long-term (3-6 months)
- Multi-environment configuration management
- Terraform migration consideration
- Enterprise-grade secret management

## Validation Criteria

The solution is considered successful when:

1. **Functional Requirements**
   - OAuth login works without errors
   - All deployments include OAuth configuration
   - No manual configuration required

2. **Non-Functional Requirements**
   - Deployment time < 10 minutes
   - Zero configuration drift
   - 100% infrastructure reproducibility

## Conclusion

This architecture provides a pragmatic solution that addresses immediate OAuth issues while establishing a sustainable deployment pattern. The two-phase approach balances MVP simplicity with production readiness, ensuring the system can scale as needed without accumulating technical debt.

### Key Takeaways
1. **Immediate action required**: Run OAuth fix script
2. **IaC update critical**: Prevents future configuration drift
3. **YAGNI principle applied**: Simple solution for MVP, extensible for growth
4. **Security maintained**: Secrets properly managed throughout

## Appendix: Quick Reference

### Commands
```bash
# Fix OAuth immediately
./scripts/fix-oauth-production.sh

# Deploy with OAuth
az deployment group create \
  --resource-group aiprofilemaker-v1 \
  --template-file infrastructure/simple-deploy.bicep \
  --parameters @deployment-params.json

# Validate OAuth
curl -I https://api.aiprofilephotomaker.com/api/auth/external-login/google

# Monitor logs
az monitor log-analytics query \
  --workspace aipm-logs-v1 \
  --analytics-query "ContainerAppConsoleLogs_CL | where ContainerAppName_s == 'aipm-api-v1'"
```

### File Locations
- Infrastructure: `/infrastructure/simple-deploy.bicep`
- Fix Script: `/scripts/fix-oauth-production.sh`
- Parameters: `/deployment-params.json` (create, don't commit)