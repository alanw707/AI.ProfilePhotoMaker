#!/bin/bash
set -euo pipefail

# Azure Key Vault Webhook Secret Update Script
# One-time script to add the real REPLICATE_WEBHOOK_SECRET to Azure Key Vault
#
# This script addresses the gap where the webhook secret exists in GitHub Actions
# but needs to be added to Azure Key Vault for the automated sync to work properly.

# Colors for output
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[1;33m'
readonly BLUE='\033[0;34m'
readonly PURPLE='\033[0;35m'
readonly NC='\033[0m' # No Color

# Configuration
readonly KEYVAULT_NAME="aipm-kv-v1-6j74jubocuukg"
readonly SECRET_NAME="ReplicateWebhookSecret"
readonly MIN_WEBHOOK_SECRET_LENGTH=32

# Logging function with timestamp
log() {
    echo -e "${1}[$(date '+%Y-%m-%d %H:%M:%S')] ${2}${NC}"
}

# Validate webhook secret format
validate_webhook_secret() {
    local secret="$1"
    
    # Check minimum length
    if [[ ${#secret} -lt $MIN_WEBHOOK_SECRET_LENGTH ]]; then
        log "$RED" "ERROR: Webhook secret too short (minimum $MIN_WEBHOOK_SECRET_LENGTH characters)"
        return 1
    fi
    
    # Check for placeholder values
    if [[ "$secret" == *"placeholder"* ]] || [[ "$secret" == *"test"* ]] || [[ "$secret" == *"REPLACE"* ]]; then
        log "$RED" "ERROR: Webhook secret appears to be a placeholder value"
        return 1
    fi
    
    # Check for sufficient entropy (no repeated patterns)
    if [[ "$secret" =~ (.)\1{10,} ]]; then
        log "$YELLOW" "WARNING: Webhook secret may have low entropy (repeated characters detected)"
    fi
    
    log "$GREEN" "✅ Webhook secret format validation passed"
    return 0
}

# Secure input function with masking
secure_input() {
    local prompt="$1"
    local value
    
    echo -n -e "${BLUE}${prompt}${NC}"
    read -s value
    echo  # New line after hidden input
    
    if [[ -z "$value" ]]; then
        log "$RED" "ERROR: Empty value provided"
        return 1
    fi
    
    echo "$value"
    return 0
}

# Main function
main() {
    log "$PURPLE" "🔐 Azure Key Vault Webhook Secret Update"
    log "$PURPLE" "======================================="
    echo
    
    log "$BLUE" "🛡️  SECURITY NOTICE:"
    log "$BLUE" "   This script will securely add the REPLICATE_WEBHOOK_SECRET to Azure Key Vault"
    log "$BLUE" "   The secret value will be masked during input and validated before storage"
    log "$BLUE" "   Only proceed if you have the authorized webhook secret value"
    echo
    
    # Check Azure authentication
    log "$BLUE" "🔐 Checking Azure CLI authentication..."
    if ! az account show &>/dev/null; then
        log "$RED" "ERROR: Not authenticated to Azure CLI"
        log "$YELLOW" "Please run: az login"
        exit 1
    fi
    
    local account_name
    account_name=$(az account show --query "name" -o tsv)
    log "$GREEN" "✅ Authenticated to Azure account: $account_name"
    echo
    
    # Show current secret status
    log "$BLUE" "🔍 Checking current webhook secret in Key Vault..."
    local current_secret
    if current_secret=$(az keyvault secret show --vault-name "$KEYVAULT_NAME" --name "$SECRET_NAME" --query "value" -o tsv 2>/dev/null); then
        if [[ "$current_secret" == *"placeholder"* ]]; then
            log "$YELLOW" "⚠️  Found placeholder webhook secret in Key Vault"
            log "$YELLOW" "   This needs to be updated with the real webhook secret"
        else
            log "$GREEN" "✅ Webhook secret already exists in Key Vault"
            log "$YELLOW" "   If you need to update it, continue. Otherwise, press Ctrl+C to exit."
        fi
    else
        log "$YELLOW" "⚠️  No webhook secret found in Key Vault"
    fi
    echo
    
    # Instructions for getting the secret
    log "$BLUE" "📋 How to get the REPLICATE_WEBHOOK_SECRET:"
    log "$YELLOW" "   1. Go to your GitHub repository settings"
    log "$YELLOW" "   2. Navigate to Settings > Secrets and variables > Actions"
    log "$YELLOW" "   3. Find REPLICATE_WEBHOOK_SECRET in the list"
    log "$YELLOW" "   4. You'll need to re-create the secret to see its value"
    log "$YELLOW" "   5. Or get it from your Replicate account webhook settings"
    echo
    
    log "$BLUE" "🔗 Alternative: Get from Replicate Dashboard"
    log "$YELLOW" "   1. Go to https://replicate.com/account/api-tokens"
    log "$YELLOW" "   2. Find your webhook configuration"
    log "$YELLOW" "   3. Copy the webhook secret value"
    echo
    
    # Get the webhook secret securely
    log "$BLUE" "📝 Please provide the REPLICATE_WEBHOOK_SECRET:"
    log "$YELLOW" "   This should be the same secret used in GitHub Actions"
    log "$YELLOW" "   Format: minimum 32 characters, high entropy string"
    echo
    
    local webhook_secret
    if ! webhook_secret=$(secure_input "Enter REPLICATE_WEBHOOK_SECRET: "); then
        exit 1
    fi
    
    # Validate the secret
    if ! validate_webhook_secret "$webhook_secret"; then
        exit 1
    fi
    echo
    
    # Confirmation
    log "$YELLOW" "🔍 Ready to update webhook secret in Azure Key Vault"
    log "$YELLOW" "   Target Key Vault: $KEYVAULT_NAME"
    log "$YELLOW" "   Secret name: $SECRET_NAME"
    echo
    
    read -p "$(echo -e ${BLUE})Proceed with update? (y/N): $(echo -e ${NC})" -n 1 -r
    echo
    
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        log "$YELLOW" "❌ Update cancelled by user"
        exit 1
    fi
    
    # Update the secret in Key Vault
    log "$BLUE" "🔄 Updating webhook secret in Azure Key Vault..."
    
    if az keyvault secret set --vault-name "$KEYVAULT_NAME" --name "$SECRET_NAME" --value "$webhook_secret" > /dev/null; then
        log "$GREEN" "✅ Webhook secret updated successfully in Key Vault"
    else
        log "$RED" "❌ Failed to update webhook secret in Key Vault"
        exit 1
    fi
    
    # Clear variable from memory (security)
    unset webhook_secret
    
    echo
    log "$GREEN" "🎉 Webhook secret update completed successfully!"
    
    # Verify the secret was updated
    log "$BLUE" "🔍 Verifying secret was updated correctly..."
    
    if az keyvault secret show --vault-name "$KEYVAULT_NAME" --name "$SECRET_NAME" --query "name" -o tsv > /dev/null; then
        log "$GREEN" "✅ Verification passed - Webhook secret found in Key Vault"
    else
        log "$RED" "❌ Verification failed - Webhook secret not found"
        exit 1
    fi
    
    echo
    log "$GREEN" "📋 Next Steps:"
    log "$GREEN" "=============="
    echo
    log "$BLUE" "1. Run the automated sync script:"
    log "$YELLOW" "   ./ClaudeDocs/Analysis/Security/automated-azure-keyvault-sync.sh"
    echo
    log "$BLUE" "2. Verify local development secrets:"
    log "$YELLOW" "   dotnet user-secrets list --project AI.ProfilePhotoMaker.API"
    echo
    log "$BLUE" "3. Test webhook functionality:"
    log "$YELLOW" "   Test webhook signature validation in your application"
    echo
    
    # Audit log
    log "$GREEN" "🔒 AUDIT: Webhook secret update completed successfully at $(date)"
    log "$GREEN" "🔒 AUDIT: Key Vault: $KEYVAULT_NAME"
    log "$GREEN" "🔒 AUDIT: Secret: $SECRET_NAME"
    log "$GREEN" "🔒 AUDIT: User: $(whoami), Host: $(hostname)"
}

# Script entry point
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    main "$@"
fi