#!/bin/bash

# Database Repair Script for AI Profile Photo Maker
# Usage: ./repair_database.sh [AUTH_TOKEN]

API_BASE="http://localhost:5035/api"
AUTH_TOKEN="$1"

if [ -z "$AUTH_TOKEN" ]; then
    echo "❌ Error: Authentication token required"
    echo "Usage: $0 <AUTH_TOKEN>"
    echo ""
    echo "To get your auth token:"
    echo "1. Log into the app in your browser"
    echo "2. Open Developer Tools (F12)"
    echo "3. Go to Application/Storage tab"
    echo "4. Find your JWT token in localStorage or sessionStorage"
    echo "5. Run: $0 'Bearer your_jwt_token_here'"
    exit 1
fi

echo "🔧 Starting database repair process..."
echo "🌐 API Base: $API_BASE"
echo ""

# Function to make authenticated API calls
make_request() {
    local endpoint="$1"
    local description="$2"
    
    echo "📡 $description..."
    response=$(curl -s -X POST "$API_BASE$endpoint" \
        -H "Content-Type: application/json" \
        -H "Authorization: $AUTH_TOKEN" \
        -w "%{http_code}")
    
    http_code="${response: -3}"
    body="${response%???}"
    
    if [ "$http_code" = "200" ]; then
        echo "✅ $description completed successfully"
        echo "📄 Response: $body" | jq '.' 2>/dev/null || echo "$body"
    else
        echo "❌ $description failed (HTTP $http_code)"
        echo "📄 Response: $body"
    fi
    echo ""
}

# Step 1: Repair Style Corruption
make_request "/image/debug/repair-style-corruption" "Repairing style corruption"

# Step 2: Cleanup Orphaned Records  
make_request "/image/debug/cleanup-orphaned-records" "Cleaning up orphaned records"

# Step 3: Sync Generated Images (optional)
echo "🤔 Do you want to sync generated images from filesystem? (y/n)"
read -r sync_generated
if [ "$sync_generated" = "y" ] || [ "$sync_generated" = "Y" ]; then
    make_request "/test/fix-generated-images" "Syncing generated images"
fi

# Step 4: Sync Uploaded Images (optional)
echo "🤔 Do you want to sync uploaded images from filesystem? (y/n)" 
read -r sync_uploaded
if [ "$sync_uploaded" = "y" ] || [ "$sync_uploaded" = "Y" ]; then
    make_request "/test/fix-uploaded-selfies" "Syncing uploaded images"
fi

echo "🎉 Database repair process completed!"
echo ""
echo "Next steps:"
echo "1. Check your app to verify 404 errors are resolved"
echo "2. Verify image counts are correct"
echo "3. Test image deletion functionality"