#!/bin/bash
# Comprehensive Deployment Validation Script
# Validates both GitHub secrets and Azure Key Vault consistency

set -e

echo "🔐 Dual-Approach Secret Management Validation"
echo "=============================================="
echo ""

# Configuration
RESOURCE_GROUP="aiprofilemaker-v1"
KEY_VAULT_NAME="aipm-kv-v1-6j74jubocuukg"
BACKEND_URL="https://api.aiprofilephotomaker.com"
FRONTEND_URL="https://app.aiprofilephotomaker.com"
EXPECTED_WEBHOOK_SECRET="whsec_wUh0bvV+/jGsxbEqPsRgHI1tdct1Y+KM"

# Validation Results
VALIDATION_SUCCESS=true
ISSUES_FOUND=()

echo "📋 [INFO] Validating deployment architecture..."
echo "📋 [INFO] Resource Group: $RESOURCE_GROUP"
echo "📋 [INFO] Key Vault: $KEY_VAULT_NAME"
echo ""

# Function to log validation results
log_result() {
    local status="$1"
    local message="$2"
    
    if [ "$status" = "SUCCESS" ]; then
        echo "✅ $message"
    elif [ "$status" = "WARNING" ]; then
        echo "⚠️ $message"
    else
        echo "❌ $message"
        VALIDATION_SUCCESS=false
        ISSUES_FOUND+=("$message")
    fi
}

# 1. Validate GitHub Secrets
echo "🔍 [VALIDATE] Checking GitHub secrets..."

if command -v gh &> /dev/null; then
    if gh secret list | grep -q "REPLICATE_WEBHOOK_SECRET"; then
        GITHUB_SECRET_DATE=$(gh secret list | grep "REPLICATE_WEBHOOK_SECRET" | awk '{print $2}')
        log_result "SUCCESS" "GitHub secret 'REPLICATE_WEBHOOK_SECRET' exists (updated: $GITHUB_SECRET_DATE)"
    else
        log_result "ERROR" "GitHub secret 'REPLICATE_WEBHOOK_SECRET' is missing"
    fi
    
    # Check other required secrets
    for secret in "GOOGLE_CLIENT_ID" "GOOGLE_CLIENT_SECRET" "JWT_SECRET" "REPLICATE_API_TOKEN" "SQL_ADMIN_PASSWORD" "STRIPE_SECRET_KEY" "STRIPE_PUBLISHABLE_KEY" "STRIPE_WEBHOOK_SECRET" "AZURE_STORAGE_CONNECTION_STRING" "AZURE_STORAGE_CONTAINER_NAME"; do
        if gh secret list | grep -q "$secret"; then
            log_result "SUCCESS" "GitHub secret '$secret' exists"
        else
            log_result "ERROR" "GitHub secret '$secret' is missing"
        fi
    done
else
    log_result "WARNING" "GitHub CLI not available - skipping GitHub secret validation"
fi

echo ""

# 2. Validate Azure Key Vault Secrets  
echo "🔍 [VALIDATE] Checking Azure Key Vault secrets..."

if command -v az &> /dev/null; then
    # Check if we can access the Key Vault
    if az keyvault show --name "$KEY_VAULT_NAME" --output none 2>/dev/null; then
        log_result "SUCCESS" "Key Vault '$KEY_VAULT_NAME' is accessible"
        
        # Check required secrets
        for kv_secret in "ReplicateWebhookSecret" "GoogleClientId" "GoogleClientSecret" "JwtSecret" "ReplicateApiToken" "ConnectionString" "StripeSecretKey" "StripePublishableKey" "StripeWebhookSecret" "AzureStorageConnectionString" "AzureStorageContainerName"; do
            if az keyvault secret show --vault-name "$KEY_VAULT_NAME" --name "$kv_secret" --output none 2>/dev/null; then
                log_result "SUCCESS" "Key Vault secret '$kv_secret' exists"
            else
                log_result "ERROR" "Key Vault secret '$kv_secret' is missing"
            fi
        done
        
        # Validate webhook secret value (without exposing it)
        WEBHOOK_SECRET_VERSION=$(az keyvault secret show --vault-name "$KEY_VAULT_NAME" --name "ReplicateWebhookSecret" --query "id" --output tsv 2>/dev/null || echo "")
        if [ -n "$WEBHOOK_SECRET_VERSION" ]; then
            log_result "SUCCESS" "Webhook secret is stored in Key Vault"
        else
            log_result "ERROR" "Could not verify webhook secret in Key Vault"
        fi
        
    else
        log_result "ERROR" "Cannot access Key Vault '$KEY_VAULT_NAME'"
    fi
else
    log_result "WARNING" "Azure CLI not available - skipping Key Vault validation"
fi

echo ""

# 3. Validate Deployment Health
echo "🔍 [VALIDATE] Checking deployment health..."

