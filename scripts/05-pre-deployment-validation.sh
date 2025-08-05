#!/bin/bash

# Azure Pre-Deployment Validation
# Validates environment readiness for V1 deployment

set -e

echo "🔍 Azure Pre-Deployment Validation"
echo "==================================="
echo ""

# Configuration
V1_RG="aiprofilemaker-v1"
VALIDATION_DIR="azure-cleanup-backup/$(date +%Y%m%d-%H%M%S)-validation"

# Create validation directory
mkdir -p "$VALIDATION_DIR"

echo "📋 Validation Configuration:"
echo "  Target Resource Group: $V1_RG"
echo "  Validation Directory: $VALIDATION_DIR"
echo ""

# Initialize validation results
VALIDATION_PASSED=true
VALIDATION_WARNINGS=0
VALIDATION_ERRORS=0

# Validation helper functions
log_success() {
    echo "  ✅ $1"
    echo "SUCCESS: $1" >> "$VALIDATION_DIR/validation-results.log"
}

log_warning() {
    echo "  ⚠️  $1"
    echo "WARNING: $1" >> "$VALIDATION_DIR/validation-results.log"
    ((VALIDATION_WARNINGS++))
}

log_error() {
    echo "  ❌ $1"
    echo "ERROR: $1" >> "$VALIDATION_DIR/validation-results.log"
    ((VALIDATION_ERRORS++))
    VALIDATION_PASSED=false
}

log_info() {
    echo "  ℹ️  $1"
    echo "INFO: $1" >> "$VALIDATION_DIR/validation-results.log"
}

# Azure CLI Validation
echo "🔧 Azure CLI Validation..."

if ! command -v az &> /dev/null; then
    log_error "Azure CLI not found"
else
    AZ_VERSION=$(az version --query '"azure-cli"' -o tsv 2>/dev/null || echo "unknown")
    log_success "Azure CLI available (version: $AZ_VERSION)"
fi

# Authentication Validation
echo ""
echo "🔐 Authentication Validation..."

if ! az account show &> /dev/null; then
    log_error "Not authenticated with Azure (run 'az login')"
else
    SUBSCRIPTION_ID=$(az account show --query "id" -o tsv)
    SUBSCRIPTION_NAME=$(az account show --query "name" -o tsv)
    log_success "Authenticated with Azure"
    log_info "Subscription: $SUBSCRIPTION_NAME ($SUBSCRIPTION_ID)"
fi

# Resource Group Validation
echo ""
echo "🏗️  Resource Group Validation..."

if ! az group show --name "$V1_RG" &> /dev/null; then
    log_info "Resource group '$V1_RG' doesn't exist - will be created during deployment"
    
    # Test resource group creation permissions
    TEST_RG="test-permissions-$(date +%s)"
    if az group create --name "$TEST_RG" --location eastus --tags Test=validation &> /dev/null; then
        az group delete --name "$TEST_RG" --yes --no-wait &> /dev/null || true
        log_success "Resource group creation permissions verified"
    else
        log_error "Cannot create resource groups - insufficient permissions"
    fi
else
    log_success "Resource group '$V1_RG' exists"
    
    # Analyze existing resources
    EXISTING_RESOURCES=$(az resource list -g "$V1_RG" --query "length(@)" -o tsv)
    if [ "$EXISTING_RESOURCES" -gt 0 ]; then
        log_warning "Resource group contains $EXISTING_RESOURCES existing resources"
        
        # Save resource list for review
        az resource list -g "$V1_RG" --output table > "$VALIDATION_DIR/existing-resources.txt"
        log_info "Existing resources list saved to: existing-resources.txt"
        
        # Check for potential naming conflicts
        POTENTIAL_CONFLICTS=$(az resource list -g "$V1_RG" --query "[?contains(name, 'aiprofilemaker') && contains(name, 'v1')].name" -o tsv)
        if [ -n "$POTENTIAL_CONFLICTS" ]; then
            log_warning "Potential naming conflicts detected:"
            for conflict in $POTENTIAL_CONFLICTS; do
                log_warning "  - $conflict"
            done
        fi
    else
        log_success "Resource group is empty - clean slate for deployment"
    fi
fi

# GitHub Secrets Validation
echo ""
echo "🔑 GitHub Secrets Validation..."

# Check if we're in a git repository
if [ -d ".git" ]; then
    REPO_URL=$(git config --get remote.origin.url 2>/dev/null || echo "unknown")
    log_info "Repository: $REPO_URL"
    
    # List required secrets
    REQUIRED_SECRETS=(
        "AZURE_CLIENT_ID"
        "AZURE_TENANT_ID" 
        "AZURE_SUBSCRIPTION_ID"
        "SQL_ADMIN_PASSWORD"
        "JWT_SECRET"
        "REPLICATE_API_TOKEN"
    )
    
    log_info "Required GitHub Secrets:"
    for secret in "${REQUIRED_SECRETS[@]}"; do
        log_info "  - $secret"
    done
    
    log_warning "Verify these secrets are configured in GitHub repository settings"
