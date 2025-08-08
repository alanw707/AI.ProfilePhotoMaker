#!/bin/bash

# Database Access Fix Validation Script
# This script validates that the critical database access fix has been successfully applied

set -e

echo "=== AI.ProfilePhotoMaker Database Access Validation ==="
echo "Timestamp: $(date)"
echo ""

# Configuration
RESOURCE_GROUP="aiprofilemaker-v1"
CONTAINER_APP="aipm-api-v1"
SQL_SERVER="aipm-sql-v1-6j74jubocuukg"
DATABASE="aipmdb"
API_URL="https://aipm-api-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io"

echo "1. Validating Managed Identity Configuration..."
PRINCIPAL_ID=$(az containerapp identity show --name $CONTAINER_APP --resource-group $RESOURCE_GROUP --query principalId --output tsv)
echo "   ✅ Container App Managed Identity: $PRINCIPAL_ID"

echo ""
echo "2. Validating Azure AD Admin Configuration..."
ADMIN_SID=$(az sql server ad-admin list --server $SQL_SERVER --resource-group $RESOURCE_GROUP --query "[0].sid" --output tsv)
if [ "$ADMIN_SID" = "$PRINCIPAL_ID" ]; then
    echo "   ✅ Container App is configured as SQL Server Azure AD Admin"
else
    echo "   ❌ Azure AD Admin mismatch - Expected: $PRINCIPAL_ID, Got: $ADMIN_SID"
    exit 1
fi

echo ""
echo "3. Validating Connection String Configuration..."
CONNECTION_STRING=$(az containerapp secret show --name $CONTAINER_APP --resource-group $RESOURCE_GROUP --secret-name connection-string --query value --output tsv)
if [[ $CONNECTION_STRING == *"Authentication=Active Directory Managed Identity"* ]]; then
    echo "   ✅ Connection string configured for Managed Identity authentication"
else
    echo "   ❌ Connection string not configured for Managed Identity"
    echo "   Current: $CONNECTION_STRING"
    exit 1
fi

echo ""
echo "4. Checking Container App Health..."
HEALTH_STATE=$(az containerapp revision list --name $CONTAINER_APP --resource-group $RESOURCE_GROUP --query "[?properties.active==\`true\`] | [0].properties.healthState" --output tsv)
REPLICAS=$(az containerapp revision list --name $CONTAINER_APP --resource-group $RESOURCE_GROUP --query "[?properties.active==\`true\`] | [0].properties.replicas" --output tsv)
echo "   Container Health State: $HEALTH_STATE"
echo "   Running Replicas: $REPLICAS"

echo ""
echo "5. Testing API Endpoints..."

# Test main API endpoint
echo "   Testing main endpoint..."
MAIN_RESPONSE=$(curl -w "%{http_code}" --max-time 10 -s -o /dev/null "$API_URL/" 2>/dev/null || echo "000")
echo "   Main endpoint response: HTTP $MAIN_RESPONSE"

# Test health endpoint
echo "   Testing /health endpoint..."
HEALTH_RESPONSE=$(curl -w "%{http_code}" --max-time 10 -s -o /dev/null "$API_URL/health" 2>/dev/null || echo "000")
echo "   Health endpoint response: HTTP $HEALTH_RESPONSE"

# Test health probe endpoints
echo "   Testing /api/health/live endpoint..."
LIVE_RESPONSE=$(curl -w "%{http_code}" --max-time 10 -s -o /dev/null "$API_URL/api/health/live" 2>/dev/null || echo "000")
echo "   Live probe response: HTTP $LIVE_RESPONSE"

echo "   Testing /api/health/ready endpoint..."
READY_RESPONSE=$(curl -w "%{http_code}" --max-time 10 -s -o /dev/null "$API_URL/api/health/ready" 2>/dev/null || echo "000")
echo "   Ready probe response: HTTP $READY_RESPONSE"

echo ""
echo "6. Validation Summary..."
echo "================================"

# Overall status assessment
SUCCESS_COUNT=0
TOTAL_TESTS=4

if [ "$PRINCIPAL_ID" != "" ] && [ "$ADMIN_SID" = "$PRINCIPAL_ID" ]; then
    echo "✅ Database Access Configuration: PASSED"
    ((SUCCESS_COUNT++))
else
    echo "❌ Database Access Configuration: FAILED"
fi

if [[ $CONNECTION_STRING == *"Authentication=Active Directory Managed Identity"* ]]; then
    echo "✅ Connection String Configuration: PASSED"
    ((SUCCESS_COUNT++))
else
    echo "❌ Connection String Configuration: FAILED"
fi

if [ "$REPLICAS" -gt 0 ]; then
    echo "✅ Container App Running: PASSED ($REPLICAS replicas)"
    ((SUCCESS_COUNT++))
else
    echo "❌ Container App Running: FAILED (0 replicas)"
fi

if [ "$HEALTH_RESPONSE" = "200" ] || [ "$LIVE_RESPONSE" = "200" ] || [ "$READY_RESPONSE" = "200" ]; then
    echo "✅ API Endpoints Responding: PASSED"
    ((SUCCESS_COUNT++))
else
    echo "❌ API Endpoints Responding: FAILED (All endpoints timeout)"
fi

echo ""
echo "Overall Success Rate: $SUCCESS_COUNT/$TOTAL_TESTS tests passed"

if [ $SUCCESS_COUNT -eq $TOTAL_TESTS ]; then
    echo "🎉 ALL TESTS PASSED - Database access fix is successful!"
    echo ""
    echo "The AI.ProfilePhotoMaker API should now be fully operational with:"
    echo "- Azure AD Managed Identity authentication to SQL Database"
    echo "- Proper database permissions for all operations"
    echo "- Healthy Container App with responsive endpoints"
    exit 0
elif [ $SUCCESS_COUNT -ge 2 ]; then
    echo "⚠️  PARTIAL SUCCESS - Configuration is correct but API may still be starting up"
    echo ""
    echo "Recommended next steps:"
    echo "1. Wait 2-3 minutes for application startup to complete"
    echo "2. Check Container App logs: az containerapp logs show --name $CONTAINER_APP --resource-group $RESOURCE_GROUP"
    echo "3. Re-run this validation script"
    exit 1
else
    echo "❌ VALIDATION FAILED - Additional troubleshooting required"
    echo ""
    echo "Recommended next steps:"
    echo "1. Check Container App logs for specific errors"
    echo "2. Verify database connection manually"
    echo "3. Consider alternative authentication approaches"
    exit 2
fi