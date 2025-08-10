#!/bin/bash

# Build Local Docker Images
# This script builds the Docker images locally for deployment to Azure Container Registry

set -euo pipefail

echo "🏗️ Building Docker Images Locally"
echo "================================="

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

# Validate Docker is running
if ! docker info > /dev/null 2>&1; then
    echo -e "${RED}❌ Error: Docker is not running or not accessible${NC}"
    echo "Please start Docker and try again"
    exit 1
fi

# Check if Dockerfiles exist
if [[ ! -f "Dockerfile.backend" ]]; then
    echo -e "${RED}❌ Error: Dockerfile.backend not found${NC}"
    exit 1
fi

if [[ ! -f "Dockerfile.frontend" ]]; then
    echo -e "${RED}❌ Error: Dockerfile.frontend not found${NC}"
    exit 1
fi

echo -e "${BLUE}🏗️ Building Backend Image...${NC}"
echo "Command: docker build -f Dockerfile.backend -t ${ACR_LOGIN_SERVER}/aiprofilemaker-api:${IMAGE_TAG} ."

docker build \
    -f Dockerfile.backend \
    -t "${ACR_LOGIN_SERVER}/aiprofilemaker-api:${IMAGE_TAG}" \
    -t "${ACR_LOGIN_SERVER}/aiprofilemaker-api:${BUILD_NUMBER}" \
    .

if [[ $? -eq 0 ]]; then
    echo -e "${GREEN}✅ Backend image built successfully${NC}"
else
    echo -e "${RED}❌ Backend image build failed${NC}"
    exit 1
fi

echo ""
echo -e "${BLUE}🏗️ Building Frontend Image...${NC}"
echo "Command: docker build -f Dockerfile.frontend -t ${ACR_LOGIN_SERVER}/aiprofilemaker-web:${IMAGE_TAG} ."

# Navigate to frontend directory for build context
cd AI.ProfilePhotoMaker.UI

docker build \
    -f ../Dockerfile.frontend \
    -t "${ACR_LOGIN_SERVER}/aiprofilemaker-web:${IMAGE_TAG}" \
    -t "${ACR_LOGIN_SERVER}/aiprofilemaker-web:${BUILD_NUMBER}" \
    .

if [[ $? -eq 0 ]]; then
    echo -e "${GREEN}✅ Frontend image built successfully${NC}"
    cd ..
else
    echo -e "${RED}❌ Frontend image build failed${NC}"
    cd ..
    exit 1
fi

echo ""
echo -e "${GREEN}🎉 All Images Built Successfully!${NC}"
echo ""
echo -e "${BLUE}📊 Built Images:${NC}"
docker images | grep "${ACR_LOGIN_SERVER}" | head -10

echo ""
echo -e "${YELLOW}💡 Next Steps:${NC}"
echo "1. Push images to ACR: ./scripts/push-to-acr.sh"
echo "2. Deploy infrastructure: git push (triggers workflow)"
echo ""
echo -e "${GREEN}✅ Build completed successfully!${NC}"