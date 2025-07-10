# Localtunnel Setup Guide

This guide explains how to make your Angular application accessible externally during development using localtunnel, while maintaining proper OAuth redirects.

## Problem Solved

The previous issue was that after OAuth login (Google), the application would redirect to `http://localhost:4200` instead of the external localtunnel URL. This implementation dynamically detects the access method and uses appropriate URLs.

## Quick Setup

### 1. Start Your Backend Server
```bash
# Start your backend server (usually on port 5035)
cd /path/to/your/backend
dotnet run
```

### 2. Create Backend Tunnel
```bash
# Create a localtunnel for your backend
npx localtunnel --port 5035
```
This will give you a URL like: `https://backend-abc123.loca.lt`

### 3. Configure Frontend
```bash
# Option A: Use the setup script
node scripts/setup-localtunnel.js https://backend-abc123.loca.lt

# Option B: Manual configuration (see below)
```

### 4. Start Frontend with Localtunnel Config
```bash
npm run start:localtunnel
```

### 5. Create Frontend Tunnel
```bash
# In another terminal
npx localtunnel --port 4200
```
This will give you a URL like: `https://frontend-xyz789.loca.lt`

### 6. Access Your App
Open the frontend tunnel URL in your browser. OAuth redirects will now work properly!

## Manual Configuration

If you prefer to configure manually, you can set the backend URL at runtime:

1. Open your app in the browser
2. Open browser console (F12)
3. Run: `localStorage.setItem('BACKEND_URL', 'https://your-backend-tunnel.loca.lt')`
4. Refresh the page

## How It Works

### Dynamic URL Detection
The `ConfigService` now automatically detects:
- Whether you're accessing via localhost or external URL
- Dynamically configures API endpoints based on access method
- Uses appropriate URLs for OAuth redirects

### Environment Configurations
Three environment files are now available:
- `environment.ts` - Default development (localhost)
- `environment.localtunnel.ts` - Localtunnel configuration
- `environment.prod.ts` - Production configuration

### OAuth Redirect Logic
The login and register components now:
- Use `configService.getOAuthRedirectUrl()` for backend OAuth calls
- Include full frontend URL in returnUrl parameter
- Automatically handle both localhost and external access

## Available npm Scripts

```bash
# Regular development
npm start

# Localtunnel development
npm run start:localtunnel

# Build for localtunnel
npm run build:localtunnel

# Build for production
npm run build
```

## Troubleshooting

### OAuth Still Redirecting to Localhost
1. Check browser console for configuration logs
2. Verify `BACKEND_URL` is set correctly in localStorage
3. Ensure you're using the localtunnel frontend URL (not localhost)

### Backend API Calls Failing
1. Check that the backend tunnel URL is correct
2. Verify CORS settings on your backend allow the frontend tunnel domain
3. Check browser network tab for actual API endpoints being called

### Setup Script Not Working
You can manually edit `src/environments/environment.localtunnel.ts`:
```typescript
externalApiUrl: 'https://your-backend-tunnel.loca.lt/api',
externalAppUrl: 'https://your-backend-tunnel.loca.lt',
```

## Production Deployment

For production, update `environment.prod.ts` with your actual production URLs:
```typescript
apiBaseUrl: 'https://your-production-api.com/api',
appBaseUrl: 'https://your-production-api.com',
frontendBaseUrl: 'https://your-production-frontend.com',
```

## Benefits

✅ **Automatic URL Detection**: No hardcoded URLs, works with any tunnel service
✅ **OAuth Compatibility**: Proper redirects for external access
✅ **Flexible Configuration**: Easy to switch between local and external access
✅ **Production Ready**: Smooth transition to production deployment
✅ **Debug Friendly**: Console logs help troubleshoot configuration issues

## Alternative Solutions

If localtunnel doesn't work well for you, consider:
- **ngrok**: `ngrok http 4200` (more stable, has free tier)
- **cloudflared**: Cloudflare's tunnel service
- **serveo**: `ssh -R 80:localhost:4200 serveo.net`

The configuration will work with any tunnel service that provides HTTPS URLs.