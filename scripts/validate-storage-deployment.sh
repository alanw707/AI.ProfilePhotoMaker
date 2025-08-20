#!/bin/bash

# AI Profile Photo Maker - Storage Deployment Validation Script
# 
# Comprehensive validation script for storage service configuration
# Tests all aspects of storage deployment to prevent production issues

set -euo pipefail

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
TIMESTAMP=$(date '+%Y-%m-%d_%H-%M-%S')

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default configuration
RESOURCE_GROUP="${RESOURCE_GROUP:-aiprofilemaker-v1}"
APP_NAME="${APP_NAME:-aipm-api-v1}"
FRONTEND_URL="${FRONTEND_URL:-https://app.aiprofilephotomaker.com}"
BACKEND_URL="${BACKEND_URL:-https://api.aiprofilephotomaker.com}"

# Validation results
VALIDATION_RESULTS=()
TOTAL_CHECKS=0
PASSED_CHECKS=0
FAILED_CHECKS=0

# Helper functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

record_check() {
    local check_name="$1"
    local status="$2"
    local details="$3"
    
    TOTAL_CHECKS=$((TOTAL_CHECKS + 1))
    
    if [[ "$status" == "PASS" ]]; then
        PASSED_CHECKS=$((PASSED_CHECKS + 1))
        log_success "$check_name: PASSED"
    else
        FAILED_CHECKS=$((FAILED_CHECKS + 1))
        log_error "$check_name: FAILED - $details"
    fi
    
    VALIDATION_RESULTS+=("$check_name|$status|$details")
}

# Main validation functions
validate_azure_infrastructure() {
    log_info "🏗️ Validating Azure infrastructure..."
    
    # Check if resource group exists
    if az group show --name "$RESOURCE_GROUP" --output none 2>/dev/null; then
        record_check "Resource Group Exists" "PASS" "Resource group $RESOURCE_GROUP found"
    else
        record_check "Resource Group Exists" "FAIL" "Resource group $RESOURCE_GROUP not found"
        return 1
    fi
    
    # Check storage accounts
    local storage_accounts
    storage_accounts=$(az storage account list --resource-group "$RESOURCE_GROUP" --query "[].name" --output tsv 2>/dev/null || echo "")
    
    if [[ -n "$storage_accounts" ]]; then
        record_check "Storage Account Exists" "PASS" "Found storage accounts: $storage_accounts"
        
        # Validate storage container
        local storage_account
        storage_account=$(echo "$storage_accounts" | head -n1)
        local storage_key
        storage_key=$(az storage account keys list --account-name "$storage_account" --resource-group "$RESOURCE_GROUP" --query "[0].value" --output tsv 2>/dev/null || echo "")
        
        if [[ -n "$storage_key" ]]; then
            if az storage container exists --name "profile-images" --account-name "$storage_account" --account-key "$storage_key" --query "exists" --output tsv 2>/dev/null | grep -q "true"; then
                record_check "Profile Images Container" "PASS" "Container exists in $storage_account"
            else
                record_check "Profile Images Container" "FAIL" "Container not found in $storage_account"
            fi
        else
            record_check "Storage Account Access" "FAIL" "Cannot access storage account keys"
        fi
    else
        record_check "Storage Account Exists" "FAIL" "No storage accounts found in resource group"
    fi
    
    # Check container app
    if az containerapp show --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" --output none 2>/dev/null; then
        record_check "Container App Exists" "PASS" "Container app $APP_NAME found"
    else
        record_check "Container App Exists" "FAIL" "Container app $APP_NAME not found"
    fi
}

