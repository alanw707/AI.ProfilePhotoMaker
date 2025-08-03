#!/bin/bash

# Quick fix script for staging database schema issues
# Runs EF Core migrations on the current staging deployment

set -e

RESOURCE_GROUP="rg-aiprofilemaker-staging"
BACKEND_APP="aiprofilemaker-api-staging"

echo "🔄 Fixing staging database schema..."
echo "📍 Resource Group: $RESOURCE_GROUP"
echo "📍 Backend App: $BACKEND_APP"
echo ""

# Check if Azure CLI is authenticated
if ! az account show > /dev/null 2>&1; then
    echo "❌ Azure CLI not authenticated. Please run 'az login' first."
    exit 1
fi

echo "✅ Azure CLI authenticated"

# Check if the container app exists
if ! az containerapp show --name "$BACKEND_APP" --resource-group "$RESOURCE_GROUP" > /dev/null 2>&1; then
    echo "❌ Container app '$BACKEND_APP' not found in resource group '$RESOURCE_GROUP'"
    exit 1
fi

echo "✅ Found container app: $BACKEND_APP"

# Run EF Core migrations
echo "🏃 Running EF Core database update..."
echo "⏳ This may take a few minutes..."

if az containerapp exec \
    --name "$BACKEND_APP" \
    --resource-group "$RESOURCE_GROUP" \
    --command "dotnet ef database update --no-build --verbose"; then
    
    echo "✅ EF Core migrations completed successfully!"
    
    # Test the API endpoint
    BACKEND_URL=$(az containerapp show --name "$BACKEND_APP" --resource-group "$RESOURCE_GROUP" --query 'properties.configuration.ingress.fqdn' -o tsv)
    
    if [ -n "$BACKEND_URL" ]; then
        echo "🧪 Testing credit packages endpoint..."
        
        if curl -s -f -H "Accept: application/json" "https://$BACKEND_URL/api/credit/packages" > /dev/null; then
            echo "✅ Credit packages API is now working!"
            
            # Show the actual response
            echo "📊 API Response:"
            curl -s -H "Accept: application/json" "https://$BACKEND_URL/api/credit/packages" | jq '.' || curl -s -H "Accept: application/json" "https://$BACKEND_URL/api/credit/packages"
        else
            echo "⚠️ Credit packages API still not working - may need additional investigation"
        fi
    fi
    
else
    echo "❌ EF Core migrations failed"
    echo "💡 Troubleshooting tips:"
    echo "1. Check that the container app is running and healthy"
    echo "2. Verify database connection and permissions"
    echo "3. Check container logs: az containerapp logs show --name $BACKEND_APP --resource-group $RESOURCE_GROUP"
    exit 1
fi

echo ""
echo "🎉 Staging database fix completed!"
echo "💡 Frontend should now be able to load styles and credit packages correctly"