else
    log_warning "Not in a git repository - cannot validate GitHub integration"
fi

# Deployment File Validation
echo ""
echo "📄 Deployment File Validation..."

# Check for required deployment files
REQUIRED_FILES=(
    "infrastructure/simple-deploy.bicep"
    ".github/workflows/simple-deploy.yml"
    "Dockerfile.backend"
    "Dockerfile.frontend"
)

for file in "${REQUIRED_FILES[@]}"; do
    if [ -f "$file" ]; then
        log_success "Found: $file"
    else
        log_error "Missing: $file"
    fi
done

# Validate Bicep file syntax (if bicep is available)
if command -v az bicep &> /dev/null; then
    echo ""
    echo "🔍 Bicep Syntax Validation..."
    
    if az bicep build --file infrastructure/simple-deploy.bicep --stdout > /dev/null 2>&1; then
        log_success "Bicep syntax validation passed"
    else
        log_error "Bicep syntax errors detected"
        # Try to capture the error
        az bicep build --file infrastructure/simple-deploy.bicep --stdout > "$VALIDATION_DIR/bicep-errors.log" 2>&1 || true
    fi
else
    log_info "Bicep CLI not available - skipping syntax validation"
fi

# Docker Validation
echo ""
echo "🐳 Docker Validation..."

if command -v docker &> /dev/null; then
    log_success "Docker CLI available"
    
    # Test Docker daemon
    if docker info &> /dev/null; then
        log_success "Docker daemon running"
    else
        log_warning "Docker daemon not running (required for GitHub Actions)"
    fi
else
    log_warning "Docker CLI not available locally (GitHub Actions will handle this)"
fi

# Network Connectivity Validation
echo ""
echo "🌐 Network Connectivity Validation..."

# Test Azure connectivity
if curl -s --max-time 10 https://management.azure.com/ > /dev/null; then
    log_success "Azure management endpoint reachable"
else
    log_warning "Azure management endpoint connectivity issues"
fi

# Test Container Registry connectivity
if curl -s --max-time 10 https://index.docker.io/ > /dev/null; then
    log_success "Docker Hub reachable"
else
    log_warning "Docker Hub connectivity issues"
fi

# Resource Provider Validation
echo ""
echo "🔌 Azure Resource Provider Validation..."

REQUIRED_PROVIDERS=(
    "Microsoft.ContainerRegistry"
    "Microsoft.ContainerInstance" 
    "Microsoft.App"
    "Microsoft.Sql"
    "Microsoft.Storage"
    "Microsoft.KeyVault"
    "Microsoft.Insights"
    "Microsoft.OperationalInsights"
)

for provider in "${REQUIRED_PROVIDERS[@]}"; do
    STATUS=$(az provider show --namespace "$provider" --query "registrationState" -o tsv 2>/dev/null || echo "NotFound")
    if [ "$STATUS" = "Registered" ]; then
        log_success "Provider registered: $provider"
    elif [ "$STATUS" = "NotRegistered" ]; then
        log_warning "Provider not registered: $provider (will auto-register during deployment)"
    else
        log_error "Provider status unknown: $provider ($STATUS)"
    fi
done

# Regional Availability Validation
echo ""
echo "🌍 Regional Availability Validation..."

DEPLOYMENT_REGION="eastus"
AVAILABLE_REGIONS=$(az account list-locations --query "[?name=='$DEPLOYMENT_REGION'].name" -o tsv 2>/dev/null || echo "")

if [ -n "$AVAILABLE_REGIONS" ]; then
    log_success "Deployment region available: $DEPLOYMENT_REGION"
else
    log_error "Deployment region not available: $DEPLOYMENT_REGION"
fi

# Resource Quota Validation (basic check)
echo ""
echo "📊 Resource Quota Validation..."

# Check compute quota (basic)
COMPUTE_USAGE=$(az vm usage list --location "$DEPLOYMENT_REGION" --query "[?name.value=='cores'].{current:currentValue,limit:limit}" -o tsv 2>/dev/null || echo "")
if [ -n "$COMPUTE_USAGE" ]; then
    log_info "Compute quota information retrieved"
else
    log_warning "Could not retrieve compute quota information"
fi

# Generate validation report
echo ""
echo "📋 Generating Validation Report..."

