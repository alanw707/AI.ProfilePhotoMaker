---
title: "Infrastructure Troubleshooting Report: AI Profile Photo Maker"
system_id: "aipm-production"
complexity: "high"
status: "review"
architectural_patterns:
  - "microservices"
  - "container-apps"
  - "azure-paas"
scalability_metrics:
  current_capacity: "MVP"
  target_capacity: "1K users"
  scaling_approach: "horizontal"
technology_stack:
  - backend: "ASP.NET Core API"
  - frontend: "Angular"
  - database: "Azure SQL"
  - hosting: "Azure Container Apps"
design_timeline:
  start: "2025-08-12T20:03:15Z"
  review: "2025-08-12T20:30:00Z"
quality_attributes:
  - attribute: "availability"
    priority: "critical"
  - attribute: "performance"
    priority: "high"
---

# Infrastructure Troubleshooting Report: AI Profile Photo Maker

## Executive Summary

The AI Profile Photo Maker application is experiencing two critical infrastructure issues:
1. **Custom domain routing failure** - The frontend custom domain (app.aiprofilephotomaker.com) is not accessible
2. **API 502 errors** - The frontend is unable to communicate with the backend API

OAuth authentication has been successfully fixed and users can authenticate, but the application functionality is blocked by these infrastructure issues.

## Current Infrastructure State

### Container Apps Configuration
- **Frontend**: aipm-web-v1 (Angular application)
  - Container App URL: https://aipm-web-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io ✅ Working
  - Custom Domain: https://app.aiprofilephotomaker.com ❌ Not accessible
  - Certificate: Configured and valid
  - Target Port: 80
  - Status: Running, Healthy

- **Backend**: aipm-api-v1 (ASP.NET Core API)
  - Container App URL: https://aipm-api-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io ✅ Working
  - Custom Domain: https://api.aiprofilephotomaker.com ✅ Working
  - Certificate: Configured and valid
  - Target Port: 8080
  - Status: Running, Healthy
  - Health Endpoints: Responding correctly (200 OK)

### DNS Configuration
- Both domains resolve to the same IP: 48.214.86.35
- DNS resolution is working correctly
- CNAME records appear to be properly configured

## Root Cause Analysis

### Issue 1: Frontend Custom Domain Not Accessible

**Symptoms:**
- Custom domain (app.aiprofilephotomaker.com) times out
- Container app URL works perfectly
- DNS resolves correctly
- Certificate is configured in Azure

**Root Cause:**
The custom domain binding is configured in Azure Container Apps but the traffic routing is not working. This appears to be an Azure Container Apps ingress configuration issue where the SNI (Server Name Indication) binding is not properly routing traffic.

**Evidence:**
```json
{
  "customDomains": [{
    "bindingType": "SniEnabled",
    "certificateId": "[valid-certificate]",
    "name": "app.aiprofilephotomaker.com"
  }],
  "fqdn": "aipm-web-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io",
  "external": true,
  "targetPort": 80
}
```

### Issue 2: API 502 Bad Gateway Errors

**Symptoms:**
- Frontend shows 502 errors for API calls in browser
- Direct API calls return 401 (unauthorized) as expected
- API health endpoints are working
- No errors in API container logs

**Root Cause:**
The frontend is attempting to make API calls but the requests are being blocked or misrouted. The 502 errors indicate a proxy/gateway issue rather than an API issue. The frontend is likely:
1. Using incorrect API URL configuration
2. Making cross-origin requests that are being blocked
3. Using the container app URL instead of the custom domain

**Evidence:**
- API responds with 401 when called directly (expected behavior)
- Health endpoints return 200 OK
- No application errors in logs
- CORS is configured for https://app.aiprofilephotomaker.com

## Impact Assessment

### Business Impact
- **Critical**: Users cannot access the application through the branded domain
- **Critical**: Application functionality is completely blocked due to API communication failure
- **High**: OAuth authentication works but users cannot proceed past login

### Technical Impact
- Custom domain branding is not working
- API integration is broken
- User experience is severely degraded

## Resolution Plan

### Step 1: Fix Frontend Custom Domain (Immediate)

