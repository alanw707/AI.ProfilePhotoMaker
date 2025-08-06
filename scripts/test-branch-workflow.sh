#!/bin/bash
# Branch-based Workflow Testing Script
# Creates test branch and validates GitHub Actions workflow in isolation

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${BLUE}🌿 Branch-based Workflow Testing${NC}"
echo -e "${BLUE}=================================${NC}"
echo ""

# Configuration
TEST_BRANCH="test-workflow-$(date +%Y%m%d-%H%M%S)"
TEST_RESOURCE_GROUP="aiprofilemaker-test-$(date +%m%d)"
PROJECT_ROOT=$(cd "$(dirname "$0")/.." && pwd)

cd "$PROJECT_ROOT"

echo -e "${BLUE}Test Configuration:${NC}"
echo -e "  Branch: ${YELLOW}$TEST_BRANCH${NC}"
echo -e "  Resource Group: ${YELLOW}$TEST_RESOURCE_GROUP${NC}"
echo -e "  Repository: ${YELLOW}$(git remote get-url origin 2>/dev/null || echo 'unknown')${NC}"
echo ""

# Step 1: Verify prerequisites
echo -e "${BLUE}[STEP 1] Verifying Prerequisites${NC}"

# Check if we're in a git repo
if ! git rev-parse --git-dir > /dev/null 2>&1; then
    echo -e "${RED}❌ Not in a git repository${NC}"
    exit 1
fi

# Check if GitHub CLI is available
if command -v gh &> /dev/null; then
    echo -e "${GREEN}✅ GitHub CLI available${NC}"
    
    # Check if authenticated
    if gh auth status > /dev/null 2>&1; then
        echo -e "${GREEN}✅ GitHub CLI authenticated${NC}"
    else
        echo -e "${YELLOW}⚠️  GitHub CLI not authenticated - some features may not work${NC}"
        echo -e "${YELLOW}   Run: gh auth login${NC}"
    fi
else
    echo -e "${YELLOW}⚠️  GitHub CLI not available - manual workflow monitoring required${NC}"
fi

# Check if Azure CLI is logged in
if az account show > /dev/null 2>&1; then
    echo -e "${GREEN}✅ Azure CLI authenticated${NC}"
else
    echo -e "${RED}❌ Azure CLI not authenticated - run 'az login'${NC}"
    exit 1
fi

# Check for uncommitted changes
if ! git diff --quiet || ! git diff --cached --quiet; then
    echo -e "${YELLOW}⚠️  Uncommitted changes detected${NC}"
    read -p "Stash changes and continue? (y/N): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        git stash push -m "Auto-stash for workflow test - $(date)"
        echo -e "${GREEN}✅ Changes stashed${NC}"
    else
        echo -e "${RED}❌ Please commit or stash changes first${NC}"
        exit 1
    fi
fi

echo ""

# Step 2: Create test branch
echo -e "${BLUE}[STEP 2] Creating Test Branch${NC}"

# Get current branch
CURRENT_BRANCH=$(git branch --show-current)
echo -e "${BLUE}Current branch: ${YELLOW}$CURRENT_BRANCH${NC}"

# Create and switch to test branch
git checkout -b "$TEST_BRANCH"
echo -e "${GREEN}✅ Created test branch: $TEST_BRANCH${NC}"

echo ""

# Step 3: Modify workflow for testing
echo -e "${BLUE}[STEP 3] Configuring Test Environment${NC}"

WORKFLOW_FILE=".github/workflows/simple-deploy.yml"
TEST_WORKFLOW_FILE=".github/workflows/test-simple-deploy.yml"

