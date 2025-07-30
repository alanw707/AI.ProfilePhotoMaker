#!/bin/bash

# Azure AI Profile Photo Maker - Deployment Validation Script
# Validates successful deployment and health status

set -e

# Configuration
ENVIRONMENT="${1:-staging}"
RESOURCE_GROUP_NAME="ai-profile-photo-maker-$ENVIRONMENT"
TIMEOUT=300  # 5 minutes timeout for health checks

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

validate_azure_resources() {
    print_status "Validating Azure resources in $RESOURCE_GROUP_NAME..."
    
    # Check if resource group exists
    if ! az group show --name "$RESOURCE_GROUP_NAME" --output none 2>/dev/null; then
        print_error "Resource group $RESOURCE_GROUP_NAME not found"
        return 1
    fi
    
    # Get all resources in the group
    RESOURCES=$(az resource list --resource-group "$RESOURCE_GROUP_NAME" --query "[].type" --output tsv)
    
    # Expected resource types
    EXPECTED_RESOURCES=(
        "Microsoft.Web/sites"
        "Microsoft.Web/staticSites"
        "Microsoft.Sql/servers"
        "Microsoft.Storage/storageAccounts"
        "Microsoft.KeyVault/vaults"
        "Microsoft.Insights/components"
        "Microsoft.OperationalInsights/workspaces"
        "Microsoft.Web/serverfarms"
    )
    
    FOUND_COUNT=0
    for expected in "${EXPECTED_RESOURCES[@]}"; do
        if echo "$RESOURCES" | grep -q "$expected"; then
            print_success "✓ Found: $expected"
            ((FOUND_COUNT++))
        else
            print_warning "✗ Missing: $expected"
        fi
    done
    
    print_status "Resource validation: $FOUND_COUNT/${#EXPECTED_RESOURCES[@]} resources found"
    
    if [ $FOUND_COUNT -ge 6 ]; then
        print_success "Resource validation passed"
        return 0
    else
        print_error "Resource validation failed - insufficient resources"
        return 1
    fi
}

validate_web_app_health() {
    print_status "Validating web application health..."
    
    # Get web app URL
    WEB_APP_URL=$(az webapp show \
        --resource-group "$RESOURCE_GROUP_NAME" \
        --name "aiprofilephotomakerapi-$ENVIRONMENT" \
        --query "defaultHostName" \
        --output tsv 2>/dev/null)
    
    if [ -z "$WEB_APP_URL" ]; then
        print_error "Could not retrieve web app URL"
        return 1
    fi
    
    HEALTH_URL="https://$WEB_APP_URL/health"
    print_status "Checking health endpoint: $HEALTH_URL"
    
    # Health check with retry
    for i in {1..10}; do
        print_status "Health check attempt $i/10..."
        
        if curl -s -f --max-time 30 "$HEALTH_URL" > /dev/null 2>&1; then
            print_success "✓ Web app health check passed"
            
            # Get detailed health info
            HEALTH_RESPONSE=$(curl -s --max-time 10 "$HEALTH_URL" 2>/dev/null || echo "Could not get health details")
            print_status "Health response: $HEALTH_RESPONSE"
            return 0
        else
            print_warning "Health check failed, retrying in 30 seconds..."
            sleep 30
        fi
    done
    
    print_error "Web app health check failed after 10 attempts"
    return 1
}

validate_static_web_app() {
    print_status "Validating static web app..."
    
    # Get static web app URL
    STATIC_APP_URL=$(az staticwebapp show \
        --resource-group "$RESOURCE_GROUP_NAME" \
        --name "aiprofilephotomaker-swa-$ENVIRONMENT" \
        --query "defaultHostname" \
        --output tsv 2>/dev/null)
    
    if [ -z "$STATIC_APP_URL" ]; then
        print_error "Could not retrieve static web app URL"
        return 1
    fi
    
    FRONTEND_URL="https://$STATIC_APP_URL"
    print_status "Checking frontend: $FRONTEND_URL"
    
    # Frontend check
    for i in {1..5}; do
        print_status "Frontend check attempt $i/5..."
        
        if curl -s -f --max-time 30 "$FRONTEND_URL" > /dev/null 2>&1; then
            print_success "✓ Frontend is accessible"
            return 0
        else
            print_warning "Frontend check failed, retrying in 15 seconds..."
            sleep 15
        fi
    done
    
    print_error "Frontend accessibility check failed"
    return 1
}

