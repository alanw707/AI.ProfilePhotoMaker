#!/bin/bash
set -euo pipefail

# Google OAuth Azure Key Vault to Environment Synchronization
# Based on the unified secrets management framework
# Specifically handles Google OAuth client ID and secret synchronization

# Colors for output
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[1;33m'
readonly BLUE='\033[0;34m'
readonly PURPLE='\033[0;35m'
readonly NC='\033[0m' # No Color

# Configuration
readonly RESOURCE_GROUP="aiprofilemaker-v1"
readonly PROJECT_PATH="AI.ProfilePhotoMaker.API"

# Key Vault secret names for Google OAuth
readonly KV_GOOGLE_CLIENT_ID_NAME="GoogleClientId"
readonly KV_GOOGLE_CLIENT_SECRET_NAME="GoogleClientSecret"

# Logging function with timestamp
log() {
    echo -e "${1}[$(date '+%Y-%m-%d %H:%M:%S')] ${2}${NC}"
}

# Google OAuth validation functions
validate_google_client_id() {
    local client_id="$1"
    
    # Check if it contains help text (the current production issue)
    if [[ "$client_id" == *"Specify --help"* ]] || [[ "$client_id" == *"command"* ]] || [[ "$client_id" == *"options"* ]]; then
        log "$RED" "ERROR: Google Client ID contains help text instead of actual OAuth client ID"
        log "$RED" "       Current value appears to be command help output"
        return 1
    fi
    
    # Check for placeholder values
    if [[ "$client_id" == *"REPLACE_WITH"* ]] || [[ "$client_id" == *"your-client-id"* ]] || [[ "$client_id" == *"placeholder"* ]]; then
        log "$RED" "ERROR: Google Client ID appears to be a placeholder value"
        return 1
    fi
    
    # Check basic format (should end with .apps.googleusercontent.com)
    if [[ ! "$client_id" == *".apps.googleusercontent.com" ]]; then
        log "$RED" "ERROR: Google Client ID should end with .apps.googleusercontent.com"
        log "$RED" "       Expected format: 123456789-abc123.apps.googleusercontent.com"
        return 1
    fi
    
    # Check minimum length
    if [[ ${#client_id} -lt 30 ]]; then
        log "$RED" "ERROR: Google Client ID too short (minimum 30 characters)"
        return 1
    fi
    
    log "$GREEN" "✅ Google Client ID format validation passed"
    return 0
}

validate_google_client_secret() {
    local client_secret="$1"
    
    # Check if it contains help text
    if [[ "$client_secret" == *"Specify --help"* ]] || [[ "$client_secret" == *"command"* ]] || [[ "$client_secret" == *"options"* ]]; then
        log "$RED" "ERROR: Google Client Secret contains help text instead of actual OAuth secret"
        return 1
    fi
    
    # Check for placeholder values
    if [[ "$client_secret" == *"REPLACE_WITH"* ]] || [[ "$client_secret" == *"your-client-secret"* ]] || [[ "$client_secret" == *"placeholder"* ]]; then
        log "$RED" "ERROR: Google Client Secret appears to be a placeholder value"
        return 1
    fi
    
    # Check minimum length (Google client secrets are typically 24+ characters)
    if [[ ${#client_secret} -lt 20 ]]; then
        log "$RED" "ERROR: Google Client Secret too short (minimum 20 characters)"
        return 1
    fi
    
    # Check if it starts with GOCSPX- (newer Google client secrets)
    if [[ "$client_secret" == GOCSPX-* ]]; then
        log "$GREEN" "✅ Google Client Secret has correct GOCSPX- prefix"
    else
        log "$YELLOW" "⚠️  Google Client Secret doesn't start with GOCSPX- (may be older format)"
    fi
    
    log "$GREEN" "✅ Google Client Secret format validation passed"
    return 0
}

# Azure authentication check
check_azure_auth() {
    log "$BLUE" "🔐 Checking Azure CLI authentication..."
    
    if ! az account show &>/dev/null; then
        log "$RED" "ERROR: Not authenticated to Azure CLI"
        log "$YELLOW" "Please run: az login"
        return 1
    fi
    
    local account_name
    account_name=$(az account show --query "name" -o tsv)
    log "$GREEN" "✅ Authenticated to Azure account: $account_name"
    return 0
}

# Discover Key Vault name automatically
discover_keyvault() {
    log "$BLUE" "🔍 Discovering Key Vault in resource group: $RESOURCE_GROUP"
    
    local keyvault_name
    keyvault_name=$(az keyvault list --resource-group "$RESOURCE_GROUP" --query "[0].name" -o tsv 2>/dev/null || echo "")
    
    if [[ -z "$keyvault_name" ]] || [[ "$keyvault_name" == "null" ]]; then
        log "$RED" "ERROR: No Key Vault found in resource group: $RESOURCE_GROUP"
        log "$YELLOW" "Available resource groups:"
        az group list --query "[].name" -o table || true
        return 1
    fi
    
    log "$GREEN" "✅ Found Key Vault: $keyvault_name"
    echo "$keyvault_name"
    return 0
}

# Securely retrieve secret from Key Vault
get_keyvault_secret() {
    local keyvault_name="$1"
    local secret_name="$2"
    local secret_value
    
    log "$BLUE" "🔓 Retrieving $secret_name from Key Vault..."
    
    # Retrieve secret value securely (no logging of value)
    if ! secret_value=$(az keyvault secret show --vault-name "$keyvault_name" --name "$secret_name" --query "value" -o tsv 2>/dev/null); then
        log "$RED" "ERROR: Failed to retrieve secret '$secret_name' from Key Vault '$keyvault_name'"
        log "$YELLOW" "Available secrets in Key Vault:"
        az keyvault secret list --vault-name "$keyvault_name" --query "[].name" -o table || true
        return 1
    fi
    
    if [[ -z "$secret_value" ]] || [[ "$secret_value" == "null" ]]; then
        log "$RED" "ERROR: Secret '$secret_name' is empty or not found"
        return 1
    fi
    
    log "$GREEN" "✅ Successfully retrieved $secret_name"
    
    # Return value through stdout (secure method)
    echo "$secret_value"
    return 0
}

# Check current user-secrets status for Google OAuth
check_current_google_secrets() {
    log "$BLUE" "🔍 Checking current Google OAuth secrets in user-secrets..."
    
    # Check if user-secrets is initialized
    if ! dotnet user-secrets list --project "$PROJECT_PATH" &>/dev/null; then
        log "$YELLOW" "⚠️  User-secrets not initialized, initializing now..."
        dotnet user-secrets init --project "$PROJECT_PATH"
    fi
    
    # List current Google OAuth secrets
    log "$BLUE" "Current Google OAuth secrets:"
    dotnet user-secrets list --project "$PROJECT_PATH" | grep -i "Authentication:Google" || log "$YELLOW" "No Google OAuth secrets found"
    
    return 0
}

# Main Google OAuth synchronization function
sync_google_oauth_from_keyvault() {
    local keyvault_name google_client_id google_client_secret
    
    log "$PURPLE" "🔐 Google OAuth Azure Key Vault Synchronization"
    log "$PURPLE" "============================================="
    echo
    
    # Security notice
    log "$BLUE" "🛡️  FIXING PRODUCTION OAUTH ISSUE:"
    log "$BLUE" "   ❌ Current issue: GOOGLE_CLIENT_ID contains help text"
    log "$BLUE" "   ✅ Solution: Retrieve correct values from Azure Key Vault"
    log "$BLUE" "   ✅ Sync to local user-secrets for development"
    log "$BLUE" "   ✅ Update production environment variables"
    echo
    
    # Check prerequisites
    if ! check_azure_auth; then
        return 1
    fi
    
    # Change to project root
    if [[ -f "AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj" ]]; then
        # Already in project root
        :
    elif [[ -f "../AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj" ]]; then
        cd ..
    else
        log "$RED" "ERROR: Could not find project root directory"
        return 1
    fi
    
    # Discover Key Vault
    if ! keyvault_name=$(discover_keyvault); then
        return 1
    fi
    
    # Check current state
    check_current_google_secrets
    echo
    
    # Retrieve Google OAuth secrets from Key Vault
    log "$BLUE" "📥 Retrieving Google OAuth secrets from Azure Key Vault..."
    echo
    
    # Get Google Client ID
    if ! google_client_id=$(get_keyvault_secret "$keyvault_name" "$KV_GOOGLE_CLIENT_ID_NAME"); then
        log "$RED" "Failed to retrieve Google Client ID"
        return 1
    fi
    
    # Validate client ID format
    if ! validate_google_client_id "$google_client_id"; then
        log "$RED" "Google Client ID validation failed"
        return 1
    fi
    
    # Get Google Client Secret
    if ! google_client_secret=$(get_keyvault_secret "$keyvault_name" "$KV_GOOGLE_CLIENT_SECRET_NAME"); then
        log "$RED" "Failed to retrieve Google Client Secret"
        return 1
    fi
    
    # Validate client secret format
    if ! validate_google_client_secret "$google_client_secret"; then
        log "$RED" "Google Client Secret validation failed"
        return 1
    fi
    
    echo
    log "$BLUE" "🔄 Adding Google OAuth secrets to dotnet user-secrets..."
    
    # Add secrets to user-secrets
    if dotnet user-secrets set "Authentication:Google:ClientId" "$google_client_id" --project "$PROJECT_PATH"; then
        log "$GREEN" "✅ Google Client ID synchronized successfully"
    else
        log "$RED" "❌ Failed to add Google Client ID"
        return 1
    fi
    
    if dotnet user-secrets set "Authentication:Google:ClientSecret" "$google_client_secret" --project "$PROJECT_PATH"; then
        log "$GREEN" "✅ Google Client Secret synchronized successfully"
    else
        log "$RED" "❌ Failed to add Google Client Secret"
        return 1
    fi
    
    # Clear variables from memory (security)
    unset google_client_id google_client_secret
    
    echo
    log "$GREEN" "🎉 Google OAuth secrets synchronization completed successfully!"
    
    # Verify the secrets were added
    log "$BLUE" "🔍 Verifying Google OAuth secrets were synchronized correctly..."
    
    local current_secrets
    current_secrets=$(dotnet user-secrets list --project "$PROJECT_PATH" | grep -i "Authentication:Google" || true)
    
    if [[ -n "$current_secrets" ]]; then
        log "$GREEN" "✅ Verification passed - Google OAuth secrets found in user-secrets:"
        echo "$current_secrets" | sed 's/^/   /'
    else
        log "$RED" "❌ Verification failed - Google OAuth secrets not found"
        return 1
    fi
    
    # Audit log
    log "$GREEN" "🔒 AUDIT: Google OAuth Key Vault synchronization completed"
    log "$GREEN" "🔒 AUDIT: Source: Key Vault '$keyvault_name'"
    log "$GREEN" "🔒 AUDIT: Target: dotnet user-secrets for $PROJECT_PATH"
    log "$GREEN" "🔒 AUDIT: Timestamp: $(date)"
    log "$GREEN" "🔒 AUDIT: User: $(whoami), Host: $(hostname)"
    
    return 0
}

# Show next steps for production environment update
show_production_fix_steps() {
    echo
    log "$PURPLE" "📋 Production Environment Fix Steps:"
    log "$PURPLE" "===================================="
    echo
    log "$BLUE" "1. Update Production Environment Variables:"
    log "$YELLOW" "   az containerapp update --name aiprofilemaker-api --resource-group aiprofilemaker-v1 \\"
    log "$YELLOW" "     --set-env-vars GOOGLE_CLIENT_ID=@${keyvault_name}@${KV_GOOGLE_CLIENT_ID_NAME}"
    log "$YELLOW" "   az containerapp update --name aiprofilemaker-api --resource-group aiprofilemaker-v1 \\"
    log "$YELLOW" "     --set-env-vars GOOGLE_CLIENT_SECRET=@${keyvault_name}@${KV_GOOGLE_CLIENT_SECRET_NAME}"
    echo
    log "$BLUE" "2. Alternative: Direct Environment Variable Update:"
    log "$YELLOW" "   # Get the values from Key Vault first"
    log "$YELLOW" "   GOOGLE_CLIENT_ID=\$(az keyvault secret show --vault-name $keyvault_name --name $KV_GOOGLE_CLIENT_ID_NAME --query value -o tsv)"
    log "$YELLOW" "   az containerapp update --name aiprofilemaker-api --resource-group aiprofilemaker-v1 \\"
    log "$YELLOW" "     --set-env-vars GOOGLE_CLIENT_ID=\$GOOGLE_CLIENT_ID"
    echo
    log "$BLUE" "3. Verify the Fix:"
    log "$YELLOW" "   # Test the OAuth URL generation endpoint"
    log "$YELLOW" "   curl https://api.aiprofilephotomaker.com/api/auth/google-oauth-url"
    log "$YELLOW" "   # Should return a proper Google OAuth URL, not an error"
    echo
    log "$BLUE" "4. Use Enhanced Validation:"
    log "$YELLOW" "   ./scripts/validate-secrets.sh Production"
    log "$YELLOW" "   # Should detect and validate Google OAuth configuration"
}

# Main execution
main() {
    if sync_google_oauth_from_keyvault; then
        show_production_fix_steps
        
        log "$GREEN" "✅ SUCCESS: Google OAuth Key Vault synchronization complete"
        log "$GREEN" "🎯 Next: Update production environment variables to fix the OAuth issue"
        exit 0
    else
        log "$RED" "❌ FAILED: Google OAuth synchronization failed"
        log "$YELLOW" "Please check the error messages above and ensure:"
        log "$YELLOW" "  - Azure CLI is authenticated (az login)"
        log "$YELLOW" "  - Key Vault contains GoogleClientId and GoogleClientSecret secrets"
        log "$YELLOW" "  - Appropriate permissions to access Key Vault"
        exit 1
    fi
}

# Script entry point
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    main "$@"
fi