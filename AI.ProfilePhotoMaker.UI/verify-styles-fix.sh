#!/bin/bash

API_URL="https://aiprofilemaker-api-staging.thankfulriver-68674ea3.eastus2.azurecontainerapps.io/api/style"

echo "🔍 VERIFYING STYLES API STATUS"
echo "=============================="
echo

echo "📡 Testing API endpoint: $API_URL"
echo

# Get API response
response=$(curl -s "$API_URL")

# Check if response is valid JSON
if echo "$response" | python3 -m json.tool > /dev/null 2>&1; then
    echo "✅ API Response: Valid JSON"
else
    echo "❌ API Response: Invalid JSON"
    echo "Raw response: $response"
    exit 1
fi

# Count styles in response
style_count=$(echo "$response" | python3 -c "
import sys, json
try:
    data = json.load(sys.stdin)
    if 'data' in data and isinstance(data['data'], list):
        print(len(data['data']))
    else:
        print('0')
except:
    print('0')
")

echo "📊 Current Style Count: $style_count"
echo

if [ "$style_count" -ge 20 ]; then
    echo "🎉 SUCCESS: API returns $style_count styles (20+ required)"
    echo "✅ Database has been properly populated"
    echo
    echo "📋 Available Styles:"
    echo "$response" | python3 -c "
import sys, json
try:
    data = json.load(sys.stdin)
    for i, style in enumerate(data['data'], 1):
        print(f'  {i:2d}. {style[\"name\"]} - {style[\"description\"]}')
except:
    print('  Error parsing styles')
"
elif [ "$style_count" -eq 3 ]; then
    echo "⚠️  INCOMPLETE: API returns only $style_count styles"
    echo "❌ Database still needs to be populated"
    echo
    echo "📋 Current Styles:"
    echo "$response" | python3 -c "
import sys, json
try:
    data = json.load(sys.stdin)
    for style in data['data']:
        print(f'  - {style[\"name\"]} - {style[\"description\"]}')
except:
    print('  Error parsing styles')
"
    echo
    echo "🔧 ACTION REQUIRED:"
    echo "  1. Run the SQL from populate-styles.sql or add-missing-styles.sh"
    echo "  2. Execute against your Azure database"
    echo "  3. Re-run this verification script"
else
    echo "❓ UNEXPECTED: API returns $style_count styles"
    echo "   Expected: 3 (before fix) or 20+ (after fix)"
fi

echo
echo "🌐 Frontend Test:"
echo "  Open your application and check if styles load from API"
echo "  (no fallback message should appear)"