# OAuth Routing Fixes - Landing Page Integration

## Issue Summary

After implementing the landing page and updating the routing structure from `/dashboard` to `/app/dashboard`, the OAuth login flow broke. Users were being redirected to the old route structure, causing tokens to be lost during navigation.

## Root Cause Analysis

1. **Route Structure Change**: The application routing was updated to include a landing page, moving protected routes from `/dashboard` to `/app/dashboard`
2. **OAuth Callback Mismatch**: The AuthController was still redirecting to the old `/dashboard` route after successful OAuth authentication
3. **Query Parameter Loss**: Route redirects weren't preserving OAuth callback parameters (tokens, state, etc.)

## Files Modified

### 1. AuthController.cs (`/AI.ProfilePhotoMaker.API/Controllers/AuthController.cs`)

**Changes Made:**
- **Line 77**: Updated default `returnUrl` parameter from `/dashboard` to `/app/dashboard`
- **Line 100**: Updated default `returnUrl` parameter from `/dashboard` to `/app/dashboard` 
- **Line 141**: Updated session fallback from `/dashboard` to `/app/dashboard`

**Before:**
```csharp
public IActionResult GetGoogleOAuthUrl(string returnUrl = "/dashboard")
public IActionResult ExternalLogin(string provider, string returnUrl = "/dashboard")
var returnUrl = HttpContext.Session.GetString("oauth_return_url") ?? "/dashboard";
```

**After:**
```csharp
public IActionResult GetGoogleOAuthUrl(string returnUrl = "/app/dashboard")
public IActionResult ExternalLogin(string provider, string returnUrl = "/app/dashboard")
var returnUrl = HttpContext.Session.GetString("oauth_return_url") ?? "/app/dashboard";
```

### 2. app.routes.ts (`/AI.ProfilePhotoMaker.UI/src/app/app.routes.ts`)

**Changes Made:**
- **Lines 8-19**: Enhanced legacy redirect handler to preserve OAuth query parameters during route migration

**Before:**
```typescript
{ 
  path: 'dashboard', 
  canActivate: [(): boolean => {
    inject(Router).navigate(['/app/dashboard']);
    return false;
  }]
}
```

**After:**
```typescript
{
  path: 'dashboard',
  canActivate: [(): Promise<boolean> => {
    const router = inject(Router);
    const queryParams = new URLSearchParams(window.location.search);
    const params: Record<string, string> = {};
    queryParams.forEach((value, key) => {
      params[key] = value;
    });
    return router.navigate(['/app/dashboard'], { queryParams: params });
  }]
}
```

### 3. login.component.ts (`/AI.ProfilePhotoMaker.UI/src/app/auth/login/login.component.ts`)

**Changes Made:**
- **Line 37**: Updated default returnUrl from '/dashboard' to '/app/dashboard'

**Before:**
```typescript
this.returnUrl = this.route.snapshot.queryParams['returnUrl'] || '/dashboard';
```

**After:**
```typescript
this.returnUrl = this.route.snapshot.queryParams['returnUrl'] || '/app/dashboard';
```

## OAuth Flow Architecture

### Current Implementation
The application uses a manual OAuth implementation that bypasses ASP.NET Core's built-in OAuth middleware:

1. **Frontend Initiation**: `/api/auth/external-login/google` 
2. **Google OAuth**: User authenticates with Google
3. **Callback Handling**: `https://awlocaldev.ngrok.app/api/auth/external-login-callback`
4. **Token Generation**: Backend creates JWT and redirects to frontend
5. **Frontend Processing**: Login component processes token and navigates to dashboard

### Required Google Console Configuration
- **Authorized Redirect URI**: `https://awlocaldev.ngrok.app/api/auth/external-login-callback`
- **Authorized JavaScript Origins**: `https://awlocaldev.ngrok.app`

## Build Issues Resolved

During implementation, TypeScript/ESLint violations were encountered and fixed:

### Import Ordering Fix
```typescript
// Before
import { Routes, Router } from '@angular/router';

// After  
import { Router, Routes } from '@angular/router';
```

### Type Safety Improvements
```typescript
// Before
canActivate: [(): any => {
  const params: any = {};

// After
canActivate: [(): Promise<boolean> => {
  const params: Record<string, string> = {};
```

### Unused Import Cleanup
- Removed unused `authGuard` import
- Added explicit return type annotations

## Testing Results

✅ **OAuth Endpoint**: Returns 302 redirect to Google OAuth with correct parameters  
✅ **Redirect URI**: Uses `https://awlocaldev.ngrok.app/api/auth/external-login-callback`  
✅ **State Parameter**: Includes CSRF protection via secure state parameter  
✅ **Scope**: Properly requests `openid profile email` permissions  
✅ **Route Preservation**: Query parameters maintained during route redirects  

## Impact Assessment

### Before Fix
- OAuth login resulted in 404 errors or lost authentication state
- Users redirected to non-existent `/dashboard` route  
- Token parameters lost during navigation
- Broken user authentication flow

### After Fix  
- Seamless OAuth authentication flow
- Proper redirection to `/app/dashboard` after login
- Token parameters preserved throughout the process
- Consistent routing structure across the application

## Server Configuration

### Development Setup
- **Frontend**: `http://localhost:4200` (Angular dev server)
- **Backend**: `http://localhost:5035` (.NET API server)  
- **Tunneling**: ngrok provides HTTPS endpoints for OAuth compliance

### Required Environment
- ngrok tunnel configured for `awlocaldev.ngrok.app` domain
- Google OAuth 2.0 credentials configured in `appsettings.Development.json`
- Angular proxy configuration for API calls

## Future Considerations

1. **OAuth Middleware Migration**: Consider migrating to ASP.NET Core's built-in OAuth middleware for better maintainability
2. **Token Security**: Implement HttpOnly cookies instead of URL-based token passing
3. **Error Handling**: Enhance OAuth error handling and user feedback
4. **SASS Deprecations**: Address SASS deprecation warnings in component stylesheets

## Related Documentation

- `/docs/OAUTH_TROUBLESHOOTING.md` - Comprehensive OAuth setup guide
- Package.json scripts for development server management
- Angular routing configuration and guard implementations