# OAuth Troubleshooting Guide

## Overview

This guide documents the OAuth implementation for AI.ProfilePhotoMaker using the standard ASP.NET Core OAuth flow, which works seamlessly across development (ngrok) and production environments.

## Architecture Overview

### OAuth Flow
```
User → Angular (ngrok) → API Challenge → Google OAuth → API Callback (/signin-google) → Controller Callback → Angular (dashboard)
```

### Key Components
- **Frontend**: Angular app served through ngrok proxy
- **Backend**: .NET 8 API served through ngrok proxy  
- **OAuth Middleware**: ASP.NET Core standard OAuth handling
- **OAuth Provider**: Google OAuth 2.0

## Implementation Approach

### Standard ASP.NET Core OAuth Pattern

We use the built-in OAuth middleware which handles:
- State parameter generation and validation
- Correlation cookie management
- Authorization code exchange
- User claims extraction

**Key Configuration** in `Program.cs`:
```csharp
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "";
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "";
    options.CallbackPath = "/signin-google"; // Middleware handles this path
    
    // Cookie configuration for same-origin
    options.CorrelationCookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    options.CorrelationCookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
    options.CorrelationCookie.IsEssential = true;
    options.CorrelationCookie.HttpOnly = true;
    
    // Simple error handling
    options.Events.OnRemoteFailure = context =>
    {
        var errorMessage = context.Failure?.Message ?? "OAuth authentication failed";
        var frontendUrl = context.Request.Headers["Origin"].FirstOrDefault() ?? 
                        context.Request.Headers["Referer"].FirstOrDefault()?.Split('?')[0] ?? 
                        builder.Configuration["AppBaseUrl"] ?? 
                        "http://localhost:4200";
                        
        context.Response.Redirect($"{frontendUrl}/login?error=oauth_failed&message={Uri.EscapeDataString(errorMessage)}");
        context.HandleResponse();
        return Task.CompletedTask;
    };
})
```

### Controller Implementation

**File**: `AI.ProfilePhotoMaker.API/Controllers/AuthController.cs`

1. **Initiate OAuth Challenge**:
```csharp
[HttpGet("external-login/{provider}")]
public IActionResult ExternalLogin(string provider, string returnUrl = "", string frontendUrl = "")
{
    var properties = new AuthenticationProperties 
    { 
        RedirectUri = Url.Action("ExternalLoginCallback", "Auth", new { returnUrl, frontendUrl }),
        Items = 
        {
            { "returnUrl", returnUrl },
            { "frontendUrl", frontendUrl }
        }
    };
    
    return Challenge(properties, provider);
}
```

2. **Handle OAuth Callback** (called AFTER middleware processes authentication):
```csharp
[HttpGet("external-login/callback")]
public async Task<IActionResult> ExternalLoginCallback(string returnUrl = "", string frontendUrl = "")
{
    // Get the external login info from the OAuth middleware
    var info = await _signInManager.GetExternalLoginInfoAsync();
    if (info == null)
    {
        return Redirect($"{targetFrontendUrl}{returnUrl}?error=external_login_failed");
    }
    
    // Process user login/registration...
}
```

## Configuration Steps

### Development Setup (ngrok)

1. **Create ngrok configuration** (`ngrok.yml`):
   ```yaml
   version: "2"
   authtoken: YOUR_AUTH_TOKEN
   tunnels:
     frontend:
       addr: 4200
       proto: http
       domain: awlocaldev.ngrok.app
       inspect: false
   ```

2. **Configure Angular proxy** (`proxy.conf.json`):
   ```json
   {
     "/api": {
       "target": "http://localhost:5035",
       "secure": false,
       "changeOrigin": true,
       "logLevel": "debug"
     }
   }
   ```

3. **Update API configuration** (`appsettings.Development.json`):
   ```json
   {
     "AppBaseUrl": "https://awlocaldev.ngrok.app",
     "JWT": {
       "ValidAudience": "https://awlocaldev.ngrok.app",
       "ValidIssuer": "https://awlocaldev.ngrok.app"
     }
   }
   ```

