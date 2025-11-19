#!/bin/bash

# Unified Infrastructure & Secrets Validation Framework
# Enhanced comprehensive validation system combining:
# - Environment-aware secrets validation across all stores
# - Infrastructure configuration alignment validation
# - Cross-validation between Bicep templates and application expectations
# - Environment-specific validation rules and configuration drift detection
# - Integration with configuration validation systems

set -euo pipefail

# Get target environment (default to Production if not specified)
TARGET_ENV="${1:-Production}"

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
log_warning() { echo -e "${YELLOW}[WARNING]${NC} $1"; VALIDATION_WARNINGS=$((VALIDATION_WARNINGS + 1)); }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; VALIDATION_ERRORS=$((VALIDATION_ERRORS + 1)); }

echo -e "${BLUE}=========================================${NC}"
echo -e "${BLUE} Environment-Aware Secrets Validation${NC}"
echo -e "${BLUE} Target Environment: ${TARGET_ENV}${NC}"
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

# Required secrets in user-secrets - ALL REQUIRED (referenced in infrastructure)
REQUIRED_USER_SECRETS=(
    "JWT:Secret"
    "ConnectionStrings:DefaultConnection" 
    "ConnectionStrings:ProductionConnection"
    "AzureStorage:ConnectionString"
    "AzureStorage:ContainerName"
    "Authentication:Google:ClientId"
    "Authentication:Google:ClientSecret"
    "Replicate:ApiToken"
    "Replicate:WebhookSecret"
    "Stripe:SecretKey"
    "Stripe:PublishableKey" 
    "Stripe:WebhookSecret"
)

# Check each required secret
for secret in "${REQUIRED_USER_SECRETS[@]}"; do
    if echo "$USER_SECRETS" | grep -q "^$secret = "; then
        log_success "✅ Found: $secret"
    else
        log_error "❌ Missing: $secret"
    fi
done

# Replicate secrets are now included in the main REQUIRED_USER_SECRETS array above

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
    
    # Required GitHub secrets - ALL REQUIRED (referenced in infrastructure)
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
        "STRIPE_SECRET_KEY"
        "STRIPE_PUBLISHABLE_KEY"
        "STRIPE_WEBHOOK_SECRET"
        "AZURE_STORAGE_CONNECTION_STRING"
        "AZURE_STORAGE_CONTAINER_NAME"
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

# Google OAuth Client ID validation (REQUIRED - referenced in infrastructure)
GOOGLE_CLIENT_ID=$(echo "$USER_SECRETS" | grep "^Authentication:Google:ClientId = " | cut -d'=' -f2- | xargs)
if [[ -z "$GOOGLE_CLIENT_ID" ]]; then
    log_error "❌ Google Client ID is REQUIRED (referenced in infrastructure deployment)"
elif [[ "$GOOGLE_CLIENT_ID" == *"Specify --help"* ]] || [[ "$GOOGLE_CLIENT_ID" == *"command"* ]]; then
    log_error "❌ CRITICAL: Google Client ID contains help text instead of OAuth client ID"
    log_error "   Expected format: 123456789-abc123.apps.googleusercontent.com"
    log_error "   Current value appears to be: ${GOOGLE_CLIENT_ID:0:50}..."
elif [[ ! "$GOOGLE_CLIENT_ID" == *".apps.googleusercontent.com" ]]; then
    log_error "❌ Google Client ID should end with .apps.googleusercontent.com"
elif [[ "$GOOGLE_CLIENT_ID" =~ ^[0-9]+-[a-zA-Z0-9]+\.apps\.googleusercontent\.com$ ]]; then
    log_success "✅ Google Client ID format valid"
else
    log_error "❌ Google Client ID format appears invalid"
fi

# Google OAuth Client Secret validation (REQUIRED - referenced in infrastructure)
GOOGLE_CLIENT_SECRET=$(echo "$USER_SECRETS" | grep "^Authentication:Google:ClientSecret = " | cut -d'=' -f2- | xargs)
if [[ -z "$GOOGLE_CLIENT_SECRET" ]]; then
    log_error "❌ Google Client Secret is REQUIRED (referenced in infrastructure deployment)"
