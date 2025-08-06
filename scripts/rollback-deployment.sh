#!/bin/bash
# Deployment Rollback Script
# Quick rollback to previous working deployment

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m'

RESOURCE_GROUP="${1:-aiprofilemaker-v1}"
ROLLBACK_STRATEGY="${2:-workflow}"

echo -e "${BLUE}🔄 Deployment Rollback Script${NC}"
echo -e "${BLUE}=============================${NC}"
echo -e "${YELLOW}Resource Group: ${RESOURCE_GROUP}${NC}"
echo -e "${YELLOW}Strategy: ${ROLLBACK_STRATEGY}${NC}"
echo ""

# Check prerequisites
if ! az account show &> /dev/null; then
    echo -e "${RED}❌ Azure CLI not authenticated${NC}"
    exit 1
fi

# Function to rollback via GitHub Actions
rollback_via_workflow() {
    echo -e "${BLUE}🔄 Rolling back via GitHub Actions...${NC}"
    
    if command -v gh &> /dev/null; then
        echo "Triggering PowerShell Deploy workflow (known working version)..."
        
        if gh workflow run "powershell-deploy.yml"; then
            echo -e "${GREEN}✅ Rollback workflow triggered${NC}"
            echo "Monitor at: $(gh repo view --json url --jq '.url')/actions"
        else
            echo -e "${RED}❌ Failed to trigger rollback workflow${NC}"
            return 1
        fi
    else
        echo -e "${YELLOW}⚠️ GitHub CLI not available, use manual trigger${NC}"
        echo "Go to: https://github.com/$(git config --get remote.origin.url | sed 's/.*github.com[:/]\([^/]*\/[^/]*\).*/\1/' | sed 's/\.git$//')/actions"
        echo "Manually trigger: 🚀 V1 PowerShell Deploy"
        return 1
    fi
}

# Function to rollback container apps directly
rollback_container_apps() {
    echo -e "${BLUE}🔄 Rolling back Container Apps directly...${NC}"
    
    # Get ACR info
    ACR_NAME=$(az acr list --resource-group "$RESOURCE_GROUP" --query "[0].name" --output tsv)
    ACR_SERVER=$(az acr show --name "$ACR_NAME" --query "loginServer" --output tsv)
    
    if [ -z "$ACR_NAME" ]; then
        echo -e "${RED}❌ No ACR found in resource group${NC}"
        return 1
    fi
    
    # Check for previous images
    echo "Checking for previous image versions..."
    
    BACKEND_TAGS=$(az acr repository show-tags --name "$ACR_NAME" --repository "aiprofilemaker-api" --query "[?contains(@, 'latest') == false]" --output tsv 2>/dev/null | head -1)
    FRONTEND_TAGS=$(az acr repository show-tags --name "$ACR_NAME" --repository "aiprofilemaker-web" --query "[?contains(@, 'latest') == false]" --output tsv 2>/dev/null | head -1)
    
    if [ -z "$BACKEND_TAGS" ] || [ -z "$FRONTEND_TAGS" ]; then
        echo -e "${YELLOW}⚠️ No previous image versions found${NC}"
        echo "Available backend tags: $(az acr repository show-tags --name "$ACR_NAME" --repository "aiprofilemaker-api" --output tsv | tr '\n' ', ')"
        echo "Available frontend tags: $(az acr repository show-tags --name "$ACR_NAME" --repository "aiprofilemaker-web" --output tsv | tr '\n' ', ')"
        
        # Use PowerShell workflow as fallback
        echo "Falling back to workflow-based rollback..."
        rollback_via_workflow
        return $?
    fi
    
    # Get container app names
    BACKEND_APP=$(az containerapp list --resource-group "$RESOURCE_GROUP" --query "[?contains(name, 'api')].name" --output tsv | head -1)
    FRONTEND_APP=$(az containerapp list --resource-group "$RESOURCE_GROUP" --query "[?contains(name, 'web')].name" --output tsv | head -1)
    
    if [ -z "$BACKEND_APP" ] || [ -z "$FRONTEND_APP" ]; then
        echo -e "${RED}❌ Container apps not found${NC}"
        return 1
    fi
    
    # Rollback backend
    echo "Rolling back backend app: $BACKEND_APP"
    az containerapp update \
        --name "$BACKEND_APP" \
        --resource-group "$RESOURCE_GROUP" \
        --image "$ACR_SERVER/aiprofilemaker-api:$BACKEND_TAGS"
    
    # Rollback frontend
    echo "Rolling back frontend app: $FRONTEND_APP"
    az containerapp update \
        --name "$FRONTEND_APP" \
        --resource-group "$RESOURCE_GROUP" \
        --image "$ACR_SERVER/aiprofilemaker-web:$FRONTEND_TAGS"
    
    echo -e "${GREEN}✅ Container apps rolled back${NC}"
}

# Function to get deployment health
check_deployment_health() {
    echo -e "${BLUE}🏥 Checking deployment health...${NC}"
    
    # Get app URLs
    BACKEND_URL=$(az containerapp list --resource-group "$RESOURCE_GROUP" --query "[?contains(name, 'api')].{url: properties.configuration.ingress.fqdn}" --output tsv | head -1)
    FRONTEND_URL=$(az containerapp list --resource-group "$RESOURCE_GROUP" --query "[?contains(name, 'web')].{url: properties.configuration.ingress.fqdn}" --output tsv | head -1)
    
    if [ -n "$BACKEND_URL" ]; then
        echo "Testing backend: https://$BACKEND_URL"
        if curl -f -s --max-time 30 "https://$BACKEND_URL/api/health" > /dev/null; then
            echo -e "${GREEN}✅ Backend healthy${NC}"
        else
            echo -e "${RED}❌ Backend unhealthy${NC}"
        fi
    fi
    
    if [ -n "$FRONTEND_URL" ]; then
        echo "Testing frontend: https://$FRONTEND_URL"
        if curl -f -s --max-time 30 "https://$FRONTEND_URL" > /dev/null; then
            echo -e "${GREEN}✅ Frontend healthy${NC}"
        else
            echo -e "${RED}❌ Frontend unhealthy${NC}"
        fi
    fi
}

# Main rollback logic
case "$ROLLBACK_STRATEGY" in
    "workflow"|"gh"|"github")
        rollback_via_workflow
        ;;
    "direct"|"container"|"apps")
        rollback_container_apps
        ;;
    "auto")
        echo -e "${BLUE}🤖 Automatic rollback strategy${NC}"
        if command -v gh &> /dev/null; then
            rollback_via_workflow
        else
            rollback_container_apps
        fi
        ;;
    *)
        echo -e "${RED}❌ Unknown rollback strategy: $ROLLBACK_STRATEGY${NC}"
        echo ""
        echo "Available strategies:"
        echo "  workflow - Use GitHub Actions (recommended)"
        echo "  direct   - Update Container Apps directly"
        echo "  auto     - Choose best available option"
        exit 1
        ;;
esac

# Wait for rollback to stabilize
echo ""
echo -e "${BLUE}⏳ Waiting for rollback to stabilize...${NC}"
sleep 30

# Check health after rollback
check_deployment_health

echo ""
echo -e "${GREEN}🎯 Rollback process completed${NC}"
echo ""
echo -e "${BLUE}Next steps:${NC}"
echo "• Monitor application health"
echo "• Investigate root cause of deployment issue"
echo "• Fix issues before attempting new deployment"