#!/bin/bash

# Comprehensive Secrets Validation Framework
# Part of Unified Secrets Management Plan
# Validates consistency across all secret stores

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Validation results
VALIDATION_ERRORS=0
VALIDATION_WARNINGS=0

log_info() { echo -e "${BLUE}[INFO]${NC} $1"; }
log_success() { echo -e "${GREEN}[SUCCESS]${NC} $1"; }
log_warning() { echo -e "${YELLOW}[WARNING]${NC} $1"; ((VALIDATION_WARNINGS++)); }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; ((VALIDATION_ERRORS++)); }

echo -e "${BLUE}=========================================${NC}"
echo -e "${BLUE} Comprehensive Secrets Validation${NC}"
echo -e "${BLUE}=========================================${NC}"
echo ""

# Check if we're in the right directory
if [[ ! -f "AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj" ]]; then
    log_error "Must run from project root directory (AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj not found)"
    exit 1
fi

log_info "Starting secrets validation across all stores..."
echo ""

# ===========================================
# 1. VALIDATE DOTNET USER-SECRETS
# ===========================================

log_info "🔍 Validating dotnet user-secrets..."

# Check if user-secrets is initialized
if ! dotnet user-secrets list --project AI.ProfilePhotoMaker.API > /dev/null 2>&1; then
    log_error "dotnet user-secrets not initialized"
    exit 1
fi

# Get all user secrets
USER_SECRETS=$(dotnet user-secrets list --project AI.ProfilePhotoMaker.API 2>/dev/null || echo "")

# Required secrets in user-secrets
REQUIRED_USER_SECRETS=(
    "JWT:Secret"
    "ConnectionStrings:DefaultConnection" 
    "ConnectionStrings:ProductionConnection"
    "AzureStorage:ConnectionString"
    "Authentication:Google:ClientId"
    "Authentication:Google:ClientSecret"
)

# Check each required secret
for secret in "${REQUIRED_USER_SECRETS[@]}"; do
    if echo "$USER_SECRETS" | grep -q "^$secret = "; then
        log_success "✅ Found: $secret"
    else
        log_error "❌ Missing: $secret"
    fi
done

# Check if Replicate secrets are present (should be after sync)
if echo "$USER_SECRETS" | grep -q "^Replicate:ApiToken = "; then
    log_success "✅ Found: Replicate:ApiToken"
else
    log_warning "⚠️  Missing: Replicate:ApiToken (sync required)"
fi

if echo "$USER_SECRETS" | grep -q "^Replicate:WebhookSecret = "; then
    log_success "✅ Found: Replicate:WebhookSecret"
else
    log_warning "⚠️  Missing: Replicate:WebhookSecret (sync required)"
fi

echo ""

# ===========================================
# 2. VALIDATE GITHUB ACTIONS SECRETS
# ===========================================

log_info "🔍 Validating GitHub Actions secrets..."

if ! command -v gh &> /dev/null; then
    log_warning "GitHub CLI not available - skipping GitHub Actions validation"
else
    # Get GitHub secrets
    GH_SECRETS=$(gh secret list 2>/dev/null || echo "")
    
    # Required GitHub secrets
    REQUIRED_GH_SECRETS=(
        "AZURE_CLIENT_ID"
        "AZURE_SUBSCRIPTION_ID" 
        "AZURE_TENANT_ID"
        "GOOGLE_CLIENT_ID"
        "GOOGLE_CLIENT_SECRET"
        "JWT_SECRET"
        "REPLICATE_API_TOKEN"
        "REPLICATE_WEBHOOK_SECRET"
        "SQL_ADMIN_PASSWORD"
    )
    
    for secret in "${REQUIRED_GH_SECRETS[@]}"; do
        if echo "$GH_SECRETS" | grep -q "^$secret[[:space:]]"; then
            log_success "✅ Found: $secret"
        else
            log_error "❌ Missing: $secret"
        fi
    done
fi

echo ""

# ===========================================
# 3. VALIDATE SECRET FORMATS
# ===========================================

log_info "🔍 Validating secret formats..."

