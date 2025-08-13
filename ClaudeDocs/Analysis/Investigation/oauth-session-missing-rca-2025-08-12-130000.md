---
title: "Root Cause Analysis: OAuth 500 Error - Session Middleware Missing in Production"
issue_id: "OAUTH-PROD-002"
severity: "critical"
status: "complete"
root_cause_categories:
  - "configuration error"
  - "code defect"
investigation_timeline:
  start: "2025-08-12T12:49:00Z"
  end: "2025-08-12T13:00:00Z"
  duration: "11m 0s"
linked_documents:
  - path: "AI.ProfilePhotoMaker.API/Program.cs"
  - path: "AI.ProfilePhotoMaker.API/Controllers/AuthController.cs"
  - path: "ClaudeDocs/Analysis/Investigation/oauth-404-production-rca-2025-08-12-124900.md"
evidence_files:
  - type: "code"
    path: "session-middleware-config.cs"
  - type: "log"
    path: "oauth-500-error.txt"
prevention_actions:
  - category: "testing"
    priority: "high"
  - category: "code review"
    priority: "medium"
---

# Root Cause Analysis: OAuth 500 Error - Session Middleware Missing in Production

## Executive Summary
The OAuth authentication failure returning HTTP 500 error is caused by **missing session middleware configuration in production**. The `AuthController.ExternalLogin` method attempts to use `HttpContext.Session` which is only configured for Development environment, causing a NullReferenceException in production.

## Issue Description
- **Symptom**: OAuth endpoint returns HTTP 500 error when accessed: `https://api.aiprofilephotomaker.com/api/auth/external-login/google`
- **Impact**: Complete OAuth authentication failure preventing user login via Google
- **Environment**: Production (Azure Container Apps)
- **Severity**: Critical - blocks all OAuth-based authentication

## Investigation Timeline

### 1. Initial API Testing (12:49:00)
- Confirmed `api.aiprofilephotomaker.com` is correctly configured as custom domain
- OAuth debug endpoint shows Google OAuth is properly configured with credentials
- Direct OAuth endpoint test returns HTTP 500 error

### 2. Code Analysis (12:52:00)
Found session dependency in `AuthController.cs` (lines 114-117):
```csharp
// Generate state parameter for security
var state = Guid.NewGuid().ToString();
HttpContext.Session.SetString("oauth_state", state);
HttpContext.Session.SetString("oauth_return_url", returnUrl);
```

### 3. Configuration Review (12:55:00)
Discovered session middleware only configured in Development (`Program.cs` lines 111-131):
```csharp
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddMemoryCache();
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSession(options =>
    {
        // Session configuration...
    });
}
```

And middleware pipeline (lines 516-519):
```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSession();
}
```

### 4. Root Cause Confirmation (12:58:00)
- Session services not registered in production DI container
- Session middleware not added to production pipeline
- `HttpContext.Session` is null in production, causing NullReferenceException

## Root Cause Analysis

### Primary Root Cause: Conditional Session Configuration

The session middleware required for OAuth state management was incorrectly configured as Development-only:

1. **Service Registration**: Session services wrapped in `if (builder.Environment.IsDevelopment())`
2. **Middleware Pipeline**: `app.UseSession()` also wrapped in Development check
3. **Controller Dependency**: `AuthController` unconditionally uses `HttpContext.Session`

This creates a runtime dependency mismatch where the controller expects session support that doesn't exist in production.

### Contributing Factors

1. **Missing Production Testing**: OAuth flow not tested in production-like environment
2. **Conditional Configuration**: Environment-specific middleware configuration without corresponding controller logic
3. **No Null Checks**: Controller doesn't validate session availability before use

## Evidence

### 1. HTTP 500 Error Response
```bash
curl -v "https://api.aiprofilephotomaker.com/api/auth/external-login/google"
< HTTP/2 500 
< content-length: 0
```

### 2. Session Usage in Controller
```csharp
// AuthController.cs, line 116-117
HttpContext.Session.SetString("oauth_state", state);
HttpContext.Session.SetString("oauth_return_url", returnUrl);
```

### 3. Development-Only Configuration
```csharp
// Program.cs, line 111
if (builder.Environment.IsDevelopment())
{
    // Session configuration only in Development
}
```

## Solution Applied

### Immediate Fix
Removed environment condition for session middleware configuration:
- Moved session service registration outside Development check (lines 116-128)
- Moved `app.UseSession()` outside Development check (line 513)

### Code Changes
```csharp
// Services configuration (always registered)
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.Domain = null;
});

// Middleware pipeline (always used)
app.UseSession(); // Required for OAuth state management
```

## Validation Steps

After deploying the fix:
1. Test OAuth endpoint returns proper redirect instead of 500
2. Verify session cookies are set in production
3. Complete end-to-end OAuth login flow
4. Confirm state parameter validation works

## Prevention Measures

### 1. Testing Strategy
- Add integration tests for OAuth flow
- Test with production-like configuration
- Include session-dependent features in test suite

### 2. Code Review Process
- Flag environment-specific middleware configuration
- Ensure controller dependencies match middleware configuration
- Review all `HttpContext.Session` usage

### 3. Configuration Validation
- Add startup validation for required middleware
- Log warning if session-dependent features lack session support
- Consider using alternative state storage (e.g., encrypted cookies)

### 4. Alternative Approaches
Consider removing session dependency entirely:
- Use encrypted state cookies
- Store state in distributed cache with correlation ID
- Use built-in OAuth middleware state handling

## Lessons Learned

1. **Environment Parity**: Middleware configuration should be consistent across environments
2. **Dependency Management**: Controllers shouldn't assume middleware availability
3. **Testing Coverage**: OAuth flows must be tested in production configuration
4. **Error Handling**: Include proper null checks and error messages

## Recommendations

### Immediate
1. Deploy session middleware fix to production
2. Verify OAuth functionality after deployment
3. Monitor for any session-related errors

### Short-term
1. Add Playwright tests for OAuth flow
2. Review all environment-specific configurations
3. Add null checks in controllers using session

### Long-term
1. Consider removing session dependency from OAuth flow
2. Implement distributed session storage for scalability
3. Standardize middleware configuration across environments

## Conclusion

The OAuth 500 error was caused by missing session middleware in production, a configuration oversight where session support was incorrectly limited to Development environment only. The fix is straightforward - enabling session middleware for all environments. This issue highlights the importance of environment parity and comprehensive testing of authentication flows.