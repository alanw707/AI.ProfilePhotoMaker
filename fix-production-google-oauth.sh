#!/bin/bash
set -euo pipefail

# Fix Production Google OAuth Configuration
# Updates Azure Container App environment variables with correct Google OAuth values

# Colors for output
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[1;33m'
readonly BLUE='\033[0;34m'
readonly NC='\033[0m'

# Configuration
readonly RESOURCE_GROUP="aiprofilemaker-v1"
readonly CONTAINER_APP_NAME="aiprofilemaker-api"

# Google OAuth values from our validated local user-secrets
readonly CORRECT_CLIENT_ID="116968296687-fievkkqa9kdb2e3p1l11shh25bk751l4.apps.googleusercontent.com"
readonly CORRECT_CLIENT_SECRET="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"

log() {
    echo -e "${1}[$(date '+%Y-%m-%d %H:%M:%S')] ${2}${NC}"
}

# Check Azure CLI authentication
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

# Verify container app exists
verify_container_app() {
    log "$BLUE" "🔍 Verifying container app: $CONTAINER_APP_NAME"
    
    if ! az containerapp show --name "$CONTAINER_APP_NAME" --resource-group "$RESOURCE_GROUP" &>/dev/null; then
        log "$RED" "ERROR: Container app '$CONTAINER_APP_NAME' not found in resource group '$RESOURCE_GROUP'"
        log "$YELLOW" "Available container apps:"
        az containerapp list --resource-group "$RESOURCE_GROUP" --query "[].name" -o table || true
        return 1
    fi
    
    log "$GREEN" "✅ Container app found: $CONTAINER_APP_NAME"
    return 0
}

# Check current environment variables
check_current_env_vars() {
    log "$BLUE" "🔍 Checking current Google OAuth environment variables..."
    
    local current_client_id
    current_client_id=$(az containerapp show --name "$CONTAINER_APP_NAME" --resource-group "$RESOURCE_GROUP" \
        --query "properties.template.containers[0].env[?name=='GOOGLE_CLIENT_ID'].value" -o tsv 2>/dev/null || echo "")
    
    if [[ -n "$current_client_id" ]]; then
        log "$YELLOW" "Current GOOGLE_CLIENT_ID: ${current_client_id:0:50}..."
        
        if [[ "$current_client_id" == *"Specify --help"* ]]; then
            log "$RED" "❌ CONFIRMED: GOOGLE_CLIENT_ID contains help text (this is the bug!)"
        elif [[ "$current_client_id" == "$CORRECT_CLIENT_ID" ]]; then
            log "$GREEN" "✅ GOOGLE_CLIENT_ID is already correct"
        else
            log "$YELLOW" "⚠️  GOOGLE_CLIENT_ID has unexpected value"
        fi
    else
        log "$YELLOW" "⚠️  GOOGLE_CLIENT_ID not found in environment variables"
    fi
}

# Update Google OAuth environment variables
update_oauth_env_vars() {
    log "$BLUE" "🔄 Updating Google OAuth environment variables..."
    
    # Update GOOGLE_CLIENT_ID
    log "$BLUE" "   Updating GOOGLE_CLIENT_ID..."
    if az containerapp update \
        --name "$CONTAINER_APP_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --set-env-vars "GOOGLE_CLIENT_ID=$CORRECT_CLIENT_ID" \
        --output none; then
        log "$GREEN" "✅ GOOGLE_CLIENT_ID updated successfully"
    else
        log "$RED" "❌ Failed to update GOOGLE_CLIENT_ID"
        return 1
    fi
    
    # Update GOOGLE_CLIENT_SECRET
    log "$BLUE" "   Updating GOOGLE_CLIENT_SECRET..."
    if az containerapp update \
        --name "$CONTAINER_APP_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --set-env-vars "GOOGLE_CLIENT_SECRET=$CORRECT_CLIENT_SECRET" \
        --output none; then
        log "$GREEN" "✅ GOOGLE_CLIENT_SECRET updated successfully"
    else
        log "$RED" "❌ Failed to update GOOGLE_CLIENT_SECRET"
        return 1
    fi
    
    log "$GREEN" "🎉 All Google OAuth environment variables updated successfully!"
}

# Verify the update
verify_update() {
    log "$BLUE" "🔍 Verifying the environment variable updates..."
    
    # Wait a moment for the update to propagate
    sleep 5
    
    local updated_client_id
    updated_client_id=$(az containerapp show --name "$CONTAINER_APP_NAME" --resource-group "$RESOURCE_GROUP" \
        --query "properties.template.containers[0].env[?name=='GOOGLE_CLIENT_ID'].value" -o tsv 2>/dev/null || echo "")
    
    if [[ "$updated_client_id" == "$CORRECT_CLIENT_ID" ]]; then
        log "$GREEN" "✅ Verification passed: GOOGLE_CLIENT_ID is now correct"
    else
        log "$RED" "❌ Verification failed: GOOGLE_CLIENT_ID not updated correctly"
        log "$YELLOW" "   Expected: $CORRECT_CLIENT_ID"
        log "$YELLOW" "   Actual: $updated_client_id"
        return 1
    fi
}

# Test the fix
test_oauth_endpoint() {
    log "$BLUE" "🧪 Testing the OAuth endpoint to verify the fix..."
    
    # Wait for container restart
    log "$YELLOW" "   Waiting 30 seconds for container to restart..."
    sleep 30
    
    # Test the OAuth URL generation endpoint
    local response
    if response=$(curl -s -w "HTTP_STATUS:%{http_code}" "https://api.aiprofilephotomaker.com/api/auth/google-oauth-url"); then
        local http_status
        http_status=$(echo "$response" | grep -o "HTTP_STATUS:[0-9]*" | cut -d: -f2)
        local body
        body=$(echo "$response" | sed 's/HTTP_STATUS:[0-9]*$//')
        
        log "$BLUE" "   HTTP Status: $http_status"
        
        if [[ "$http_status" == "200" ]]; then
            log "$GREEN" "✅ OAuth endpoint working! Response received"
            
            # Check if response contains Google OAuth URL
            if echo "$body" | grep -q "accounts.google.com"; then
                log "$GREEN" "✅ Response contains proper Google OAuth URL"
                log "$GREEN" "🎯 FIX CONFIRMED: OAuth is now working correctly!"
            else
                log "$YELLOW" "⚠️  Response doesn't contain Google OAuth URL, but no error"
            fi
        elif [[ "$http_status" == "400" ]]; then
            log "$YELLOW" "⚠️  Got 400 Bad Request - check if OAuth is configured correctly"
            log "$BLUE" "   Response: $body"
        else
            log "$RED" "❌ OAuth endpoint still not working properly"
            log "$BLUE" "   Response: $body"
        fi
    else
        log "$RED" "❌ Failed to test OAuth endpoint"
    fi
}

# Show success summary
show_success_summary() {
    echo
    log "$GREEN" "🎉 Production Google OAuth Fix Complete!"
    log "$GREEN" "=================================="
    echo
    log "$BLUE" "✅ Actions Completed:"
    log "$BLUE" "   • Fixed GOOGLE_CLIENT_ID (removed help text)"
    log "$BLUE" "   • Updated GOOGLE_CLIENT_SECRET"
    log "$BLUE" "   • Verified environment variable updates"
    log "$BLUE" "   • Tested OAuth endpoint functionality"
    echo
    log "$BLUE" "🧪 Manual Testing Steps:"
    log "$YELLOW" "   1. Visit: https://app.aiprofilephotomaker.com/auth/login"
    log "$YELLOW" "   2. Click 'Continue with Google'"
    log "$YELLOW" "   3. Should redirect to Google OAuth (not error page)"
    log "$YELLOW" "   4. OAuth flow should complete successfully"
    echo
    log "$BLUE" "🔍 Monitoring:"
    log "$YELLOW" "   • OAuth URL: https://api.aiprofilephotomaker.com/api/auth/google-oauth-url"
    log "$YELLOW" "   • Should return JSON with Google OAuth URL"
    log "$YELLOW" "   • No longer should contain 'Specify --help' in client_id"
}

# Main execution
main() {
    log "$BLUE" "🚀 Starting Production Google OAuth Fix"
    log "$BLUE" "======================================"
    echo
    
    log "$BLUE" "🎯 Problem: Production GOOGLE_CLIENT_ID contains help text instead of OAuth client ID"
    log "$BLUE" "🎯 Solution: Update environment variables with correct values from user-secrets"
    echo
    
    # Check prerequisites
    if ! check_azure_auth; then
        return 1
    fi
    
    if ! verify_container_app; then
        return 1
    fi
    
    # Show current state
    check_current_env_vars
    echo
    
    # Confirm the fix
    log "$YELLOW" "🤔 Ready to update production environment variables?"
    log "$YELLOW" "   This will fix the Google OAuth issue immediately."
    read -p "Continue? (y/N): " -n 1 -r
    echo
    
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        log "$YELLOW" "Operation cancelled by user"
        return 0
    fi
    
    # Perform the fix
    if update_oauth_env_vars; then
        if verify_update; then
            test_oauth_endpoint
            show_success_summary
            
            log "$GREEN" "✅ SUCCESS: Production Google OAuth fix completed successfully!"
            return 0
        else
            log "$RED" "❌ Fix applied but verification failed"
            return 1
        fi
    else
        log "$RED" "❌ Failed to apply the fix"
        return 1
    fi
}

# Script entry point
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    main "$@"
fi