---
title: "System Architecture: Domain Persistence with Existing Certificates"
system_id: "aiprofilemaker-domain-persistence"
complexity: "medium"
status: "implemented"
architectural_patterns:
  - "infrastructure-as-code"
  - "declarative-configuration"
  - "resource-reference-pattern"
scalability_metrics:
  current_capacity: "production"
  target_capacity: "production"
  scaling_approach: "horizontal"
technology_stack:
  - backend: "Azure Container Apps"
  - infrastructure: "Azure Bicep"
  - certificates: "Azure Managed Certificates"
design_timeline:
  start: "2025-01-11T18:00:00Z"
  review: "2025-01-11T19:00:00Z"
  completion: "2025-01-11T19:21:00Z"
linked_documents:
  - path: "infrastructure/simple-deploy.bicep"
  - path: "ClaudeDocs/Design/Architecture/subdomain-implementation-2025-01-11-024856.md"
dependencies:
  - system: "azure-container-apps"
    type: "external"
  - system: "azure-managed-certificates"
    type: "external"
quality_attributes:
  - attribute: "reliability"
    priority: "critical"
  - attribute: "simplicity"
    priority: "high"
  - attribute: "maintainability"
    priority: "high"
---

# Domain Persistence with Existing Certificates - Implementation

## Executive Summary

Successfully implemented a pragmatic solution for domain persistence in Azure Container Apps by referencing existing managed certificates instead of creating new ones during each deployment. This approach follows the YAGNI principle and provides a stable MVP solution.

## Problem Statement

The application was experiencing domain configuration loss during deployments due to attempted recreation of managed certificates, which caused:
- Certificate validation failures
- Domain configuration removal
- Service interruptions

## Solution Architecture

### Design Principles

1. **Reference over Recreation**: Use existing certificate IDs rather than creating new certificates
2. **Declarative Configuration**: Maintain domain configuration in Bicep template for persistence
3. **Simplicity First**: Remove complex certificate automation for MVP phase
4. **Proven State**: Use certificates that are already validated and working

### Implementation Details

#### Certificate Reference Pattern

```bicep
// Existing certificate IDs - using working certificates
var frontendCertificateId = '/subscriptions/.../managedCertificates/mc-aipm-env-v1-6j-app-aiprofilepho-5691'
var backendCertificateId = '/subscriptions/.../managedCertificates/mc-aipm-env-v1-6j-api-aiprofilepho-8094'
```

#### Container App Configuration

```bicep
customDomains: [
  {
    name: 'api.aiprofilephotomaker.com'
    certificateId: backendCertificateId
    bindingType: 'SniEnabled'
  }
]
```

### Key Changes Made

1. **Removed Certificate Resources**: Eliminated `managedCertificates` resources from Bicep template
2. **Added Certificate Variables**: Defined certificate IDs as variables for reuse
3. **Updated Dependencies**: Removed certificate dependencies from Container Apps
4. **Preserved Domain Config**: Maintained `customDomains` configuration in template

## Deployment Validation

### Test Results

1. **Deployment Success**: Template deployed without errors
2. **Domain Persistence**: Custom domains retained after deployment
3. **Certificate Status**: Both certificates showing "Succeeded" status
4. **Endpoint Health**: Both domains responding with HTTP 200

### Verification Commands

```bash
# Check domain configuration
az containerapp show --name aipm-api-v1 --resource-group aiprofilemaker-v1 \
  --query "properties.configuration.ingress.customDomains"

# Test domain endpoints
curl -s -o /dev/null -w "%{http_code}" https://api.aiprofilephotomaker.com/api/health
curl -s -o /dev/null -w "%{http_code}" https://app.aiprofilephotomaker.com

# Verify certificate status
az containerapp env certificate list --name aipm-env-v1-6j74jubocuukg \
  --resource-group aiprofilemaker-v1
```

## Architecture Benefits

### Achieved Goals

1. **Domain Persistence**: Domains remain configured after deployments
2. **Zero Downtime**: No service interruption during updates
3. **Simplified Deployment**: Removed complex certificate automation
4. **Predictable Behavior**: Consistent deployment results

### Trade-offs Accepted

1. **Manual Certificate Updates**: New certificates require manual ID updates
2. **Environment Coupling**: Certificate IDs tied to specific environment
3. **Limited Automation**: Certificate renewal not automated in template

## Migration Path

### Current State (MVP)
- Manually created certificates
- Hard-coded certificate IDs
- Stable domain configuration

### Future State (Production)
- Certificate automation via separate process
- Dynamic certificate reference
- Automated renewal handling

### Migration Steps

1. **Phase 1**: Current implementation (complete)
2. **Phase 2**: Extract certificate IDs to parameters
3. **Phase 3**: Implement certificate automation pipeline
4. **Phase 4**: Add automatic renewal handling

## Operational Considerations

### Certificate Management

1. **Renewal Timeline**: Managed certificates auto-renew 30 days before expiry
2. **Monitoring**: Check certificate expiry dates monthly
3. **Update Process**: Update Bicep template when certificates change

### Deployment Process

1. Build and push Docker images
2. Deploy infrastructure with existing certificate IDs
3. Verify domain configuration post-deployment
4. Monitor application health

## Lessons Learned

### What Worked

1. **Pragmatic Approach**: Focusing on working solution over perfect automation
2. **Incremental Changes**: Small, testable modifications
3. **Existing Resources**: Leveraging already-validated certificates

### What Didn't Work

1. **Certificate Recreation**: Attempting to create new certificates each deployment
2. **Complex Dependencies**: Over-engineering certificate management
3. **Automation First**: Trying to automate before understanding requirements

## Recommendations

### Immediate Actions

1. **Document Certificate IDs**: Maintain registry of certificate IDs
2. **Monitor Expiry**: Set up alerts for certificate expiration
3. **Test Deployments**: Regular deployment validation

### Future Improvements

1. **Parameter Extraction**: Move certificate IDs to deployment parameters
2. **Certificate Pipeline**: Separate certificate management workflow
3. **Monitoring Dashboard**: Certificate status visualization

## Conclusion

The implementation successfully achieves domain persistence through a pragmatic approach that prioritizes stability and simplicity. By referencing existing certificates rather than recreating them, we eliminate the primary cause of domain configuration loss while maintaining a clean, maintainable infrastructure template.

This solution provides a solid foundation for the MVP phase while allowing for future enhancements as the application scales and requirements evolve.

## Appendix: Working Certificate IDs

### Production Certificates
- **Frontend**: `/subscriptions/7e5147a4-3abb-4a43-aef7-5a2ae770c739/resourceGroups/aiprofilemaker-v1/providers/Microsoft.App/managedEnvironments/aipm-env-v1-6j74jubocuukg/managedCertificates/mc-aipm-env-v1-6j-app-aiprofilepho-5691`
- **Backend**: `/subscriptions/7e5147a4-3abb-4a43-aef7-5a2ae770c739/resourceGroups/aiprofilemaker-v1/providers/Microsoft.App/managedEnvironments/aipm-env-v1-6j74jubocuukg/managedCertificates/mc-aipm-env-v1-6j-api-aiprofilepho-8094`

### Validation Status
- Both certificates: `Succeeded`
- Both domains: `HTTP 200 OK`
- Deployment: `Successful`