1. **Refresh the custom domain binding:**
```bash
# Remove and re-add the custom domain
az containerapp hostname delete \
  --resource-group aiprofilemaker-v1 \
  --name aipm-web-v1 \
  --hostname app.aiprofilephotomaker.com

# Re-add with proper certificate
az containerapp hostname add \
  --resource-group aiprofilemaker-v1 \
  --name aipm-web-v1 \
  --hostname app.aiprofilephotomaker.com
```

2. **Alternative: Update ingress configuration:**
```bash
# Update the container app with refreshed ingress settings
az containerapp update \
  --resource-group aiprofilemaker-v1 \
  --name aipm-web-v1 \
  --set-env-vars API_URL=https://api.aiprofilephotomaker.com
```

### Step 2: Fix API Communication (Immediate)

1. **Verify frontend environment configuration:**
   - Ensure API_URL environment variable is set to https://api.aiprofilephotomaker.com
   - Not the container app URL

2. **Update CORS configuration on backend:**
```bash
# Add both domain variations to CORS
az containerapp update \
  --resource-group aiprofilemaker-v1 \
  --name aipm-api-v1 \
  --set-env-vars CORS_ALLOWED_ORIGINS="https://app.aiprofilephotomaker.com,https://aipm-web-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io"
```

### Step 3: Temporary Workaround (If Needed)

While fixing the custom domain issue:
1. Users can access via: https://aipm-web-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io
2. Update OAuth redirect URLs to include the container app URL temporarily

## Verification Steps

### After Resolution:
1. **Test custom domain access:**
   ```bash
   curl -I https://app.aiprofilephotomaker.com
   # Should return 200 OK
   ```

2. **Test API connectivity:**
   ```bash
   # From browser console when on the app
   fetch('https://api.aiprofilephotomaker.com/api/health/ready')
   # Should return 200 OK
   ```

3. **Verify OAuth flow:**
   - Login via Google OAuth
   - Ensure redirect works to custom domain
   - Verify session is maintained

4. **Test application functionality:**
   - Load user dashboard
   - Verify API calls succeed
   - Test image upload functionality

## Prevention Recommendations

### Short-term (This Week)
1. **Implement health monitoring:**
   - Set up Azure Monitor alerts for custom domain availability
   - Configure uptime monitoring for both domains
   - Add synthetic transactions to test end-to-end flow

2. **Document configuration:**
   - Create runbook for custom domain configuration
   - Document all environment variables and their purposes
   - Maintain infrastructure configuration changelog

### Medium-term (This Month)
1. **Infrastructure as Code improvements:**
   - Ensure Bicep template includes all custom domain configurations
   - Add validation steps to deployment script
   - Implement automated smoke tests post-deployment

2. **Resilience improvements:**
   - Implement retry logic in frontend for API calls
   - Add fallback mechanisms for service degradation
   - Configure proper timeout and circuit breaker patterns

### Long-term (This Quarter)
1. **Architecture improvements:**
   - Consider Azure Front Door for global load balancing and SSL termination
   - Implement API Gateway pattern for better routing control
   - Evaluate Azure Application Gateway for advanced routing

## Configuration Reference

### Required Environment Variables

**Frontend (aipm-web-v1):**
```
API_URL=https://api.aiprofilephotomaker.com
```

**Backend (aipm-api-v1):**
```
CORS_ALLOWED_ORIGINS=https://app.aiprofilephotomaker.com,https://aiprofilephotomaker.com
ASPNETCORE_ENVIRONMENT=Production
```

### DNS Configuration
```
app.aiprofilephotomaker.com -> CNAME -> aipm-web-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io
api.aiprofilephotomaker.com -> CNAME -> aipm-api-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io
```

## Conclusion

The infrastructure issues are related to Azure Container Apps ingress configuration rather than application code problems. The custom domain routing needs to be refreshed, and the frontend needs proper API URL configuration. Both issues can be resolved through Azure CLI commands without code changes.

The fact that OAuth is working confirms that the basic infrastructure is healthy. The issues are specifically with domain routing and inter-service communication, which are configuration-level problems that can be fixed quickly once the proper commands are executed.

## Next Steps

1. Execute the resolution steps in order
2. Verify each fix before proceeding to the next
3. Document any additional findings
4. Implement monitoring to prevent recurrence
5. Schedule a post-incident review to improve deployment processes