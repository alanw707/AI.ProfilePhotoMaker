#!/bin/bash
# Manual Workflow Trigger Script
# Provides CLI-based control over GitHub Actions workflows

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${BLUE}🚀 Manual Workflow Trigger${NC}"
echo -e "${BLUE}==========================${NC}"
echo ""

# Configuration
WORKFLOW_NAME="${1:-simple-deploy.yml}"
BRANCH="${2:-main}"
PROJECT_ROOT=$(cd "$(dirname "$0")/.." && pwd)

cd "$PROJECT_ROOT"

echo -e "${BLUE}Configuration:${NC}"
echo -e "  Workflow: ${YELLOW}$WORKFLOW_NAME${NC}"
echo -e "  Branch: ${YELLOW}$BRANCH${NC}"
echo -e "  Repository: ${YELLOW}$(gh repo view --json nameWithOwner -q .nameWithOwner 2>/dev/null || echo 'unknown')${NC}"
echo ""

# Function to show usage
show_usage() {
    echo -e "${BLUE}Usage:${NC}"
    echo -e "  $0 [workflow-name] [branch] [action]"
    echo ""
    echo -e "${BLUE}Examples:${NC}"
    echo -e "  $0 simple-deploy.yml main trigger"
    echo -e "  $0 simple-deploy.yml main monitor"
    echo -e "  $0 simple-deploy.yml main status"
    echo ""
    echo -e "${BLUE}Actions:${NC}"
    echo -e "  trigger  - Trigger workflow and monitor (default)"
    echo -e "  monitor  - Monitor latest workflow run"
    echo -e "  status   - Show workflow status only"
    echo -e "  list     - List recent workflow runs"
    echo -e "  cancel   - Cancel running workflows"
    echo ""
}

# Check prerequisites
echo -e "${BLUE}[STEP 1] Checking Prerequisites${NC}"

# Check if GitHub CLI is available
if ! command -v gh &> /dev/null; then
    echo -e "${RED}❌ GitHub CLI not installed${NC}"
    echo -e "${YELLOW}Install: https://cli.github.com${NC}"
    exit 1
fi

# Check if authenticated
if ! gh auth status > /dev/null 2>&1; then
    echo -e "${RED}❌ GitHub CLI not authenticated${NC}"
    echo -e "${YELLOW}Run: gh auth login${NC}"
    exit 1
fi

echo -e "${GREEN}✅ GitHub CLI ready${NC}"

# Check if we're in a git repository
if ! git rev-parse --git-dir > /dev/null 2>&1; then
    echo -e "${RED}❌ Not in a git repository${NC}"
    exit 1
fi

echo -e "${GREEN}✅ Git repository detected${NC}"

# Check if workflow file exists
WORKFLOW_PATH=".github/workflows/$WORKFLOW_NAME"
if [ ! -f "$WORKFLOW_PATH" ]; then
    echo -e "${RED}❌ Workflow file not found: $WORKFLOW_PATH${NC}"
    echo ""
    echo -e "${BLUE}Available workflows:${NC}"
    find .github/workflows -name "*.yml" -o -name "*.yaml" | sed 's|.github/workflows/||' || echo "None found"
    exit 1
fi

echo -e "${GREEN}✅ Workflow file exists${NC}"
echo ""

# Determine action
ACTION="${3:-trigger}"

