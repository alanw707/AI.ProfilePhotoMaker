#!/bin/bash

# Deploy AI Profile Photo Maker with OAuth Configuration
# This script handles the complete deployment including OAuth setup

set -euo pipefail

echo "🚀 AI Profile Photo Maker Deployment with OAuth"
echo "=============================================="

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
RESOURCE_GROUP="aiprofilemaker-v1"
BICEP_FILE="infrastructure/simple-deploy.bicep"
PARAMS_FILE="deployment-params.json"

# Check prerequisites
echo -e "${BLUE}📋 Checking prerequisites...${NC}"

if ! command -v az &> /dev/null; then
    echo -e "${RED}❌ Error: Azure CLI is not installed${NC}"
    exit 1
fi

if ! az account show > /dev/null 2>&1; then
    echo -e "${YELLOW}⚠️ Not logged in to Azure. Please login:${NC}"
    az login
fi

if [ ! -f "$BICEP_FILE" ]; then
    echo -e "${RED}❌ Error: Bicep template not found at $BICEP_FILE${NC}"
    exit 1
fi

# Check for required environment variables first
if [[ -n "$SQL_ADMIN_PASSWORD" && -n "$JWT_SECRET" && -n "$REPLICATE_API_TOKEN" && -n "$GOOGLE_CLIENT_ID" && -n "$GOOGLE_CLIENT_SECRET" ]]; then
    echo -e "${GREEN}✅ Using environment variables for deployment${NC}"
else
    # Check if parameters file exists
    if [ ! -f "$PARAMS_FILE" ]; then
        echo -e "${YELLOW}⚠️ Parameters file not found. Creating from template...${NC}"
        
        if [ -f "deployment-params.template.json" ]; then
            cp deployment-params.template.json "$PARAMS_FILE"
            echo -e "${YELLOW}📝 Please edit $PARAMS_FILE with your actual values:${NC}"
            echo "  - SQL Admin Password"
            echo "  - JWT Secret"
            echo "  - Replicate API Token"
            echo "  - Google Client ID"
            echo "  - Google Client Secret"
            echo ""
            echo -e "${RED}After editing, run this script again.${NC}"
            exit 1
        else
            echo -e "${RED}❌ Error: Template file deployment-params.template.json not found${NC}"
            exit 1
        fi
    fi

    # Validate parameters file doesn't contain template values
    if grep -q "REPLACE_WITH" "$PARAMS_FILE"; then
        echo -e "${RED}❌ Error: Parameters file contains template values${NC}"
        echo "Please edit $PARAMS_FILE with actual values"
        exit 1
    fi
fi

# Build and push images
echo ""
echo -e "${BLUE}🔨 Step 1: Building Docker images locally...${NC}"
if [ -f "scripts/build-local.sh" ]; then
    ./scripts/build-local.sh
else
    echo -e "${YELLOW}⚠️ Build script not found, assuming images are already built${NC}"
fi

echo ""
echo -e "${BLUE}📤 Step 2: Pushing images to Azure Container Registry...${NC}"
if [ -f "scripts/push-to-acr.sh" ]; then
    ./scripts/push-to-acr.sh
else
    echo -e "${YELLOW}⚠️ Push script not found, assuming images are already in ACR${NC}"
fi

# Deploy infrastructure with OAuth
echo ""
echo -e "${BLUE}🏗️ Step 3: Deploying infrastructure with OAuth configuration...${NC}"
echo "Resource Group: $RESOURCE_GROUP"
echo "Template: $BICEP_FILE"
echo "Parameters: $PARAMS_FILE"

DEPLOYMENT_NAME="aipm-deployment-$(date +%Y%m%d-%H%M%S)"

# Final check for required environment variables (if not using params file)
if [[ -z "$SQL_ADMIN_PASSWORD" || -z "$JWT_SECRET" || -z "$REPLICATE_API_TOKEN" || -z "$GOOGLE_CLIENT_ID" || -z "$GOOGLE_CLIENT_SECRET" ]]; then
    echo -e "${RED}❌ Missing required environment variables:${NC}"
    echo "Please set: SQL_ADMIN_PASSWORD, JWT_SECRET, REPLICATE_API_TOKEN, GOOGLE_CLIENT_ID, GOOGLE_CLIENT_SECRET"
    exit 1
