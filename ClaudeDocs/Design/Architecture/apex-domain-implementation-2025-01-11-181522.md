---
title: "System Architecture: Clean Apex Domain Implementation for AI Profile Photo Maker"
system_id: "AIPM-APEX-001"
complexity: "medium"
status: "draft"
architectural_patterns:
  - "microservices"
  - "container-orchestration"
  - "api-gateway"
  - "spa-frontend"
scalability_metrics:
  current_capacity: "1K users"
  target_capacity: "10K users"
  scaling_approach: "horizontal"
technology_stack:
  - backend: "ASP.NET Core, Azure Container Apps"
  - database: "Azure SQL Database"
  - frontend: "Angular, Azure Container Apps"
  - cdn: "Azure Front Door (future)"
  - dns: "GoDaddy DNS"
design_timeline:
  start: "2025-01-11T18:15:22Z"
  review: "2025-01-12T10:00:00Z"
  completion: "2025-01-12T18:00:00Z"
linked_documents:
  - path: "infrastructure/deploy-fixed.ps1"
  - path: "docker-compose.yml"
dependencies:
  - system: "azure-container-apps"
    type: "infrastructure"
  - system: "godaddy-dns"
    type: "external"
  - system: "google-oauth"
    type: "external"
quality_attributes:
  - attribute: "performance"
    priority: "high"
  - attribute: "security"
    priority: "critical"
  - attribute: "maintainability"
    priority: "high"
---

# Clean Apex Domain Implementation Architecture
## AI Profile Photo Maker - Production Domain Configuration

### Executive Summary

This document outlines the implementation strategy for configuring a clean apex domain architecture for AI Profile Photo Maker, where both frontend and backend services are already deployed on Azure Container Apps. The approach prioritizes professional standards with the main domain (aiprofilephotomaker.com) pointing directly to the frontend application.

### Current State Analysis

#### Infrastructure Components
- **Frontend Service**: Angular SPA on Azure Container Apps
  - Current URL: Unknown (needs discovery)
  - Container: aipm-web-v1
  - Technology: Angular 17, Nginx
  
- **Backend Service**: ASP.NET Core API on Azure Container Apps
  - Current URL: aipm-api-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io
  - Container: aipm-api-v1
  - Technology: .NET 8, SQL Server

#### Key Observations
1. Both services are containerized and running successfully
2. No service migration required (staying within Azure Container Apps)
3. TLS certificates will be Azure-managed (automatic)
4. Current CORS configuration allows specific origins
5. Frontend environment configs use hardcoded API URLs

### Target Architecture (Option A - Clean Apex)

```
┌─────────────────────────────────────────────────────────────┐
│                         Internet                             │
└─────────────────────────────────────────────────────────────┘
                              │
                    ┌─────────┴─────────┐
                    │   GoDaddy DNS     │
                    └─────────┬─────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
        ▼                     ▼                     ▼
┌───────────────┐   ┌──────────────────┐   ┌──────────────┐
│ aiprofile... │   │ api.aiprofile... │   │ www.aiprof...│
│ (A Record)    │   │ (CNAME Record)   │   │ (CNAME →apex)│
└───────┬───────┘   └────────┬─────────┘   └──────────────┘
        │                    │
        ▼                    ▼
┌───────────────────────────────────────────────────────────┐
│              Azure Container Apps Environment              │
├─────────────────────────────┬──────────────────────────────┤
│     Frontend Container App  │    Backend Container App     │
│  ┌────────────────────┐    │   ┌─────────────────────┐   │
│  │  Angular Frontend  │    │   │   ASP.NET Core API  │   │
│  │  (aipm-web-v1)     │    │   │   (aipm-api-v1)     │   │
│  └────────────────────┘    │   └─────────────────────┘   │
└─────────────────────────────┴──────────────────────────────┘
```

### Implementation Tasks Breakdown

## Phase 1: Discovery & Planning (1-2 hours)

### 1.1 Resource Discovery
**Time Estimate: 30 minutes**
- Identify frontend Container App URL
- Document resource group structure
- Verify Container App configurations
- Check current ingress settings

