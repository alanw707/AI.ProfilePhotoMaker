#!/bin/bash
# ACR Push Script for AI Profile Photo Maker
# Pushes locally built images to Azure Container Registry

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
PROJECT_NAME="aiprofilemaker"
TAG="${1:-latest}"  # Use provided tag or default to 'latest'
RESOURCE_GROUP="${2:-aiprofilemaker-v1}"  # Default resource group

echo -e "${BLUE}📤 AI Profile Photo Maker - ACR Push Script${NC}"
echo -e "${BLUE}=============================================${NC}"
echo ""

# Validate Azure CLI is installed and logged in
if ! command -v az &> /dev/null; then
    echo -e "${RED}❌ ERROR: Azure CLI is not installed${NC}"
    echo "Please install Azure CLI: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
fi

echo -e "${GREEN}✅ Azure CLI found${NC}"

# Check if logged in to Azure
if ! az account show &> /dev/null; then
    echo -e "${RED}❌ ERROR: Not logged in to Azure${NC}"
    echo "Please run: ${YELLOW}az login${NC}"
    exit 1
fi

echo -e "${GREEN}✅ Logged in to Azure${NC}"

# Get current subscription info
SUBSCRIPTION=$(az account show --query "name" --output tsv)
echo -e "${BLUE}🔗 Active subscription: ${SUBSCRIPTION}${NC}"

# Discover Container Registry
echo -e "${BLUE}🔍 Discovering Container Registry...${NC}"

ACR_NAME=$(az acr list --resource-group "$RESOURCE_GROUP" --query "[0].name" --output tsv 2>/dev/null || echo "")

if [ -z "$ACR_NAME" ]; then
    echo -e "${YELLOW}⚠️  No ACR found in resource group '$RESOURCE_GROUP'${NC}"
    echo -e "${BLUE}🔍 Searching all resource groups...${NC}"
    
    ACR_NAME=$(az acr list --query "[?contains(name, 'aipm') || contains(name, 'aiprofile')].name | [0]" --output tsv 2>/dev/null || echo "")
    
    if [ -z "$ACR_NAME" ]; then
        echo -e "${RED}❌ ERROR: No Container Registry found${NC}"
        echo ""
        echo "Available Container Registries:"
        az acr list --query "[].{Name:name, ResourceGroup:resourceGroup, Location:location}" --output table
        echo ""
        echo "Usage: $0 [tag] [resource-group-name]"
        exit 1
    fi
    
    # Get resource group of found ACR
    RESOURCE_GROUP=$(az acr show --name "$ACR_NAME" --query "resourceGroup" --output tsv)
fi

ACR_LOGIN_SERVER=$(az acr show --name "$ACR_NAME" --resource-group "$RESOURCE_GROUP" --query "loginServer" --output tsv)

echo -e "${GREEN}✅ Found Container Registry: ${ACR_NAME}${NC}"
echo -e "${BLUE}🌐 Login Server: ${ACR_LOGIN_SERVER}${NC}"
echo -e "${BLUE}📁 Resource Group: ${RESOURCE_GROUP}${NC}"
echo ""

# Validate local images exist
echo -e "${BLUE}🔍 Validating local images...${NC}"

BACKEND_IMAGE="${PROJECT_NAME}-api:${TAG}"
FRONTEND_IMAGE="${PROJECT_NAME}-web:${TAG}" 

if ! docker image inspect "$BACKEND_IMAGE" &> /dev/null; then
    echo -e "${RED}❌ ERROR: Backend image not found: $BACKEND_IMAGE${NC}"
    echo "Please run: ${YELLOW}./scripts/build-local.sh $TAG${NC}"
    exit 1
fi

if ! docker image inspect "$FRONTEND_IMAGE" &> /dev/null; then
    echo -e "${RED}❌ ERROR: Frontend image not found: $FRONTEND_IMAGE${NC}"
    echo "Please run: ${YELLOW}./scripts/build-local.sh $TAG${NC}"
    exit 1
fi

echo -e "${GREEN}✅ Local images validated${NC}"
echo ""