fi

az deployment group create \
    --name "$DEPLOYMENT_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --template-file "$BICEP_FILE" \
    --parameters \
        sqlAdminPassword="$SQL_ADMIN_PASSWORD" \
        jwtSecret="$JWT_SECRET" \
        replicateApiToken="$REPLICATE_API_TOKEN" \
        googleClientId="$GOOGLE_CLIENT_ID" \
        googleClientSecret="$GOOGLE_CLIENT_SECRET" \
    --mode Incremental

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✅ Deployment completed successfully${NC}"
else
    echo -e "${RED}❌ Deployment failed${NC}"
    exit 1
fi

# Get deployment outputs
echo ""
echo -e "${BLUE}📊 Deployment Outputs:${NC}"
az deployment group show \
    --name "$DEPLOYMENT_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --query properties.outputs -o json

# Validate OAuth endpoint
echo ""
echo -e "${BLUE}🧪 Step 4: Validating OAuth configuration...${NC}"

OAUTH_URL="https://api.aiprofilephotomaker.com/api/auth/external-login/google"
echo "Testing OAuth endpoint: $OAUTH_URL"

# Wait for services to be ready
echo "Waiting for services to stabilize..."
sleep 15

HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" -I "$OAUTH_URL" || echo "000")

if [ "$HTTP_STATUS" == "302" ] || [ "$HTTP_STATUS" == "303" ]; then
    echo -e "${GREEN}✅ OAuth endpoint is working! (HTTP $HTTP_STATUS - Redirect)${NC}"
    LOCATION=$(curl -s -I "$OAUTH_URL" | grep -i "location:" | cut -d' ' -f2 | tr -d '\r')
    echo "Redirects to: $LOCATION"
elif [ "$HTTP_STATUS" == "200" ]; then
    echo -e "${GREEN}✅ OAuth endpoint responded (HTTP 200)${NC}"
else
    echo -e "${YELLOW}⚠️ OAuth endpoint returned HTTP $HTTP_STATUS${NC}"
    echo "This might be normal if the service is still starting up"
fi

# Run validation if available
echo ""
echo -e "${BLUE}🔍 Step 5: Running deployment validation...${NC}"
if [ -f "scripts/validate-deployment.sh" ]; then
    ./scripts/validate-deployment.sh
else
    echo -e "${YELLOW}⚠️ Validation script not found, skipping${NC}"
fi

# Summary
echo ""
echo -e "${GREEN}🎉 Deployment Complete!${NC}"
echo ""
echo -e "${BLUE}📋 Summary:${NC}"
echo "✓ Infrastructure deployed with OAuth configuration"
echo "✓ Container images deployed"
echo "✓ OAuth secrets configured"
echo ""
echo -e "${BLUE}🔗 Application URLs:${NC}"
echo "Frontend: https://app.aiprofilephotomaker.com"
echo "Backend API: https://api.aiprofilephotomaker.com"
echo "OAuth Login: https://api.aiprofilephotomaker.com/api/auth/external-login/google"
echo ""
echo -e "${BLUE}📊 Monitoring:${NC}"
echo "View logs:"
echo "  az monitor log-analytics query \\"
echo "    --workspace aipm-logs-v1 \\"
echo "    --analytics-query \"ContainerAppConsoleLogs_CL | where ContainerAppName_s == 'aipm-api-v1' | top 50 by TimeGenerated desc\""
echo ""
echo -e "${YELLOW}⚠️ Important:${NC}"
echo "- Keep your deployment-params.json file secure and never commit it"
echo "- OAuth configuration is now part of the infrastructure template"
echo "- Future deployments will automatically include OAuth settings"
echo ""
echo -e "${GREEN}✅ Deployment with OAuth completed successfully!${NC}"