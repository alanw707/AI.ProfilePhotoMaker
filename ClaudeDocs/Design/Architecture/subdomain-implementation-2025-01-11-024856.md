---
title: "System Architecture: Subdomain Strategy Implementation for Azure Container Apps"
system_id: "AIPM-SUBDOMAIN-001"
complexity: "low"
status: "draft"
architectural_patterns:
  - "microservices"
  - "container-orchestration"
  - "api-gateway-pattern"
scalability_metrics:
  current_capacity: "Azure default"
  target_capacity: "Same (configuration only)"
  scaling_approach: "horizontal"
technology_stack:
  - backend: ".NET 8, ASP.NET Core"
  - frontend: "Angular"
  - infrastructure: "Azure Container Apps"
  - dns: "GoDaddy"
design_timeline:
  start: "2025-01-11T02:48:56Z"
  review: "2025-01-11T08:00:00Z"
  completion: "2025-01-11T12:00:00Z"
linked_documents:
  - path: "infrastructure/simple-deploy.json"
  - path: "AI.ProfilePhotoMaker.API/Program.cs"
dependencies:
  - system: "Azure Container Apps"
    type: "platform"
  - system: "GoDaddy DNS"
    type: "external"
  - system: "Google OAuth"
    type: "external"
quality_attributes:
  - attribute: "availability"
    priority: "high"
  - attribute: "security"
    priority: "high"
  - attribute: "maintainability"
    priority: "medium"
---

# Subdomain Strategy Implementation for Azure Container Apps

## Executive Summary

This document outlines the implementation plan for migrating from Azure Container Apps default URLs to custom subdomains for aiprofilephotomaker.com. Since both frontend and backend services are already deployed and running on Azure Container Apps, this is a **configuration-only change** with no service migration required.

### Current State
- **Frontend**: Azure Container Apps (aipm-web-v1)
- **Backend**: Azure Container Apps (aipm-api-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io)
- **Infrastructure**: Fully deployed via ARM template (simple-deploy.json)

### Target State
- **app.aiprofilephotomaker.com** → Frontend Container App
- **api.aiprofilephotomaker.com** → Backend Container App
- **aiprofilephotomaker.com** → Redirect to app subdomain
- **www.aiprofilephotomaker.com** → Redirect to app subdomain

## Why This Approach is Faster Than Migration

This implementation is significantly faster than migrating between Azure service types because:

1. **No Infrastructure Changes**: Services remain on Azure Container Apps
2. **No Data Migration**: Database and storage connections unchanged
3. **No Redeployment**: Existing container images continue running
4. **Configuration Only**: Changes are limited to DNS and Container App settings
5. **Zero Downtime**: Can be implemented with rolling updates
6. **Simplified Testing**: Services remain accessible via old URLs during transition

## Implementation Timeline

### Total Estimated Time: 3-4 Hours

This is a significant reduction from the 2-3 days required for service migration. Here's the detailed breakdown:

### Phase 1: Preparation (30 minutes)
**Tasks:**
- Verify current Container App configurations
- Document existing URLs and settings
- Prepare DNS records
- Create configuration backup

**Time Breakdown:**
- Container App verification: 10 minutes
- Documentation: 10 minutes
- DNS preparation: 5 minutes
- Backup creation: 5 minutes

### Phase 2: DNS Configuration (45 minutes)
**Tasks:**
1. Add CNAME records in GoDaddy:
   - app.aiprofilephotomaker.com → Frontend Container App
   - api.aiprofilephotomaker.com → Backend Container App
2. Configure root domain redirect
3. Configure www subdomain redirect
4. Wait for initial DNS propagation

**Time Breakdown:**
- CNAME record creation: 10 minutes
- Redirect configuration: 10 minutes
- DNS propagation wait: 25 minutes

### Phase 3: Azure Container Apps Configuration (60 minutes)
**Tasks:**
1. Add custom domains to Container Apps
2. Configure Azure-managed TLS certificates
3. Update environment variables
4. Apply configuration changes

**Time Breakdown:**
- Frontend custom domain: 15 minutes
- Backend custom domain: 15 minutes
- TLS certificate provisioning: 20 minutes (automated)
- Environment variable updates: 10 minutes

### Phase 4: Application Configuration (45 minutes)
**Tasks:**
1. Update frontend environment:
   ```javascript
   API_URL=https://api.aiprofilephotomaker.com
   ```
2. Update backend CORS settings:
   ```csharp
   allowedOrigins.Add("https://app.aiprofilephotomaker.com");
   ```
3. Deploy configuration changes

**Time Breakdown:**
- Frontend configuration: 15 minutes
- Backend CORS update: 15 minutes
- Configuration deployment: 15 minutes

### Phase 5: External Services (30 minutes)
**Tasks:**
1. Update Google OAuth redirect URIs:
   - Add: https://app.aiprofilephotomaker.com/auth/callback
   - Add: https://api.aiprofilephotomaker.com/api/auth/google/callback
2. Update any other OAuth providers
3. Update webhook URLs if applicable