if [ -f "$WORKFLOW_FILE" ]; then
    # Create test-specific workflow
    cp "$WORKFLOW_FILE" "$TEST_WORKFLOW_FILE"
    
    # Modify workflow for testing
    sed -i.bak "s/RESOURCE_GROUP: aiprofilemaker-v1/RESOURCE_GROUP: $TEST_RESOURCE_GROUP/" "$TEST_WORKFLOW_FILE"
    sed -i.bak "s/branches: \[main\]/branches: [$TEST_BRANCH]/" "$TEST_WORKFLOW_FILE"
    sed -i.bak "s/name: 🚀 Simple Deploy/name: 🧪 Test Simple Deploy/" "$TEST_WORKFLOW_FILE"
    
    echo -e "${GREEN}✅ Created test workflow: $TEST_WORKFLOW_FILE${NC}"
    echo -e "${BLUE}   Modified settings:${NC}"
    echo -e "${YELLOW}   - Resource Group: $TEST_RESOURCE_GROUP${NC}"
    echo -e "${YELLOW}   - Trigger Branch: $TEST_BRANCH${NC}"
    
    # Clean up backup file
    rm -f "$TEST_WORKFLOW_FILE.bak"
else
    echo -e "${RED}❌ Workflow file not found: $WORKFLOW_FILE${NC}"
    exit 1
fi

echo ""

# Step 4: Prepare test infrastructure template
echo -e "${BLUE}[STEP 4] Preparing Test Infrastructure${NC}"

# Create test-specific infrastructure template (optional)
TEST_INFRA_FILE="infrastructure/test-simple-deploy.bicep"
if [ -f "infrastructure/simple-deploy.bicep" ]; then
    cp "infrastructure/simple-deploy.bicep" "$TEST_INFRA_FILE"
    
    # Modify default parameters for testing
    sed -i.bak "s/param environment string = 'v1'/param environment string = 'test'/" "$TEST_INFRA_FILE"
    
    echo -e "${GREEN}✅ Created test infrastructure template${NC}"
    rm -f "$TEST_INFRA_FILE.bak"
fi

echo ""

# Step 5: Build and push test images
echo -e "${BLUE}[STEP 5] Building Test Images${NC}"

TEST_TAG="test-$(date +%Y%m%d-%H%M%S)"
echo -e "${YELLOW}Building with tag: $TEST_TAG${NC}"

if ./scripts/build-local.sh "$TEST_TAG"; then
    echo -e "${GREEN}✅ Test images built successfully${NC}"
else
    echo -e "${RED}❌ Image build failed${NC}"
    git checkout "$CURRENT_BRANCH"
    git branch -D "$TEST_BRANCH"
    exit 1
fi

echo ""

# Step 6: Set up test resource group
echo -e "${BLUE}[STEP 6] Setting Up Test Resource Group${NC}"

# Create test resource group
if az group create --name "$TEST_RESOURCE_GROUP" --location "East US 2" --tags Environment=test Application=AIProfileMaker-Test > /dev/null; then
    echo -e "${GREEN}✅ Test resource group created: $TEST_RESOURCE_GROUP${NC}"
else
    echo -e "${RED}❌ Failed to create test resource group${NC}"
    git checkout "$CURRENT_BRANCH"
    git branch -D "$TEST_BRANCH"
    exit 1
fi

# Create minimal ACR for testing (if needed)
ACR_NAME=$(echo "aipmtest$(date +%m%d%H%M)" | tr '[:upper:]' '[:lower:]')
echo -e "${YELLOW}Creating test ACR: $ACR_NAME${NC}"

if az acr create --name "$ACR_NAME" --resource-group "$TEST_RESOURCE_GROUP" --sku Basic --admin-enabled > /dev/null; then
    echo -e "${GREEN}✅ Test ACR created: $ACR_NAME${NC}"
else
    echo -e "${RED}❌ Failed to create test ACR${NC}"
    az group delete --name "$TEST_RESOURCE_GROUP" --yes --no-wait
    git checkout "$CURRENT_BRANCH"
    git branch -D "$TEST_BRANCH"
    exit 1
fi

echo ""

