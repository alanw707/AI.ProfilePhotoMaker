#!/bin/bash

# Production Configuration Validation Script
# Run this before deploying to production

echo "🔍 Validating production configuration..."

# Required environment variables for production
REQUIRED_VARS=(
    "REPLICATE_API_TOKEN"
    "REPLICATE_WEBHOOK_SECRET" 
    "AZURE_STORAGE_CONNECTION_STRING"
    "AZURE_STORAGE_CONTAINER_NAME"
    "JWT_SECRET"
    "GOOGLE_CLIENT_ID"
    "GOOGLE_CLIENT_SECRET"
)

# Check each required variable
MISSING_VARS=()
for var in "${REQUIRED_VARS[@]}"; do
    if [ -z "${!var}" ]; then
        MISSING_VARS+=("$var")
    else
        echo "✅ $var is set"
    fi
done

# Test Replicate API token if set
if [ -n "$REPLICATE_API_TOKEN" ]; then
    echo "🧪 Testing Replicate API token..."
    if curl -s -H "Authorization: Token $REPLICATE_API_TOKEN" https://api.replicate.com/v1/models | grep -q '"results"'; then
        echo "✅ Replicate API token is valid"
    else
        echo "❌ Replicate API token is invalid"
        exit 1
    fi
fi

# Report results
if [ ${#MISSING_VARS[@]} -eq 0 ]; then
    echo "🎉 All required environment variables are configured!"
    echo "✅ Ready for production deployment"
else
    echo "❌ Missing required environment variables:"
    printf '   - %s\n' "${MISSING_VARS[@]}"
    echo ""
    echo "Please set these variables before deploying to production."
    exit 1
fi