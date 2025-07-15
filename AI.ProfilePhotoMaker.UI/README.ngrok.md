# ngrok Setup Guide

## Configuration

This project is configured to use ngrok with a custom domain for simplified OAuth authentication.

### Prerequisites

1. ngrok account with custom domain: `awlocaldev.ngrok.app`
2. ngrok CLI installed
3. Backend API running on `http://localhost:5035`
4. Frontend running on `http://localhost:4200`

### Setup Steps

1. **Update ngrok configuration**
   - Edit `/ngrok.yml` and add your authtoken:
   ```yaml
   authtoken: YOUR_ACTUAL_NGROK_AUTH_TOKEN
   ```

2. **Start the backend API**
   ```bash
   cd AI.ProfilePhotoMaker.API
   dotnet run
   ```

3. **Start the frontend with ngrok configuration**
   ```bash
   cd AI.ProfilePhotoMaker.UI
   npm run start:ngrok
   ```

4. **Start ngrok tunnel**
   ```bash
   # From project root directory
   ngrok start --config ngrok.yml frontend
   
   # Or use the npm script from UI directory
   npm run ngrok
   ```

### How it Works

- Frontend runs on `http://localhost:4200`
- ngrok tunnels frontend to `https://awlocaldev.ngrok.app`
- Vite proxy configuration routes all `/api/*` requests to backend at `http://localhost:5035`
- OAuth redirects use the single ngrok domain, eliminating cross-domain issues

### Google OAuth Configuration

Update your Google Cloud Console OAuth 2.0 Client to include:

**Authorized JavaScript origins:**
- `https://awlocaldev.ngrok.app`
- `http://localhost:4200` (for local development)

**Authorized redirect URIs:**
- `https://awlocaldev.ngrok.app/api/auth/external-login/callback`
- `http://localhost:4200/api/auth/external-login/callback`

### Benefits

1. **Single Domain**: Everything runs through one ngrok domain
2. **No CORS Issues**: API calls are proxied through the same domain
3. **Simplified OAuth**: No complex cross-domain redirect handling
4. **Easy Testing**: Share your ngrok URL with testers

### Troubleshooting

1. **ngrok agent limit error**: Make sure you're only running one ngrok instance
2. **API calls failing**: Ensure backend is running on port 5035
3. **OAuth redirect issues**: Check Google Console configuration includes ngrok domain