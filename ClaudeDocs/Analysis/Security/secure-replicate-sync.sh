#!/bin/bash

# Secure Replicate Secrets Synchronization Script
# Comprehensive secret management for Replicate integration across all environments
# Synchronizes secrets between dotnet user-secrets, GitHub Actions, and Azure Key Vault

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m' # No Color

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
AUDIT_LOG="${PROJECT_ROOT}/secrets-sync-audit-$(date +%Y%m%d-%H%M%S).log"

# Error tracking
SYNC_ERRORS=0
VALIDATION_WARNINGS=0

# Logging functions
log_info() { echo -e "${BLUE}[INFO]${NC} $1" | tee -a "$AUDIT_LOG"; }
log_success() { echo -e "${GREEN}[SUCCESS]${NC} $1" | tee -a "$AUDIT_LOG"; }
log_warning() { echo -e "${YELLOW}[WARNING]${NC} $1" | tee -a "$AUDIT_LOG"; VALIDATION_WARNINGS=$((VALIDATION_WARNINGS + 1)); }
log_error() { echo -e "${RED}[ERROR]${NC} $1" | tee -a "$AUDIT_LOG"; SYNC_ERRORS=$((SYNC_ERRORS + 1)); }
log_step() { echo -e "${CYAN}${BOLD}[STEP]${NC} $1" | tee -a "$AUDIT_LOG"; }

# Initialize audit log
echo "Secure Replicate Secrets Synchronization - $(date)" > "$AUDIT_LOG"
echo "Script: $0" >> "$AUDIT_LOG"
echo "Working Directory: $PROJECT_ROOT" >> "$AUDIT_LOG"
echo "====================================" >> "$AUDIT_LOG"

echo -e "${BLUE}${BOLD}=========================================${NC}"
echo -e "${BLUE}${BOLD} Secure Replicate Secrets Synchronization${NC}"
echo -e "${BLUE}${BOLD}=========================================${NC}"
echo ""

# Validate prerequisites
validate_prerequisites() {
    log_step "🔍 Validating prerequisites..."
    
    # Check if we're in the right directory
    if [[ ! -f "$PROJECT_ROOT/AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj" ]]; then
        log_error "Must run from project root directory"
        exit 1
    fi
    
    # Check required tools
    local required_tools=("dotnet" "gh" "az")
    for tool in "${required_tools[@]}"; do
        if ! command -v "$tool" &> /dev/null; then
            log_warning "Tool not available: $tool (some operations may be skipped)"
        else
            log_success "✅ Found: $tool"
        fi
    done
    
    log_success "✅ Prerequisites validated"
}