elif [[ "$GOOGLE_CLIENT_SECRET" =~ ^GOCSPX- ]]; then
    log_success "✅ Google Client Secret format valid"
else
    log_error "❌ Google Client Secret format invalid (should start with GOCSPX-)"
fi

# Replicate API Token validation (REQUIRED)
REPLICATE_TOKEN=$(echo "$USER_SECRETS" | grep "^Replicate:ApiToken = " | cut -d'=' -f2- | xargs)
if [[ -z "$REPLICATE_TOKEN" ]]; then
    log_error "❌ Replicate API Token is REQUIRED (referenced in infrastructure deployment)"
elif [[ "$REPLICATE_TOKEN" =~ ^r8_ ]]; then
    log_success "✅ Replicate API Token format valid"
else
    log_error "❌ Replicate API Token should start with 'r8_'"
fi

# Stripe Secret Key validation (REQUIRED)
STRIPE_SECRET_KEY=$(echo "$USER_SECRETS" | grep "^Stripe:SecretKey = " | cut -d'=' -f2- | xargs)
if [[ -z "$STRIPE_SECRET_KEY" ]]; then
    log_error "❌ Stripe Secret Key is REQUIRED (referenced in infrastructure and payment processing)"
elif [[ "$STRIPE_SECRET_KEY" =~ ^sk_ ]]; then
    log_success "✅ Stripe Secret Key format valid"
else
    log_error "❌ Stripe Secret Key should start with 'sk_'"
fi

# Stripe Publishable Key validation (REQUIRED)
STRIPE_PUBLISHABLE_KEY=$(echo "$USER_SECRETS" | grep "^Stripe:PublishableKey = " | cut -d'=' -f2- | xargs)
if [[ -z "$STRIPE_PUBLISHABLE_KEY" ]]; then
    log_error "❌ Stripe Publishable Key is REQUIRED (referenced in infrastructure and frontend payment processing)"
elif [[ "$STRIPE_PUBLISHABLE_KEY" =~ ^pk_ ]]; then
    log_success "✅ Stripe Publishable Key format valid"
else
    log_error "❌ Stripe Publishable Key should start with 'pk_'"
fi

# Stripe Webhook Secret validation (REQUIRED)
STRIPE_WEBHOOK_SECRET=$(echo "$USER_SECRETS" | grep "^Stripe:WebhookSecret = " | cut -d'=' -f2- | xargs)
if [[ -z "$STRIPE_WEBHOOK_SECRET" ]]; then
    log_error "❌ Stripe Webhook Secret is REQUIRED (referenced in infrastructure and webhook validation)"
elif [[ "$STRIPE_WEBHOOK_SECRET" =~ ^whsec_ ]]; then
    log_success "✅ Stripe Webhook Secret format valid"
else
    log_error "❌ Stripe Webhook Secret should start with 'whsec_'"
fi

# Azure Storage Container Name validation (REQUIRED)
AZURE_STORAGE_CONTAINER=$(echo "$USER_SECRETS" | grep "^AzureStorage:ContainerName = " | cut -d'=' -f2- | xargs)
if [[ -z "$AZURE_STORAGE_CONTAINER" ]]; then
    log_error "❌ Azure Storage Container Name is REQUIRED (referenced in infrastructure deployment)"
else
    log_success "✅ Azure Storage Container Name configured"
fi

echo ""

# ===========================================
# 4. VALIDATE ENVIRONMENT-SPECIFIC REQUIREMENTS
# ===========================================

log_info "🔍 Validating environment-specific requirements for ${TARGET_ENV}..."

# Azure Storage validation - REQUIRED in ALL environments (referenced in infrastructure)
log_info "🎯 Azure Storage is REQUIRED in ALL environments (referenced in infrastructure configuration)"

