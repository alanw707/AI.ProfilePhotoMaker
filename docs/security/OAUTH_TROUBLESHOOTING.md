# OAuth Troubleshooting Guide

## Overview

AI.ProfilePhotoMaker currently uses a manual Google OAuth flow implemented in `AI.ProfilePhotoMaker.API/Controllers/AuthController.cs`.

Key idea: the API must generate a `redirect_uri` that exactly matches an authorized redirect URI in the Google OAuth Console.

## OAuth Flow

```
User → Angular → API (/api/auth/external-login/google?ageConfirmed=true) → Google OAuth → API (/api/auth/external-login-callback) → Angular (Photo Workspace)
```

## Key Configuration

- Frontend base URL: `AppBaseUrl`
- Backend base URL used to construct `redirect_uri`:
  - `Authentication:OAuth:BaseUrl` (recommended), or
  - `OAUTH_BASE_URL` (env override)
  - In development, `Jwt:ValidIssuer` is used as a fallback
- Google OAuth credentials:
  - `Authentication:Google:ClientId` / `Authentication:Google:ClientSecret`
  - or `GOOGLE_CLIENT_ID` / `GOOGLE_CLIENT_SECRET`

## Development Setup (localhost)

1. **Angular proxy**: ensure `/api` targets the API port:
   ```json
   {
     "/api": {
       "target": "http://localhost:5032",
       "secure": false,
       "changeOrigin": true
     }
   }
   ```

2. **API config** (`AI.ProfilePhotoMaker.API/appsettings.Development.json`):
   - `AppBaseUrl`: `http://localhost:4200`
   - `Authentication:OAuth:BaseUrl`: `http://localhost:5032` (or set `OAUTH_BASE_URL`)

3. **Google OAuth Console**:
   - Authorized redirect URI: `http://localhost:5032/api/auth/external-login-callback`

## Development Setup (ngrok)

If the API is reached via an ngrok domain (recommended for HTTPS):

1. Set `Authentication:OAuth:BaseUrl` (or `OAUTH_BASE_URL`) to your API ngrok origin, e.g. `https://your-api.ngrok.app`.
2. Add this authorized redirect URI in Google OAuth Console:
   - `https://your-api.ngrok.app/api/auth/external-login-callback`

## Production Setup

1. Confirm `Authentication:OAuth:BaseUrl` is set to the public API origin (e.g. `https://api.aiprofilephotomaker.com`).
2. Ensure Google OAuth Console has the matching authorized redirect URI:
   - `https://api.aiprofilephotomaker.com/api/auth/external-login-callback`

## Common Issues

### Issue: `redirect_uri_mismatch`

- The `redirect_uri` in the request must match an authorized redirect URI exactly (scheme, host, port, path).
- If you initiate OAuth via an Angular dev proxy, make sure the API is not deriving the redirect origin from the proxied host (e.g. `localhost:4200`):
  - Set `Authentication:OAuth:BaseUrl` (or `OAUTH_BASE_URL`) to `http://localhost:5032`
- Ensure `Authentication:OAuth:BaseUrl` / `OAUTH_BASE_URL` does not end with a trailing slash (it would generate a double-slash path and fail the match).
- Debug tip: call `GET /api/auth/google-oauth-url` and inspect the returned `redirectUri` + `backendBaseUrlSource` (development only) to see what Google expects to be authorized.

### Issue: OAuth callback returns `oauth_state missing/invalid`

- State is stored in session for CSRF protection.
- If cookies/session are blocked/misconfigured (common with cross-origin setups), state validation can fail.
- In development, the API may proceed without session state (reduced CSRF protection) if session initialization fails.

### Issue: OAuth succeeds but user isn’t logged in

- Verify the callback redirects to `{AppBaseUrl}{returnUrl}`.
- In development, verify the token is present in the redirect URL (e.g. `?token=...`) and Angular processes it.

## Quick Reference

- Initiate (returns auth URL): `GET /api/auth/google-oauth-url`
- Initiate (redirects to Google): `GET /api/auth/external-login/google?ageConfirmed=true`
- Callback (Google redirects here): `GET /api/auth/external-login-callback`
