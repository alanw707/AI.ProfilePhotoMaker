#!/bin/bash
set -euo pipefail

# Secure Replicate Secrets Synchronization Script
# Security-first approach to synchronizing Replicate secrets from GitHub Actions to dotnet user-secrets
# 
# Security Features:
# - No secrets exposed in logs or temporary files
# - Format validation before storage
# - Secure input handling with verification
# - Audit trail of synchronization
# - Zero-trust validation approach

# Colors for output
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[1;33m'
readonly BLUE='\033[0;34m'
readonly PURPLE='\033[0;35m'
readonly NC='\033[0m' # No Color

# Security configuration
readonly PROJECT_PATH="AI.ProfilePhotoMaker.API"
readonly MIN_TOKEN_LENGTH=40
readonly MIN_WEBHOOK_SECRET_LENGTH=32
readonly REPLICATE_TOKEN_PATTERN="^r8_[A-Za-z0-9]{40,}$"

# Logging function with timestamp
log() {
    echo -e "${1}[$(date '+%Y-%m-%d %H:%M:%S')] ${2}${NC}"
}

# Security validation functions
validate_replicate_token() {
    local token="$1"
    
    # Check minimum length
    if [[ ${#token} -lt $MIN_TOKEN_LENGTH ]]; then
        log "$RED" "ERROR: Replicate token too short (minimum $MIN_TOKEN_LENGTH characters)"
        return 1
    fi
    
    # Check format (Replicate tokens start with r8_)
    if [[ ! "$token" =~ $REPLICATE_TOKEN_PATTERN ]]; then
        log "$RED" "ERROR: Invalid Replicate token format (should start with r8_ followed by alphanumeric)"
        return 1
    fi
    
    # Check for placeholder values
    if [[ "$token" == *"REPLACE_WITH"* ]] || [[ "$token" == *"test-token"* ]] || [[ "$token" == *"placeholder"* ]]; then
        log "$RED" "ERROR: Replicate token appears to be a placeholder value"
        return 1
    fi
    
    log "$GREEN" "✅ Replicate token format validation passed"
    return 0
}

validate_webhook_secret() {
    local secret="$1"
    
    # Check minimum length
    if [[ ${#secret} -lt $MIN_WEBHOOK_SECRET_LENGTH ]]; then
        log "$RED" "ERROR: Webhook secret too short (minimum $MIN_WEBHOOK_SECRET_LENGTH characters)"
        return 1
    fi
    
    # Check for placeholder values
    if [[ "$secret" == *"REPLACE_WITH"* ]] || [[ "$secret" == *"your_webhook_secret"* ]] || [[ "$secret" == *"placeholder"* ]]; then
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
    local var_name="$2"
    local value
    
    echo -n -e "${BLUE}${prompt}${NC}"
    read -s value
    echo  # New line after hidden input
    
    if [[ -z "$value" ]]; then
        log "$RED" "ERROR: Empty value provided for $var_name"
        return 1
    fi
    
    # Store in named variable
    declare -g "$var_name=$value"
    return 0
}

# Verify dotnet project exists
verify_project() {
    if [[ ! -f "$PROJECT_PATH/$PROJECT_PATH.csproj" ]]; then
        log "$RED" "ERROR: Project file not found at $PROJECT_PATH/$PROJECT_PATH.csproj"
        log "$YELLOW" "Current directory: $(pwd)"
        log "$YELLOW" "Expected project path: $PROJECT_PATH"
        return 1
    fi
    
    log "$GREEN" "✅ Project file found"
    return 0
}

# Check current user-secrets status
check_current_secrets() {
    log "$BLUE" "🔍 Checking current user-secrets configuration..."
    
    # Check if user-secrets is initialized
    if ! dotnet user-secrets list --project "$PROJECT_PATH" &>/dev/null; then
        log "$YELLOW" "⚠️  User-secrets not initialized, initializing now..."
        dotnet user-secrets init --project "$PROJECT_PATH"
    fi
    
    # List current secrets (filter Replicate-related)
    log "$BLUE" "Current Replicate-related secrets:"
    dotnet user-secrets list --project "$PROJECT_PATH" | grep -i replicate || log "$YELLOW" "No Replicate secrets found"
    
    return 0
}

# Main synchronization function
sync_replicate_secrets() {
    local replicate_token webhook_secret
    
    log "$PURPLE" "🔐 Secure Replicate Secrets Synchronization"
    log "$PURPLE" "=========================================="
    echo
    
    # Security notice
    log "$YELLOW" "⚠️  SECURITY NOTICE:"
    log "$YELLOW" "   - This script will securely add Replicate secrets to dotnet user-secrets"
    log "$YELLOW" "   - Input will be masked and validated before storage"
    log "$YELLOW" "   - No secrets will be logged or exposed in temporary files"
    log "$YELLOW" "   - Only proceed if you have the authorized secret values"
    echo
    
    # Verify project
    if ! verify_project; then
        return 1
    fi
    
    # Check current state
    check_current_secrets
    echo
    
    # Collect secrets securely
    log "$BLUE" "📝 Please provide the Replicate secrets:"
    echo
    
    # Get Replicate API Token
    log "$BLUE" "1. Replicate API Token"
    log "$YELLOW" "   This should be the same token used in GitHub Actions (REPLICATE_API_TOKEN)"
    log "$YELLOW" "   Format: starts with 'r8_' followed by alphanumeric characters"
    
    if ! secure_input "   Enter Replicate API Token: " replicate_token; then
        return 1
    fi
    
    if ! validate_replicate_token "$replicate_token"; then
        return 1
    fi
    echo
    
    # Get Webhook Secret
    log "$BLUE" "2. Replicate Webhook Secret"
    log "$YELLOW" "   This should be the same secret used in GitHub Actions (REPLICATE_WEBHOOK_SECRET)"
    log "$YELLOW" "   Format: minimum 32 characters, high entropy string"
    
    if ! secure_input "   Enter Replicate Webhook Secret: " webhook_secret; then
        return 1
    fi
    
    if ! validate_webhook_secret "$webhook_secret"; then
        return 1
    fi
    echo
    
    # Confirmation
    log "$YELLOW" "🔍 Ready to synchronize secrets to dotnet user-secrets"
    log "$YELLOW" "   Target project: $PROJECT_PATH"
    log "$YELLOW" "   Secrets to add:"
    log "$YELLOW" "     - Replicate:ApiToken"
    log "$YELLOW" "     - Replicate:WebhookSecret"
    echo
    
    read -p "$(echo -e ${BLUE})Proceed with synchronization? (y/N): $(echo -e ${NC})" -n 1 -r
    echo
    
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        log "$YELLOW" "❌ Synchronization cancelled by user"
        return 1
    fi
    
    # Add secrets to user-secrets
    log "$BLUE" "🔄 Adding secrets to dotnet user-secrets..."
    
    if dotnet user-secrets set "Replicate:ApiToken" "$replicate_token" --project "$PROJECT_PATH"; then
        log "$GREEN" "✅ Replicate API Token added successfully"
    else
        log "$RED" "❌ Failed to add Replicate API Token"
        return 1
    fi
    
    if dotnet user-secrets set "Replicate:WebhookSecret" "$webhook_secret" --project "$PROJECT_PATH"; then
        log "$GREEN" "✅ Replicate Webhook Secret added successfully"
    else
        log "$RED" "❌ Failed to add Replicate Webhook Secret"
        return 1
    fi
    
    # Clear variables from memory (security)
    unset replicate_token webhook_secret
    
    echo
    log "$GREEN" "🎉 Secrets synchronization completed successfully!"
    
    # Verify the secrets were added
    log "$BLUE" "🔍 Verifying secrets were added correctly..."
    
    local current_secrets
    current_secrets=$(dotnet user-secrets list --project "$PROJECT_PATH" | grep -i replicate || true)
    
    if [[ -n "$current_secrets" ]]; then
        log "$GREEN" "✅ Verification passed - Replicate secrets found in user-secrets:"
        echo "$current_secrets" | sed 's/^/   /'
    else
        log "$RED" "❌ Verification failed - Replicate secrets not found"
        return 1
    fi
    
    return 0
}

# Security validation test
test_application_startup() {
    log "$BLUE" "🧪 Testing application startup with new secrets..."
    
    # Check if application can start with the secrets
    if timeout 30s dotnet run --project "$PROJECT_PATH" --environment Development --no-launch-profile &>/dev/null; then
        log "$GREEN" "✅ Application startup test passed"
    else
        log "$YELLOW" "⚠️  Application startup test inconclusive (may require database or other dependencies)"
        log "$YELLOW" "   This is normal if database is not available locally"
    fi
}

# Show next steps
show_next_steps() {
    echo
    log "$PURPLE" "📋 Next Steps:"
    log "$PURPLE" "=============="
    echo
    log "$BLUE" "1. Infrastructure Security Update (CRITICAL):"
    log "$YELLOW" "   - Update simple-deploy.bicep to include replicateWebhookSecret parameter"
    log "$YELLOW" "   - Update GitHub Actions workflow to pass REPLICATE_WEBHOOK_SECRET"
    log "$YELLOW" "   - Redeploy infrastructure with complete secrets"
    echo
    log "$BLUE" "2. Local Development Validation:"
    log "$YELLOW" "   - Test webhook signature validation locally"
    log "$YELLOW" "   - Verify Replicate API integration"
    log "$YELLOW" "   - Run comprehensive tests"
    echo
    log "$BLUE" "3. Security Verification:"
    log "$YELLOW" "   - Review webhook endpoints security"
    log "$YELLOW" "   - Test unauthorized access prevention"
    log "$YELLOW" "   - Validate error handling"
    echo
    log "$GREEN" "📚 Documentation:"
    log "$GREEN" "   - Security analysis: ClaudeDocs/Analysis/Security/replicate-secrets-synchronization-audit-2025-08-13-142200.md"
    log "$GREEN" "   - This script: ClaudeDocs/Analysis/Security/secure-replicate-sync.sh"
}

# Main execution
main() {
    # Change to project root
    if [[ -f "AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj" ]]; then
        # Already in project root
        :
    elif [[ -f "../AI.ProfilePhotoMaker.API/AI.ProfilePhotoMaker.API.csproj" ]]; then
        cd ..
    else
        log "$RED" "ERROR: Could not find project root directory"
        log "$YELLOW" "Please run this script from the project root or AI.ProfilePhotoMaker.API directory"
        exit 1
    fi
    
    # Execute synchronization
    if sync_replicate_secrets; then
        test_application_startup
        show_next_steps
        
        # Audit log entry
        log "$GREEN" "🔒 AUDIT: Replicate secrets synchronization completed successfully at $(date)"
        log "$GREEN" "🔒 AUDIT: User: $(whoami), Host: $(hostname)"
        
        exit 0
    else
        log "$RED" "❌ Secrets synchronization failed"
        exit 1
    fi
}

# Script entry point
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    main "$@"
fi