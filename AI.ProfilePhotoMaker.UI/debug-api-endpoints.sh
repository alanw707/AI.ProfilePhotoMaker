#!/bin/bash

echo "🔍 API ENDPOINT DIAGNOSTICS"
echo "=========================="
echo

# Test all known API endpoints
endpoints=(
  "https://aiprofilemaker-api-staging.thankfulriver-68674ea3.eastus2.azurecontainerapps.io/api/style"
  "https://aiprofilemaker-api-staging.thankfulriver-68674ea3.eastus2.azurecontainerapps.io/api/styles"
  "https://aiprofilephotomakerapi.azurewebsites.net/api/style"
  "https://aiprofilephotomakerapi.azurewebsites.net/api/styles"
)

for endpoint in "${endpoints[@]}"; do
  echo "Testing: $endpoint"
  echo "----------------------------------------"
  
  # Test with curl and show response
  if curl -f -s -I "$endpoint" > /dev/null 2>&1; then
    echo "✅ REACHABLE"
    echo "Response:"
    curl -s "$endpoint" | jq '.' 2>/dev/null || curl -s "$endpoint"
    echo
  else
    echo "❌ FAILED"
    echo "Error details:"
    curl -s -I "$endpoint" 2>&1 | head -3
    echo
  fi
  echo
done

echo "🔧 SOLUTION RECOMMENDATIONS"
echo "=========================="
echo
echo "1. Working API: aiprofilemaker-api-staging (3 styles)"
echo "2. Missing 17 styles from expected 20"
echo "3. Run populate-styles.sql to add missing styles"
echo "4. Update production API endpoint if needed"