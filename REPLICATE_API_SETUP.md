# 🔑 Replicate API Setup Instructions

## Problem Identified
The webhook test is failing because the Replicate API token is set to `"test-token"` instead of a real API token.

## Solution Steps

### 1. Get Your Replicate API Token
1. Go to [https://replicate.com/account/api-tokens](https://replicate.com/account/api-tokens)
2. Sign in or create an account
3. Create a new API token
4. Copy the token (starts with `r8_...`)

### 2. Set the API Token (Choose ONE method)

#### Method A: Environment Variable (Recommended)
```bash
export REPLICATE_API_TOKEN="r8_your_actual_token_here"
```

#### Method B: Update appsettings.Development.json
Replace `"test-token"` with your real token:
```json
{
  "Replicate": {
    "ApiToken": "r8_your_actual_token_here"
  }
}
```

### 3. Restart the API
```bash
# Kill current API process
pkill -f "AI.ProfilePhotoMaker.API"

# Restart with new token
cd /home/alanw/projects/AI.ProfilePhotoMaker/AI.ProfilePhotoMaker.API
dotnet run
```

### 4. Test Again
The enhancement endpoint should now work properly with webhooks!

## What This Fixes
- ✅ HTTP Connection Pool errors
- ✅ Authentication failures  
- ✅ Webhook functionality
- ✅ Photo enhancement requests

## Security Note
- Never commit real API tokens to git
- Use environment variables in production
- Keep tokens secure and rotate them regularly