# OAuth Troubleshooting Guide

## Overview

This guide documents the OAuth implementation challenges and solutions for AI.ProfilePhotoMaker, specifically addressing issues with Google OAuth when using ngrok for development and preparation for production deployment.

## Problem Summary

The main challenge was implementing Google OAuth authentication in a development environment using ngrok as a proxy, which created several issues:

1. **Correlation Cookie Failures**: ASP.NET Core OAuth correlation cookies couldn't persist across domain changes
2. **PKCE (Proof Key for Code Exchange) Issues**: Code verifiers weren't accessible after correlation failures
3. **URL Configuration Management**: Hardcoded URLs throughout the codebase made ngrok URL changes difficult
4. **Token Processing**: Angular app wasn't properly handling OAuth callback tokens

## Architecture Overview

### OAuth Flow
```
User → Angular (localhost:4200) → Google OAuth → API (ngrok URL) → Angular (dashboard)
```

### Key Components
- **Frontend**: Angular 19 app running on `localhost:4200`
- **Backend**: .NET 8 API running on `localhost:5035` 
- **Proxy**: ngrok tunnel exposing API to internet for OAuth callbacks
- **OAuth Provider**: Google OAuth 2.0

## Root Causes Identified

### 1. Correlation Cookie Domain Mismatch
**Problem**: ASP.NET Core OAuth sets correlation cookies for one domain (localhost) but Google redirects to another (ngrok domain).

**Error Logs**:
```
warn: Microsoft.AspNetCore.Authentication.Google.GoogleHandler[15]
      '.AspNetCore.Correlation.{cookieId}' cookie not found.
OAuth Remote Failure: Correlation failed.
```

### 2. PKCE Code Verifier Inaccessibility  
**Problem**: When correlation fails, the PKCE code verifier becomes inaccessible, preventing direct token exchange.

**Error Logs**:
```
Token exchange failed: {
  "error": "invalid_grant",
  "error_description": "Missing code verifier."
}
```

### 3. Hardcoded URLs
**Problem**: Magic string URLs scattered throughout codebase made environment changes difficult.

**Example Issues**:
- API redirects hardcoded to specific ngrok URLs
- Frontend OAuth initiation URLs hardcoded
- No centralized configuration management

### 4. Token Processing in Wrong Component
**Problem**: Angular login component processed OAuth tokens, but OAuth redirected directly to dashboard.

## Solutions Implemented

### 1. Configuration-Based URL Management

**File**: `AI.ProfilePhotoMaker.API/appsettings.Development.json`
```json
{
  "AppBaseUrl": "https://06f6-71-38-148-86.ngrok-free.app"
}
```

**Implementation**:
- All URLs now use `builder.Configuration["AppBaseUrl"]`
- Single point of configuration change
- Easy environment switching

### 2. OAuth Correlation Failure Bypass

**File**: `AI.ProfilePhotoMaker.API/Program.cs`
```csharp
// Disable PKCE for development
if (builder.Environment.IsDevelopment())
{
    options.UsePkce = false;
}

// Enhanced failure handling
options.Events.OnRemoteFailure = context =>
{
    var code = context.Request.Query["code"].ToString();
    if (!string.IsNullOrEmpty(code))
    {
        // Redirect to custom handler that bypasses correlation
        var redirectUrl = $"{appBaseUrl}/api/auth/google-direct-callback?code={Uri.EscapeDataString(code)}&returnUrl=/dashboard";
        context.Response.Redirect(redirectUrl);
        context.HandleResponse();
    }
    return Task.CompletedTask;
};
```

### 3. Custom OAuth Callback Handler

**File**: `AI.ProfilePhotoMaker.API/Controllers/AuthController.cs`
```csharp
[HttpGet("google-direct-callback")]
public async Task<IActionResult> GoogleDirectCallback(string? code = null, string returnUrl = "/dashboard")
{
    // Direct token exchange with Google
    var userInfo = await GetGoogleUserInfoAsync(code);
    return await ProcessGoogleUserAsync(userInfo.Email, userInfo.GivenName, userInfo.FamilyName, returnUrl);
}

private async Task<GoogleUserInfo?> GetGoogleUserInfoAsync(string code)
{
    // Manual OAuth token exchange without relying on ASP.NET Core state
}
```

### 4. Frontend Token Processing

**File**: `AI.ProfilePhotoMaker.UI/src/app/services/auth.service.ts`
```typescript
handleOAuthCallback(token: string, expiration?: string): void {
    if (token) {
        localStorage.setItem('authToken', token);
        if (expiration) {
            localStorage.setItem('tokenExpiration', expiration);
        }
        
        const user = this.extractUserFromToken(token);
        if (user) {
            localStorage.setItem('currentUser', JSON.stringify(user));
            this.currentUserSubject.next(user);
        }
        
        this.isAuthenticatedSubject.next(true);
    }
}
```

**File**: `AI.ProfilePhotoMaker.UI/src/app/dashboard/dashboard.component.ts`
```typescript
ngOnInit() {
    // Check for OAuth callback token in URL parameters
    this.route.queryParams.subscribe(params => {
        if (params['token']) {
            this.authService.handleOAuthCallback(params['token'], params['expiration']);
            this.router.navigate(['/dashboard']); // Clean URL
            return;
        }
    });
    
    this.loadDashboardData();
}
```

