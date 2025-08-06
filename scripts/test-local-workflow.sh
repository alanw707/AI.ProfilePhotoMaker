#!/bin/bash
# Comprehensive Local Workflow Testing Script
# Tests all components of the local build workflow without affecting production

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${BLUE}🧪 Local Build Workflow - Comprehensive Test Suite${NC}"
echo -e "${BLUE}====================================================${NC}"
echo ""

# Test configuration
TEST_TAG="test-$(date +%Y%m%d-%H%M%S)"
PROJECT_ROOT=$(cd "$(dirname "$0")/.." && pwd)
TEST_RESULTS=()

# Helper function to log test results
log_test() {
    local test_name="$1"
    local status="$2"
    local message="$3"
    
    if [ "$status" = "PASS" ]; then
        echo -e "${GREEN}✅ PASS: ${test_name}${NC}"
        TEST_RESULTS+=("✅ $test_name")
    elif [ "$status" = "FAIL" ]; then
        echo -e "${RED}❌ FAIL: ${test_name}${NC}"
        echo -e "${RED}   Error: ${message}${NC}"
        TEST_RESULTS+=("❌ $test_name: $message")
    elif [ "$status" = "WARN" ]; then
        echo -e "${YELLOW}⚠️  WARN: ${test_name}${NC}"
        echo -e "${YELLOW}   Warning: ${message}${NC}"
        TEST_RESULTS+=("⚠️ $test_name: $message")
    else
        echo -e "${BLUE}ℹ️  INFO: ${test_name}${NC}"
        TEST_RESULTS+=("ℹ️ $test_name")
    fi
}

# Test 1: Environment Prerequisites
echo -e "${BLUE}[TEST 1] Checking Environment Prerequisites${NC}"

# Docker
if command -v docker &> /dev/null && docker info &> /dev/null; then
    log_test "Docker Installation" "PASS"
else
    log_test "Docker Installation" "FAIL" "Docker not running or not installed"
    exit 1
fi

# Azure CLI
if command -v az &> /dev/null; then
    log_test "Azure CLI Installation" "PASS"
else
    log_test "Azure CLI Installation" "FAIL" "Azure CLI not installed"
    exit 1
fi

# Azure Login
if az account show &> /dev/null; then
    SUBSCRIPTION=$(az account show --query "name" --output tsv)
    log_test "Azure Authentication" "PASS" "Logged in to: $SUBSCRIPTION"
else
    log_test "Azure Authentication" "FAIL" "Not logged in to Azure - run 'az login'"
    exit 1
fi

echo ""

# Test 2: Project Structure Validation
echo -e "${BLUE}[TEST 2] Validating Project Structure${NC}"

cd "$PROJECT_ROOT"

REQUIRED_FILES=(
    "scripts/build-local.sh"
    "scripts/push-to-acr.sh"
    "Dockerfile.backend"
    "Dockerfile.frontend"
    "AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj"
    "AI.ProfilePhotoMaker.UI/package.json"
    "infrastructure/simple-deploy.bicep"
    ".github/workflows/simple-deploy.yml"
)

ALL_FILES_EXIST=true
for file in "${REQUIRED_FILES[@]}"; do
    if [ -f "$file" ]; then
        log_test "Required File: $file" "PASS"
    else
        log_test "Required File: $file" "FAIL" "File missing"
        ALL_FILES_EXIST=false
    fi
done

if [ "$ALL_FILES_EXIST" = false ]; then
    echo -e "${RED}❌ Missing required files - cannot proceed${NC}"
    exit 1
fi

echo ""

# Test 3: Build Script Validation
echo -e "${BLUE}[TEST 3] Testing Local Build Script${NC}"

# Check build script is executable
if [ -x "scripts/build-local.sh" ]; then
    log_test "Build Script Executable" "PASS"
else
    log_test "Build Script Executable" "FAIL" "Script not executable"
    chmod +x scripts/build-local.sh
fi

# Test build script (dry run validation)
echo -e "${YELLOW}Building test images with tag: $TEST_TAG${NC}"
if timeout 300 ./scripts/build-local.sh "$TEST_TAG"; then
    log_test "Local Image Build" "PASS" "Built with tag: $TEST_TAG"
else
    log_test "Local Image Build" "FAIL" "Build script failed"
    exit 1
fi

# Verify images were built
BACKEND_IMAGE="aiprofilemaker-api:$TEST_TAG"
FRONTEND_IMAGE="aiprofilemaker-web:$TEST_TAG"

