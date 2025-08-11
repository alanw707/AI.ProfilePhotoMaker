#!/bin/bash

# AI Profile Photo Maker - Deployment Validation Script
# Simple wrapper for the Node.js validation script
# 
# Usage:
#   ./scripts/validate-deployment.sh
#   ./scripts/validate-deployment.sh --headless
#   ./scripts/validate-deployment.sh --wait 60

set -e  # Exit on any error

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default options
WAIT_TIME=0
HEADLESS=true
RETRIES=3

# Parse command line arguments
while [[ $# -gt 0 ]]; do
  case $1 in
    --wait)
      WAIT_TIME="$2"
      shift 2
      ;;
    --headless)
      HEADLESS=true
      shift
      ;;
    --headed)
      HEADLESS=false
      shift
      ;;
    --retries)
      RETRIES="$2"
      shift 2
      ;;
    --help)
      echo "Usage: $0 [OPTIONS]"
      echo "Options:"
      echo "  --wait SECONDS    Wait before starting validation (default: 0)"
      echo "  --headless        Run in headless mode (default)"
      echo "  --headed          Run with visible browser"
      echo "  --retries COUNT   Number of retry attempts (default: 3)"
      echo "  --help            Show this help message"
      exit 0
      ;;
    *)
      echo "Unknown option $1"
      exit 1
      ;;
  esac
done

echo -e "${BLUE}🚀 AI Profile Photo Maker - Deployment Validation${NC}"
echo "=================================================="
echo ""
echo "📋 Configuration:"
echo "   Project Root: $PROJECT_ROOT"
echo "   Wait Time: ${WAIT_TIME}s"
echo "   Headless Mode: $HEADLESS"
echo "   Max Retries: $RETRIES"
echo ""

# Wait if specified
if [ "$WAIT_TIME" -gt 0 ]; then
  echo -e "${YELLOW}⏳ Waiting ${WAIT_TIME} seconds for deployment to stabilize...${NC}"
  sleep "$WAIT_TIME"
fi

# Check if Node.js is available
if ! command -v node &> /dev/null; then
  echo -e "${RED}❌ Node.js is not installed or not in PATH${NC}"
  echo "Please install Node.js to run deployment validation"
  exit 1
fi

# Create temporary package.json and install Playwright if needed
if ! node -e "require('playwright')" 2>/dev/null; then
  echo -e "${YELLOW}⚠️  Playwright not found. Attempting to install...${NC}"
  cd "$SCRIPT_DIR"
  if [ ! -f "package.json" ]; then
    echo '{"name": "deployment-validation", "private": true}' > package.json
  fi
  npm install playwright
  npx playwright install chromium
fi

# Function to run validation with retries
run_validation() {
  local attempt=1
  local max_attempts=$1
  
  while [ $attempt -le $max_attempts ]; do
    echo -e "${BLUE}🧪 Validation Attempt $attempt of $max_attempts${NC}"
    echo "----------------------------------------"
    
    # Run the validation script
    if node "$SCRIPT_DIR/validate-deployment.js"; then
      echo ""
      echo -e "${GREEN}✅ Deployment validation PASSED${NC}"
      echo -e "${GREEN}🎉 All systems operational!${NC}"
      return 0
    else
      echo ""
      echo -e "${RED}❌ Validation attempt $attempt failed${NC}"
      
      if [ $attempt -lt $max_attempts ]; then
        local wait_time=$((attempt * 10))
        echo -e "${YELLOW}⏳ Waiting ${wait_time} seconds before retry...${NC}"
        sleep $wait_time
      fi
    fi
    
    attempt=$((attempt + 1))
  done
  
  echo ""
  echo -e "${RED}💥 Deployment validation FAILED after $max_attempts attempts${NC}"
  echo -e "${RED}🚨 Deployment may have issues that require attention${NC}"
  return 1
}

# Change to scripts directory for validation
cd "$SCRIPT_DIR"

# Run validation with retries
if run_validation $RETRIES; then
  echo ""
  echo -e "${GREEN}🎯 Deployment validation completed successfully!${NC}"
  echo "📄 Check validation-report.json for detailed results"
  exit 0
else
  echo ""
  echo -e "${RED}🚨 Deployment validation failed!${NC}"
  echo "📄 Check validation-report.json for detailed error information"
  echo ""
  echo "💡 Common fixes:"
  echo "   • Ensure custom domains are properly configured in Azure"
  echo "   • Check DNS propagation (may take up to 24 hours)"
  echo "   • Verify SSL certificates are bound correctly"
  echo "   • Confirm Container Apps are running and healthy"
  exit 1
fi