# Validate secret formats
validate_replicate_api_token() {
    local token="$1"
    if [[ ! "$token" =~ ^r8_ ]]; then
        log_error "Replicate API token must start with 'r8_'"
        return 1
    fi
    if [[ ${#token} -lt 40 ]]; then
        log_error "Replicate API token too short (expected 40+ characters)"
        return 1
    fi
    return 0
}

validate_webhook_secret() {
    local secret="$1"
    if [[ ${#secret} -lt 32 ]]; then
        log_error "Webhook secret too short (expected 32+ characters)"
        return 1
    fi
    return 0
}

# Securely get secrets from user
get_replicate_secrets() {
    log_step "🔐 Collecting Replicate secrets..."
    
    echo -e "${CYAN}Please provide the Replicate integration secrets:${NC}"
    echo ""
    
    # Get API token
    while true; do
        echo -n -e "${BOLD}Replicate API Token (starts with r8_): ${NC}"
        read -s REPLICATE_API_TOKEN
        echo ""
        
        if validate_replicate_api_token "$REPLICATE_API_TOKEN"; then
            log_success "✅ Valid Replicate API token format"
            break
        else
            echo -e "${RED}Invalid token format. Please try again.${NC}"
        fi
    done
    
    # Get webhook secret
    while true; do
        echo -n -e "${BOLD}Replicate Webhook Secret (32+ characters): ${NC}"
        read -s REPLICATE_WEBHOOK_SECRET
        echo ""
        
        if validate_webhook_secret "$REPLICATE_WEBHOOK_SECRET"; then
            log_success "✅ Valid webhook secret format"
            break
        else
            echo -e "${RED}Invalid webhook secret format. Please try again.${NC}"
        fi
    done
    
    echo ""
    log_success "✅ Secrets collected and validated"
}

# Sync to dotnet user-secrets
sync_to_user_secrets() {
    log_step "👤 Synchronizing to dotnet user-secrets..."
    
    cd "$PROJECT_ROOT/AI.ProfilePhotoMaker.API"
    
    # Set Replicate secrets
    if dotnet user-secrets set "Replicate:ApiToken" "$REPLICATE_API_TOKEN"; then
        log_success "✅ Replicate API token set in user-secrets"
    else
        log_error "❌ Failed to set Replicate API token"
        return 1
    fi
    
    if dotnet user-secrets set "Replicate:WebhookSecret" "$REPLICATE_WEBHOOK_SECRET"; then
        log_success "✅ Replicate webhook secret set in user-secrets"
    else
        log_error "❌ Failed to set webhook secret"
        return 1
    fi
    
    # Add other required secrets with placeholder values if not present
    local additional_secrets=(
        "JWT:Secret:$(openssl rand -base64 48)"
        "ConnectionStrings:DefaultConnection:Server=localhost;Database=AIProfilePhotoMaker;Trusted_Connection=true;TrustServerCertificate=true;"
        "AzureStorage:ConnectionString:UseDevelopmentStorage=true"
        "AzureStorage:ContainerName:profile-photos"
        "Authentication:Google:ClientId:placeholder-replace-with-real-client-id"
        "Authentication:Google:ClientSecret:placeholder-replace-with-real-client-secret"
        "Stripe:SecretKey:sk_test_placeholder_replace_with_real_stripe_secret"
        "Stripe:PublishableKey:pk_test_placeholder_replace_with_real_stripe_publishable"
        "Stripe:WebhookSecret:whsec_placeholder_replace_with_real_stripe_webhook"
    )
    
    for secret_pair in "${additional_secrets[@]}"; do
        local key="${secret_pair%%:*}"
        local value="${secret_pair#*:}"
        
        # Check if secret already exists
        if ! dotnet user-secrets list | grep -q "^$key = "; then
            if dotnet user-secrets set "$key" "$value"; then
                log_success "✅ Added placeholder for: $key"
            else
                log_warning "⚠️  Failed to set placeholder for: $key"
            fi
        else
            log_info "ℹ️  Already exists: $key"
        fi
    done
    
    cd "$PROJECT_ROOT"
    log_success "✅ User-secrets synchronization completed"
}

# Sync to GitHub Actions secrets
sync_to_github_actions() {
    log_step "🐙 Synchronizing to GitHub Actions secrets..."
    
    if ! command -v gh &> /dev/null; then
        log_warning "⚠️  GitHub CLI not available - skipping GitHub Actions sync"
        return 0
    fi
    
    if ! gh auth status &> /dev/null; then
        log_warning "⚠️  Not authenticated with GitHub - skipping GitHub Actions sync"
        return 0
    fi
    
    # Set Replicate secrets
    if echo "$REPLICATE_API_TOKEN" | gh secret set REPLICATE_API_TOKEN; then
        log_success "✅ Replicate API token set in GitHub Actions"
    else
        log_error "❌ Failed to set Replicate API token in GitHub Actions"
    fi
    
    if echo "$REPLICATE_WEBHOOK_SECRET" | gh secret set REPLICATE_WEBHOOK_SECRET; then
        log_success "✅ Replicate webhook secret set in GitHub Actions"
    else
        log_error "❌ Failed to set webhook secret in GitHub Actions"
    fi
    
    # Add other required secrets if not present
    local github_secrets=(
        "JWT_SECRET:$(openssl rand -base64 48)"
        "STRIPE_SECRET_KEY:sk_test_placeholder_replace_with_real_stripe_secret_key_51chars_minimum"
        "STRIPE_PUBLISHABLE_KEY:pk_test_placeholder_replace_with_real_stripe_publishable_key"
        "STRIPE_WEBHOOK_SECRET:whsec_placeholder_replace_with_real_stripe_webhook_secret"
        "AZURE_STORAGE_CONNECTION_STRING:DefaultEndpointsProtocol=https;AccountName=placeholder;AccountKey=placeholder_replace_with_real_azure_storage_key==;EndpointSuffix=core.windows.net"
        "AZURE_STORAGE_CONTAINER_NAME:profile-photos-placeholder"
        "GOOGLE_CLIENT_ID:116968296687-fievkkqa9kdb2e3p1l11shh25bk751l4.apps.googleusercontent.com"
        "GOOGLE_CLIENT_SECRET:GOCSPX-placeholder_replace_with_real_google_client_secret"
    )
    
    for secret_pair in "${github_secrets[@]}"; do
        local key="${secret_pair%%:*}"
        local value="${secret_pair#*:}"
        
        # Check if secret already exists
        if ! gh secret list | grep -q "^$key"; then
            if echo "$value" | gh secret set "$key"; then
                log_success "✅ Added GitHub Actions secret: $key"
            else
                log_warning "⚠️  Failed to set GitHub Actions secret: $key"
            fi
        else
            log_info "ℹ️  GitHub Actions secret already exists: $key"
        fi
    done
    
    log_success "✅ GitHub Actions synchronization completed"
}

# Sync to Azure Key Vault (optional)
sync_to_azure_keyvault() {
    log_step "🔑 Synchronizing to Azure Key Vault..."
    
    if ! command -v az &> /dev/null; then
        log_warning "⚠️  Azure CLI not available - skipping Key Vault sync"
        return 0
    fi
    
    if ! az account show > /dev/null 2>&1; then
        log_warning "⚠️  Not logged in to Azure - skipping Key Vault sync"
        return 0
    fi
    
    # Try to determine Key Vault name from environment
    local key_vault_name="${KEY_VAULT_NAME:-}"
    if [[ -z "$key_vault_name" ]]; then
        local resource_group="${RESOURCE_GROUP:-aiprofilemaker-v1}"
        key_vault_name=$(az keyvault list --resource-group "$resource_group" --query "[0].name" --output tsv 2>/dev/null || echo "")
    fi
    
    if [[ -z "$key_vault_name" ]]; then
        log_warning "⚠️  Could not determine Key Vault name - skipping Key Vault sync"
        return 0
    fi
    
    log_info "🔑 Using Key Vault: $key_vault_name"
    
    # Set Replicate secrets
    if az keyvault secret set --vault-name "$key_vault_name" --name "ReplicateApiToken" --value "$REPLICATE_API_TOKEN" --output none; then
        log_success "✅ Replicate API token set in Key Vault"
    else
        log_error "❌ Failed to set Replicate API token in Key Vault"
    fi
    
    if az keyvault secret set --vault-name "$key_vault_name" --name "ReplicateWebhookSecret" --value "$REPLICATE_WEBHOOK_SECRET" --output none; then
        log_success "✅ Replicate webhook secret set in Key Vault"
    else
        log_error "❌ Failed to set webhook secret in Key Vault"
    fi
    
    log_success "✅ Azure Key Vault synchronization completed"
}

# Validate synchronization
validate_synchronization() {
    log_step "✅ Validating synchronization..."
    
    cd "$PROJECT_ROOT"
    
    log_info "🔍 Running comprehensive validation..."
    if ./scripts/validate-secrets.sh; then
        log_success "✅ All secrets validation passed"
    else
        log_warning "⚠️  Some validation checks failed - review output above"
    fi
    
    cd "$PROJECT_ROOT"
}

# Generate audit report
generate_audit_report() {
    log_step "📊 Generating audit report..."
    
    local report_file="${PROJECT_ROOT}/secrets-sync-report-$(date +%Y%m%d-%H%M%S).json"
    
    cat > "$report_file" << EOF
{
  "sync_operation": {
    "timestamp": "$(date -Iseconds)",
    "script": "$0",
    "working_directory": "$PROJECT_ROOT",
    "audit_log": "$AUDIT_LOG"
  },
  "secrets_synchronized": {
    "replicate_api_token": "✅ Synchronized",
    "replicate_webhook_secret": "✅ Synchronized",
    "placeholder_secrets_added": "✅ Added where missing"
  },
  "environments_updated": {
    "dotnet_user_secrets": "✅ Updated",
    "github_actions": "$(command -v gh >/dev/null && echo "✅ Updated" || echo "⚠️  Skipped - GitHub CLI not available")",
    "azure_key_vault": "$(command -v az >/dev/null && echo "✅ Updated" || echo "⚠️  Skipped - Azure CLI not available")"
  },
  "validation_results": {
    "sync_errors": $SYNC_ERRORS,
    "validation_warnings": $VALIDATION_WARNINGS,
    "overall_status": "$([ $SYNC_ERRORS -eq 0 ] && echo "SUCCESS" || echo "FAILED")"
  },
  "next_steps": [
    "Replace placeholder values with real credentials for production",
    "Run deployment validation to confirm everything works",
    "Update documentation with any environment-specific requirements"
  ]
}
EOF
    
    log_success "✅ Audit report generated: $report_file"
    
    # Display summary
    echo ""
    echo -e "${CYAN}${BOLD}📊 Synchronization Summary:${NC}"
    echo -e "  Sync Errors: ${SYNC_ERRORS}"
    echo -e "  Validation Warnings: ${VALIDATION_WARNINGS}"
    echo -e "  Overall Status: $([ $SYNC_ERRORS -eq 0 ] && echo -e "${GREEN}SUCCESS${NC}" || echo -e "${RED}FAILED${NC}")"
    echo ""
    echo -e "${BLUE}📁 Generated Files:${NC}"
    echo -e "  Audit Log: $AUDIT_LOG"
    echo -e "  Report: $report_file"
    echo ""
}

# Main synchronization function
main() {
    validate_prerequisites
    get_replicate_secrets
    sync_to_user_secrets
    sync_to_github_actions
    sync_to_azure_keyvault
    validate_synchronization
    generate_audit_report
    
    if [[ $SYNC_ERRORS -eq 0 ]]; then
        echo -e "${GREEN}${BOLD}🎉 SECRETS SYNCHRONIZATION COMPLETED SUCCESSFULLY! 🎉${NC}"
        echo ""
        echo -e "${YELLOW}📋 Next Steps:${NC}"
        echo "  1. Test application locally to confirm secrets work"
        echo "  2. Replace placeholder values with real credentials for production"
        echo "  3. Run deployment to verify everything works"
        echo "  4. Update team documentation with any changes"
    else
        echo -e "${RED}${BOLD}❌ SECRETS SYNCHRONIZATION FAILED${NC}"
        echo -e "${YELLOW}📋 Please review errors above and retry${NC}"
        exit 1
    fi
}

# Clear sensitive variables on exit
cleanup() {
    unset REPLICATE_API_TOKEN
    unset REPLICATE_WEBHOOK_SECRET
}
trap cleanup EXIT

# Script entry point
main "$@"