**Time Breakdown:**
- Google OAuth configuration: 15 minutes
- Other OAuth providers: 10 minutes
- Webhook updates: 5 minutes

### Phase 6: Testing & Validation (30 minutes)
**Tasks:**
1. Verify DNS resolution
2. Test HTTPS certificates
3. Validate authentication flow
4. Test API connectivity
5. Verify CORS functionality
6. Load testing (basic)

**Time Breakdown:**
- DNS verification: 5 minutes
- Certificate validation: 5 minutes
- Authentication testing: 10 minutes
- API testing: 5 minutes
- CORS testing: 5 minutes

## Detailed Implementation Steps

### Step 1: DNS Configuration in GoDaddy

```dns
# CNAME Records
app     CNAME   aipm-web-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io
api     CNAME   aipm-api-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io

# Root Domain Forwarding
@       Forward to https://app.aiprofilephotomaker.com (301 Redirect)
www     Forward to https://app.aiprofilephotomaker.com (301 Redirect)
```

### Step 2: Azure Container Apps Custom Domain

Using Azure CLI:
```bash
# Add custom domain to frontend
az containerapp hostname add \
  --resource-group aipm-rg \
  --name aipm-web-v1 \
  --hostname app.aiprofilephotomaker.com

# Add custom domain to backend
az containerapp hostname add \
  --resource-group aipm-rg \
  --name aipm-api-v1 \
  --hostname api.aiprofilephotomaker.com

# Bind certificates (automatic with managed certificates)
az containerapp hostname bind \
  --resource-group aipm-rg \
  --name aipm-web-v1 \
  --hostname app.aiprofilephotomaker.com \
  --environment aipm-env-v1 \
  --validation-method CNAME
```

### Step 3: Update Application Configurations

**Frontend Environment Update:**
```json
{
  "name": "API_URL",
  "value": "https://api.aiprofilephotomaker.com"
}
```

**Backend CORS Update (Program.cs):**
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("V1Production",
        corsBuilder =>
        {
            var allowedOrigins = new List<string>
            {
                "https://app.aiprofilephotomaker.com",
                "https://aiprofilephotomaker.com",  // Keep for compatibility
                "https://test.profilephotomaker.com"
            };
            
            // Existing V1 URL for rollback capability
            if (!string.IsNullOrEmpty(v1FrontendUrl))
            {
                allowedOrigins.Add(v1FrontendUrl);
            }
            
            corsBuilder.WithOrigins(allowedOrigins.ToArray())
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
        });
});
```

### Step 4: Google OAuth Configuration

Update authorized redirect URIs in Google Cloud Console:
```
https://app.aiprofilephotomaker.com/auth/callback
https://api.aiprofilephotomaker.com/api/auth/google/callback
```

## Risk Mitigation

### Low-Risk Approach
1. **Parallel Operation**: Old URLs remain functional during transition
2. **Incremental Rollout**: Test with subdomain first, then redirect root
3. **Quick Rollback**: DNS changes can be reverted in minutes
4. **No Data Risk**: No database or storage changes required

### Rollback Plan
If issues arise:
1. Remove DNS CNAME records (5 minutes)
2. Revert Container App custom domain settings (10 minutes)
3. Total rollback time: 15 minutes

## Comparison with Service Migration Approach

| Aspect | Current Approach (Config Only) | Service Migration |
|--------|-------------------------------|-------------------|
| **Total Time** | 3-4 hours | 2-3 days |
| **Downtime Risk** | Zero | 30-60 minutes |
| **Complexity** | Low | High |
| **Testing Required** | Minimal | Extensive |
| **Rollback Time** | 15 minutes | 2-4 hours |
| **Infrastructure Changes** | None | Complete |
| **Cost Impact** | None | Potential increase |

## Post-Implementation Checklist

- [ ] DNS records properly configured
- [ ] TLS certificates validated
- [ ] Frontend can reach API at new URL
- [ ] CORS allows requests from app subdomain
- [ ] Authentication flow works end-to-end
- [ ] Google OAuth redirects properly
- [ ] Root domain redirects to app subdomain
- [ ] www subdomain redirects to app subdomain
- [ ] Old URLs still accessible (for rollback)
- [ ] Monitoring alerts updated
- [ ] Documentation updated

## Monitoring During Transition

1. **Application Insights**: Monitor for increased errors
2. **Container App Metrics**: Watch for connection issues
3. **DNS Propagation**: Use tools like whatsmydns.net
4. **User Reports**: Have support channel ready

## Success Criteria

1. All subdomains resolve correctly
2. HTTPS works on all subdomains
3. Authentication flow completes successfully
4. API calls from frontend succeed
5. No increase in error rates
6. Performance metrics remain stable

## Conclusion

This subdomain implementation strategy leverages the existing Azure Container Apps infrastructure, requiring only configuration changes rather than service migration. The estimated 3-4 hour implementation time represents a 90% reduction compared to migrating between Azure service types. The approach maintains zero downtime, provides easy rollback options, and minimizes risk while achieving the desired subdomain structure for professional presentation and better service separation.