# Step 7: Push test images to test ACR
echo -e "${BLUE}[STEP 7] Pushing Test Images to ACR${NC}"

if ./scripts/push-to-acr.sh "$TEST_TAG" "$TEST_RESOURCE_GROUP"; then
    echo -e "${GREEN}✅ Test images pushed to ACR${NC}"
else
    echo -e "${YELLOW}⚠️  Image push failed - continuing with workflow test${NC}"
    echo -e "${YELLOW}   Deployment may fail, but we'll test the workflow logic${NC}"
fi

echo ""

# Step 8: Commit and push test branch
echo -e "${BLUE}[STEP 8] Pushing Test Branch${NC}"

git add -A
git commit -m "test: workflow validation on test branch

- Created test-specific workflow for $TEST_BRANCH
- Modified resource group to $TEST_RESOURCE_GROUP  
- Built and pushed test images with tag $TEST_TAG
- Safe to delete after testing"

git push -u origin "$TEST_BRANCH"
echo -e "${GREEN}✅ Test branch pushed to remote${NC}"

echo ""

# Step 9: Monitor workflow execution
echo -e "${BLUE}[STEP 9] Monitoring Workflow Execution${NC}"

if command -v gh &> /dev/null && gh auth status > /dev/null 2>&1; then
    echo -e "${YELLOW}Waiting for workflow to start...${NC}"
    sleep 10
    
    # List recent workflow runs
    echo -e "${BLUE}Recent workflow runs:${NC}"
    gh run list --limit 3 --branch "$TEST_BRANCH" || echo "No runs found yet"
    
    echo ""
    echo -e "${BLUE}Monitoring latest workflow run...${NC}"
    
    # Watch the latest run (with timeout)
    timeout 600 gh run watch --exit-status || {
        echo -e "${YELLOW}⚠️  Workflow monitoring timed out or failed${NC}"
        echo -e "${BLUE}Check workflow status manually:${NC}"
        echo -e "${YELLOW}  gh run list --branch $TEST_BRANCH${NC}"
        echo -e "${YELLOW}  gh run view <RUN_ID>${NC}"
    }
else
    echo -e "${YELLOW}GitHub CLI not available - monitor workflow manually${NC}"
    echo -e "${BLUE}Check workflow at: https://github.com/$(gh repo view --json nameWithOwner -q .nameWithOwner)/actions${NC}"
fi

echo ""

# Step 10: Test results and cleanup options
echo -e "${BLUE}[STEP 10] Test Results and Cleanup${NC}"

# Get workflow status if possible
if command -v gh &> /dev/null && gh auth status > /dev/null 2>&1; then
    LATEST_RUN_STATUS=$(gh run list --limit 1 --branch "$TEST_BRANCH" --json status -q '.[0].status' 2>/dev/null || echo "unknown")
    LATEST_RUN_CONCLUSION=$(gh run list --limit 1 --branch "$TEST_BRANCH" --json conclusion -q '.[0].conclusion' 2>/dev/null || echo "unknown")
    
    echo -e "${BLUE}Latest workflow run:${NC}"
    echo -e "  Status: ${YELLOW}$LATEST_RUN_STATUS${NC}"
    echo -e "  Conclusion: ${YELLOW}$LATEST_RUN_CONCLUSION${NC}"
    
    if [ "$LATEST_RUN_CONCLUSION" = "success" ]; then
        echo -e "${GREEN}🎉 WORKFLOW TEST: PASSED${NC}"
        echo -e "${GREEN}✅ Test workflow completed successfully${NC}"
    elif [ "$LATEST_RUN_CONCLUSION" = "failure" ]; then
        echo -e "${RED}❌ WORKFLOW TEST: FAILED${NC}"
        echo -e "${RED}   Review workflow logs for details${NC}"
    else
        echo -e "${YELLOW}⚠️  WORKFLOW TEST: INCONCLUSIVE${NC}"
        echo -e "${YELLOW}   Manual review required${NC}"
    fi
