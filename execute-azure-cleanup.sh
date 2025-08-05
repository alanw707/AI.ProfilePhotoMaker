#!/bin/bash

# Azure Resource Cleanup - Master Execution Script
# Orchestrates the complete cleanup strategy for V1 deployment preparation

set -e

echo "🚀 Azure Resource Cleanup - Master Execution"
echo "============================================="
echo ""

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPTS_DIR="$SCRIPT_DIR/scripts"
MASTER_LOG_DIR="azure-cleanup-backup/$(date +%Y%m%d-%H%M%S)-master-cleanup"

# Create master log directory
mkdir -p "$MASTER_LOG_DIR"

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Logging functions
log_phase() {
    echo ""
    echo -e "${BLUE}🔹 PHASE: $1${NC}"
    echo "$(date): PHASE: $1" >> "$MASTER_LOG_DIR/master-execution.log"
}

log_success() {
    echo -e "${GREEN}✅ $1${NC}"
    echo "$(date): SUCCESS: $1" >> "$MASTER_LOG_DIR/master-execution.log"
}

log_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
    echo "$(date): WARNING: $1" >> "$MASTER_LOG_DIR/master-execution.log"
}

log_error() {
    echo -e "${RED}❌ $1${NC}"
    echo "$(date): ERROR: $1" >> "$MASTER_LOG_DIR/master-execution.log"
}

log_info() {
    echo -e "ℹ️  $1"
    echo "$(date): INFO: $1" >> "$MASTER_LOG_DIR/master-execution.log"
}

# Script execution function
execute_script() {
    local script_path="$1"
    local script_name="$(basename "$script_path")"
    local phase_name="$2"
    
    if [ ! -f "$script_path" ]; then
        log_error "Script not found: $script_path"
        return 1
    fi
    
    log_info "Executing: $script_name"
    chmod +x "$script_path"
    
    # Execute script and capture output
    if "$script_path" > "$MASTER_LOG_DIR/${script_name}.log" 2>&1; then
        log_success "$phase_name completed successfully"
        return 0
    else
        log_error "$phase_name failed - check logs"
        log_info "Error log: $MASTER_LOG_DIR/${script_name}.log"
        return 1
    fi
}

# Interactive confirmation
confirm_execution() {
    echo ""
    echo "📋 Cleanup Strategy Overview:"
    echo "  Phase 1: Legacy staging environment removal"
    echo "  Phase 2: V1 environment assessment"
    echo "  Phase 3: Valuable resources backup"
    echo "  Phase 4: Selective V1 cleanup"
    echo "  Phase 5: Pre-deployment validation"
    echo ""
    echo "⚠️  WARNING: This process will modify your Azure environment!"
    echo "   • Legacy staging resources will be deleted"
    echo "   • V1 resources may be selectively removed"
    echo "   • Backups will be created for valuable resources"
    echo ""
    
    read -p "🤔 Do you want to proceed with the cleanup? (y/N): " confirm
    if [[ ! "$confirm" =~ ^[Yy]$ ]]; then
        echo "❌ Cleanup cancelled by user"
        exit 1
    fi
    
    echo ""
    read -p "🤔 Run in interactive mode (recommended)? (Y/n): " interactive
    if [[ "$interactive" =~ ^[Nn]$ ]]; then
        INTERACTIVE_MODE=false
        log_warning "Running in non-interactive mode - all phases will execute automatically"
    else
        INTERACTIVE_MODE=true
        log_info "Running in interactive mode - you'll be prompted between phases"
    fi
}

# Phase execution with confirmation
execute_phase() {
    local script_path="$1"
    local phase_name="$2"
    local description="$3"
    
    log_phase "$phase_name"
    log_info "$description"
    
    if [ "$INTERACTIVE_MODE" = true ]; then
        echo ""
        read -p "🤔 Execute $phase_name? (Y/n): " proceed
        if [[ "$proceed" =~ ^[Nn]$ ]]; then
            log_warning "$phase_name skipped by user"
            return 0
        fi
    fi
    
    execute_script "$script_path" "$phase_name"
    return $?
}

# Initialize execution
echo "📋 Master Cleanup Configuration:"
echo "  Script Directory: $SCRIPTS_DIR"
echo "  Master Log Directory: $MASTER_LOG_DIR"
echo "  Timestamp: $(date)"
echo ""

# Verify scripts exist
REQUIRED_SCRIPTS=(
    "$SCRIPTS_DIR/01-staging-cleanup.sh"
    "$SCRIPTS_DIR/02-v1-assessment.sh"
    "$SCRIPTS_DIR/03-backup-valuable-resources.sh"
    "$SCRIPTS_DIR/04-selective-v1-cleanup.sh"
    "$SCRIPTS_DIR/05-pre-deployment-validation.sh"
)