# Login to ACR
echo -e "${BLUE}🔐 Logging in to Azure Container Registry...${NC}"

if az acr login --name "$ACR_NAME" &> /dev/null; then
    echo -e "${GREEN}✅ Successfully logged in to ACR${NC}"
else
    echo -e "${RED}❌ ERROR: Failed to login to ACR${NC}"
    echo "Please check your permissions for ACR: $ACR_NAME"
    exit 1
fi

echo ""

# Tag images for ACR
echo -e "${BLUE}🏷️ Tagging images for ACR...${NC}"

ACR_BACKEND_IMAGE="${ACR_LOGIN_SERVER}/${PROJECT_NAME}-api:${TAG}"
ACR_FRONTEND_IMAGE="${ACR_LOGIN_SERVER}/${PROJECT_NAME}-web:${TAG}"

docker tag "$BACKEND_IMAGE" "$ACR_BACKEND_IMAGE"
docker tag "$FRONTEND_IMAGE" "$ACR_FRONTEND_IMAGE"

echo -e "${GREEN}✅ Images tagged for ACR${NC}"
echo -e "${BLUE}   Backend: ${ACR_BACKEND_IMAGE}${NC}"
echo -e "${BLUE}   Frontend: ${ACR_FRONTEND_IMAGE}${NC}"
echo ""

# Push backend image
echo -e "${BLUE}📤 Pushing backend image...${NC}"
if docker push "$ACR_BACKEND_IMAGE"; then
    echo -e "${GREEN}✅ Backend image pushed successfully${NC}"
else
    echo -e "${RED}❌ Backend image push failed${NC}"
    exit 1
fi

echo ""

# Push frontend image
echo -e "${BLUE}📤 Pushing frontend image...${NC}"
if docker push "$ACR_FRONTEND_IMAGE"; then
    echo -e "${GREEN}✅ Frontend image pushed successfully${NC}"
else
    echo -e "${RED}❌ Frontend image push failed${NC}"
    exit 1
fi

echo ""

# Verify images in ACR
echo -e "${BLUE}📋 Verifying images in ACR...${NC}"

BACKEND_REPOSITORIES=$(az acr repository list --name "$ACR_NAME" --query "[?contains(@, '${PROJECT_NAME}-api')]" --output tsv)
FRONTEND_REPOSITORIES=$(az acr repository list --name "$ACR_NAME" --query "[?contains(@, '${PROJECT_NAME}-web')]" --output tsv)

if [ -n "$BACKEND_REPOSITORIES" ] && [ -n "$FRONTEND_REPOSITORIES" ]; then
    echo -e "${GREEN}✅ Images verified in ACR${NC}"
    
    echo ""
    echo -e "${BLUE}📊 Repository summary:${NC}"
    az acr repository list --name "$ACR_NAME" --output table | head -1
    az acr repository list --name "$ACR_NAME" --query "[?contains(@, '${PROJECT_NAME}')]" --output tsv | while read repo; do
        TAGS=$(az acr repository show-tags --name "$ACR_NAME" --repository "$repo" --output tsv | tr '\n' ', ' | sed 's/,$//')
        printf "%-30s %s\n" "$repo" "$TAGS"
    done
else
    echo -e "${YELLOW}⚠️  Warning: Could not verify all images in ACR${NC}"
fi

echo ""
echo -e "${GREEN}🎉 Push completed successfully!${NC}"
echo ""
echo -e "${BLUE}Next steps:${NC}"
echo -e "  1. Images are now available in ACR: ${YELLOW}${ACR_LOGIN_SERVER}${NC}"
echo -e "  2. Deploy infrastructure: ${YELLOW}git push origin main${NC}"
echo -e "  3. Container Apps will use these images: ${YELLOW}${PROJECT_NAME}-api:${TAG}${NC} and ${YELLOW}${PROJECT_NAME}-web:${TAG}${NC}"
echo ""
echo -e "${BLUE}Image URLs:${NC}"
echo -e "  Backend:  ${ACR_BACKEND_IMAGE}"
echo -e "  Frontend: ${ACR_FRONTEND_IMAGE}"