**Commands Required:**
```bash
# List all Container Apps
az containerapp list --resource-group <rg-name> --output table

# Get frontend app details
az containerapp show --name aipm-web-v1 --resource-group <rg-name>

# Get current ingress configuration
az containerapp ingress show --name aipm-web-v1 --resource-group <rg-name>
```

### 1.2 DNS Planning
**Time Estimate: 30 minutes**
- Verify domain ownership in GoDaddy
- Document current DNS records
- Plan DNS cutover strategy
- Identify Container App IP addresses

### 1.3 Configuration Audit
**Time Estimate: 1 hour**
- Review current CORS settings in backend
- Document OAuth redirect URIs
- List all environment variables
- Identify hardcoded URLs in frontend

## Phase 2: Azure Configuration (2-3 hours)

### 2.1 Frontend Custom Domain Setup
**Time Estimate: 1 hour**

**Steps:**
1. Add custom domain to frontend Container App
2. Configure apex domain (aiprofilephotomaker.com)
3. Validate domain ownership (TXT record)
4. Enable managed certificate

**Azure CLI Commands:**
```bash
# Add custom domain to frontend
az containerapp hostname add \
  --resource-group <rg-name> \
  --name aipm-web-v1 \
  --hostname aiprofilephotomaker.com

# Bind managed certificate
az containerapp hostname bind \
  --resource-group <rg-name> \
  --name aipm-web-v1 \
  --hostname aiprofilephotomaker.com \
  --environment <env-name> \
  --validation-method HTTP
```

**Potential Issues:**
- Domain validation might take 10-15 minutes
- Certificate provisioning can take up to 20 minutes
- May need to temporarily add TXT record for validation

### 2.2 Backend API Subdomain Setup
**Time Estimate: 1 hour**

**Steps:**
1. Add api.aiprofilephotomaker.com to backend Container App
2. Configure subdomain binding
3. Enable managed certificate
4. Verify HTTPS endpoints

**Azure CLI Commands:**
```bash
# Add API subdomain
az containerapp hostname add \
  --resource-group <rg-name> \
  --name aipm-api-v1 \
  --hostname api.aiprofilephotomaker.com

# Bind certificate
az containerapp hostname bind \
  --resource-group <rg-name> \
  --name aipm-api-v1 \
  --hostname api.aiprofilephotomaker.com \
  --environment <env-name> \
  --validation-method HTTP
```

### 2.3 WWW Redirect Configuration
**Time Estimate: 30 minutes**

**Options:**
1. Add www subdomain to frontend Container App with built-in redirect
2. Configure at DNS level (if GoDaddy supports)
3. Use Azure Front Door (future enhancement)

**Recommended: Container App approach**
```bash
# Add www subdomain
az containerapp hostname add \
  --resource-group <rg-name> \
  --name aipm-web-v1 \
  --hostname www.aiprofilephotomaker.com
```

## Phase 3: Application Updates (2-3 hours)

### 3.1 Frontend Configuration Updates
**Time Estimate: 1 hour**

**Files to Update:**
- `/AI.ProfilePhotoMaker.UI/src/environments/environment.prod.ts`
- `/AI.ProfilePhotoMaker.UI/src/environments/environment.mvp-v1.ts`

**Changes Required:**
```typescript
// environment.prod.ts
export const environment = {
  production: true,
  apiUrl: 'https://api.aiprofilephotomaker.com/api',
  baseUrl: 'https://api.aiprofilephotomaker.com',
  name: 'production',
  stripePublishableKey: '...',
  googleClientId: '...'
};
```

**Build & Deploy:**
```bash
# Build frontend with production config
npm run build:prod

# Build Docker image
docker build -t aipm-web-v1:latest -f Dockerfile.frontend .

# Push to ACR
az acr build --registry <acr-name> --image aipm-web-v1:latest .
```

### 3.2 Backend CORS Updates
**Time Estimate: 45 minutes**

**File to Update:**
- `/AI.ProfilePhotoMaker.API/Program.cs`

**Changes Required:**
```csharp
// Add new production domains to CORS
allowedOrigins.Add("https://aiprofilephotomaker.com");
allowedOrigins.Add("https://www.aiprofilephotomaker.com");
// Keep existing for rollback capability
allowedOrigins.Add("https://aipm-web-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io");
```

