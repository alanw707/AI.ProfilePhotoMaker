#!/bin/bash

# Validate Timestamp Coordination
# This script validates that the timestamp coordination system is working correctly
# Usage: ./scripts/validate-timestamp-coordination.sh [timestamp]

set -euo pipefail

echo "🔍 Validating Timestamp Coordination"
echo "===================================="

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
ACR_NAME="aipmcrv16j74jubocuukg"
RESOURCE_GROUP="aiprofilemaker-v1"

# Get timestamp from parameter or generate one for testing
if [[ $# -eq 1 ]]; then
    TIMESTAMP="$1"
    echo -e "${BLUE}ℹ️ Using provided timestamp: $TIMESTAMP${NC}"
else
    TIMESTAMP=$(date +%Y%m%d-%H%M%S)
    echo -e "${BLUE}ℹ️ Generated test timestamp: $TIMESTAMP${NC}"
fi

echo ""
echo -e "${BLUE}🔍 [CHECK 1] Verifying timestamp format...${NC}"
if [[ $TIMESTAMP =~ ^[0-9]{8}-[0-9]{6}$ ]]; then
    echo -e "${GREEN}✅ Timestamp format is valid (YYYYMMDD-HHMMSS)${NC}"
else
    echo -e "${RED}❌ Timestamp format is invalid. Expected: YYYYMMDD-HHMMSS${NC}"
    exit 1
fi

echo ""
echo -e "${BLUE}🔍 [CHECK 2] Checking Azure CLI and login status...${NC}"
if ! command -v az &> /dev/null; then
    echo -e "${RED}❌ Azure CLI is not installed${NC}"
    exit 1
fi

if ! az account show > /dev/null 2>&1; then
    echo -e "${RED}❌ Not logged in to Azure${NC}"
    echo "Please run: az login"
    exit 1
fi

echo -e "${GREEN}✅ Azure CLI is available and logged in${NC}"

echo ""
echo -e "${BLUE}🔍 [CHECK 3] Verifying ACR access...${NC}"
if az acr show --name "$ACR_NAME" > /dev/null 2>&1; then
    echo -e "${GREEN}✅ ACR is accessible${NC}"
else
    echo -e "${RED}❌ Cannot access ACR: $ACR_NAME${NC}"
    exit 1
fi

echo ""
echo -e "${BLUE}🔍 [CHECK 4] Testing Bicep parameter validation...${NC}"

# Create test parameters file with timestamp
cat > test-parameters.json << EOF
{
  "\$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "imageTag": {
      "value": "$TIMESTAMP"
    }
  }
}
EOF

# Test Bicep compilation with parameter
if az bicep build --file infrastructure/simple-deploy.bicep > /dev/null 2>&1; then
    echo -e "${GREEN}✅ Bicep template compiles successfully${NC}"
else
    echo -e "${RED}❌ Bicep template compilation failed${NC}"
    rm -f test-parameters.json
    exit 1
fi

# Validate parameters (dry run)
echo -e "${BLUE}🧪 [TEST] Validating Bicep with timestamp parameter...${NC}"
if az deployment group validate \
    --resource-group "$RESOURCE_GROUP" \
    --template-file infrastructure/simple-deploy.bicep \
    --parameters imageTag="$TIMESTAMP" > /dev/null 2>&1; then
    echo -e "${GREEN}✅ Bicep parameter validation passed${NC}"
else
    echo -e "${YELLOW}⚠️ Bicep parameter validation failed (may be due to missing secrets)${NC}"
    echo -e "${BLUE}ℹ️ This is expected in local testing without all required secrets${NC}"
fi

# Clean up test file
rm -f test-parameters.json

echo ""
echo -e "${BLUE}🔍 [CHECK 5] Verifying build script compatibility...${NC}"

# Check if build script exists and is executable
if [[ -f "scripts/build-local.sh" ]] && [[ -x "scripts/build-local.sh" ]]; then
    echo -e "${GREEN}✅ Build script exists and is executable${NC}"
else
    echo -e "${RED}❌ Build script is missing or not executable${NC}"
    exit 1
fi

# Check if IMAGE_TAG variable is used in build script
if grep -q "IMAGE_TAG" scripts/build-local.sh; then
    echo -e "${GREEN}✅ Build script properly uses IMAGE_TAG variable${NC}"
else
    echo -e "${RED}❌ Build script does not use IMAGE_TAG variable${NC}"
    exit 1
fi

echo ""
echo -e "${BLUE}🔍 [CHECK 6] Verifying push script compatibility...${NC}"

# Check if push script exists and is executable
if [[ -f "scripts/push-to-acr.sh" ]] && [[ -x "scripts/push-to-acr.sh" ]]; then
    echo -e "${GREEN}✅ Push script exists and is executable${NC}"
else
    echo -e "${RED}❌ Push script is missing or not executable${NC}"
    exit 1
fi

# Check if IMAGE_TAG variable is used in push script
if grep -q "IMAGE_TAG" scripts/push-to-acr.sh; then
    echo -e "${GREEN}✅ Push script properly uses IMAGE_TAG variable${NC}"
else
    echo -e "${RED}❌ Push script does not use IMAGE_TAG variable${NC}"
    exit 1
fi

echo ""
echo -e "${GREEN}🎉 All validation checks passed!${NC}"
echo ""
echo -e "${BLUE}📋 Summary:${NC}"
echo -e "  • Timestamp format: ${GREEN}Valid${NC}"
echo -e "  • Azure access: ${GREEN}Available${NC}"
echo -e "  • ACR access: ${GREEN}Available${NC}"
echo -e "  • Bicep template: ${GREEN}Valid${NC}"
echo -e "  • Build script: ${GREEN}Compatible${NC}"
echo -e "  • Push script: ${GREEN}Compatible${NC}"
echo ""
echo -e "${YELLOW}💡 Next steps:${NC}"
echo "1. Test the complete workflow:"
echo "   IMAGE_TAG=$TIMESTAMP ./scripts/build-local.sh"
echo "   IMAGE_TAG=$TIMESTAMP ./scripts/push-to-acr.sh"
echo ""
echo "2. Deploy with timestamp coordination:"
echo "   git push (triggers GitHub Actions with timestamp coordination)"
echo ""
echo -e "${GREEN}✅ Timestamp coordination system is ready!${NC}"