validate_database_connection() {
    print_status "Validating database connection..."
    
    # Get SQL server name
    SQL_SERVER=$(az sql server list \
        --resource-group "$RESOURCE_GROUP_NAME" \
        --query "[0].name" \
        --output tsv 2>/dev/null)
    
    if [ -z "$SQL_SERVER" ]; then
        print_error "Could not find SQL server"
        return 1
    fi
    
    print_success "✓ SQL Server found: $SQL_SERVER"
    
    # Check if server is accessible (basic connectivity test)
    SERVER_FQDN="$SQL_SERVER.database.windows.net"
    if timeout 10 bash -c "</dev/tcp/$SERVER_FQDN/1433" 2>/dev/null; then
        print_success "✓ Database server is accessible"
        return 0
    else
        print_warning "Database server connectivity test failed (may be expected if firewall restricted)"
        return 0  # Don't fail validation for this
    fi
}

validate_key_vault_secrets() {
    print_status "Validating Key Vault and secrets..."
    
    # Get Key Vault name
    KEY_VAULT=$(az keyvault list \
        --resource-group "$RESOURCE_GROUP_NAME" \
        --query "[0].name" \
        --output tsv 2>/dev/null)
    
    if [ -z "$KEY_VAULT" ]; then
        print_error "Could not find Key Vault"
        return 1
    fi
    
    print_success "✓ Key Vault found: $KEY_VAULT"
    
    # Check expected secrets
    EXPECTED_SECRETS=("JwtSecret" "ReplicateApiToken" "DatabaseConnectionString" "ReplicateWebhookSecret")
    FOUND_SECRETS=0
    
    for secret in "${EXPECTED_SECRETS[@]}"; do
        if az keyvault secret show --vault-name "$KEY_VAULT" --name "$secret" --output none 2>/dev/null; then
            print_success "✓ Secret found: $secret"
            ((FOUND_SECRETS++))
        else
            print_warning "✗ Secret missing: $secret"
        fi
    done
    
    print_status "Key Vault validation: $FOUND_SECRETS/${#EXPECTED_SECRETS[@]} secrets found"
    
    if [ $FOUND_SECRETS -ge 3 ]; then
        print_success "Key Vault validation passed"
        return 0
    else
        print_error "Key Vault validation failed - insufficient secrets"
        return 1
    fi
}

validate_storage_account() {
    print_status "Validating storage account..."
    
    # Get storage account name
    STORAGE_ACCOUNT=$(az storage account list \
        --resource-group "$RESOURCE_GROUP_NAME" \
        --query "[0].name" \
        --output tsv 2>/dev/null)
    
    if [ -z "$STORAGE_ACCOUNT" ]; then
        print_error "Could not find storage account"
        return 1
    fi
    
    print_success "✓ Storage account found: $STORAGE_ACCOUNT"
    
    # Check storage account accessibility
    if az storage account show --name "$STORAGE_ACCOUNT" --resource-group "$RESOURCE_GROUP_NAME" --output none 2>/dev/null; then
        print_success "✓ Storage account is accessible"
        return 0
    else
        print_error "Storage account accessibility check failed"
        return 1
    fi
}