# Check for Azure Storage in environment variables (production deployment)
if [[ -n "${AZURE_STORAGE_CONNECTION_STRING:-}" ]]; then
    if [[ "$AZURE_STORAGE_CONNECTION_STRING" == *"AccountName="* && "$AZURE_STORAGE_CONNECTION_STRING" == *"AccountKey="* ]]; then
        log_success "✅ Azure Storage connection string format valid"
    else
        log_error "❌ Azure Storage connection string invalid format"
    fi
else
    # Check in user secrets for deployment preparation
    AZURE_STORAGE_SECRET=$(echo "$USER_SECRETS" | grep "^AzureStorage:ConnectionString = " | cut -d'=' -f2- | xargs)
    if [[ -n "$AZURE_STORAGE_SECRET" ]]; then
        if [[ "$AZURE_STORAGE_SECRET" == *"UseDevelopmentStorage=true"* ]]; then
            log_error "❌ CRITICAL: Development storage not allowed for infrastructure deployment"
            log_error "   Must configure real Azure Storage for infrastructure consistency"
        else
            log_success "✅ Azure Storage configured for deployment"
        fi
    else
        log_error "❌ CRITICAL: Azure Storage connection string missing (required in infrastructure configuration)"
    fi
fi

# Container name validation
if [[ -n "${AZURE_STORAGE_CONTAINER_NAME:-}" ]]; then
    log_success "✅ Azure Storage container name configured"
else
    log_error "❌ CRITICAL: Azure Storage container name missing (required in infrastructure configuration)"
fi

echo ""

# ===========================================
# 5. VALIDATE INFRASTRUCTURE CONFIGURATION
# ===========================================

log_info "🔍 Validating infrastructure configuration alignment..."