validate_environment_variables() {
    log_info "🔧 Validating environment variables..."
    
    # Get environment variables from container app
    local env_vars
    env_vars=$(az containerapp show --name "$APP_NAME" --resource-group "$RESOURCE_GROUP" \
        --query "properties.template.containers[0].env[].name" --output tsv 2>/dev/null || echo "")
    
    if [[ -z "$env_vars" ]]; then
        record_check "Environment Variables Access" "FAIL" "Cannot retrieve environment variables"
        return 1
    fi
    
    # Check critical storage environment variables
    if echo "$env_vars" | grep -q "^ConnectionStrings__AzureStorage$"; then
        record_check "ConnectionStrings__AzureStorage" "PASS" "Primary .NET configuration pattern found"
    else
        record_check "ConnectionStrings__AzureStorage" "FAIL" "Primary .NET configuration pattern missing"
    fi
    
    if echo "$env_vars" | grep -q "^AzureStorage__ConnectionString$"; then
        record_check "AzureStorage__ConnectionString" "PASS" "Alternative configuration pattern found"
    else
        record_check "AzureStorage__ConnectionString" "WARN" "Alternative configuration pattern missing (optional)"
    fi
    
    if echo "$env_vars" | grep -q "^AzureStorage__ContainerName$"; then
        record_check "AzureStorage__ContainerName" "PASS" "Container name configuration found"
    else
        record_check "AzureStorage__ContainerName" "FAIL" "Container name configuration missing"
    fi
    
    # Check for legacy incorrect naming
    if echo "$env_vars" | grep -q "^AZURE_STORAGE_CONNECTION_STRING$"; then
        record_check "Legacy Environment Variable" "WARN" "Found AZURE_STORAGE_CONNECTION_STRING (works but not preferred)"
    fi
}

validate_application_health() {
    log_info "🏥 Validating application health..."
    
    # Test basic application health
    if curl -f -s --max-time 30 "$BACKEND_URL/api/health" > /dev/null 2>&1; then
        record_check "Application Health" "PASS" "Basic health endpoint accessible"
    else
        record_check "Application Health" "FAIL" "Basic health endpoint not accessible"
        return 1
    fi
    
    # Test storage-specific health
    local storage_health_response
    storage_health_response=$(curl -f -s --max-time 15 "$BACKEND_URL/api/health/storage" 2>/dev/null || echo "")
    
    if [[ -n "$storage_health_response" ]]; then
        record_check "Storage Health Endpoint" "PASS" "Storage health endpoint accessible"
        
        # Parse storage provider from response
        local storage_provider
        storage_provider=$(echo "$storage_health_response" | grep -o '"provider":"[^"]*"' | sed 's/"provider":"//' | sed 's/"//' || echo "")
        
        if [[ "$storage_provider" == "AzureBlobStorage" ]]; then
            record_check "Storage Provider Type" "PASS" "Using AzureBlobStorage (correct for production)"
        elif [[ "$storage_provider" == "LocalStorage" ]]; then
            record_check "Storage Provider Type" "FAIL" "Using LocalStorage (will cause 404 errors in production)"
        else
            record_check "Storage Provider Type" "WARN" "Unknown storage provider: $storage_provider"
        fi
        
        # Check storage connectivity
        local can_connect
        can_connect=$(echo "$storage_health_response" | grep -o '"canConnect":[^,}]*' | sed 's/"canConnect"://' || echo "false")
        
        if [[ "$can_connect" == "true" ]]; then
            record_check "Storage Connectivity" "PASS" "Storage service can connect successfully"
        else
            record_check "Storage Connectivity" "FAIL" "Storage service cannot connect"
        fi
    else
        record_check "Storage Health Endpoint" "FAIL" "Storage health endpoint not accessible"
    fi
}

validate_frontend_accessibility() {
    log_info "🌐 Validating frontend accessibility..."
    
    if curl -f -s --max-time 30 "$FRONTEND_URL" > /dev/null 2>&1; then
        record_check "Frontend Accessibility" "PASS" "Frontend application accessible"
    else
        record_check "Frontend Accessibility" "FAIL" "Frontend application not accessible"
    fi
}

run_e2e_storage_validation() {
    log_info "🧪 Running E2E storage validation..."
    
    # Check if Playwright is available and tests exist
    if [[ -f "$PROJECT_ROOT/tests/e2e/image-upload-validation.spec.js" ]]; then
        if command -v npx &> /dev/null; then
            log_info "Running Playwright E2E tests..."
            
            cd "$PROJECT_ROOT"
            
            # Run storage validation tests
            if npx playwright test tests/e2e/image-upload-validation.spec.js --project=production-validation --reporter=line 2>/dev/null; then
                record_check "E2E Storage Tests" "PASS" "Automated E2E storage validation passed"
            else
                record_check "E2E Storage Tests" "FAIL" "Automated E2E storage validation failed"
            fi
        else
            record_check "E2E Storage Tests" "WARN" "Playwright not available for automated testing"
        fi
    else
        record_check "E2E Storage Tests" "WARN" "E2E test files not found"
    fi
}

