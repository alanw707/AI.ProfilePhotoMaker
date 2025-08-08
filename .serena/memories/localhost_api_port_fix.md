# Localhost API Port Configuration Fix

## Problem
After OAuth login, dashboard API calls were failing with 404 errors because they were going to `localhost:4200/api/*` instead of the backend port `localhost:5035/api/*`.

## Solution
Updated `AI.ProfilePhotoMaker.UI/src/environments/environment.ts` to use explicit backend URL:

```typescript
apiUrl: 'http://localhost:5035/api',  // Changed from '/api'
```

## Key Files
- **Frontend Config**: `AI.ProfilePhotoMaker.UI/src/environments/environment.ts`
- **Config Service**: `AI.ProfilePhotoMaker.UI/src/app/services/config.service.ts` (already handles full URLs)
- **Backend CORS**: `AI.ProfilePhotoMaker.API/Program.cs` (AllowDevelopment policy configured)

## Required Actions After Fix
1. Restart Angular dev server: `npm run dev:local`
2. Ensure backend is running: `dotnet run`
3. Clear browser cache (Hard reload)

## Verification
- API calls should go to `http://localhost:5035/api/*`
- No CORS errors
- Dashboard loads real data (credits, photos, etc.)

## Alternative Approaches
- Use proxy configuration with `ng serve --proxy-config proxy.conf.json`
- Keep relative `/api` path and ensure proxy is working

This fix enables direct API communication without proxy overhead, improving development performance and debugging clarity.