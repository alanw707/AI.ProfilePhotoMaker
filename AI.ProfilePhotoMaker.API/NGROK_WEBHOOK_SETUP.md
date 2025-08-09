# 🔗 Ngrok Webhook Setup for Development

This guide explains how to set up ngrok webhooks for development while ensuring Azure deployment works seamlessly.

## 🎯 Quick Setup

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

## 🔧 Configuration Options

### appsettings.Development.json
```json
{
  "Webhooks": {
    "NgrokTunnelUrl": "",           // Manual override for ngrok URL
    "BaseUrl": "",                   // Alternative webhook base URL  
    "NgrokApiUrl": "http://localhost:4040/api/tunnels"  // ngrok API endpoint
  }
}
```

## 📝 How It Works

### Development Environment
- ✅ **Automatic Detection**: API calls ngrok API (`http://localhost:4040/api/tunnels`) to find HTTPS tunnel
- ✅ **Manual Override**: Set `Webhooks:NgrokTunnelUrl` for custom ngrok URL
- ✅ **Fallback**: Uses `Webhooks:BaseUrl` if ngrok unavailable
- ⚠️ **HTTP Disabled**: Only HTTPS webhooks work (Replicate API requirement)

### Production/Azure Environment  
- ✅ **Automatic**: Uses `AppBaseUrl` configuration (Azure sets this automatically)
- ✅ **No ngrok Logic**: Development-specific code never runs in production
- ✅ **HTTPS Required**: Production URLs must be HTTPS

## 🚀 Startup Validation

The API validates webhook configuration on startup and logs helpful messages:

### ✅ Success (Development with ngrok)
```
🔗 Validating webhook URL configuration for Development environment...
✅ Webhook base URL resolved: https://abc123.ngrok.io
📨 Sample webhook URL: https://abc123.ngrok.io/api/webhooks/replicate/prediction-complete
✅ Webhook URL validation passed - endpoints are reachable
```

### ⚠️ Warning (Development without ngrok)
```
🔗 Validating webhook URL configuration for Development environment...
⚠️ Webhook URLs are disabled in development. Consider setting up ngrok for webhook testing.
💡 To enable webhooks in development:
   1. Start ngrok: ngrok http 5000
   2. Set Webhooks:NgrokTunnelUrl in appsettings.Development.json
   3. Or set Webhooks:BaseUrl to your preferred HTTPS endpoint
```

### 🚀 Success (Production)
```
🔗 Validating webhook URL configuration for Production environment...
✅ Webhook base URL resolved: https://your-app.azurewebsites.net
🚀 Production webhook configuration active
```

## 🧪 Testing Webhooks

1. **Start ngrok**: `ngrok http 5000`
2. **Start API**: `dotnet run` (should show successful webhook validation)
3. **Test photo enhancement**: Use the `/api/replicate/enhance` endpoint
4. **Check ngrok web interface**: Go to `http://localhost:4040` to see webhook requests
5. **Monitor API logs**: Watch for webhook received messages

## 🔍 Troubleshooting

### "Webhook URLs are disabled"
- **Cause**: No HTTPS endpoint available
- **Fix**: Start ngrok or configure `Webhooks:NgrokTunnelUrl`

### "ngrok API not accessible"  
- **Cause**: ngrok not running or different port
- **Fix**: Start ngrok or update `Webhooks:NgrokApiUrl`

### "Webhook URL validation failed"
- **Cause**: ngrok tunnel not reachable
- **Fix**: Check ngrok is running and tunnel is active

### Azure Deployment Issues
- **Cause**: `AppBaseUrl` not set to HTTPS
- **Fix**: Ensure Azure App Service uses HTTPS URLs

## 💡 Tips

- **Restart API**: After changing ngrok URL, restart the API to clear cache
- **Multiple Developers**: Each developer needs their own ngrok tunnel
- **ngrok Auth**: For persistent subdomains, use `ngrok config add-authtoken <token>`
- **Production Safety**: ngrok code never runs in production (environment check)

## 🛠️ Environment Detection Logic

```csharp
// Development: Try multiple approaches
if (IsDevelopment()) {
    1. Check Webhooks:NgrokTunnelUrl config
    2. Check Webhooks:BaseUrl config  
    3. Auto-detect via ngrok API
    4. Fall back to AppBaseUrl (if HTTPS)
    5. Disable webhooks if no HTTPS found
}

// Production: Simple and reliable
else {
    Use AppBaseUrl (must be HTTPS)
}
```

This ensures webhooks work seamlessly across all environments! 🎉