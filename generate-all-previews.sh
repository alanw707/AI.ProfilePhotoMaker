#!/bin/bash

# Script to generate all style preview images
# Usage: ./generate-all-previews.sh <API_URL> <AUTH_TOKEN>

API_URL=${1:-"https://localhost:5001"}
AUTH_TOKEN=$2

if [ -z "$AUTH_TOKEN" ]; then
    echo "Usage: $0 <API_URL> <AUTH_TOKEN>"
    echo "Example: $0 https://localhost:5001 'Bearer eyJ...'"
    exit 1
fi

echo "Starting style preview generation..."
echo "API URL: $API_URL"

# Call the generate-all endpoint
response=$(curl -X POST "$API_URL/api/style-preview/generate-all" \
    -H "Authorization: $AUTH_TOKEN" \
    -H "Content-Type: application/json" \
    -k 2>/dev/null)

echo "Response: $response"

# Extract prediction IDs from response (if needed for polling)
# You can parse the JSON response here to get prediction IDs

echo ""
echo "Generation started. You can check the status of individual predictions using:"
echo "curl -X GET '$API_URL/api/style-preview/check-status/{predictionId}' -H 'Authorization: $AUTH_TOKEN' -k"
echo ""
echo "Or list all available previews:"
echo "curl -X GET '$API_URL/api/style-preview/list' -k"