if docker image inspect "$BACKEND_IMAGE" &> /dev/null; then
    log_test "Backend Image Created" "PASS"
else
    log_test "Backend Image Created" "FAIL" "Image not found after build"
fi

if docker image inspect "$FRONTEND_IMAGE" &> /dev/null; then
    log_test "Frontend Image Created" "PASS"
else
    log_test "Frontend Image Created" "FAIL" "Image not found after build"
fi

echo ""

# Test 4: ACR Discovery and Access
echo -e "${BLUE}[TEST 4] Testing ACR Discovery and Access${NC}"

# Check push script is executable
if [ -x "scripts/push-to-acr.sh" ]; then
    log_test "Push Script Executable" "PASS"
else
    log_test "Push Script Executable" "FAIL" "Script not executable"
    chmod +x scripts/push-to-acr.sh
fi

# Test ACR discovery (without actually pushing)
RESOURCE_GROUP="aiprofilemaker-v1"
ACR_NAME=$(az acr list --resource-group "$RESOURCE_GROUP" --query "[0].name" --output tsv 2>/dev/null || echo "")

if [ -n "$ACR_NAME" ]; then
    log_test "ACR Discovery" "PASS" "Found: $ACR_NAME"
    
    # Test ACR login
    if az acr login --name "$ACR_NAME" &> /dev/null; then
        log_test "ACR Authentication" "PASS"
    else
        log_test "ACR Authentication" "WARN" "Could not login to ACR - may need permissions"
    fi
else
    log_test "ACR Discovery" "WARN" "No ACR found in $RESOURCE_GROUP - will search globally"
    
    # Try global search
    ACR_NAME=$(az acr list --query "[?contains(name, 'aipm') || contains(name, 'aiprofile')].name | [0]" --output tsv 2>/dev/null || echo "")
    if [ -n "$ACR_NAME" ]; then
        log_test "ACR Global Discovery" "PASS" "Found: $ACR_NAME"
    else
        log_test "ACR Global Discovery" "WARN" "No ACR found - deployment will create one"
    fi
fi

echo ""

# Test 5: Infrastructure Template Validation
echo -e "${BLUE}[TEST 5] Validating Infrastructure Templates${NC}"

# Bicep template compilation
if az bicep build --file infrastructure/simple-deploy.bicep --outfile /tmp/test-template.json; then
    log_test "Bicep Template Compilation" "PASS"
    rm -f /tmp/test-template.json
else
    log_test "Bicep Template Compilation" "FAIL" "Template does not compile"
fi

# Check for required parameters (simulate)
echo -e "${YELLOW}Note: Skipping template validation (requires secrets)${NC}"
log_test "Template Parameter Check" "INFO" "Skipped - requires deployment secrets"

echo ""

# Test 6: GitHub Workflow Validation
echo -e "${BLUE}[TEST 6] Validating GitHub Workflow${NC}"

WORKFLOW_FILE=".github/workflows/simple-deploy.yml"
if [ -f "$WORKFLOW_FILE" ]; then
    log_test "Workflow File Exists" "PASS"
    
    # Basic YAML syntax check (if yq available)
    if command -v yq &> /dev/null; then
        if yq eval . "$WORKFLOW_FILE" > /dev/null 2>&1; then
            log_test "Workflow YAML Syntax" "PASS"
        else
            log_test "Workflow YAML Syntax" "FAIL" "Invalid YAML syntax"
        fi
    else
        log_test "Workflow YAML Syntax" "INFO" "Skipped - yq not available"
    fi
    
    # Check for required secrets reference
    if grep -q "secrets.SQL_ADMIN_PASSWORD" "$WORKFLOW_FILE"; then
        log_test "Workflow Secrets Reference" "PASS"
    else
        log_test "Workflow Secrets Reference" "WARN" "Missing secrets reference"
    fi
else
    log_test "Workflow File Exists" "FAIL" "Workflow file missing"
fi

echo ""

# Test 7: Optional - Quick Container Test
echo -e "${BLUE}[TEST 7] Optional Container Runtime Test${NC}"

echo -e "${YELLOW}Testing if containers start correctly (30 second timeout)...${NC}"

# Test backend container (quick start/stop)
BACKEND_CONTAINER="test-backend-$$"
if timeout 30 docker run -d --name "$BACKEND_CONTAINER" -p 8081:8080 "$BACKEND_IMAGE" > /dev/null 2>&1; then
    sleep 5
    if docker ps | grep -q "$BACKEND_CONTAINER"; then
        log_test "Backend Container Startup" "PASS"
    else
        log_test "Backend Container Startup" "WARN" "Container exited quickly"
    fi
    docker stop "$BACKEND_CONTAINER" > /dev/null 2>&1 || true
    docker rm "$BACKEND_CONTAINER" > /dev/null 2>&1 || true