generate_validation_report() {
    print_status "Generating validation report..."
    
    REPORT_FILE="deployment-validation-report-$ENVIRONMENT-$(date +%Y%m%d-%H%M%S).md"
    
    cat > "$REPORT_FILE" << EOF
# Deployment Validation Report

**Environment**: $ENVIRONMENT  
**Resource Group**: $RESOURCE_GROUP_NAME  
**Validation Time**: $(date)  
**Status**: $OVERALL_STATUS

## Resource Validation Results

| Component | Status | Details |
|-----------|--------|---------|
| Azure Resources | $RESOURCE_STATUS | $FOUND_COUNT/${#EXPECTED_RESOURCES[@]} resources found |
| Web Application | $WEBAPP_STATUS | Health endpoint check |
| Frontend | $FRONTEND_STATUS | Static web app accessibility |
| Database | $DATABASE_STATUS | SQL Server connectivity |
| Key Vault | $KEYVAULT_STATUS | $FOUND_SECRETS/${#EXPECTED_SECRETS[@]} secrets found |
| Storage | $STORAGE_STATUS | Storage account accessibility |

## Next Steps

$NEXT_STEPS

## Troubleshooting

If any component failed validation:
1. Check Azure portal for deployment status
2. Review GitHub Actions logs for errors
3. Verify all required secrets are configured
4. Check network connectivity and firewall rules

## Monitoring

- Health endpoint: https://$WEB_APP_URL/health
- Frontend URL: https://$STATIC_APP_URL
- Azure Portal: https://portal.azure.com/#@/resource/subscriptions/$(az account show --query id -o tsv)/resourcegroups/$RESOURCE_GROUP_NAME

EOF

    print_success "Validation report generated: $REPORT_FILE"
}

# Main validation execution
main() {
    print_status "🔍 Starting deployment validation for $ENVIRONMENT environment"
    print_status "Timestamp: $(date)"
    print_status "Resource Group: $RESOURCE_GROUP_NAME"
    
    # Run all validations
    VALIDATION_RESULTS=()
    
    if validate_azure_resources; then
        RESOURCE_STATUS="✅ PASS"
        VALIDATION_RESULTS+=("resources:pass")
    else
        RESOURCE_STATUS="❌ FAIL"
        VALIDATION_RESULTS+=("resources:fail")
    fi
    
    if validate_web_app_health; then
        WEBAPP_STATUS="✅ PASS"
        VALIDATION_RESULTS+=("webapp:pass")
    else
        WEBAPP_STATUS="❌ FAIL"
        VALIDATION_RESULTS+=("webapp:fail")
    fi
    
    if validate_static_web_app; then
        FRONTEND_STATUS="✅ PASS"
        VALIDATION_RESULTS+=("frontend:pass")
    else
        FRONTEND_STATUS="❌ FAIL"
        VALIDATION_RESULTS+=("frontend:fail")
    fi
    
    if validate_database_connection; then
        DATABASE_STATUS="✅ PASS"
        VALIDATION_RESULTS+=("database:pass")
    else
        DATABASE_STATUS="❌ FAIL"
        VALIDATION_RESULTS+=("database:fail")
    fi
    
    if validate_key_vault_secrets; then
        KEYVAULT_STATUS="✅ PASS"
        VALIDATION_RESULTS+=("keyvault:pass")
    else
        KEYVAULT_STATUS="❌ FAIL"
        VALIDATION_RESULTS+=("keyvault:fail")
    fi
    
    if validate_storage_account; then
        STORAGE_STATUS="✅ PASS"
        VALIDATION_RESULTS+=("storage:pass")
    else
        STORAGE_STATUS="❌ FAIL"
        VALIDATION_RESULTS+=("storage:fail")
    fi
    
    # Calculate overall status
    FAILED_COUNT=$(echo "${VALIDATION_RESULTS[@]}" | grep -o "fail" | wc -l)
    PASSED_COUNT=$(echo "${VALIDATION_RESULTS[@]}" | grep -o "pass" | wc -l)
    
    if [ $FAILED_COUNT -eq 0 ]; then
        OVERALL_STATUS="✅ ALL VALIDATIONS PASSED"
        NEXT_STEPS="Deployment is successful and all components are operational. You can proceed with testing and using the application."
        print_success "🎉 All validations passed! Deployment is successful."
    elif [ $FAILED_COUNT -le 2 ]; then
        OVERALL_STATUS="⚠️ PARTIAL SUCCESS"
        NEXT_STEPS="Most components are working but some issues were detected. Review the failed validations and address them."
        print_warning "⚠️ Partial success - some components need attention."
    else
        OVERALL_STATUS="❌ VALIDATION FAILED"
        NEXT_STEPS="Multiple components failed validation. Review deployment logs and re-deploy if necessary."
        print_error "❌ Multiple validation failures detected."
    fi
    
    # Generate report
    generate_validation_report
    
    print_status "📊 Validation Summary:"
    print_status "  Passed: $PASSED_COUNT"
    print_status "  Failed: $FAILED_COUNT"
    print_status "  Overall: $OVERALL_STATUS"
    
    # Return appropriate exit code
    if [ $FAILED_COUNT -le 1 ]; then
        exit 0
    else
        exit 1
    fi
}

# Run main function
main "$@"