# Infrastructure alignment validation
validateInfrastructureAlignment() {
    local errors=0
    
    log_info "🏗️ Validating Bicep templates..."
    
    # Check main deployment template
    if [[ -f "infrastructure/simple-deploy.bicep" ]]; then
        log_success "✅ Main Bicep template found"
        
        # Extract environment variables from Bicep template
        local bicep_env_vars=$(grep -E "^\s*name:\s*'[A-Z_]+'" infrastructure/simple-deploy.bicep | sed "s/.*name: '//" | sed "s/'.*$//" | sort -u)
        local bicep_config_vars=$(grep -E "^\s*name:\s*'[A-Za-z]+__[A-Za-z]+'" infrastructure/simple-deploy.bicep | sed "s/.*name: '//" | sed "s/'.*$//" | sort -u)
        
        log_info "📊 Environment variables in Bicep template:"
        echo "$bicep_env_vars" | while read var; do
            if [[ -n "$var" ]]; then
                echo "  • $var"
            fi
        done
        
        if [[ -n "$bicep_config_vars" ]]; then
            log_info "📊 ASP.NET Core configuration variables in Bicep:"
            echo "$bicep_config_vars" | while read var; do
                if [[ -n "$var" ]]; then
                    echo "  • $var"
                fi
            done
        fi
        
        # Extract expected environment variables from application
        local app_env_vars=""
        if [[ -f "AI.ProfilePhotoMaker.API/Configuration/EnvironmentConfiguration.cs" ]]; then
            app_env_vars=$(grep -E "^\s*public const string [A-Z_]+ = \"[A-Z_]+\";" AI.ProfilePhotoMaker.API/Configuration/EnvironmentConfiguration.cs | sed -E 's/.*= "([^"]+)".*/\1/' | sort -u)
            
            log_info "📊 Environment variables expected by application:"
            echo "$app_env_vars" | while read var; do
                if [[ -n "$var" ]]; then
                    echo "  • $var"
                fi
            done
        else
            log_warning "⚠️  EnvironmentConfiguration.cs not found - skipping application validation"
        fi
        
        # Critical variables cross-validation - ALL REQUIRED
        local critical_vars=(
            "AZURE_STORAGE_CONNECTION_STRING"
            "AZURE_STORAGE_CONTAINER_NAME"
            "JWT_SECRET"
            "REPLICATE_API_TOKEN"
            "REPLICATE_WEBHOOK_SECRET"
            "GOOGLE_CLIENT_ID"
            "GOOGLE_CLIENT_SECRET"
            "STRIPE_SECRET_KEY"
            "STRIPE_PUBLISHABLE_KEY"
            "STRIPE_WEBHOOK_SECRET"
        )
        
        log_info "🔍 Cross-validating critical environment variables..."
        
        for var in "${critical_vars[@]}"; do
            local infrastructure_provides=""
            local app_expects=""
            
            # Check if infrastructure provides this variable (direct or config pattern)
            if echo "$bicep_env_vars" | grep -q "^$var$"; then
                infrastructure_provides="direct"
            else
                case "$var" in
                    "JWT_SECRET")
                        if echo "$bicep_config_vars" | grep -q "^Jwt__Secret$"; then
                            infrastructure_provides="config:Jwt__Secret"
                        fi
                        ;;
                    "REPLICATE_API_TOKEN")
                        if echo "$bicep_config_vars" | grep -q "^Replicate__ApiToken$"; then
                            infrastructure_provides="config:Replicate__ApiToken"
                        fi
                        ;;
                    "REPLICATE_WEBHOOK_SECRET")
                        if echo "$bicep_config_vars" | grep -q "^Replicate__WebhookSecret$"; then
                            infrastructure_provides="config:Replicate__WebhookSecret"
                        fi
                        ;;
                    "STRIPE_SECRET_KEY")
                        if echo "$bicep_config_vars" | grep -q "^Stripe__SecretKey$"; then
                            infrastructure_provides="config:Stripe__SecretKey"
                        fi
                        ;;
                    "STRIPE_PUBLISHABLE_KEY")
                        if echo "$bicep_config_vars" | grep -q "^Stripe__PublishableKey$"; then
                            infrastructure_provides="config:Stripe__PublishableKey"
                        fi
                        ;;
                    "STRIPE_WEBHOOK_SECRET")
                        if echo "$bicep_config_vars" | grep -q "^Stripe__WebhookSecret$"; then
                            infrastructure_provides="config:Stripe__WebhookSecret"
                        fi
                        ;;
                esac
            fi
            
            # Check if application expects this variable
            if echo "$app_env_vars" | grep -q "^$var$"; then
                app_expects="yes"
            fi
            
            echo -n "  Checking $var: "
            
            if [[ -n "$app_expects" ]]; then
                if [[ -n "$infrastructure_provides" ]]; then
                    if [[ "$infrastructure_provides" == "direct" ]]; then
                        log_success "✅ MATCH (Direct environment variable)"
                    else
                        log_success "✅ MATCH (Via $infrastructure_provides)"
                    fi
                else
                    log_error "❌ MISSING in infrastructure (Application expects this variable)"
                    errors=$((errors + 1))
                fi
            else
                if [[ -n "$infrastructure_provides" ]]; then
                    echo -e "${YELLOW}⚠️  PROVIDED but not explicitly expected (May be unused)${NC}"
                else
                    echo -e "${BLUE}ℹ️  Not configured (Neither provided nor expected)${NC}"
                fi
            fi
        done
        
        # Check for naming pattern mismatches
        log_info "🔍 Checking for common naming pattern issues..."
        
        local storage_vars=$(echo "$bicep_env_vars" | grep -i storage || true)
        if [[ -n "$storage_vars" ]]; then
            echo "$storage_vars" | while read var; do
                if [[ "$var" != "AZURE_STORAGE_CONNECTION_STRING" && "$var" != "AZURE_STORAGE_CONTAINER_NAME" ]]; then
                    log_warning "⚠️  $var (Potential mismatch - check if this should be AZURE_STORAGE_*)"
                    errors=$((errors + 1))
                else
                    echo "  ✅ $var (Correct naming)"
                fi
            done
        fi
        
    else
        log_error "❌ Main Bicep template not found: infrastructure/simple-deploy.bicep"
        errors=$((errors + 1))
    fi
    
    # Check environment configuration template
    if [[ -f "infrastructure/azure-env-config.bicep" ]]; then
        log_success "✅ Environment configuration template found"
        
        # Validate Key Vault references
        local kv_refs=$(grep -c "@Microsoft.KeyVault" infrastructure/azure-env-config.bicep || echo "0")
        if [[ $kv_refs -gt 0 ]]; then
            log_success "✅ Key Vault references found ($kv_refs total)"
        else
            log_warning "⚠️  No Key Vault references found in environment config"
        fi
    else
        log_warning "⚠️  Environment configuration template not found: infrastructure/azure-env-config.bicep"
    fi
    
    return $errors
}

