# Ngrok Webhook Setup for Development

This document was relocated from `AI.ProfilePhotoMaker.API/NGROK_WEBHOOK_SETUP.md` to centralize webhook documentation.

## Quick Setup

### Option 1: Automatic ngrok Detection (Recommended)
1. Start your API: `dotnet run`
2. Start ngrok in another terminal: `ngrok http 5000`
3. The API will automatically detect and use the ngrok tunnel URL for webhooks

### Option 2: Manual ngrok Configuration
1. Start ngrok: `ngrok http 5000`
2. Copy the HTTPS URL (e.g., `https://abc123.ngrok.io`)
3. Update `appsettings.Development.json`:
   ```json
   {
     "Webhooks": {
       "NgrokTunnelUrl": "https://abc123.ngrok.io"
     }
   }
   ```
4. Restart your API: `dotnet run`

## Configuration Options

### appsettings.Development.json
```json
{
  "Webhooks": {
    "NgrokTunnelUrl": "",           
    "BaseUrl": "",                   
    "NgrokApiUrl": "http://localhost:4040/api/tunnels"  
  }
}
```

## How It Works

### Development Environment
- Automatic Detection via ngrok API (`http://localhost:4040/api/tunnels`)
- Manual Override with `Webhooks:NgrokTunnelUrl`
- Fallback to `Webhooks:BaseUrl` if ngrok unavailable
- HTTPS required for webhooks

### Production/Azure Environment
- Uses `AppBaseUrl` configuration
- No ngrok logic runs in production
- HTTPS required

## Startup Validation

Examples of expected startup logs for development and production environments are unchanged from the original guide and apply here as well.

## Testing Webhooks
1. Start ngrok: `ngrok http 5000`
2. Start API: `dotnet run`
3. Test photo enhancement via `/api/replicate/enhance`
4. Inspect `http://localhost:4040` for webhook requests
5. Monitor API logs

## Troubleshooting
- "Webhook URLs are disabled": Start ngrok or set `Webhooks:NgrokTunnelUrl`
- "ngrok API not accessible": Ensure ngrok is running or update `Webhooks:NgrokApiUrl`
- "Webhook URL validation failed": Verify active tunnel
- Azure: Ensure `AppBaseUrl` is HTTPS

## Tips
- Restart API after changing ngrok URL
- Each developer needs their own tunnel
- Use `ngrok config add-authtoken <token>` for reserved subdomains
- Production safety: ngrok code never runs in production

## Environment Detection Logic (Summary)

```csharp
// Development resolution order
1) Webhooks:NgrokTunnelUrl
2) Webhooks:BaseUrl
3) Auto-detect via ngrok API
4) AppBaseUrl (if HTTPS)
5) Disable webhooks if no HTTPS found

// Production
Use AppBaseUrl (must be HTTPS)
```