**Environment Variables to Update:**
```bash
APP_BASE_URL=https://aiprofilephotomaker.com
JWT_VALID_AUDIENCE=https://aiprofilephotomaker.com
JWT_VALID_ISSUER=https://api.aiprofilephotomaker.com
```

### 3.3 Google OAuth Updates
**Time Estimate: 45 minutes**

**Google Cloud Console Tasks:**
1. Navigate to APIs & Services > Credentials
2. Edit OAuth 2.0 Client ID
3. Add Authorized JavaScript origins:
   - https://aiprofilephotomaker.com
   - https://www.aiprofilephotomaker.com
4. Add Authorized redirect URIs:
   - https://api.aiprofilephotomaker.com/api/auth/google/callback
   - https://aiprofilephotomaker.com/auth/callback

**Verification Steps:**
- Test OAuth flow immediately after update
- Keep old URLs temporarily for rollback

## Phase 4: DNS Configuration (1-2 hours)

### 4.1 GoDaddy DNS Setup
**Time Estimate: 45 minutes**

**DNS Records Required:**

| Type | Name | Value | TTL | Purpose |
|------|------|-------|-----|---------|
| A | @ | Container App IP | 600 | Apex domain |
| CNAME | api | aipm-api-v1.region.azurecontainerapps.io | 600 | API subdomain |
| CNAME | www | aiprofilephotomaker.com | 600 | WWW redirect |
| TXT | _verification | Azure-provided-value | 600 | Domain verification |

**Steps:**
1. Log into GoDaddy DNS management
2. Remove any existing A/CNAME records for the domain
3. Add new records as specified
4. Lower TTL initially for quick changes (600 seconds)
5. After stable, increase TTL to 3600

### 4.2 DNS Propagation Monitoring
**Time Estimate: 30-45 minutes**

**Tools to Use:**
- whatsmydns.net - Global DNS propagation checker
- `nslookup` / `dig` commands locally
- Azure Portal domain verification status

**Verification Commands:**
```bash
# Check apex domain
nslookup aiprofilephotomaker.com

# Check API subdomain
nslookup api.aiprofilephotomaker.com

# Verify HTTPS
curl -I https://aiprofilephotomaker.com
curl -I https://api.aiprofilephotomaker.com/api/health
```

## Phase 5: Testing & Validation (2-3 hours)

### 5.1 Functional Testing
**Time Estimate: 1.5 hours**

**Test Checklist:**
- [ ] Frontend loads on https://aiprofilephotomaker.com
- [ ] API responds on https://api.aiprofilephotomaker.com
- [ ] WWW redirects to apex domain
- [ ] TLS certificates are valid and trusted
- [ ] No mixed content warnings
- [ ] Console free of CORS errors

**API Integration Tests:**
- [ ] User registration works
- [ ] Login/logout functions
- [ ] Google OAuth flow completes
- [ ] Image upload succeeds
- [ ] Style generation works
- [ ] Payment flow (if enabled)

### 5.2 Performance Testing
**Time Estimate: 45 minutes**

**Metrics to Validate:**
- Page load time < 3 seconds
- API response times consistent with baseline
- No increase in error rates
- CDN cache headers properly set

**Tools:**
- Chrome DevTools Network tab
- Lighthouse performance audit
- Azure Application Insights

### 5.3 Security Validation
**Time Estimate: 45 minutes**

**Security Checklist:**
- [ ] HTTPS enforced on all endpoints
- [ ] HSTS headers present
- [ ] CSP headers configured
- [ ] Cookies have Secure flag
- [ ] API authentication working
- [ ] CORS properly restricted

## Phase 6: Cutover & Monitoring (1 hour)

### 6.1 Production Cutover
**Time Estimate: 30 minutes**

**Cutover Steps:**
1. Final verification of all configurations
2. Update DNS records in GoDaddy
3. Monitor DNS propagation
4. Clear CDN caches if applicable
5. Notify stakeholders

### 6.2 Post-Deployment Monitoring
**Time Estimate: 30 minutes**

**Monitoring Checklist:**
- Azure Application Insights dashboards
- Container App metrics (CPU, memory, requests)
- Error rate monitoring
- User activity tracking
- DNS query success rate

## Total Time Estimate Summary