# Environment consistency validation
validateEnvironmentConsistency() {
    local errors=0
    
    log_info "🔄 Validating environment consistency..."
    
    # Check GitHub Actions workflow consistency
    if [[ -f ".github/workflows/simple-deploy.yml" ]]; then
        log_success "✅ GitHub Actions workflow found"
        
        # Check for infrastructure validation step
        if grep -q "Validate Infrastructure Configuration" .github/workflows/simple-deploy.yml; then
            log_success "✅ Infrastructure validation step present in workflow"
        else
            log_warning "⚠️  Infrastructure validation step missing from workflow"
        fi
        
        # Check for secrets validation step
        if grep -q "Validate Secrets" .github/workflows/simple-deploy.yml; then
            log_success "✅ Secrets validation step present in workflow"
        else
            log_warning "⚠️  Secrets validation step missing from workflow"
        fi
        
        # Check for required parameters in workflow - ALL REQUIRED
        local required_params=(
            "sqlAdminPassword"
            "jwtSecret"
            "replicateApiToken"
            "replicateWebhookSecret"
            "googleClientId"
            "googleClientSecret"
            "stripeSecretKey"
            "stripePublishableKey"
            "stripeWebhookSecret"
            "azureStorageConnectionString"
            "azureStorageContainerName"
        )
        
        for param in "${required_params[@]}"; do
            if grep -q "$param=" .github/workflows/simple-deploy.yml; then
                echo "  ✅ $param parameter in workflow"
            else
                log_error "❌ $param parameter missing from workflow"
                errors=$((errors + 1))
            fi
        done
        
    else
        log_error "❌ GitHub Actions workflow not found: .github/workflows/simple-deploy.yml"
        errors=$((errors + 1))
    fi
    
    return $errors
}

# Configuration completeness validation
validateConfigurationCompleteness() {
    local errors=0
    
    log_info "📋 Validating configuration completeness..."
    
    # Check application configuration files
    local config_files=(
        "AI.ProfilePhotoMaker.API/Configuration/EnvironmentConfiguration.cs"
        "AI.ProfilePhotoMaker.API/appsettings.json"
        "AI.ProfilePhotoMaker.API/appsettings.Development.json"
    )
    
    for file in "${config_files[@]}"; do
        if [[ -f "$file" ]]; then
            log_success "✅ Configuration file found: $file"
        else
            log_error "❌ Configuration file missing: $file"
            errors=$((errors + 1))
        fi
    done
    
    # Check for environment validation in application startup
    if [[ -f "AI.ProfilePhotoMaker.API/Configuration/EnvironmentConfiguration.cs" ]]; then
        if grep -q "ValidateAsync" AI.ProfilePhotoMaker.API/Configuration/EnvironmentConfiguration.cs; then
            log_success "✅ Environment validation method found in application"
        else
            log_error "❌ Environment validation method missing from application"
            errors=$((errors + 1))
        fi
        
        # Check for Azure Storage environment-aware validation
        if grep -q "IsProduction\|IsStaging" AI.ProfilePhotoMaker.API/Configuration/EnvironmentConfiguration.cs; then
            log_success "✅ Environment-aware Azure Storage validation implemented"
        else
            log_warning "⚠️  Environment-aware Azure Storage validation not implemented"
        fi
    fi
    
    # Check for build scripts
    local build_scripts=(
        "scripts/build-local.sh"
        "scripts/push-to-acr.sh"
    )
    
    for script in "${build_scripts[@]}"; do
        if [[ -f "$script" ]]; then
            log_success "✅ Build script found: $script"
        else
            log_warning "⚠️  Build script missing: $script"
        fi
    done
    
    return $errors
}