generate_validation_report() {
    log_info "📊 Generating validation report..."
    
    local report_file="storage-validation-report-$TIMESTAMP.md"
    local success_rate=0
    
    if [[ $TOTAL_CHECKS -gt 0 ]]; then
        success_rate=$(( (PASSED_CHECKS * 100) / TOTAL_CHECKS ))
    fi
    
    cat > "$report_file" << EOF
# Storage Deployment Validation Report

**Generated**: $(date)
**Resource Group**: $RESOURCE_GROUP
**Container App**: $APP_NAME
**Frontend URL**: $FRONTEND_URL
**Backend URL**: $BACKEND_URL

## Summary

- **Total Checks**: $TOTAL_CHECKS
- **Passed**: ✅ $PASSED_CHECKS
- **Failed**: ❌ $FAILED_CHECKS
- **Success Rate**: $success_rate%

## Validation Results

EOF

    for result in "${VALIDATION_RESULTS[@]}"; do
        IFS='|' read -r check_name status details <<< "$result"
        local icon="❌"
        if [[ "$status" == "PASS" ]]; then
            icon="✅"
        elif [[ "$status" == "WARN" ]]; then
            icon="⚠️"
        fi
        
        echo "- $icon **$check_name**: $details" >> "$report_file"
    done
    
    cat >> "$report_file" << EOF

## Recommendations

EOF

    if [[ $FAILED_CHECKS -gt 0 ]]; then
        cat >> "$report_file" << EOF
### Critical Issues Found

🚨 **$FAILED_CHECKS validation check(s) failed**

1. **Review Failed Checks**: Address all failed validations above
2. **Check Environment Variables**: Ensure correct .NET configuration patterns
3. **Verify Storage Configuration**: Confirm Azure Storage connection string format
4. **Run Deployment Again**: Re-deploy if infrastructure issues found

### Immediate Actions

\`\`\`bash
# Check container app environment variables
az containerapp show --name $APP_NAME --resource-group $RESOURCE_GROUP \\
  --query "properties.template.containers[0].env[?contains(name, 'Storage')]"

# Check storage account details
az storage account list --resource-group $RESOURCE_GROUP --output table

# Test storage health endpoint
curl -s $BACKEND_URL/api/health/storage | jq
\`\`\`

EOF
    else
        cat >> "$report_file" << EOF
### All Validations Passed ✅

🎉 **Storage deployment validation successful!**

The application is properly configured with:
- Azure Blob Storage for image storage
- Correct environment variable configuration
- Healthy storage connectivity
- Accessible frontend and backend services

### Ongoing Monitoring

- Monitor storage health endpoint: \`$BACKEND_URL/api/health/storage\`
- Watch for application logs showing "Using Azure Blob Storage"
- Track image upload success rates and accessibility
- Run E2E tests regularly to validate continued operation

EOF
    fi
    
    echo "📄 Validation report saved to: $report_file"
    
    # Also log summary to console
    echo ""
    echo "======================================"
    echo "  STORAGE VALIDATION SUMMARY"
    echo "======================================"
    echo "Total Checks: $TOTAL_CHECKS"
    echo "Passed: $PASSED_CHECKS"
    echo "Failed: $FAILED_CHECKS"
    echo "Success Rate: $success_rate%"
    echo ""
    
    if [[ $FAILED_CHECKS -eq 0 ]]; then
        log_success "🎉 All storage validation checks passed!"
        echo "✅ Production environment properly configured for image uploads"
        return 0
    else
        log_error "❌ $FAILED_CHECKS validation check(s) failed"
        echo "🔧 Review the generated report for specific issues and solutions"
        return 1
    fi
}

# Main execution
main() {
    echo "🚀 AI Profile Photo Maker - Storage Deployment Validation"
    echo "========================================================"
    echo ""
    
    log_info "Starting comprehensive storage validation..."
    log_info "Target: $RESOURCE_GROUP / $APP_NAME"
    echo ""
    
    # Run all validation checks
    validate_azure_infrastructure
    validate_environment_variables
    validate_application_health
    validate_frontend_accessibility
    run_e2e_storage_validation
    
    # Generate final report
    echo ""
    generate_validation_report
}

# Execute main function
main "$@"