## Configuration Steps

### Development Setup (ngrok)

1. **Start ngrok tunnel**:
   ```bash
   ngrok http https://localhost:5035
   ```

2. **Update configuration**:
   ```json
   // appsettings.Development.json
   {
     "AppBaseUrl": "https://YOUR-NGROK-URL.ngrok-free.app"
   }
   ```

3. **Update Google OAuth Console**:
   - Authorized redirect URIs: `https://YOUR-NGROK-URL.ngrok-free.app/api/auth/external-login/callback`

4. **Restart API**:
   ```bash
   cd AI.ProfilePhotoMaker.API
   dotnet run
   ```

### Production Setup

1. **Update configuration**:
   ```json
   // appsettings.Production.json
   {
     "AppBaseUrl": "https://yourdomain.com"
   }
   ```

2. **Enable PKCE for production**:
   ```csharp
   // Program.cs - Remove development PKCE disable
   .AddGoogle(options =>
   {
       // Don't set UsePkce = false in production
   })
   ```

3. **Update Google OAuth Console**:
   - Authorized redirect URIs: `https://yourdomain.com/api/auth/external-login/callback`

4. **Configure HTTPS and proper cookie settings**:
   ```csharp
   options.CorrelationCookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
   options.CorrelationCookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
   ```

## Testing and Verification

### Successful OAuth Flow Logs
```
Configuring OAuth with base URL: https://06f6-71-38-148-86.ngrok-free.app
Setting redirect URI to: https://06f6-71-38-148-86.ngrok-free.app/api/auth/external-login/callback
OAuth Remote Failure: Correlation failed.
Found authorization code: 4/0AUJR-x7gUQKjlibjB...
OAuth failure detected, redirecting to direct OAuth handler
Successfully retrieved Google user info: user@gmail.com
Generated JWT token, length: 608
Redirecting to: http://localhost:4200/dashboard?token=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### Common Issues and Solutions

#### Issue: "redirect_uri_mismatch"
**Solution**: Update Google OAuth Console with current ngrok URL

#### Issue: "Correlation failed" 
**Solution**: Verify custom failure handler is working and extracting authorization code

#### Issue: OAuth redirects but Angular shows login screen
**Solution**: Ensure dashboard component is processing tokens from URL parameters

#### Issue: Token not persisted after OAuth
**Solution**: Verify `authService.handleOAuthCallback()` is being called

## Monitoring and Debugging

### Key Log Messages to Monitor
1. `Configuring OAuth with base URL: {url}` - Confirms correct base URL
2. `OAuth failure detected, redirecting to direct OAuth handler` - Custom handler triggered
3. `Successfully retrieved Google user info: {email}` - Token exchange successful
4. `Generated JWT token, length: {length}` - JWT creation successful
5. `Redirecting to: {url}` - Final redirect with token

### Development Tools
- **ngrok Web Interface**: `http://127.0.0.1:4040` - Monitor HTTP requests
- **Browser DevTools**: Monitor network requests and localStorage
- **API Logs**: Real-time OAuth flow tracking

## Security Considerations

### Development vs Production
- **Development**: PKCE disabled, permissive cookie settings
- **Production**: PKCE enabled, strict HTTPS-only cookies

### Token Security
- JWT tokens in URLs are temporary (immediately processed and removed)
- Tokens stored in localStorage (consider httpOnly cookies for production)
- Short token expiration times recommended

## Future Improvements

1. **HttpOnly Cookies**: Move from localStorage to httpOnly cookies for token storage
2. **Refresh Tokens**: Implement OAuth refresh token flow
3. **Multiple OAuth Providers**: Extend pattern to Facebook, Apple OAuth
4. **Environment Detection**: Automatic ngrok URL detection for development

## Quick Reference Commands

```bash
# Start development environment
cd AI.ProfilePhotoMaker.API && dotnet run &
cd AI.ProfilePhotoMaker.UI && ng serve &
ngrok http https://localhost:5035

# Update configuration after ngrok URL change
# 1. Copy new ngrok URL from terminal
# 2. Update appsettings.Development.json -> AppBaseUrl
# 3. Update Google OAuth Console redirect URI
# 4. Restart API: Ctrl+C, dotnet run

# Test OAuth flow
# 1. Go to http://localhost:4200/login
# 2. Click "Login with Google"
# 3. Should redirect to dashboard after authentication
```

## Troubleshooting Checklist

- [ ] ngrok tunnel active and accessible
- [ ] AppBaseUrl in appsettings matches ngrok URL
- [ ] Google OAuth Console redirect URI updated
- [ ] API restarted after configuration change
- [ ] Browser cache cleared if testing repeatedly
- [ ] Check API logs for correlation failure handling
- [ ] Verify Angular dashboard component token processing
- [ ] Confirm JWT token generation and redirect

---

*This guide was created after resolving OAuth correlation cookie issues in development environment using ngrok proxy. The solution provides a robust foundation for both development and production OAuth implementations.*