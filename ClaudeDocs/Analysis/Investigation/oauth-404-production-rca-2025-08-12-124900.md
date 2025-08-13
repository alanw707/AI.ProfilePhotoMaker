---
title: "Root Cause Analysis: OAuth Authentication 404 Error in Production"
issue_id: "OAUTH-PROD-001"
severity: "critical"
status: "complete"
root_cause_categories:
  - "configuration error"
  - "infrastructure issue"
investigation_timeline:
  start: "2025-08-12T12:45:00Z"
  end: "2025-08-12T12:49:00Z"
  duration: "4m 0s"
linked_documents:
  - path: "AI.ProfilePhotoMaker.API/Controllers/AuthController.cs"
  - path: "AI.ProfilePhotoMaker.API/Program.cs"
  - path: "AI.ProfilePhotoMaker.UI/src/app/auth/login/login.component.ts"
evidence_files:
  - type: "log"
    path: "azure-container-apps-env-vars.txt"
  - type: "code"
    path: "oauth-configuration-mismatch.txt"
prevention_actions:
  - category: "configuration management"
    priority: "high"
  - category: "deployment validation"
    priority: "high"
---

# Root Cause Analysis: OAuth Authentication 404 Error in Production

## Executive Summary
The OAuth authentication failure in production is caused by **missing OAuth environment variables** in the Azure Container Apps deployment, combined with a **routing configuration issue** between the custom domain and the Container Apps backend.

## Issue Description
- **Symptom**: Users receive a 404 error after attempting Google OAuth login at `app.aiprofilephotomaker.com/404`
- **Impact**: Complete OAuth authentication failure preventing user login via Google
- **Environment**: Production (Azure Container Apps)
- **Severity**: Critical - blocks all OAuth-based authentication

## Investigation Timeline

### 1. Initial Discovery (12:45:00)
- Identified OAuth flow redirecting to 404 page
- Confirmed deployment was successful but OAuth non-functional

### 2. Environment Configuration Check (12:46:00)
Discovered critical missing environment variables in Azure Container Apps:
```
Current variables:
- ASPNETCORE_ENVIRONMENT
- ConnectionStrings__DefaultConnection
- Jwt__Secret
- Replicate__ApiToken
- AzureStorage__ConnectionString
- ApplicationInsights__ConnectionString
- Database__AutoMigrateOnStartup
- Database__ValidateOnStartup
- CORS_ALLOWED_ORIGINS

Missing OAuth variables:
- Authentication__Google__ClientId ❌
- Authentication__Google__ClientSecret ❌
- AppBaseUrl ❌
```

### 3. API Accessibility Test (12:47:00)
- Direct Container App URL works: `https://aipm-api-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io`
- Custom domain times out: `https://app.aiprofilephotomaker.com/api/*`
- OAuth debug endpoint shows wrong Google Client ID configured

### 4. Configuration Mismatch Analysis (12:48:00)
Found multiple configuration issues:
1. **Wrong Google Client ID**: API using `116968296687-fievkkq...` instead of `331984288023-lh1upthod...`
2. **Missing AppBaseUrl**: Required for OAuth redirect URL construction
3. **Frontend-Backend Routing**: Custom domain not properly routing to backend API

## Root Cause Analysis

### Primary Root Causes

#### 1. Missing OAuth Environment Variables
The Azure Container Apps deployment is missing critical OAuth configuration:
- `Authentication__Google__ClientId` not set
- `Authentication__Google__ClientSecret` not set
- `AppBaseUrl` not configured

This causes the API to fall back to development/default OAuth credentials which don't match the production Google OAuth app configuration.

#### 2. Custom Domain Routing Issue
The custom domain `app.aiprofilephotomaker.com` is not properly routing API calls to the backend Container App:
- Frontend at `app.aiprofilephotomaker.com` loads correctly
- API calls to `app.aiprofilephotomaker.com/api/*` fail/timeout
- Direct Container App URLs work: `aipm-api-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io`

#### 3. OAuth Redirect URI Mismatch
The OAuth flow is attempting to redirect to URLs that don't match the Google OAuth app configuration:
- Expected: `https://api.aiprofilephotomaker.com/api/auth/external-login-callback`
- Actual: Various mismatched URLs due to missing configuration

## Evidence

### 1. Missing Environment Variables
```bash
az containerapp show --name aipm-api-v1 --resource-group aiprofilemaker-v1 \
  --query "properties.template.containers[0].env[].name" -o tsv
# Output shows no OAuth-related variables
```

### 2. OAuth Debug Response
```json
{
  "clientId": "116968296687-fievkkq...", // Wrong Client ID
  "callbackPath": "/signin-google",
  "authorizationEndpoint": "https://accounts.google.com/o/oauth2/v2/auth"
}
```

### 3. Frontend Configuration
```typescript
// environment.prod.ts
apiUrl: 'https://api.aiprofilephotomaker.com/api',
baseUrl: 'https://api.aiprofilephotomaker.com',
```

## Immediate Fix Required

### 1. Add Missing Environment Variables
```bash
# Add OAuth configuration to Container App
az containerapp update \
  --name aipm-api-v1 \
  --resource-group aiprofilemaker-v1 \
  --set-env-vars \
    "Authentication__Google__ClientId=331984288023-lh1upthod06meoko58g7hn9d7h68l311.apps.googleusercontent.com" \
    "Authentication__Google__ClientSecret=<ACTUAL_SECRET>" \
    "AppBaseUrl=https://app.aiprofilephotomaker.com"
```

### 2. Fix Custom Domain Routing
The custom domain needs proper routing configuration:
- Ensure `api.aiprofilephotomaker.com` points to the API Container App
- Or configure path-based routing to route `/api/*` to the backend

### 3. Update Google OAuth Console
Ensure these redirect URIs are configured in Google Cloud Console:
- `https://api.aiprofilephotomaker.com/api/auth/external-login-callback`
- `https://api.aiprofilephotomaker.com/signin-google`

## Prevention Measures

### 1. Environment Variable Validation
- Add startup validation to check required OAuth variables
- Fail fast if OAuth configuration is incomplete
- Log clear error messages for missing configuration

### 2. Deployment Checklist
Create deployment validation that verifies:
- All required environment variables are set
- OAuth endpoints are accessible
- Redirect URIs match OAuth provider configuration

### 3. Infrastructure as Code
- Store environment variables in Azure Key Vault
- Use Bicep/ARM templates to ensure consistent deployment
- Version control all infrastructure configuration

### 4. Automated Testing
- Add Playwright tests for OAuth flow
- Test OAuth with production-like configuration
- Validate OAuth endpoints during deployment

## Validation Steps

After applying fixes:
1. Verify environment variables are set correctly
2. Test OAuth debug endpoint returns correct configuration
3. Perform end-to-end OAuth login test
4. Verify token generation and user creation

## Lessons Learned

1. **Configuration Management**: OAuth configuration must be explicitly set in production deployments
2. **Custom Domain Complexity**: Custom domains require careful routing configuration for API access
3. **Environment Parity**: Development and production OAuth configurations must be properly separated
4. **Validation Importance**: OAuth configuration should be validated during deployment

## Recommendations

1. **Immediate**: Apply environment variable fixes to restore OAuth functionality
2. **Short-term**: Fix custom domain routing for proper API access
3. **Long-term**: Implement automated deployment validation and configuration management

## Conclusion

The OAuth authentication failure is a configuration issue, not a code problem. The missing environment variables and routing configuration prevent the OAuth flow from completing successfully. The fix is straightforward but requires careful application of the correct configuration values.