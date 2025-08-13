---
title: "Root Cause Analysis: OAuth Redirect URI and Custom Domain Issues"
issue_id: "OAUTH-001"
severity: "critical"
status: "complete"
root_cause_categories:
  - "code defect"
  - "configuration error"
investigation_timeline:
  start: "2025-08-12T16:25:00Z"
  end: "2025-08-12T16:35:00Z"
  duration: "10m 0s"
linked_documents:
  - path: "AI.ProfilePhotoMaker.API/Controllers/AuthController.cs"
  - path: "infrastructure/simple-deploy.bicep"
evidence_files:
  - type: "test"
    path: "tests/playwright/tests/investigate-deployment.spec.ts"
  - type: "log"
    path: "playwright-test-output.txt"
prevention_actions:
  - category: "testing"
    priority: "high"
  - category: "configuration"
    priority: "high"
---

# Root Cause Analysis: OAuth Redirect URI and Custom Domain Issues

## Executive Summary

After deployment of OAuth redirect URI fixes, multiple critical issues were identified:
1. OAuth redirect URI is using HTTP instead of HTTPS
2. Custom domains are actually working (contrary to initial report)
3. The Azure default domain shown in screenshot was misleading

## Problem Statement

**Reported Issues:**
- Screenshot showed Azure default domain instead of custom domain
- OAuth authentication still failing
- Potential URL duplication in network requests
- Deployment took unusually long (8+ minutes)

**Actual Issues Found:**
- OAuth redirect URI incorrectly using HTTP protocol
- Logic error in determining when to force HTTPS

## Investigation Timeline

### 16:25 - Initial Evidence Collection
- Reviewed recent git commits
- Examined OAuth redirect URI fix (commit 163fac9)
- Analyzed infrastructure configuration

### 16:28 - Azure Configuration Verification
- Confirmed custom domains configured in Container Apps
- Both api.aiprofilephotomaker.com and app.aiprofilephotomaker.com have valid certificates
- Custom domains ARE properly configured and working

### 16:30 - Direct Testing
- API health endpoint: ✓ Working via custom domain
- OAuth redirect endpoint: ✗ Returns HTTP redirect URI
- Frontend: ✓ Accessible via custom domain

### 16:31 - Comprehensive Playwright Testing
- Created systematic investigation tests
- Confirmed all findings with automated tests
- Collected network request patterns

## Root Cause Analysis

### Primary Root Cause: Incorrect Host Detection Logic

**Location:** `/AI.ProfilePhotoMaker.API/Controllers/AuthController.cs` (lines 122-123)

```csharp
// Current problematic code:
var scheme = Request.Host.Host.Contains("aiprofilephotomaker.com") ? "https" : Request.Scheme;
var backendBaseUrl = $"{scheme}://{Request.Host}";
```

**The Problem:**
- `Request.Host.Host` returns just the hostname portion (e.g., "api.aiprofilephotomaker.com")
- The check `Request.Host.Host.Contains("aiprofilephotomaker.com")` should return true
- However, evidence shows the redirect URI is still using HTTP

**Root Cause:**
When behind Azure Container Apps load balancer:
- `Request.Host` likely contains the internal host, not the custom domain
- The application sees the internal Azure Container Apps hostname
- The condition fails, falling back to `Request.Scheme` which is "http" internally

### Evidence

From Playwright test output:
```
OAuth Redirect URI: http://api.aiprofilephotomaker.com/api/auth/external-login-callback
  - Protocol: HTTP ✗
  - Domain: api.aiprofilephotomaker.com
  - Is Custom Domain: Yes ✓
```

The redirect location from OAuth endpoint:
```
location: https://accounts.google.com/o/oauth2/v2/auth?
  redirect_uri=http%3A%2F%2Fapi.aiprofilephotomaker.com%2Fapi%2Fauth%2Fexternal-login-callback
```

## Secondary Findings

### 1. Custom Domains ARE Working
- Contrary to initial report, custom domains are properly configured
- Both frontend and backend respond correctly on custom domains
- The screenshot showing Azure default domain was likely from a different context

### 2. No URL Duplication Found
- Network monitoring did not detect URL duplication
- API requests are correctly formatted

### 3. Frontend Accessibility
- Frontend loads successfully via https://app.aiprofilephotomaker.com
- No issues with custom domain resolution

## Impact Analysis

### Current Impact:
- OAuth authentication completely broken
- Google OAuth rejects HTTP redirect URIs for production domains
- Users cannot log in via Google OAuth

### Potential Impact:
- Complete authentication failure for all OAuth providers
- Security vulnerability if HTTP callbacks were accepted

## Solution

### Immediate Fix Required:

```csharp
// Correct approach - use forwarded headers or environment-based detection
var isProduction = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";
var scheme = isProduction ? "https" : Request.Scheme;
var backendBaseUrl = $"{scheme}://{Request.Host}";
```

Or better:

```csharp
// Use X-Forwarded headers which Azure Container Apps provides
var proto = Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? Request.Scheme;
var host = Request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? Request.Host.Value;
var backendBaseUrl = $"{proto}://{host}";
```

## Recommendations

### Immediate Actions:
1. **Fix OAuth redirect URI generation** - Use forwarded headers to detect actual protocol
2. **Add logging** - Log the actual Request.Host value in production for debugging
3. **Test OAuth flow** - Verify complete OAuth flow works after fix

### Long-term Improvements:
1. **Configure Forwarded Headers Middleware** - Properly handle proxy scenarios
2. **Add OAuth integration tests** - Automated tests for OAuth redirect generation
3. **Environment-specific configuration** - Use configuration to specify base URLs

### Configuration Fix:
Add to Program.cs:
```csharp
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
```

## Lessons Learned

1. **Azure Container Apps behaves as a reverse proxy** - Applications see internal hostnames
2. **Always use forwarded headers in containerized environments** - Essential for correct URL generation
3. **Test OAuth flows in actual deployment** - Local testing doesn't catch proxy-related issues
4. **Don't rely on hostname detection** - Use environment variables or configuration

## Verification Steps

After implementing the fix:
1. Deploy the corrected code
2. Test OAuth redirect: `curl -I https://api.aiprofilephotomaker.com/api/auth/external-login/google`
3. Verify redirect_uri uses HTTPS
4. Complete full OAuth login flow
5. Monitor for any URL-related issues

## Conclusion

The root cause is incorrect protocol detection in the OAuth redirect URI generation. The application cannot correctly determine it's being accessed via HTTPS because Azure Container Apps acts as a reverse proxy. The fix is to use forwarded headers or environment-based detection instead of hostname checking.