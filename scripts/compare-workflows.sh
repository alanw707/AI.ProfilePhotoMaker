#!/bin/bash
# Workflow Performance Comparison Script
# Compares local build vs CI build workflows

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${BLUE}📊 Workflow Performance Comparison${NC}"
echo -e "${BLUE}===================================${NC}"
echo ""

# Check GitHub CLI
if ! command -v gh &> /dev/null; then
    echo -e "${RED}❌ GitHub CLI required for workflow comparison${NC}"
    exit 1
fi

# Function to get workflow run stats
get_workflow_stats() {
    local workflow_name="$1"
    local runs_count="${2:-5}"
    
    echo -e "${BLUE}📈 ${workflow_name} (last ${runs_count} runs):${NC}"
    
    # Get recent successful runs
    gh run list --workflow="$workflow_name" --status=completed --limit="$runs_count" --json conclusion,createdAt,updatedAt,displayTitle | jq -r '.[] | select(.conclusion == "success") | [.displayTitle, .createdAt, .updatedAt] | @tsv' | while IFS=$'\t' read -r title created updated; do
        
        # Calculate duration
        created_epoch=$(date -d "$created" +%s 2>/dev/null || gdate -d "$created" +%s 2>/dev/null || echo "0")
        updated_epoch=$(date -d "$updated" +%s 2>/dev/null || gdate -d "$updated" +%s 2>/dev/null || echo "0")
        
        if [ "$created_epoch" -gt 0 ] && [ "$updated_epoch" -gt 0 ]; then
            duration=$((updated_epoch - created_epoch))
            minutes=$((duration / 60))
            seconds=$((duration % 60))
            
            printf "  ⏱️  %2dm %2ds - %s\n" "$minutes" "$seconds" "$title"
        else
            echo "  📝 $title (duration calc failed)"
        fi
    done
    
    echo ""
}

# Function to measure local build time
measure_local_build() {
    echo -e "${BLUE}⏱️ Measuring local build time...${NC}"
    
    local start_time=$(date +%s)
    local test_tag="perf-test-$(date +%H%M%S)"
    
    # Build locally (suppress output for cleaner display)
    if ./scripts/build-local.sh "$test_tag" > /dev/null 2>&1; then
        local end_time=$(date +%s)
        local duration=$((end_time - start_time))
        local minutes=$((duration / 60))
        local seconds=$((duration % 60))
        
        echo -e "${GREEN}✅ Local build completed in ${minutes}m ${seconds}s${NC}"
        
        # Cleanup
        docker rmi "aiprofilemaker-api:$test_tag" "aiprofilemaker-web:$test_tag" > /dev/null 2>&1 || true
        
        return $duration
    else
        echo -e "${RED}❌ Local build failed${NC}"
        return 0
    fi
}

# Compare workflows
echo -e "${YELLOW}Analyzing workflow performance...${NC}"
echo ""

# PowerShell Deploy (CI Build)
get_workflow_stats "🚀 V1 PowerShell Deploy" 5

# Simple Deploy (Local Build) 
get_workflow_stats "🚀 Simple Deploy (Local Build)" 5

# Test Simple Deploy
get_workflow_stats "🧪 Test Simple Deploy (Local Build)" 3

# Measure local build time
echo -e "${BLUE}🏠 Local Build Performance:${NC}"
LOCAL_DURATION=$(measure_local_build)

echo ""
echo -e "${BLUE}📋 Performance Analysis:${NC}"
echo "----------------------------------------"

echo -e "${YELLOW}Local Build Advantages:${NC}"
echo "• Faster feedback loop for build issues"
echo "• No GitHub Actions minutes consumed for builds"
echo "• Better developer experience (build before push)"
echo "• Parallel development (multiple devs can build simultaneously)"

echo ""
echo -e "${YELLOW}CI Build Advantages:${NC}"
echo "• Consistent build environment"
echo "• No local Docker/resource requirements"
echo "• Automatic builds on every push"
echo "• Build logs stored in GitHub"

echo ""
echo -e "${BLUE}💡 Recommendations:${NC}"
echo "• Use local build for development iterations"
echo "• Use CI build for release/production deployments"
echo "• Consider hybrid approach based on change size"

echo ""
echo -e "${GREEN}✅ Performance comparison complete${NC}"