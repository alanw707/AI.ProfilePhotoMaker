#!/bin/bash

# Push Docker Images to Azure Container Registry
# This script pushes the locally built Docker images to ACR for deployment

set -euo pipefail

echo "🚀 Pushing Docker Images to ACR"
echo "==============================="

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
ACR_NAME="aipmcrv16j74jubocuukg"
ACR_LOGIN_SERVER="${ACR_NAME}.azurecr.io"
IMAGE_TAG="${IMAGE_TAG:-latest}"
BUILD_NUMBER="${BUILD_NUMBER:-$(date +%Y%m%d-%H%M%S)}"

echo -e "${BLUE}ℹ️ Configuration:${NC}"
echo "  ACR: $ACR_LOGIN_SERVER"
echo "  Tag: $IMAGE_TAG"
echo "  Build: $BUILD_NUMBER"
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
    echo "az login"
    exit 1
fi

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo -e "${RED}❌ Error: Docker is not running or not accessible${NC}"
    echo "Please start Docker and try again"
    exit 1
fi

# Verify images exist locally
echo -e "${BLUE}🔍 Verifying local images exist...${NC}"

BACKEND_IMAGE="${ACR_LOGIN_SERVER}/aiprofilemaker-api:${IMAGE_TAG}"
FRONTEND_IMAGE="${ACR_LOGIN_SERVER}/aiprofilemaker-web:${IMAGE_TAG}"

if ! docker image inspect "$BACKEND_IMAGE" > /dev/null 2>&1; then
    echo -e "${RED}❌ Error: Backend image not found: $BACKEND_IMAGE${NC}"
    echo "Please run ./scripts/build-local.sh first"
    exit 1
fi

if ! docker image inspect "$FRONTEND_IMAGE" > /dev/null 2>&1; then
    echo -e "${RED}❌ Error: Frontend image not found: $FRONTEND_IMAGE${NC}"
    echo "Please run ./scripts/build-local.sh first"
    exit 1
fi

echo -e "${GREEN}✅ Local images verified${NC}"
echo ""

# Login to ACR
echo -e "${BLUE}🔐 Logging in to ACR...${NC}"
az acr login --name "$ACR_NAME"

if [[ $? -ne 0 ]]; then
    echo -e "${RED}❌ Error: ACR login failed${NC}"
    echo "Please check your Azure credentials and ACR permissions"
    exit 1
fi

echo -e "${GREEN}✅ ACR login successful${NC}"
echo ""

# Push Backend Image
echo -e "${BLUE}🚀 Pushing Backend Image...${NC}"
echo "Image: $BACKEND_IMAGE"

docker push "$BACKEND_IMAGE"

if [[ $? -eq 0 ]]; then
    echo -e "${GREEN}✅ Backend image pushed successfully${NC}"
else
    echo -e "${RED}❌ Backend image push failed${NC}"
    exit 1
fi

# Push Backend Build Number Tag
if [[ "$BUILD_NUMBER" != "latest" ]]; then
    echo -e "${BLUE}🏷️ Tagging Backend with Build Number...${NC}"
    docker tag "${ACR_LOGIN_SERVER}/aiprofilemaker-api:${IMAGE_TAG}" "${ACR_LOGIN_SERVER}/aiprofilemaker-api:${BUILD_NUMBER}"
    echo -e "${BLUE}🚀 Pushing Backend Build Tag...${NC}"
    docker push "${ACR_LOGIN_SERVER}/aiprofilemaker-api:${BUILD_NUMBER}"
fi

echo ""

# Push Frontend Image
echo -e "${BLUE}🚀 Pushing Frontend Image...${NC}"
echo "Image: $FRONTEND_IMAGE"

docker push "$FRONTEND_IMAGE"

if [[ $? -eq 0 ]]; then
    echo -e "${GREEN}✅ Frontend image pushed successfully${NC}"
else
    echo -e "${RED}❌ Frontend image push failed${NC}"
    exit 1
fi

# Push Frontend Build Number Tag
if [[ "$BUILD_NUMBER" != "latest" ]]; then
    echo -e "${BLUE}🏷️ Tagging Frontend with Build Number...${NC}"
    docker tag "${ACR_LOGIN_SERVER}/aiprofilemaker-web:${IMAGE_TAG}" "${ACR_LOGIN_SERVER}/aiprofilemaker-web:${BUILD_NUMBER}"
    echo -e "${BLUE}🚀 Pushing Frontend Build Tag...${NC}"
    docker push "${ACR_LOGIN_SERVER}/aiprofilemaker-web:${BUILD_NUMBER}"
fi

echo ""
echo -e "${GREEN}🎉 All Images Pushed Successfully!${NC}"
echo ""

# Verify images in ACR
echo -e "${BLUE}🔍 Verifying images in ACR...${NC}"
echo ""
echo "Backend images:"
az acr repository show-tags --name "$ACR_NAME" --repository "aiprofilemaker-api" --orderby time_desc --output table || echo "No backend images found"

echo ""
echo "Frontend images:"
az acr repository show-tags --name "$ACR_NAME" --repository "aiprofilemaker-web" --orderby time_desc --output table || echo "No frontend images found"

echo ""
echo -e "${YELLOW}💡 Next Steps:${NC}"
echo "1. Trigger deployment: git push (or re-run GitHub Actions workflow)"
echo "2. Container Apps will automatically pull the new images"
echo "3. Monitor deployment: az containerapp logs tail --name aipm-api-v1 --resource-group aiprofilemaker-v1"
echo ""
echo -e "${GREEN}✅ Push completed successfully!${NC}"