fi

echo ""

# Cleanup options
echo -e "${BLUE}🧹 Cleanup Options${NC}"
echo ""
echo -e "${YELLOW}What would you like to do?${NC}"
echo "1. Clean up everything (delete test resources and branch)"
echo "2. Keep test resources, delete branch" 
echo "3. Keep everything for further investigation"
echo "4. Just switch back to main branch (manual cleanup later)"

read -p "Choice (1-4): " -n 1 -r
echo

case $REPLY in
    1)
        echo -e "${BLUE}🧹 Cleaning up everything...${NC}"
        
        # Delete test resource group
        echo -e "${YELLOW}Deleting test resource group (background)...${NC}"
        az group delete --name "$TEST_RESOURCE_GROUP" --yes --no-wait
        
        # Switch back to original branch
        git checkout "$CURRENT_BRANCH"
        
        # Delete test branch (local and remote)
        git branch -D "$TEST_BRANCH"
        git push origin --delete "$TEST_BRANCH"
        
        echo -e "${GREEN}✅ Full cleanup completed${NC}"
        ;;
    2)
        echo -e "${BLUE}🧹 Deleting branch, keeping resources...${NC}"
        
        git checkout "$CURRENT_BRANCH"
        git branch -D "$TEST_BRANCH"
        git push origin --delete "$TEST_BRANCH"
        
        echo -e "${GREEN}✅ Branch deleted${NC}"
        echo -e "${YELLOW}⚠️  Test resources remain: $TEST_RESOURCE_GROUP${NC}"
        echo -e "${BLUE}   Clean up manually: az group delete --name $TEST_RESOURCE_GROUP${NC}"
        ;;
    3)
        echo -e "${BLUE}📋 Keeping everything for investigation${NC}"
        
        git checkout "$CURRENT_BRANCH"
        
        echo -e "${YELLOW}Resources available for investigation:${NC}"
        echo -e "  Branch: $TEST_BRANCH"
        echo -e "  Resource Group: $TEST_RESOURCE_GROUP"
        echo -e "  ACR: $ACR_NAME"
        echo -e "  Images: aiprofilemaker-api:$TEST_TAG, aiprofilemaker-web:$TEST_TAG"
        ;;
    4)
        echo -e "${BLUE}📋 Switching back to main branch${NC}"
        
        git checkout "$CURRENT_BRANCH"
        
        echo -e "${YELLOW}Manual cleanup required:${NC}"
        echo -e "  Delete branch: git branch -D $TEST_BRANCH && git push origin --delete $TEST_BRANCH"
        echo -e "  Delete resources: az group delete --name $TEST_RESOURCE_GROUP"
        ;;
    *)
        echo -e "${YELLOW}⚠️  Invalid choice - no cleanup performed${NC}"
        git checkout "$CURRENT_BRANCH"
        ;;
esac

echo ""
echo -e "${BLUE}🎯 Branch workflow test completed${NC}"

# Summary
echo -e "${BLUE}📊 Test Summary:${NC}"
echo -e "  Test Branch: ${YELLOW}$TEST_BRANCH${NC}"
echo -e "  Resource Group: ${YELLOW}$TEST_RESOURCE_GROUP${NC}"  
echo -e "  Image Tag: ${YELLOW}$TEST_TAG${NC}"
if [ -n "${LATEST_RUN_CONCLUSION:-}" ]; then
    echo -e "  Workflow Result: ${YELLOW}$LATEST_RUN_CONCLUSION${NC}"
fi

echo ""
echo -e "${BLUE}Next Steps (if test passed):${NC}"
echo -e "  1. ${YELLOW}Build production images:${NC} ./scripts/build-local.sh"
echo -e "  2. ${YELLOW}Push to production ACR:${NC} ./scripts/push-to-acr.sh"
echo -e "  3. ${YELLOW}Deploy to production:${NC} git push origin main"