#!/bin/bash
# Secret Synchronization Script
# Ensures all secret stores (Azure Key Vault, GitHub Actions, dotnet user-secrets) stay in sync
# Usage: ./scripts/sync-secrets.sh [--source azure|github|local] [--secret SECRET_NAME]

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
RESOURCE_GROUP="aiprofilemaker-v1"
KEY_VAULT_NAME="aipm-kv-v1-6j74jubocuukg"
API_PROJECT_PATH="AI.ProfilePhotoMaker.API"

# Secret mappings: LOCAL_NAME:GITHUB_NAME:KEYVAULT_NAME
SECRET_MAPPINGS=(
    "MSSQL_SA_PASSWORD|SQL_ADMIN_PASSWORD|ConnectionString"
    "JWT_SECRET|JWT_SECRET|JwtSecret"
    "REPLICATE_API_TOKEN|REPLICATE_API_TOKEN|ReplicateApiToken"
    "REPLICATE_WEBHOOK_SECRET|REPLICATE_WEBHOOK_SECRET|ReplicateWebhookSecret"
    "GOOGLE_CLIENT_ID|GOOGLE_CLIENT_ID|GoogleClientId"
    "GOOGLE_CLIENT_SECRET|GOOGLE_CLIENT_SECRET|GoogleClientSecret"
    "STRIPE_SECRET_KEY|STRIPE_SECRET_KEY|StripeSecretKey"
    "STRIPE_PUBLISHABLE_KEY|STRIPE_PUBLISHABLE_KEY|StripePublishableKey"
    "STRIPE_WEBHOOK_SECRET|STRIPE_WEBHOOK_SECRET|StripeWebhookSecret"
    "OpenAI:ApiKey|OPENAI_API_KEY|OpenAIApiKey"
)

print_usage() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  --source SOURCE    Set source of truth: azure, github, or local (default: azure)"
    echo "  --secret SECRET    Sync only specific secret (default: all)"
    echo "  --dry-run         Show what would be done without making changes"
    echo "  --validate-only   Only validate consistency, don't sync"
    echo "  --help            Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0                                    # Sync all secrets from Azure Key Vault"
    echo "  $0 --source github                   # Sync all secrets from GitHub Actions"
    echo "  $0 --secret SQL_ADMIN_PASSWORD       # Sync only SQL password from Azure"
    echo "  $0 --validate-only                   # Check consistency without changes"
}