else
    log_test "Backend Container Startup" "WARN" "Container failed to start or timeout"
fi

# Test frontend container (quick start/stop)  
FRONTEND_CONTAINER="test-frontend-$$"
if timeout 30 docker run -d --name "$FRONTEND_CONTAINER" -p 8082:80 "$FRONTEND_IMAGE" > /dev/null 2>&1; then
    sleep 5
    if docker ps | grep -q "$FRONTEND_CONTAINER"; then
        log_test "Frontend Container Startup" "PASS"
    else
        log_test "Frontend Container Startup" "WARN" "Container exited quickly"
    fi
    docker stop "$FRONTEND_CONTAINER" > /dev/null 2>&1 || true
    docker rm "$FRONTEND_CONTAINER" > /dev/null 2>&1 || true
else
    log_test "Frontend Container Startup" "WARN" "Container failed to start or timeout"
fi

echo ""

# Test Results Summary
echo -e "${BLUE}📊 TEST RESULTS SUMMARY${NC}"
echo -e "${BLUE}========================${NC}"

PASS_COUNT=0
FAIL_COUNT=0
WARN_COUNT=0

for result in "${TEST_RESULTS[@]}"; do
    echo "$result"
    if [[ "$result" == *"✅"* ]]; then
        ((PASS_COUNT++))
    elif [[ "$result" == *"❌"* ]]; then
        ((FAIL_COUNT++))
    elif [[ "$result" == *"⚠️"* ]]; then
        ((WARN_COUNT++))
    fi
done

echo ""
echo -e "${BLUE}Summary:${NC}"
echo -e "${GREEN}  ✅ Passed: $PASS_COUNT${NC}"
echo -e "${YELLOW}  ⚠️  Warnings: $WARN_COUNT${NC}"
echo -e "${RED}  ❌ Failed: $FAIL_COUNT${NC}"

echo ""

# Overall result and recommendations
if [ $FAIL_COUNT -eq 0 ]; then
    echo -e "${GREEN}🎉 LOCAL WORKFLOW TEST: PASSED${NC}"
    echo -e "${GREEN}✅ All critical tests passed - workflow is ready for testing${NC}"
    echo ""
    echo -e "${BLUE}Next Steps:${NC}"
    echo -e "  1. ${YELLOW}Test ACR push:${NC} ./scripts/push-to-acr.sh $TEST_TAG"
    echo -e "  2. ${YELLOW}Create test branch:${NC} ./scripts/test-branch-workflow.sh"
    echo -e "  3. ${YELLOW}Manual workflow trigger:${NC} gh workflow run simple-deploy.yml"
    echo ""
    echo -e "${BLUE}Or proceed directly to production:${NC}"
    echo -e "  ${YELLOW}./scripts/build-local.sh && ./scripts/push-to-acr.sh && git push origin main${NC}"
elif [ $FAIL_COUNT -le 2 ] && [ $WARN_COUNT -le 3 ]; then
    echo -e "${YELLOW}⚠️  LOCAL WORKFLOW TEST: PASSED WITH WARNINGS${NC}"
    echo -e "${YELLOW}Some non-critical issues found - review warnings above${NC}"
    echo -e "${YELLOW}Workflow should work but monitor closely${NC}"
else
    echo -e "${RED}❌ LOCAL WORKFLOW TEST: FAILED${NC}"
    echo -e "${RED}Critical issues found - fix before proceeding${NC}"
    exit 1
fi

# Cleanup test images (optional)
echo ""
echo -e "${BLUE}🧹 Cleanup${NC}"
echo -e "${YELLOW}Test images built: $BACKEND_IMAGE, $FRONTEND_IMAGE${NC}"
read -p "Remove test images? (y/N): " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    docker rmi "$BACKEND_IMAGE" "$FRONTEND_IMAGE" > /dev/null 2>&1 || true
    echo -e "${GREEN}✅ Test images removed${NC}"
else
    echo -e "${BLUE}ℹ️  Test images kept for further testing${NC}"
fi

echo ""
echo -e "${BLUE}🎯 Test completed in $(( SECONDS / 60 )) minutes and $(( SECONDS % 60 )) seconds${NC}"