| Phase | Task | Optimistic | Realistic | Pessimistic |
|-------|------|------------|-----------|-------------|
| 1 | Discovery & Planning | 1 hour | 1.5 hours | 2 hours |
| 2 | Azure Configuration | 2 hours | 2.5 hours | 3 hours |
| 3 | Application Updates | 2 hours | 2.5 hours | 3 hours |
| 4 | DNS Configuration | 1 hour | 1.5 hours | 2 hours |
| 5 | Testing & Validation | 2 hours | 2.5 hours | 3 hours |
| 6 | Cutover & Monitoring | 0.5 hours | 1 hour | 1.5 hours |
| **Total** | **Full Implementation** | **8.5 hours** | **11.5 hours** | **14.5 hours** |

## Risk Analysis & Mitigation

### High-Risk Areas

1. **DNS Propagation Delays**
   - Risk: DNS changes take 24-48 hours globally
   - Mitigation: Lower TTL before changes, use multiple DNS checkers
   - Fallback: Keep old URLs active for 48 hours

2. **Certificate Provisioning Issues**
   - Risk: Azure managed certificates fail validation
   - Mitigation: Pre-validate domain ownership, have manual cert ready
   - Fallback: Use Let's Encrypt as backup

3. **CORS Misconfiguration**
   - Risk: Frontend can't communicate with API
   - Mitigation: Test in staging first, keep old origins active
   - Fallback: Quick rollback capability in place

4. **OAuth Redirect Failures**
   - Risk: Google OAuth stops working
   - Mitigation: Add new URLs before removing old ones
   - Fallback: Keep parallel OAuth configs for 7 days

### Low-Risk Areas
- Container Apps are stable (no migration)
- Both services already containerized
- Azure manages TLS automatically
- No database changes required

## Rollback Strategy

### Immediate Rollback (< 5 minutes)
1. Update DNS records back to Container App URLs
2. Revert environment variables in Container Apps
3. Keep monitoring active

### Partial Rollback Options
- Keep API on subdomain, rollback only frontend
- Use old URLs as fallback for specific features
- Maintain dual configuration for gradual migration

## Success Criteria

### Technical Success Metrics
- Zero downtime during migration
- All tests passing post-deployment
- Response times within 10% of baseline
- No increase in error rates

### Business Success Metrics
- Improved brand presence with clean domain
- Better SEO potential with apex domain
- Professional appearance for investors/customers
- Simplified user experience

## Future Enhancements

### Phase 2 Considerations (Not in MVP)
1. **Azure Front Door Integration**
   - Global CDN distribution
   - Advanced routing rules
   - WAF protection
   - Estimated additional time: 4-6 hours

2. **Multi-Region Deployment**
   - Geo-distributed Container Apps
   - Regional failover capability
   - Estimated additional time: 8-12 hours

3. **Advanced Monitoring**
   - Custom domain-specific dashboards
   - Real user monitoring (RUM)
   - Synthetic monitoring
   - Estimated additional time: 3-4 hours

## Implementation Checklist

### Pre-Implementation
- [ ] Verify domain ownership
- [ ] Backup current configurations
- [ ] Document all current URLs
- [ ] Prepare rollback scripts
- [ ] Schedule maintenance window

### During Implementation
- [ ] Follow phase-by-phase approach
- [ ] Document any deviations
- [ ] Test after each phase
- [ ] Keep stakeholders informed
- [ ] Monitor error logs actively

### Post-Implementation
- [ ] Update documentation
- [ ] Remove old configurations (after 7 days)
- [ ] Increase DNS TTL values
- [ ] Archive deployment artifacts
- [ ] Conduct retrospective

## Conclusion

The clean apex domain implementation for AI Profile Photo Maker represents a straightforward but important infrastructure upgrade. With both services already running on Azure Container Apps, the primary complexity lies in coordination rather than technical challenges. The estimated 11.5 hours (realistic) timeline accounts for careful testing and validation to ensure zero downtime.

The approach prioritizes:
1. Professional domain structure (apex for app, subdomain for API)
2. Minimal service disruption
3. Comprehensive testing
4. Clear rollback procedures
5. Future scalability considerations

This architecture positions the application for growth while maintaining the simplicity appropriate for an MVP production system.