#!/bin/bash
# Local Docker Build Script for AI Profile Photo Maker
# Builds both frontend and backend images locally for development

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

echo -e "${BLUE}🏗️ AI Profile Photo Maker - Local Build Script${NC}"
echo -e "${BLUE}=================================================${NC}"
echo ""

# Validate Docker is running
if ! docker info > /dev/null 2>&1; then
    echo -e "${RED}❌ ERROR: Docker is not running or accessible${NC}"
    echo "Please start Docker Desktop and try again"
    exit 1
fi

echo -e "${GREEN}✅ Docker is running${NC}"

# Change to project root
cd "$(dirname "$0")/.."
PROJECT_ROOT=$(pwd)

echo -e "${BLUE}📁 Project root: ${PROJECT_ROOT}${NC}"
echo ""

# Validate required files exist
echo -e "${BLUE}🔍 Validating build context...${NC}"

REQUIRED_FILES=(
    "Dockerfile.backend"
    "Dockerfile.frontend" 
    "AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj"
    "AI.ProfilePhotoMaker.UI/package.json"
    "nginx.conf"
    "docker-entrypoint.sh"
)

MISSING_FILES=()
for file in "${REQUIRED_FILES[@]}"; do
    if [ ! -f "$file" ]; then
        MISSING_FILES+=("$file")
    fi
done

if [ ${#MISSING_FILES[@]} -gt 0 ]; then
    echo -e "${RED}❌ ERROR: Missing required files for Docker build:${NC}"
    for missing in "${MISSING_FILES[@]}"; do
        echo -e "${RED}  - $missing${NC}"
    done
    echo ""
    echo "Please ensure you're running this script from the project root directory."
    exit 1
fi

echo -e "${GREEN}✅ All required files present${NC}"
echo ""

# Build backend image
echo -e "${BLUE}🔨 Building backend image...${NC}"
echo -e "${YELLOW}Tag: ${PROJECT_NAME}-api:${TAG}${NC}"

if docker build -f Dockerfile.backend -t "${PROJECT_NAME}-api:${TAG}" . ; then
    echo -e "${GREEN}✅ Backend image built successfully${NC}"
else
    echo -e "${RED}❌ Backend image build failed${NC}"
    exit 1
fi

echo ""

# Build frontend image  
echo -e "${BLUE}🔨 Building frontend image...${NC}"
echo -e "${YELLOW}Tag: ${PROJECT_NAME}-web:${TAG}${NC}"

if docker build -f Dockerfile.frontend -t "${PROJECT_NAME}-web:${TAG}" . ; then
    echo -e "${GREEN}✅ Frontend image built successfully${NC}"
else
    echo -e "${RED}❌ Frontend image build failed${NC}"
    exit 1
fi

echo ""

# List built images
echo -e "${BLUE}📋 Built images:${NC}"
docker images | head -1  # Header
docker images | grep "${PROJECT_NAME}" | grep "${TAG}"

echo ""
echo -e "${GREEN}🎉 Build completed successfully!${NC}"
echo ""
echo -e "${BLUE}Next steps:${NC}"
echo -e "  1. Run ${YELLOW}./scripts/push-to-acr.sh ${TAG}${NC} to push images to Azure Container Registry"
echo -e "  2. Or test locally with ${YELLOW}docker run -p 8080:8080 ${PROJECT_NAME}-api:${TAG}${NC}"
echo -e "  3. Or test frontend with ${YELLOW}docker run -p 80:80 ${PROJECT_NAME}-web:${TAG}${NC}"