log() {
    echo -e "${BLUE}[$(date +'%Y-%m-%d %H:%M:%S')]${NC} $1"
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

# Extract password from Azure Key Vault connection string
extract_password_from_connection_string() {
    local connection_string="$1"
    echo "$connection_string" | sed 's/.*Password=\([^;]*\).*/\1/'
}

# Get secret from Azure Key Vault
get_azure_secret() {
    local secret_name="$1"
    local secret_type="$2"  # "direct" or "connection_string"
    
    if [[ "$secret_type" == "connection_string" ]]; then
        # Special handling for SQL password in connection string
        local connection_string
        connection_string=$(az keyvault secret show --name "$secret_name" --vault-name "$KEY_VAULT_NAME" --query "value" -o tsv 2>/dev/null || echo "")
        if [[ -n "$connection_string" ]]; then
            extract_password_from_connection_string "$connection_string"
        else
            echo ""
        fi
    else
        az keyvault secret show --name "$secret_name" --vault-name "$KEY_VAULT_NAME" --query "value" -o tsv 2>/dev/null || echo ""
    fi
}

# Get secret from dotnet user-secrets
get_local_secret() {
    local secret_name="$1"
    cd "$API_PROJECT_PATH"
    dotnet user-secrets list 2>/dev/null | grep "^$secret_name = " | cut -d' ' -f3- || echo ""
}

# Set secret in dotnet user-secrets
set_local_secret() {
    local secret_name="$1"
    local secret_value="$2"
    cd "$API_PROJECT_PATH"
    echo "$secret_value" | dotnet user-secrets set "$secret_name" --stdin
}

# Set secret in GitHub Actions
set_github_secret() {
    local secret_name="$1"
    local secret_value="$2"
    echo "$secret_value" | gh secret set "$secret_name"
}

# Validate prerequisites
validate_prerequisites() {
    log "Validating prerequisites..."
    
    # Check Azure CLI
    if ! command -v az &> /dev/null; then
        log_error "Azure CLI is not installed or not in PATH"
        return 1
    fi
    
    # Check GitHub CLI
    if ! command -v gh &> /dev/null; then
        log_error "GitHub CLI is not installed or not in PATH"
        return 1
    fi
    
    # Check dotnet
    if ! command -v dotnet &> /dev/null; then
        log_error "dotnet CLI is not installed or not in PATH"
        return 1
    fi
    
    # Check Azure login
    if ! az account show &> /dev/null; then
        log_error "Not logged into Azure CLI. Run 'az login' first."
        return 1
    fi
    
    # Check GitHub login
    if ! gh auth status &> /dev/null; then
        log_error "Not logged into GitHub CLI. Run 'gh auth login' first."
        return 1
    fi
    
    # Check if API project exists
    if [[ ! -f "$API_PROJECT_PATH/AI.ProfilePhotoMaker.API.csproj" ]]; then
        log_error "API project not found at $API_PROJECT_PATH"
        return 1
    fi
    
    log_success "All prerequisites validated"
    return 0
}

# Sync a single secret
sync_secret() {
    local mapping="$1"
    local source="$2"
    local dry_run="$3"
    
    IFS='|' read -r local_name github_name keyvault_name <<< "$mapping"
    
    log "Syncing secret: $local_name"
    
    # Get current values
    local azure_value=""
    local github_value="[ENCRYPTED - Cannot read]"
    local local_value=""
    
    # Get Azure value
    if [[ "$keyvault_name" == "ConnectionString" ]]; then
        azure_value=$(get_azure_secret "$keyvault_name" "connection_string")
    else
        azure_value=$(get_azure_secret "$keyvault_name" "direct")
    fi
    
    # Get local value
    local_value=$(get_local_secret "$local_name")
    
    # Determine source value
    local source_value=""
    case "$source" in
        "azure")
            source_value="$azure_value"
            if [[ -z "$source_value" ]]; then
                log_warning "No value found in Azure Key Vault for $keyvault_name"
                return 1
            fi
            ;;
        "local")
            source_value="$local_value"
            if [[ -z "$source_value" ]]; then
                log_warning "No value found in local secrets for $local_name"
                return 1
            fi
            ;;
        "github")
            log_error "Cannot read GitHub Actions secrets directly. Use --source azure or --source local"
            return 1
            ;;
        *)
            log_error "Invalid source: $source"
            return 1
            ;;
    esac
    
    # Show current state
    echo "  Azure Key Vault ($keyvault_name): ${azure_value:0:8}..."
    echo "  GitHub Actions ($github_name): $github_value"
    echo "  Local Secrets ($local_name): ${local_value:0:8}..."
    echo "  Source value ($source): ${source_value:0:8}..."
    
    # Update secrets if not dry run
    if [[ "$dry_run" == "false" ]]; then
        # Update local secrets
        if [[ "$local_value" != "$source_value" ]]; then
            log "Updating local secret: $local_name"
            set_local_secret "$local_name" "$source_value"
            log_success "Updated local secret"
        else
            echo "  Local secret already up to date"
        fi
        
        # Update GitHub Actions secret
        log "Updating GitHub Actions secret: $github_name"
        set_github_secret "$github_name" "$source_value"
        log_success "Updated GitHub Actions secret"
    else
        echo "  [DRY RUN] Would update secrets to match source"
    fi
    
    echo ""
}

