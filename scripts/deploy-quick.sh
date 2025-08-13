#!/bin/bash

# Quick Deployment Wrapper Script
# Provides simple shortcuts for common deployment scenarios

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MAIN_DEPLOY_SCRIPT="$SCRIPT_DIR/deploy-with-unified-secrets.sh"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
BOLD='\033[1m'
NC='\033[0m'

show_usage() {
    echo -e "${BLUE}${BOLD}Quick Deployment Shortcuts${NC}"
    echo ""
    echo "Usage: $0 [SCENARIO]"
    echo ""
    echo "Available scenarios:"
    echo -e "  ${GREEN}full${NC}           Complete deployment with all validations"
    echo -e "  ${GREEN}quick${NC}          Skip tests, validate secrets"
    echo -e "  ${GREEN}rebuild${NC}        Force rebuild images + deploy"
    echo -e "  ${GREEN}test${NC}           Dry-run to test deployment process"
    echo -e "  ${GREEN}rollback${NC}       Rollback to previous deployment"
    echo -e "  ${GREEN}validate${NC}       Only run secrets validation"
    echo ""
    echo "Examples:"
    echo "  $0 full      # Complete deployment (recommended)"
    echo "  $0 quick     # Fast deployment for urgent fixes"
    echo "  $0 rebuild   # When Docker images changed"
    echo "  $0 test      # Test deployment process safely"
}

case "${1:-help}" in
    full)
        echo -e "${BLUE}🚀 Running full deployment with all validations...${NC}"
        exec "$MAIN_DEPLOY_SCRIPT"
        ;;
    quick)
        echo -e "${YELLOW}⚡ Running quick deployment (skip tests)...${NC}"
        exec "$MAIN_DEPLOY_SCRIPT" --skip-tests
        ;;
    rebuild)
        echo -e "${BLUE}🔄 Force rebuild and deploy...${NC}"
        exec "$MAIN_DEPLOY_SCRIPT" --force-rebuild
        ;;
    test)
        echo -e "${CYAN}🧪 Running deployment test (dry-run)...${NC}"
        exec "$MAIN_DEPLOY_SCRIPT" --dry-run
        ;;
    rollback)
        echo -e "${RED}🔄 Rolling back deployment...${NC}"
        exec "$MAIN_DEPLOY_SCRIPT" --rollback
        ;;
    validate)
        echo -e "${GREEN}🔍 Running secrets validation only...${NC}"
        exec "$SCRIPT_DIR/validate-secrets.sh"
        ;;
    help|--help|-h)
        show_usage
        ;;
    *)
        echo -e "${RED}❌ Unknown scenario: $1${NC}"
        echo ""
        show_usage
        exit 1
        ;;
esac