# JWT Secret validation
JWT_SECRET=$(echo "$USER_SECRETS" | grep "^JWT:Secret = " | cut -d'=' -f2- | xargs)
if [[ -n "$JWT_SECRET" ]]; then
    if [[ ${#JWT_SECRET} -ge 32 ]]; then
        log_success "✅ JWT Secret length valid (${#JWT_SECRET} chars)"
    else
        log_error "❌ JWT Secret too short (${#JWT_SECRET} chars, minimum 32)"
    fi
else
    log_warning "⚠️  JWT Secret not found for validation"
fi

# Google OAuth Client ID validation
GOOGLE_CLIENT_ID=$(echo "$USER_SECRETS" | grep "^Authentication:Google:ClientId = " | cut -d'=' -f2- | xargs)
if [[ -n "$GOOGLE_CLIENT_ID" ]]; then
    if [[ "$GOOGLE_CLIENT_ID" =~ ^[0-9]+-[a-zA-Z0-9]+\.apps\.googleusercontent\.com$ ]]; then
        log_success "✅ Google Client ID format valid"
    else
        log_warning "⚠️  Google Client ID format may be invalid"
    fi
fi

# Google OAuth Client Secret validation
GOOGLE_CLIENT_SECRET=$(echo "$USER_SECRETS" | grep "^Authentication:Google:ClientSecret = " | cut -d'=' -f2- | xargs)
if [[ -n "$GOOGLE_CLIENT_SECRET" ]]; then
    if [[ "$GOOGLE_CLIENT_SECRET" =~ ^GOCSPX- ]]; then
        log_success "✅ Google Client Secret format valid"
    else
        log_warning "⚠️  Google Client Secret format may be invalid (should start with GOCSPX-)"
    fi
fi

# Replicate API Token validation (if present)
REPLICATE_TOKEN=$(echo "$USER_SECRETS" | grep "^Replicate:ApiToken = " | cut -d'=' -f2- | xargs)
if [[ -n "$REPLICATE_TOKEN" ]]; then
    if [[ "$REPLICATE_TOKEN" =~ ^r8_ ]]; then
        log_success "✅ Replicate API Token format valid"
    else
        log_error "❌ Replicate API Token should start with 'r8_'"
    fi
fi

echo ""

# ===========================================
# 4. VALIDATE INFRASTRUCTURE CONFIGURATION
# ===========================================

log_info "🔍 Validating infrastructure configuration..."

# Check Bicep template
if [[ -f "infrastructure/simple-deploy.bicep" ]]; then
    log_success "✅ Bicep template found"
    
    # Check for required parameters
    if grep -q "param replicateWebhookSecret string" infrastructure/simple-deploy.bicep; then
        log_success "✅ replicateWebhookSecret parameter present"
    else
        log_error "❌ replicateWebhookSecret parameter missing from Bicep template"
    fi
    
    # Check for webhook secret in environment variables
    if grep -q "Replicate__WebhookSecret" infrastructure/simple-deploy.bicep; then
        log_success "✅ Replicate__WebhookSecret environment variable configured"
    else
        log_error "❌ Replicate__WebhookSecret environment variable missing"
    fi
else
    log_error "❌ Bicep template not found"
fi

# Check GitHub Actions workflow
if [[ -f ".github/workflows/simple-deploy.yml" ]]; then
    log_success "✅ GitHub Actions workflow found"
    
    if grep -q "replicateWebhookSecret=" .github/workflows/simple-deploy.yml; then
        log_success "✅ replicateWebhookSecret parameter in workflow"
    else
        log_error "❌ replicateWebhookSecret parameter missing from workflow"
    fi
else
    log_error "❌ GitHub Actions workflow not found"
fi

echo ""

# ===========================================
# 5. VALIDATE APPLICATION CONFIGURATION
# ===========================================

log_info "🔍 Validating application configuration..."

# Check EnvironmentConfiguration.cs for Replicate validation
if [[ -f "AI.ProfilePhotoMaker.API/Configuration/EnvironmentConfiguration.cs" ]]; then
    if grep -q "REPLICATE_WEBHOOK_SECRET" AI.ProfilePhotoMaker.API/Configuration/EnvironmentConfiguration.cs; then
        log_success "✅ Replicate webhook secret validation in EnvironmentConfiguration"
    else
        log_warning "⚠️  Replicate webhook secret validation not in EnvironmentConfiguration"
    fi
fi

echo ""

# ===========================================
# FINAL VALIDATION SUMMARY
# ===========================================

echo -e "${BLUE}=========================================${NC}"
echo -e "${BLUE} Validation Summary${NC}"
echo -e "${BLUE}=========================================${NC}"

if [[ $VALIDATION_ERRORS -eq 0 ]] && [[ $VALIDATION_WARNINGS -eq 0 ]]; then
    log_success "🎉 All validations passed successfully!"
    echo ""
    echo -e "${GREEN}Ready for deployment with complete secrets management!${NC}"
    exit 0
elif [[ $VALIDATION_ERRORS -eq 0 ]]; then
    echo -e "${YELLOW}⚠️  Validation completed with $VALIDATION_WARNINGS warnings${NC}"
    echo ""
    echo -e "${YELLOW}Warnings should be addressed but deployment can proceed${NC}"
    exit 0
else
    echo -e "${RED}❌ Validation failed with $VALIDATION_ERRORS errors and $VALIDATION_WARNINGS warnings${NC}"
    echo ""
    echo -e "${RED}Critical errors must be fixed before deployment${NC}"
    
    # Provide remediation suggestions
    echo ""
    echo -e "${BLUE}Remediation suggestions:${NC}"
    echo "1. Run: ./ClaudeDocs/Analysis/Security/secure-replicate-sync.sh"
    echo "2. Ensure all GitHub Actions secrets are set"
    echo "3. Update infrastructure configuration files"
    echo "4. Re-run this validation script"
    
    exit 1
fi