4. **Update Google OAuth Console**:
   - Authorized redirect URI: `https://awlocaldev.ngrok.app/signin-google`

5. **Start services**:
   ```bash
   # Terminal 1: Start API
   cd AI.ProfilePhotoMaker.API
   dotnet run
   
   # Terminal 2: Start Angular with ngrok config
   cd AI.ProfilePhotoMaker.UI
   npm run start:ngrok
   
   # Terminal 3: Start ngrok
   ngrok start --config ngrok.yml frontend
   ```

### Production Setup

1. **Update configuration** (`appsettings.Production.json`):
   ```json
   {
     "AppBaseUrl": "https://yourdomain.com"
   }
   ```

2. **Update Google OAuth Console**:
   - Authorized redirect URI: `https://yourdomain.com/signin-google`

3. **Deploy normally** - no code changes required!

## Common Issues and Solutions

### Issue: "The oauth state was missing or invalid"
**Causes**:
- Callback path conflicts with controller routes
- Cookie domain restrictions
- Cross-origin cookie issues

**Solution**: 
- Use standard `/signin-google` callback path
- Don't specify cookie domain (let browser handle same-origin)
- Ensure proxy is configured correctly

### Issue: "redirect_uri_mismatch"
**Solution**: 
- Google Console must have exact redirect URI: `https://yourdomain.com/signin-google`
- Note: It's `/signin-google`, not `/api/auth/external-login/callback`

### Issue: OAuth succeeds but user not logged in
**Solution**:
- Verify JWT token is being generated in ExternalLoginCallback
- Check Angular is processing token from URL parameters
- Ensure localStorage is accessible

### Issue: CORS errors during OAuth flow
**Solution**:
- Use proxy configuration to serve everything from same domain
- Don't make cross-origin API calls during OAuth

## Testing OAuth Flow

### Successful Flow Logs
```
info: Microsoft.AspNetCore.Authentication.Google.GoogleHandler[4]
      Google was successfully authenticated.
info: Microsoft.AspNetCore.Authentication.Google.GoogleHandler[10]
      AuthenticationScheme: Google signed in.
Generated JWT token for user: user@example.com
Redirecting to: https://awlocaldev.ngrok.app/dashboard?token=eyJhbG...
```

### Debug Checklist
- [ ] ngrok running with correct domain
- [ ] Angular proxy configured for /api routes
- [ ] API AppBaseUrl matches ngrok domain
- [ ] Google Console has correct redirect URI
- [ ] Browser developer tools show cookies being set
- [ ] JWT token appears in redirect URL

## Security Best Practices

### Development
- Use HTTPS even in development (ngrok provides this)
- Short-lived JWT tokens
- Secure cookie settings

### Production
- HTTPS required
- HttpOnly cookies for tokens (future enhancement)
- Implement refresh tokens
- Rate limiting on auth endpoints
- CSRF protection

## Key Differences from Previous Approach

### Old Approach (Manual OAuth Handling)
- Complex manual OAuth code exchange
- Custom correlation cookie handling
- Bypassed ASP.NET Core OAuth middleware
- Required PKCE to be disabled
- Fragile and environment-specific

### New Approach (Standard ASP.NET Core)
- Uses built-in OAuth middleware
- Automatic state validation
- Works identically in dev/prod
- PKCE enabled by default
- Robust and maintainable

## Quick Reference

### OAuth Endpoints
- Initiate: `GET /api/auth/external-login/google`
- Callback (middleware): `/signin-google`
- Callback (controller): `GET /api/auth/external-login/callback`

### Configuration Files
- `ngrok.yml` - ngrok tunnel configuration
- `proxy.conf.json` - Angular proxy for API calls
- `appsettings.Development.json` - API configuration
- `angular.json` - Angular dev server settings

### Required Google Console Settings
- Authorized JavaScript origins: `https://awlocaldev.ngrok.app`
- Authorized redirect URIs: `https://awlocaldev.ngrok.app/signin-google`

---

*Updated to reflect standard ASP.NET Core OAuth implementation that works seamlessly across environments.*