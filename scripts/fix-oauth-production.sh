#!/bin/bash

# Fix OAuth Configuration in Production
# This script adds the missing Google OAuth environment variables to the production Container App
# CRITICAL: Run this immediately to fix OAuth 500 errors in production

set -euo pipefail

echo "🔧 Fixing OAuth Configuration in Production"
echo "=========================================="

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
RESOURCE_GROUP="aiprofilemaker-v1"
CONTAINER_APP_NAME="aipm-api-v1"

echo -e "${BLUE}ℹ️ Target Environment:${NC}"
echo "  Resource Group: $RESOURCE_GROUP"
echo "  Container App: $CONTAINER_APP_NAME"
echo ""

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    echo -e "${RED}❌ Error: Azure CLI is not installed${NC}"
    echo "Please install Azure CLI: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
fi

# Check if user is logged in to Azure
if ! az account show > /dev/null 2>&1; then
    echo -e "${YELLOW}⚠️ Not logged in to Azure. Please login:${NC}"
    az login
fi

# Verify the container app exists
echo -e "${BLUE}🔍 Verifying Container App exists...${NC}"
if ! az containerapp show --name "$CONTAINER_APP_NAME" --resource-group "$RESOURCE_GROUP" > /dev/null 2>&1; then
    echo -e "${RED}❌ Error: Container App $CONTAINER_APP_NAME not found in resource group $RESOURCE_GROUP${NC}"
    exit 1
fi
echo -e "${GREEN}✅ Container App found${NC}"
echo ""

# Check current environment variables
echo -e "${BLUE}📊 Current OAuth Configuration:${NC}"
CURRENT_VARS=$(az containerapp show --name "$CONTAINER_APP_NAME" --resource-group "$RESOURCE_GROUP" \
    --query "properties.template.containers[0].env[?contains(name, 'GOOGLE')].name" -o json 2>/dev/null || echo "[]")

if [ "$CURRENT_VARS" == "[]" ]; then
    echo -e "${YELLOW}⚠️ No Google OAuth variables currently configured${NC}"
else
    echo "Current OAuth variables: $CURRENT_VARS"
fi
echo ""

# Prompt for OAuth credentials
echo -e "${YELLOW}📝 Please provide Google OAuth credentials:${NC}"
echo "You can find these in the Google Cloud Console:"
echo "https://console.cloud.google.com/apis/credentials"
echo ""

read -p "Enter GOOGLE_CLIENT_ID: " GOOGLE_CLIENT_ID
if [ -z "$GOOGLE_CLIENT_ID" ]; then
    echo -e "${RED}❌ Error: GOOGLE_CLIENT_ID cannot be empty${NC}"
    exit 1
fi

read -sp "Enter GOOGLE_CLIENT_SECRET (hidden): " GOOGLE_CLIENT_SECRET
echo ""
if [ -z "$GOOGLE_CLIENT_SECRET" ]; then
    echo -e "${RED}❌ Error: GOOGLE_CLIENT_SECRET cannot be empty${NC}"
    exit 1
fi

echo ""
echo -e "${BLUE}🚀 Updating Container App with OAuth credentials...${NC}"

# Update the container app with OAuth environment variables
az containerapp update \
    --name "$CONTAINER_APP_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --set-env-vars \
    GOOGLE_CLIENT_ID="$GOOGLE_CLIENT_ID" \
    GOOGLE_CLIENT_SECRET="$GOOGLE_CLIENT_SECRET" \
    --output none

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✅ OAuth credentials added successfully${NC}"
else
    echo -e "${RED}❌ Failed to update Container App${NC}"
    exit 1
fi

echo ""
echo -e "${BLUE}🔄 Waiting for Container App to restart...${NC}"
sleep 10

# Verify the configuration
echo -e "${BLUE}✅ Verifying OAuth configuration...${NC}"
OAUTH_VARS=$(az containerapp show --name "$CONTAINER_APP_NAME" --resource-group "$RESOURCE_GROUP" \
    --query "properties.template.containers[0].env[?contains(name, 'GOOGLE')].name" -o json)

if [[ "$OAUTH_VARS" == *"GOOGLE_CLIENT_ID"* ]] && [[ "$OAUTH_VARS" == *"GOOGLE_CLIENT_SECRET"* ]]; then
    echo -e "${GREEN}✅ OAuth environment variables confirmed${NC}"
else
    echo -e "${YELLOW}⚠️ Warning: OAuth variables may not be properly set${NC}"
    echo "Found: $OAUTH_VARS"
fi

echo ""
echo -e "${BLUE}🧪 Testing OAuth endpoint...${NC}"

# Test the OAuth endpoint
OAUTH_URL="https://api.aiprofilephotomaker.com/api/auth/external-login/google"
echo "Testing: $OAUTH_URL"

HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" -I "$OAUTH_URL")

if [ "$HTTP_STATUS" == "302" ] || [ "$HTTP_STATUS" == "303" ]; then
    echo -e "${GREEN}✅ OAuth endpoint is working! (HTTP $HTTP_STATUS - Redirect)${NC}"
    LOCATION=$(curl -s -I "$OAUTH_URL" | grep -i "location:" | cut -d' ' -f2 | tr -d '\r')
    echo "Redirects to: $LOCATION"
elif [ "$HTTP_STATUS" == "200" ]; then
    echo -e "${GREEN}✅ OAuth endpoint responded successfully (HTTP 200)${NC}"
else
    echo -e "${YELLOW}⚠️ OAuth endpoint returned HTTP $HTTP_STATUS${NC}"
    echo "This might be normal if additional configuration is needed"
fi

echo ""
echo -e "${GREEN}🎉 OAuth Configuration Fix Complete!${NC}"
echo ""
echo -e "${BLUE}📋 Next Steps:${NC}"
echo "1. Test OAuth login at: https://aiprofilephotomaker.com"
echo "2. Monitor logs: az monitor log-analytics query --workspace aipm-logs-v1 --analytics-query \"ContainerAppConsoleLogs_CL | where ContainerAppName_s == 'aipm-api-v1' | top 50 by TimeGenerated desc\""
echo "3. Update infrastructure template: infrastructure/simple-deploy.bicep"
echo ""
echo -e "${YELLOW}⚠️ Important:${NC}"
echo "This is a temporary fix. Please update the Bicep template to include OAuth configuration permanently."
echo ""
echo -e "${GREEN}✅ Production OAuth fix completed successfully!${NC}"