log_info "Verifying cleanup scripts..."
for script in "${REQUIRED_SCRIPTS[@]}"; do
    if [ -f "$script" ]; then
        log_success "Found: $(basename "$script")"
    else
        log_error "Missing: $(basename "$script")"
        echo ""
        echo "❌ Required cleanup scripts are missing!"
        echo "   Please ensure all scripts are present in: $SCRIPTS_DIR"
        exit 1
    fi
done

# Pre-execution checks
log_info "Performing pre-execution checks..."

# Check Azure CLI
if ! command -v az &> /dev/null; then
    log_error "Azure CLI not found - please install Azure CLI first"
    echo "   Install: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
fi

# Check authentication
if ! az account show &> /dev/null; then
    log_error "Not authenticated with Azure - please run 'az login' first"
    exit 1
fi

SUBSCRIPTION_NAME=$(az account show --query "name" -o tsv)
log_success "Azure CLI authenticated with subscription: $SUBSCRIPTION_NAME"

# Get user confirmation
confirm_execution

# Begin execution phases
echo ""
echo "🚀 Starting Azure Cleanup Execution..."
echo "======================================"

OVERALL_SUCCESS=true

# Phase 1: Legacy Staging Cleanup
if ! execute_phase "$SCRIPTS_DIR/01-staging-cleanup.sh" "Phase 1" "Remove legacy staging environment (rg-aiprofilemaker-staging)"; then
    log_warning "Phase 1 failed - continuing with remaining phases"
    # Don't fail overall execution for staging cleanup - it might not exist
fi

# Phase 2: V1 Assessment  
if ! execute_phase "$SCRIPTS_DIR/02-v1-assessment.sh" "Phase 2" "Assess current V1 environment resources"; then
    log_error "Phase 2 failed - V1 assessment required for safe cleanup"
    OVERALL_SUCCESS=false
fi

# Phase 3: Backup Valuable Resources
if [ "$OVERALL_SUCCESS" = true ]; then
    if ! execute_phase "$SCRIPTS_DIR/03-backup-valuable-resources.sh" "Phase 3" "Create backups of valuable V1 resources"; then
        log_warning "Phase 3 failed - backup recommended before cleanup"
        # Continue but warn user
        echo ""
        read -p "🤔 Continue without complete backup? (y/N): " continue_without_backup
        if [[ ! "$continue_without_backup" =~ ^[Yy]$ ]]; then
            log_error "Execution halted - backup required for safe cleanup"
            OVERALL_SUCCESS=false
        fi
    fi
fi

# Phase 4: Selective V1 Cleanup
if [ "$OVERALL_SUCCESS" = true ]; then
    if ! execute_phase "$SCRIPTS_DIR/04-selective-v1-cleanup.sh" "Phase 4" "Selective cleanup of V1 environment"; then
        log_error "Phase 4 failed - selective cleanup encountered issues"
        OVERALL_SUCCESS=false
    fi
fi

# Phase 5: Pre-deployment Validation
if [ "$OVERALL_SUCCESS" = true ]; then
    if ! execute_phase "$SCRIPTS_DIR/05-pre-deployment-validation.sh" "Phase 5" "Validate environment readiness for deployment"; then
        log_warning "Phase 5 failed - validation issues detected"
        log_info "Review validation results before proceeding with deployment"
    fi
fi

# Generate master execution report
echo ""
log_phase "Execution Report Generation"

cat > "$MASTER_LOG_DIR/master-execution-report.md" << EOF
# Azure Cleanup Master Execution Report

## Execution Summary
- Date: $(date)
- Overall Status: $(if [ "$OVERALL_SUCCESS" = true ]; then echo "SUCCESS ✅"; else echo "FAILED ❌"; fi)
- Master Log Directory: $MASTER_LOG_DIR

## Phase Results
1. **Legacy Staging Cleanup**: $(grep -q "SUCCESS.*Phase 1" "$MASTER_LOG_DIR/master-execution.log" && echo "✅ SUCCESS" || echo "⚠️ WARNING/SKIPPED")
2. **V1 Assessment**: $(grep -q "SUCCESS.*Phase 2" "$MASTER_LOG_DIR/master-execution.log" && echo "✅ SUCCESS" || echo "❌ FAILED")
3. **Resource Backup**: $(grep -q "SUCCESS.*Phase 3" "$MASTER_LOG_DIR/master-execution.log" && echo "✅ SUCCESS" || echo "⚠️ WARNING")
4. **Selective Cleanup**: $(grep -q "SUCCESS.*Phase 4" "$MASTER_LOG_DIR/master-execution.log" && echo "✅ SUCCESS" || echo "❌ FAILED")
5. **Pre-deployment Validation**: $(grep -q "SUCCESS.*Phase 5" "$MASTER_LOG_DIR/master-execution.log" && echo "✅ SUCCESS" || echo "⚠️ WARNING")