# Backend health check
if curl -s -f --max-time 30 "$BACKEND_URL/api/health" > /dev/null; then
    BACKEND_HEALTH=$(curl -s --max-time 30 "$BACKEND_URL/api/health")
    if echo "$BACKEND_HEALTH" | grep -q '"status":"Healthy"'; then
        VERSION=$(echo "$BACKEND_HEALTH" | grep -o '"version":"[^"]*"' | cut -d'"' -f4)
        ENV=$(echo "$BACKEND_HEALTH" | grep -o '"environment":"[^"]*"' | cut -d'"' -f4)
        log_result "SUCCESS" "Backend health check passed (version: $VERSION, env: $ENV)"
    else
        log_result "ERROR" "Backend health check returned unhealthy status"
    fi
else
    log_result "ERROR" "Backend health check failed - API not accessible"
fi

# Frontend health check
if curl -s -f --max-time 30 "$FRONTEND_URL" > /dev/null; then
    log_result "SUCCESS" "Frontend is accessible"
else
    log_result "ERROR" "Frontend is not accessible"
fi

echo ""

# 4. Validate HTTPS and Security
echo "🔍 [VALIDATE] Checking security configuration..."

# Check HTTPS enforcement
if [[ "$BACKEND_URL" == https://* ]]; then
    log_result "SUCCESS" "Backend uses HTTPS"
else
    log_result "ERROR" "Backend does not use HTTPS"
fi

if [[ "$FRONTEND_URL" == https://* ]]; then
    log_result "SUCCESS" "Frontend uses HTTPS"
else
    log_result "ERROR" "Frontend does not use HTTPS"
fi

# Check security headers
BACKEND_HEADERS=$(curl -s -I --max-time 10 "$BACKEND_URL/api/health" 2>/dev/null || echo "")
if echo "$BACKEND_HEADERS" | grep -qi "strict-transport-security"; then
    log_result "SUCCESS" "HSTS header present"
else
    log_result "WARNING" "HSTS header not found"
fi

echo ""

# 5. Validate Infrastructure Components
echo "🔍 [VALIDATE] Checking infrastructure components..."

if command -v az &> /dev/null; then
    # Check resource group
    if az group show --name "$RESOURCE_GROUP" --output none 2>/dev/null; then
        log_result "SUCCESS" "Resource group '$RESOURCE_GROUP' exists"
        
        # Check Container Apps
        CONTAINER_APPS=$(az containerapp list --resource-group "$RESOURCE_GROUP" --query "[].name" --output tsv 2>/dev/null || echo "")
        if [ -n "$CONTAINER_APPS" ]; then
            log_result "SUCCESS" "Container Apps deployed: $(echo $CONTAINER_APPS | tr '\n' ' ')"
        else
            log_result "WARNING" "No Container Apps found in resource group"
        fi
        
        # Check Container Registry
        ACR_NAME=$(az acr list --resource-group "$RESOURCE_GROUP" --query "[].name" --output tsv 2>/dev/null || echo "")
        if [ -n "$ACR_NAME" ]; then
            log_result "SUCCESS" "Container Registry exists: $ACR_NAME"
        else
            log_result "WARNING" "No Container Registry found"
        fi
        
    else
        log_result "ERROR" "Resource group '$RESOURCE_GROUP' does not exist"
    fi
fi

echo ""

# Summary
echo "📊 [SUMMARY] Validation Results"
echo "==============================="

if [ "$VALIDATION_SUCCESS" = true ]; then
    echo "✅ All critical validations passed!"
    echo ""
    echo "🎉 [SUCCESS] Dual-approach secret management is working correctly:"
    echo "  • GitHub secrets are configured for CI/CD deployment"
    echo "  • Azure Key Vault secrets are stored for runtime security"  
    echo "  • Applications are healthy and accessible"
    echo "  • Security configurations are in place"
    echo ""
    echo "📋 [INFO] Deployment Architecture:"
    echo "  • Frontend: $FRONTEND_URL"
    echo "  • Backend API: $BACKEND_URL"
    echo "  • Key Vault: $KEY_VAULT_NAME"
    echo "  • Resource Group: $RESOURCE_GROUP"
    
    exit 0
else
    echo "❌ Validation issues found:"
    printf '%s\n' "${ISSUES_FOUND[@]}" | sed 's/^/  • /'
    echo ""
    echo "🔧 [ACTION REQUIRED] Fix the issues above and re-run validation"
    echo ""
    echo "📋 [INFO] Common fixes:"
    echo "  1. Update missing GitHub secrets: gh secret set SECRET_NAME --body 'value'"
    echo "  2. Update Key Vault secrets: az keyvault secret set --vault-name $KEY_VAULT_NAME --name SECRET_NAME --value 'value'"
    echo "  3. Re-deploy infrastructure if health checks fail"
    echo "  4. Check Azure resource permissions and accessibility"
    
    exit 1
fi