# Validate secret consistency
validate_consistency() {
    log "Validating secret consistency across all stores..."
    local inconsistencies=0
    
    for mapping in "${SECRET_MAPPINGS[@]}"; do
        IFS='|' read -r local_name github_name keyvault_name <<< "$mapping"
        
        # Get Azure value
        local azure_value=""
        if [[ "$keyvault_name" == "ConnectionString" ]]; then
            azure_value=$(get_azure_secret "$keyvault_name" "connection_string")
        else
            azure_value=$(get_azure_secret "$keyvault_name" "direct")
        fi
        
        # Get local value
        local local_value
        local_value=$(get_local_secret "$local_name")
        
        # Check consistency
        if [[ -n "$azure_value" && -n "$local_value" ]]; then
            if [[ "$azure_value" != "$local_value" ]]; then
                log_warning "Inconsistency detected for $local_name:"
                echo "  Azure: ${azure_value:0:8}..."
                echo "  Local: ${local_value:0:8}..."
                ((inconsistencies++))
            else
                echo "✅ $local_name: Consistent"
            fi
        elif [[ -z "$azure_value" ]]; then
            log_warning "Missing in Azure Key Vault: $keyvault_name"
            ((inconsistencies++))
        elif [[ -z "$local_value" ]]; then
            log_warning "Missing in local secrets: $local_name"
            ((inconsistencies++))
        fi
    done
    
    if [[ $inconsistencies -eq 0 ]]; then
        log_success "All secrets are consistent across stores"
    else
        log_error "Found $inconsistencies inconsistencies"
    fi
    
    return $inconsistencies
}

# Main function
main() {
    local source="azure"
    local specific_secret=""
    local dry_run="false"
    local validate_only="false"
    
    # Parse arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            --source)
                source="$2"
                shift 2
                ;;
            --secret)
                specific_secret="$2"
                shift 2
                ;;
            --dry-run)
                dry_run="true"
                shift
                ;;
            --validate-only)
                validate_only="true"
                shift
                ;;
            --help)
                print_usage
                exit 0
                ;;
            *)
                log_error "Unknown option: $1"
                print_usage
                exit 1
                ;;
        esac
    done
    
    # Validate source
    if [[ "$source" != "azure" && "$source" != "github" && "$source" != "local" ]]; then
        log_error "Invalid source: $source. Must be 'azure', 'github', or 'local'"
        exit 1
    fi
    
    # Validate prerequisites
    if ! validate_prerequisites; then
        exit 1
    fi
    
    # Validation mode
    if [[ "$validate_only" == "true" ]]; then
        validate_consistency
        exit $?
    fi
    
    # Show plan
    log "Secret synchronization plan:"
    echo "  Source of truth: $source"
    echo "  Target: All secret stores"
    echo "  Dry run: $dry_run"
    echo "  Specific secret: ${specific_secret:-"All secrets"}"
    echo ""
    
    # Sync secrets
    local secrets_synced=0
    for mapping in "${SECRET_MAPPINGS[@]}"; do
        IFS='|' read -r local_name github_name keyvault_name <<< "$mapping"
        
        # Skip if specific secret requested and this isn't it
        if [[ -n "$specific_secret" ]]; then
            if [[ "$specific_secret" != "$local_name" && "$specific_secret" != "$github_name" && "$specific_secret" != "$keyvault_name" ]]; then
                continue
            fi
        fi
        
        if sync_secret "$mapping" "$source" "$dry_run"; then
            ((secrets_synced++))
        fi
    done
    
    if [[ $secrets_synced -gt 0 ]]; then
        log_success "Successfully synchronized $secrets_synced secret(s)"
        
        if [[ "$dry_run" == "false" ]]; then
            log "Recommendation: Test your application to ensure all secrets are working correctly"
            echo "  1. Test local development: dotnet run"
            echo "  2. Test VS Code SQL connection"
            echo "  3. Trigger GitHub Actions deployment"
        fi
    else
        log_warning "No secrets were synchronized"
    fi
}

# Run main function with all arguments
main "$@"