## Detailed Logs
$(for script in "${REQUIRED_SCRIPTS[@]}"; do
    script_name="$(basename "$script")"
    if [ -f "$MASTER_LOG_DIR/${script_name}.log" ]; then
        echo "- $script_name: $MASTER_LOG_DIR/${script_name}.log"
    fi
done)

## Next Steps
$(if [ "$OVERALL_SUCCESS" = true ]; then
    echo "### Deployment Ready 🚀"
    echo ""
    echo "Your Azure environment has been successfully cleaned and prepared for V1 deployment."
    echo ""
    echo "**Recommended Actions:**"
    echo "1. Review phase logs for any warnings"
    echo "2. Verify GitHub repository secrets are configured"
    echo "3. Trigger GitHub Actions deployment workflow"
    echo "4. Monitor deployment progress"
    echo ""
    echo "**GitHub Actions Deployment:**"
    echo "\`\`\`bash"
    echo "# Via GitHub CLI (if available)"
    echo "gh workflow run '🚀 V1 Deploy' --ref main"
    echo ""
    echo "# Or via GitHub web interface:"
    echo "# https://github.com/YOUR_USERNAME/YOUR_REPO/actions/workflows/simple-deploy.yml"
    echo "\`\`\`"
else
    echo "### Issues Detected 🚨"
    echo ""
    echo "Critical issues were encountered during cleanup that must be resolved."
    echo ""
    echo "**Required Actions:**"
    echo "1. Review failed phase logs in detail"
    echo "2. Resolve critical issues manually"
    echo "3. Re-run specific phases or full cleanup as needed"
    echo "4. Ensure pre-deployment validation passes before deployment"
    echo ""
    echo "**Manual Issue Resolution:**"
    echo "- Check individual phase logs for specific error details"
    echo "- Verify Azure permissions and connectivity"
    echo "- Resolve resource conflicts manually if needed"
    echo "- Re-run validation script to confirm readiness"
fi)

## Support and Troubleshooting
- **Master Execution Log**: $MASTER_LOG_DIR/master-execution.log
- **Individual Phase Logs**: Check $MASTER_LOG_DIR/ directory
- **Azure Portal**: Monitor resources in Azure Portal during and after cleanup
- **GitHub Actions**: Check deployment workflow results in repository

---
*Generated by Azure Cleanup Master Execution Script*
EOF

# Display final results
echo ""
echo "🎯 Master Execution Results:"
echo "============================="
echo ""

if [ "$OVERALL_SUCCESS" = true ]; then
    log_success "CLEANUP COMPLETED SUCCESSFULLY!"
    echo ""
    echo "🚀 Your Azure environment is ready for V1 deployment!"
    echo ""
    echo "📋 Summary:"
    echo "  • All critical phases completed successfully"
    echo "  • Environment prepared for clean deployment"
    echo "  • Ready to trigger GitHub Actions workflow"
else
    log_error "CLEANUP COMPLETED WITH ISSUES!"
    echo ""
    echo "🚨 Critical issues must be resolved before deployment!"
    echo ""
    echo "📋 Summary:"
    echo "  • Some phases failed or encountered errors"
    echo "  • Manual intervention required"
    echo "  • Review phase logs for specific issues"
fi

echo ""
echo "📂 Generated Files:"
echo "  • Master execution log: $MASTER_LOG_DIR/master-execution.log"
echo "  • Detailed report: $MASTER_LOG_DIR/master-execution-report.md"
echo "  • Individual phase logs: $MASTER_LOG_DIR/*.log"

echo ""
if [ "$OVERALL_SUCCESS" = true ]; then
    echo "🎯 Next Steps:"
    echo "  1. Review the master execution report"
    echo "  2. Verify GitHub repository secrets are configured"
    echo "  3. Trigger GitHub Actions deployment: '.github/workflows/simple-deploy.yml'"
    echo "  4. Monitor deployment progress and validate results"
else
    echo "🔧 Required Actions:"
    echo "  1. Review failed phase logs for specific errors"
    echo "  2. Resolve critical issues manually"
    echo "  3. Re-run specific phases or full cleanup as needed"
    echo "  4. Ensure all phases pass before attempting deployment"
fi

echo ""
echo "✅ Master cleanup execution completed"

# Exit with appropriate code
if [ "$OVERALL_SUCCESS" = true ]; then
    exit 0
else
    exit 1
fi