case "$ACTION" in
    "trigger")
        echo -e "${BLUE}[STEP 2] Triggering Workflow${NC}"
        
        # Check if branch exists
        if git ls-remote --heads origin "$BRANCH" | grep -q "$BRANCH"; then
            echo -e "${GREEN}✅ Branch '$BRANCH' exists on remote${NC}"
        else
            echo -e "${RED}❌ Branch '$BRANCH' not found on remote${NC}"
            echo ""
            echo -e "${BLUE}Available branches:${NC}"
            git ls-remote --heads origin | sed 's|.*refs/heads/||'
            exit 1
        fi
        
        # Trigger workflow
        echo -e "${YELLOW}Triggering workflow: $WORKFLOW_NAME on $BRANCH${NC}"
        if gh workflow run "$WORKFLOW_NAME" --ref "$BRANCH"; then
            echo -e "${GREEN}✅ Workflow triggered successfully${NC}"
        else
            echo -e "${RED}❌ Failed to trigger workflow${NC}"
            exit 1
        fi
        
        echo ""
        echo -e "${BLUE}[STEP 3] Monitoring Workflow${NC}"
        
        # Wait for workflow to start
        echo -e "${YELLOW}Waiting for workflow to start...${NC}"
        sleep 10
        
        # Get the latest run ID
        RUN_ID=$(gh run list --workflow="$WORKFLOW_NAME" --branch="$BRANCH" --limit=1 --json databaseId -q '.[0].databaseId' 2>/dev/null || echo "")
        
        if [ -n "$RUN_ID" ]; then
            echo -e "${GREEN}✅ Workflow started (ID: $RUN_ID)${NC}"
            echo -e "${BLUE}URL: https://github.com/$(gh repo view --json nameWithOwner -q .nameWithOwner)/actions/runs/$RUN_ID${NC}"
            echo ""
            
            # Monitor the run
            echo -e "${YELLOW}Monitoring workflow execution...${NC}"
            if timeout 900 gh run watch "$RUN_ID" --exit-status; then
                echo -e "${GREEN}🎉 Workflow completed successfully!${NC}"
            else
                echo -e "${RED}❌ Workflow failed or timed out${NC}"
                
                # Show failure details
                echo -e "${BLUE}Getting failure details...${NC}"
                gh run view "$RUN_ID" --log-failed || echo "Could not get failure logs"
                exit 1
            fi
        else
            echo -e "${YELLOW}⚠️  Could not get workflow run ID - check manually${NC}"
        fi
        ;;
        
    "monitor")
        echo -e "${BLUE}[STEP 2] Monitoring Latest Workflow${NC}"
        
        # Get the latest run
        RUN_ID=$(gh run list --workflow="$WORKFLOW_NAME" --branch="$BRANCH" --limit=1 --json databaseId -q '.[0].databaseId' 2>/dev/null || echo "")
        
        if [ -n "$RUN_ID" ]; then
            echo -e "${BLUE}Monitoring run ID: $RUN_ID${NC}"
            if timeout 900 gh run watch "$RUN_ID" --exit-status; then
                echo -e "${GREEN}🎉 Workflow completed successfully!${NC}"
            else
                echo -e "${RED}❌ Workflow failed or timed out${NC}"
                exit 1
            fi
        else
            echo -e "${YELLOW}⚠️  No workflow runs found for $WORKFLOW_NAME on $BRANCH${NC}"
        fi
        ;;
        
    "status")
        echo -e "${BLUE}[STEP 2] Workflow Status${NC}"
        
        echo -e "${BLUE}Recent runs for $WORKFLOW_NAME on $BRANCH:${NC}"
        if gh run list --workflow="$WORKFLOW_NAME" --branch="$BRANCH" --limit=5; then
            echo ""
            # Get latest run details
            LATEST_RUN=$(gh run list --workflow="$WORKFLOW_NAME" --branch="$BRANCH" --limit=1 --json status,conclusion,createdAt,url -q '.[0]' 2>/dev/null || echo "{}")
            
            if [ "$LATEST_RUN" != "{}" ]; then
                STATUS=$(echo "$LATEST_RUN" | jq -r '.status // "unknown"')
                CONCLUSION=$(echo "$LATEST_RUN" | jq -r '.conclusion // "none"')
                CREATED=$(echo "$LATEST_RUN" | jq -r '.createdAt // "unknown"')
                URL=$(echo "$LATEST_RUN" | jq -r '.url // ""')
                
                echo -e "${BLUE}Latest Run Details:${NC}"
                echo -e "  Status: ${YELLOW}$STATUS${NC}"
                echo -e "  Conclusion: ${YELLOW}$CONCLUSION${NC}"
                echo -e "  Created: ${YELLOW}$CREATED${NC}"
                [ -n "$URL" ] && echo -e "  URL: ${BLUE}$URL${NC}"
            fi
        else
            echo -e "${YELLOW}⚠️  No workflow runs found${NC}"
        fi
        ;;
        
    "list")
        echo -e "${BLUE}[STEP 2] Listing Workflow Runs${NC}"
        
        echo -e "${BLUE}All workflows on $BRANCH:${NC}"
        gh run list --branch="$BRANCH" --limit=10
        
        echo ""
        echo -e "${BLUE}Specific workflow ($WORKFLOW_NAME) on $BRANCH:${NC}"
        gh run list --workflow="$WORKFLOW_NAME" --branch="$BRANCH" --limit=10
        ;;
        
    "cancel")
        echo -e "${BLUE}[STEP 2] Canceling Running Workflows${NC}"
        
        # Get running workflows
        RUNNING_RUNS=$(gh run list --workflow="$WORKFLOW_NAME" --branch="$BRANCH" --status=in_progress --json databaseId -q '.[].databaseId' 2>/dev/null || echo "")
        
        if [ -n "$RUNNING_RUNS" ]; then
            echo -e "${YELLOW}Found running workflows:${NC}"
            echo "$RUNNING_RUNS"
            
            read -p "Cancel all running workflows? (y/N): " -n 1 -r
            echo
            
            if [[ $REPLY =~ ^[Yy]$ ]]; then
                for run_id in $RUNNING_RUNS; do
                    echo -e "${YELLOW}Canceling run: $run_id${NC}"
                    if gh run cancel "$run_id"; then
                        echo -e "${GREEN}✅ Canceled run $run_id${NC}"
                    else
                        echo -e "${RED}❌ Failed to cancel run $run_id${NC}"
                    fi
                done
            else
                echo -e "${BLUE}ℹ️  No workflows canceled${NC}"
            fi
        else
            echo -e "${GREEN}✅ No running workflows found${NC}"
        fi
        ;;
        
    *)
        echo -e "${RED}❌ Invalid action: $ACTION${NC}"
        echo ""
        show_usage
        exit 1
        ;;
esac

echo ""
echo -e "${BLUE}🎯 Workflow operation completed${NC}"

# Show helpful commands
echo ""
echo -e "${BLUE}Helpful Commands:${NC}"
echo -e "  Monitor latest run: ${YELLOW}./scripts/trigger-workflow.sh $WORKFLOW_NAME $BRANCH monitor${NC}"
echo -e "  Check status: ${YELLOW}./scripts/trigger-workflow.sh $WORKFLOW_NAME $BRANCH status${NC}"
echo -e "  List recent runs: ${YELLOW}./scripts/trigger-workflow.sh $WORKFLOW_NAME $BRANCH list${NC}"
echo -e "  View run in browser: ${YELLOW}gh run view <RUN_ID> --web${NC}"