# Run infrastructure validation functions
INFRA_ERRORS=0

# Validate infrastructure alignment
if ! validateInfrastructureAlignment; then
    INFRA_ERRORS=$((INFRA_ERRORS + $?))
fi

echo ""

# Validate environment consistency
if ! validateEnvironmentConsistency; then
    INFRA_ERRORS=$((INFRA_ERRORS + $?))
fi

echo ""

# Validate configuration completeness
if ! validateConfigurationCompleteness; then
    INFRA_ERRORS=$((INFRA_ERRORS + $?))
fi

# Add infrastructure errors to total validation errors
VALIDATION_ERRORS=$((VALIDATION_ERRORS + INFRA_ERRORS))

if [[ $INFRA_ERRORS -eq 0 ]]; then
    log_success "✅ Infrastructure validation completed successfully"
else
    log_error "❌ Infrastructure validation failed with $INFRA_ERRORS errors"
fi

# ===========================================
# 6. VALIDATE APPLICATION CONFIGURATION
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
    
    # Enhanced remediation suggestions
    echo ""
    echo -e "${BLUE}Remediation suggestions:${NC}"
    echo ""
    echo -e "${BLUE}🔐 Secrets Issues (ALL REQUIRED):${NC}"
    echo "1. Check user-secrets: dotnet user-secrets list --project AI.ProfilePhotoMaker.API"
    echo "2. Validate GitHub Actions secrets with: gh secret list"
    echo "3. Update missing secrets - ALL secrets are REQUIRED (referenced in infrastructure)"
    echo ""
    echo -e "${BLUE}🏗️ Infrastructure Issues:${NC}"
    echo "4. Review Bicep templates in infrastructure/ directory"
    echo "5. Check environment variable naming consistency between:"
    echo "   - infrastructure/simple-deploy.bicep"
    echo "   - AI.ProfilePhotoMaker.API/Configuration/EnvironmentConfiguration.cs"
    echo "6. Validate GitHub Actions workflow parameters"
    echo ""
    echo -e "${BLUE}⚙️ Configuration Issues:${NC}"
    echo "7. Ensure ${TARGET_ENV} environment has required configuration"
    echo "8. For Azure Storage issues: verify connection string format and container name"
    echo "9. Check application configuration files for completeness"
    echo ""
    echo -e "${BLUE}🔄 Next Steps:${NC}"
    echo "10. Fix identified issues above"
    echo "11. Re-run: ./scripts/validate-secrets.sh ${TARGET_ENV}"
    echo "12. For production deployment: ensure all infrastructure validation passes"
    echo ""
    echo -e "${YELLOW}Common Issues (ALL SECRETS ARE REQUIRED):${NC}"
    echo "• Azure Storage naming: Use AZURE_STORAGE_CONNECTION_STRING (not AzureStorage__ConnectionString)"
    echo "• Google OAuth: REQUIRED - Client ID should end with .apps.googleusercontent.com, Client Secret should start with GOCSPX-"
    echo "• Replicate tokens: REQUIRED - Should start with 'r8_' and be properly formatted"
    echo "• Stripe keys: REQUIRED - Secret Key starts with 'sk_', Publishable Key starts with 'pk_', Webhook Secret starts with 'whsec_'"
    echo "• Azure Storage: REQUIRED in ALL environments (referenced in infrastructure configuration)"
    echo "• ALL secrets are referenced in infrastructure or application code and must be provided"
    
    exit 1
fi
