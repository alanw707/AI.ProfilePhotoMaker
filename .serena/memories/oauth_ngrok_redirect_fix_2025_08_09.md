# OAuth Redirect Fix - ngrok Configuration Issue

## Date: 2025-08-09

## Issue Summary
OAuth redirect was failing with FileNotFoundException when trying to access `/app/dashboard` route after Google authentication.

## Root Cause
- ngrok tunnel (`awlocaldev.ngrok.app`) configured to proxy only API server (localhost:5032)
- Angular frontend running on localhost:4200 (not tunneled)
- OAuth callback redirecting to: `https://awlocaldev.ngrok.app/app/dashboard?token=...`
- This hits the API server which can't serve Angular routes, causing FileNotFoundException

## Solution Applied
Changed `AppBaseUrl` in `appsettings.Development.json` from:
```json
"AppBaseUrl": "https://awlocaldev.ngrok.app"
```
to:
```json
"AppBaseUrl": "http://localhost:4200"
```

## OAuth Flow After Fix
1. User clicks "Login with Google" → API redirects to Google OAuth
2. Google redirects to: `awlocaldev.ngrok.app/api/auth/external-login-callback`  (API via ngrok)
3. API processes OAuth, generates JWT, redirects to: `http://localhost:4200/app/dashboard?token=...`
4. ✅ Angular frontend receives token and handles authentication

## Technical Details
- **ngrok tunnel**: `awlocaldev.ngrok.app` → `localhost:5032` (API only)
- **Frontend**: `localhost:4200` (not tunneled, local only)
- **OAuth callback**: Now redirects to local frontend instead of through ngrok
- **API access**: Still works through ngrok for external Replicate webhooks

## Verification
- API server restarted successfully with new configuration
- Health check passed: `curl http://localhost:5032/api/health`
- OAuth now redirects to localhost frontend which can handle the token

## Alternative Solutions Considered
1. ✅ **Chosen**: Redirect OAuth to localhost frontend (simple, works for development)
2. **Dual ngrok tunnels**: One for API, one for frontend (more complex)
3. **API serves Angular**: Configure .NET to serve static files (complex, mixing concerns)

## Files Changed
- `AI.ProfilePhotoMaker.API/appsettings.Development.json`: Updated AppBaseUrl to localhost:4200