cat > "$VALIDATION_DIR/validation-report.md" << EOF
# Azure Pre-Deployment Validation Report

## Validation Summary
- Date: $(date)
- Target Resource Group: $V1_RG
- Region: $DEPLOYMENT_REGION

## Results Overview
- ✅ Successes: $(grep -c "SUCCESS:" "$VALIDATION_DIR/validation-results.log" 2>/dev/null || echo "0")
- ⚠️  Warnings: $VALIDATION_WARNINGS
- ❌ Errors: $VALIDATION_ERRORS
- Overall Status: $(if [ "$VALIDATION_PASSED" = true ]; then echo "PASSED ✅"; else echo "FAILED ❌"; fi)

## Detailed Results
\`\`\`
$(cat "$VALIDATION_DIR/validation-results.log" 2>/dev/null || echo "No validation results")
\`\`\`

## Required Actions Before Deployment
$(if [ $VALIDATION_ERRORS -gt 0 ]; then
    echo "### Critical Issues (Must Fix)"
    grep "ERROR:" "$VALIDATION_DIR/validation-results.log" | sed 's/ERROR: /- /'
fi)

$(if [ $VALIDATION_WARNINGS -gt 0 ]; then
    echo "### Warnings (Review Recommended)"
    grep "WARNING:" "$VALIDATION_DIR/validation-results.log" | sed 's/WARNING: /- /'
fi)

## Deployment Readiness
$(if [ "$VALIDATION_PASSED" = true ]; then
    echo "🚀 **READY FOR DEPLOYMENT**"
    echo ""
    echo "Your environment has passed validation and is ready for V1 deployment."
    echo ""
    echo "### Next Steps:"
    echo "1. Review any warnings above"
    echo "2. Trigger GitHub Actions deployment"
    echo "3. Monitor deployment progress"
    echo "4. Validate application functionality post-deployment"
else
    echo "🚨 **NOT READY FOR DEPLOYMENT**"
    echo ""
    echo "Critical issues must be resolved before deployment can proceed."
    echo ""
    echo "### Required Actions:"
    echo "1. Fix all error conditions listed above"
    echo "2. Re-run validation script"
    echo "3. Proceed with deployment only after validation passes"
fi)

## GitHub Actions Deployment Command
\`\`\`bash
# Trigger deployment via GitHub CLI (if available)
gh workflow run "🚀 V1 Deploy" --ref main

# Or trigger via GitHub web interface:
# https://github.com/YOUR_USERNAME/YOUR_REPO/actions/workflows/simple-deploy.yml
\`\`\`

## Monitoring and Troubleshooting
- Monitor deployment: Check GitHub Actions tab
- Azure resources: Monitor in Azure Portal
- Logs: Check Container Apps logs post-deployment
- Health checks: Test application URLs after deployment
EOF

# Display final results
echo ""
echo "🎯 Validation Results:"
echo "========================"
echo ""

if [ "$VALIDATION_PASSED" = true ]; then
    echo "🚀 VALIDATION PASSED - Ready for deployment!"
    echo ""
    echo "📊 Summary:"
    echo "  • Errors: $VALIDATION_ERRORS"
    echo "  • Warnings: $VALIDATION_WARNINGS"
    echo "  • Status: READY ✅"
else
    echo "🚨 VALIDATION FAILED - Issues must be resolved!"
    echo ""
    echo "📊 Summary:"
    echo "  • Errors: $VALIDATION_ERRORS (MUST FIX)"
    echo "  • Warnings: $VALIDATION_WARNINGS"
    echo "  • Status: NOT READY ❌"
fi

echo ""
echo "📋 Detailed Results:"
echo "  • Validation log: $VALIDATION_DIR/validation-results.log"
echo "  • Full report: $VALIDATION_DIR/validation-report.md"

if [ -f "$VALIDATION_DIR/existing-resources.txt" ]; then
    echo "  • Existing resources: $VALIDATION_DIR/existing-resources.txt"
fi

echo ""
if [ "$VALIDATION_PASSED" = true ]; then
    echo "🚀 Next Steps:"
    echo "  1. Review validation report for any warnings"
    echo "  2. Trigger GitHub Actions deployment workflow"
    echo "  3. Monitor deployment progress in GitHub Actions"
    echo "  4. Test application functionality post-deployment"
else
    echo "🔧 Required Actions:"
    echo "  1. Fix all validation errors listed above"
    echo "  2. Re-run this validation script"
    echo "  3. Proceed with deployment only after validation passes"
fi

echo ""
echo "✅ Pre-deployment validation completed"

# Exit with appropriate code
if [ "$VALIDATION_PASSED" = true ]; then
    exit 0
else
    exit 1
fi