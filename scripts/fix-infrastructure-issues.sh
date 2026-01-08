#!/bin/bash

# Fix Infrastructure Issues for AI Profile Photo Maker
# This script resolves custom domain and API communication issues

set -euo pipefail

echo "AI Profile Photo Maker - Infrastructure Fix Script"
echo "=================================================="

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
RESOURCE_GROUP="aiprofilemaker-v1"
FRONTEND_APP="aipm-web-v1"
BACKEND_APP="aipm-api-v1"
FRONTEND_DOMAIN="aiprofilephotomaker.com"
API_DOMAIN="api.aiprofilephotomaker.com"

echo ""
echo -e "${BLUE}Step 1: Checking current infrastructure status...${NC}"

# Check if logged in to Azure
if ! az account show > /dev/null 2>&1; then
    echo -e "${YELLOW}Not logged in to Azure. Please login:${NC}"
    az login
fi

# Get current frontend URL
FRONTEND_URL=$(az containerapp show --resource-group $RESOURCE_GROUP --name $FRONTEND_APP --query "properties.configuration.ingress.fqdn" -o tsv)
echo "Frontend Container App URL: https://$FRONTEND_URL"

# Get current backend URL
BACKEND_URL=$(az containerapp show --resource-group $RESOURCE_GROUP --name $BACKEND_APP --query "properties.configuration.ingress.fqdn" -o tsv)
echo "Backend Container App URL: https://$BACKEND_URL"

echo ""
echo -e "${BLUE}Step 2: Testing current connectivity...${NC}"

# Test frontend container app URL
echo -n "Testing frontend container app URL: "
if curl -s -o /dev/null -w "%{http_code}" --max-time 5 "https://$FRONTEND_URL" | grep -q "200"; then
    echo -e "${GREEN}✓ Working${NC}"
else
    echo -e "${RED}✗ Not working${NC}"
fi

# Test backend container app URL
echo -n "Testing backend container app URL: "
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" --max-time 5 "https://$BACKEND_URL/api/health/ready")
if [ "$HTTP_CODE" == "200" ]; then
    echo -e "${GREEN}✓ Working${NC}"
else
    echo -e "${YELLOW}⚠ Returned HTTP $HTTP_CODE${NC}"
fi

# Test custom domains
echo -n "Testing frontend custom domain: "
if curl -s -o /dev/null -w "%{http_code}" --max-time 5 "https://$FRONTEND_DOMAIN" | grep -q "200"; then
    echo -e "${GREEN}✓ Working${NC}"
    FRONTEND_DOMAIN_WORKING=true
else
    echo -e "${RED}✗ Not accessible${NC}"
    FRONTEND_DOMAIN_WORKING=false
fi

echo -n "Testing API custom domain: "
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" --max-time 5 "https://$API_DOMAIN/api/health/ready")
if [ "$HTTP_CODE" == "200" ]; then
    echo -e "${GREEN}✓ Working${NC}"
else
    echo -e "${YELLOW}⚠ Returned HTTP $HTTP_CODE${NC}"
fi

echo ""
echo -e "${BLUE}Step 3: Fixing frontend environment configuration...${NC}"

# Update frontend with correct API URL
echo "Updating frontend API_URL environment variable..."
az containerapp update \
    --resource-group $RESOURCE_GROUP \
    --name $FRONTEND_APP \
    --set-env-vars API_URL="https://$API_DOMAIN" \
    --output none

echo -e "${GREEN}✓ Frontend API_URL updated${NC}"

echo ""
echo -e "${BLUE}Step 4: Updating CORS configuration...${NC}"

# Update backend CORS to include both domains
echo "Updating backend CORS configuration..."
az containerapp update \
    --resource-group $RESOURCE_GROUP \
    --name $BACKEND_APP \
    --set-env-vars \
        CORS_ALLOWED_ORIGINS="https://$FRONTEND_DOMAIN,https://$FRONTEND_URL,https://aiprofilephotomaker.com" \
    --output none

echo -e "${GREEN}✓ CORS configuration updated${NC}"

# If frontend domain is not working, try to refresh it
if [ "$FRONTEND_DOMAIN_WORKING" = false ]; then
    echo ""
    echo -e "${BLUE}Step 5: Refreshing frontend custom domain binding...${NC}"
    
    echo -e "${YELLOW}This will temporarily remove and re-add the custom domain.${NC}"
    echo -e "${YELLOW}Note: This may take a few minutes to propagate.${NC}"
    
    # Get the certificate ID
    CERT_ID=$(az containerapp show \
        --resource-group $RESOURCE_GROUP \
        --name $FRONTEND_APP \
        --query "properties.configuration.ingress.customDomains[?name=='$FRONTEND_DOMAIN'].certificateId" \
        -o tsv)
    
    if [ -n "$CERT_ID" ]; then
        echo "Found certificate: $CERT_ID"
        
        # Remove the custom domain
        echo "Removing custom domain..."
        az containerapp hostname delete \
            --resource-group $RESOURCE_GROUP \
            --name $FRONTEND_APP \
            --hostname $FRONTEND_DOMAIN \
            --yes \
            --output none || true
        
        echo "Waiting 10 seconds..."
        sleep 10
        
        # Re-add the custom domain
        echo "Re-adding custom domain..."
        az containerapp hostname add \
            --resource-group $RESOURCE_GROUP \
            --name $FRONTEND_APP \
            --hostname $FRONTEND_DOMAIN \
            --output none
        
        echo -e "${GREEN}✓ Custom domain binding refreshed${NC}"
    else
        echo -e "${RED}Could not find certificate for domain. Manual intervention required.${NC}"
    fi
else
    echo ""
    echo -e "${GREEN}✓ Frontend custom domain is already working${NC}"
fi

echo ""
echo -e "${BLUE}Step 6: Restarting container apps...${NC}"

# Get the latest revision names
FRONTEND_REVISION=$(az containerapp revision list \
    --resource-group $RESOURCE_GROUP \
    --name $FRONTEND_APP \
    --query "[0].name" -o tsv)

BACKEND_REVISION=$(az containerapp revision list \
    --resource-group $RESOURCE_GROUP \
    --name $BACKEND_APP \
    --query "[0].name" -o tsv)

# Restart by activating the current revision
echo "Restarting frontend..."
az containerapp revision restart \
    --resource-group $RESOURCE_GROUP \
    --name $FRONTEND_APP \
    --revision $FRONTEND_REVISION \
    --output none || true

echo "Restarting backend..."
az containerapp revision restart \
    --resource-group $RESOURCE_GROUP \
    --name $BACKEND_APP \
    --revision $BACKEND_REVISION \
    --output none || true

echo -e "${GREEN}✓ Container apps restarted${NC}"

echo ""
echo -e "${BLUE}Step 7: Waiting for services to stabilize...${NC}"
echo "Waiting 30 seconds for services to fully restart..."
sleep 30

echo ""
echo -e "${BLUE}Step 8: Final verification...${NC}"

# Test frontend custom domain again
echo -n "Testing frontend custom domain: "
if curl -s -o /dev/null -w "%{http_code}" --max-time 10 "https://$FRONTEND_DOMAIN" | grep -q "200"; then
    echo -e "${GREEN}✓ Working${NC}"
    FRONTEND_FIXED=true
else
    echo -e "${RED}✗ Still not accessible${NC}"
    FRONTEND_FIXED=false
fi

# Test API from frontend perspective (checking CORS)
echo -n "Testing API accessibility: "
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" --max-time 5 "https://$API_DOMAIN/api/health/ready")
if [ "$HTTP_CODE" == "200" ]; then
    echo -e "${GREEN}✓ API is accessible${NC}"
    API_FIXED=true
else
    echo -e "${YELLOW}⚠ API returned HTTP $HTTP_CODE${NC}"
    API_FIXED=false
fi

echo ""
echo "=========================================="
echo -e "${BLUE}Infrastructure Fix Summary${NC}"
echo "=========================================="

if [ "$FRONTEND_FIXED" = true ] && [ "$API_FIXED" = true ]; then
    echo -e "${GREEN}✅ All issues resolved successfully!${NC}"
    echo ""
    echo "The application should now be fully functional at:"
    echo "  Frontend: https://$FRONTEND_DOMAIN"
    echo "  API: https://$API_DOMAIN"
else
    echo -e "${YELLOW}⚠ Some issues remain:${NC}"
    
    if [ "$FRONTEND_FIXED" = false ]; then
        echo -e "${RED}  - Frontend custom domain is still not accessible${NC}"
        echo ""
        echo "  Workaround: Use the container app URL temporarily:"
        echo "  https://$FRONTEND_URL"
        echo ""
        echo "  To fix: Check DNS records and certificate configuration"
    fi
    
    if [ "$API_FIXED" = false ]; then
        echo -e "${RED}  - API communication issues may persist${NC}"
        echo ""
        echo "  Check the container logs for more details:"
        echo "  az containerapp logs show -n $BACKEND_APP -g $RESOURCE_GROUP"
    fi
fi

echo ""
echo "Next steps:"
echo "1. Clear browser cache and cookies"
echo "2. Try accessing the application in an incognito/private window"
echo "3. Test the OAuth login flow"
echo "4. Monitor the application for any remaining issues"

echo ""
echo -e "${BLUE}For detailed logs, run:${NC}"
echo "Frontend logs: az containerapp logs show -n $FRONTEND_APP -g $RESOURCE_GROUP --follow"
echo "Backend logs: az containerapp logs show -n $BACKEND_APP -g $RESOURCE_GROUP --follow"